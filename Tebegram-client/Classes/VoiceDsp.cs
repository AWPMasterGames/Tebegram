using System;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Обработка исходящего звука звонка на ПК-клиенте (PCM Int16, моно).
    /// В отличие от веб-клиента, у NAudio нет встроенного шумо-/эхоподавления
    /// (в браузере это делает движок getUserMedia), поэтому базовую обработку
    /// делаем сами — но АККУРАТНО, чтобы не усиливать шум:
    /// 1) ФВЧ 120 Гц — срез низкочастотного гула (ветер, вибрации, гул);
    /// 2) АДАПТИВНЫЙ шумовой гейт — отслеживает уровень фона и открывается только
    ///    когда сигнал ЯВНО громче фона (речь), между словами фон приглушается;
    /// 3) мягкая нормализация — тянет речь к целевой громкости, но усиление
    ///    применяется ТОЛЬКО когда гейт открыт (речь) → шум никогда не усиливается.
    /// Обрабатывает буфер на месте, состояние живёт между буферами.
    /// </summary>
    public class VoiceDsp
    {
        // ── Биквад-ФВЧ (RBJ cookbook), считается один раз под частоту дискретизации ──
        private readonly double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2; // состояние ФВЧ

        // ── Присутствие (peaking EQ ~2.8 кГц, +4 дБ) — разборчивость/«приятнее» ──
        private readonly double _pb0, _pb1, _pb2, _pa1, _pa2;
        private double _px1, _px2, _py1, _py2; // состояние EQ

        // ── Адаптивный гейт ──
        private double _noiseFloor = 0.02; // оценка уровня фона (RMS), адаптируется
        private double _gateGain;          // текущий коэффициент гейта (0..1)
        private int _holdFrames;           // удержание гейта (в кадрах ~20 мс)
        private bool _floorInit;

        // ── Нормализация ──
        private double _agcGain = 1.0;

        private readonly double _attack;   // сглаживание гейта на сэмпл: атака ~5 мс
        private readonly double _release;  // спад ~120 мс

        // Гейт открывается, когда RMS выше max(AbsFloor, фон*OpenRatio).
        private const double AbsFloor = 0.01;   // абсолютный минимум (тишина)
        private const double OpenRatio = 2.2;   // во сколько раз громче фона = речь
        private const double CloseRatio = 1.3;  // ниже порог закрытия — мягче к тихим хвостам слов
        private const int GateHold = 24;        // кадров удержания (~480 мс) — хвосты фраз не режутся
        private const double AgcTargetRms = 0.14;         // чуть громче цель
        private const double AgcMin = 0.4, AgcMax = 5.0;  // шире диапазон нормализации
        private const double GateFloorGain = 0.3;         // насколько глушить фон (не «в вакуум»)

        // Lookahead на один буфер (~20 мс): решение «речь/фон» принимается по
        // ТЕКУЩЕМУ буферу, а наружу уходит ПРЕДЫДУЩИЙ с уже новым коэффициентом —
        // гейт открыт к моменту начала речи, первые слоги не съедаются
        private double[] _pending;

        public VoiceDsp(int sampleRate)
        {
            // ФВЧ 120 Гц, Q=0.707
            double w0 = 2.0 * Math.PI * 120.0 / sampleRate;
            double cosW0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * 0.707);
            double a0 = 1.0 + alpha;
            _b0 = (1.0 + cosW0) / 2.0 / a0;
            _b1 = -(1.0 + cosW0) / a0;
            _b2 = (1.0 + cosW0) / 2.0 / a0;
            _a1 = -2.0 * cosW0 / a0;
            _a2 = (1.0 - alpha) / a0;

            // Peaking EQ (RBJ) 2800 Гц, Q=1, +4 дБ — присутствие голоса
            double pw0 = 2.0 * Math.PI * 2800.0 / sampleRate;
            double pcos = Math.Cos(pw0);
            double palpha = Math.Sin(pw0) / (2.0 * 1.0);
            double A = Math.Pow(10.0, 4.0 / 40.0); // +4 дБ
            double pa0 = 1.0 + palpha / A;
            _pb0 = (1.0 + palpha * A) / pa0;
            _pb1 = (-2.0 * pcos) / pa0;
            _pb2 = (1.0 - palpha * A) / pa0;
            _pa1 = (-2.0 * pcos) / pa0;
            _pa2 = (1.0 - palpha / A) / pa0;

            _attack = 1.0 - Math.Exp(-1.0 / (0.004 * sampleRate));
            _release = 1.0 - Math.Exp(-1.0 / (0.18 * sampleRate)); // плавнее — хвосты фраз не режутся
        }

        /// <summary>Обрабатывает PCM Int16 LE моно на месте.</summary>
        public void Process(byte[] buffer, int bytes)
        {
            int samples = bytes / 2;
            if (samples == 0) return;

            // Int16 → double [-1..1] + ФВЧ + presence EQ + RMS
            double[] f = new double[samples];
            double sum = 0;
            for (int i = 0; i < samples; i++)
            {
                double x = BitConverter.ToInt16(buffer, i * 2) / 32768.0;
                // ФВЧ
                double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1; _x1 = x;
                _y2 = _y1; _y1 = y;
                // Presence EQ
                double z = _pb0 * y + _pb1 * _px1 + _pb2 * _px2 - _pa1 * _py1 - _pa2 * _py2;
                _px2 = _px1; _px1 = y;
                _py2 = _py1; _py1 = z;
                f[i] = z;
                sum += z * z;
            }
            double rms = Math.Sqrt(sum / samples);

            if (!_floorInit) { _noiseFloor = rms; _floorInit = true; }

            // Оценка фона: вниз — мгновенно; вверх — быстро (~1 с), но ТОЛЬКО пока
            // сигнал не похож на речь (ниже 1.5×порога открытия): речь «пол» не
            // задирает, а шум, появившийся посреди звонка, выучивается за ~секунду
            double openPrev = Math.Max(AbsFloor, _noiseFloor * OpenRatio);
            if (rms < _noiseFloor) _noiseFloor = rms;
            else if (rms < openPrev * 1.5) _noiseFloor += (rms - _noiseFloor) * 0.02;
            _noiseFloor = Math.Max(1e-4, _noiseFloor);

            double openThresh = Math.Max(AbsFloor, _noiseFloor * OpenRatio);
            double closeThresh = Math.Max(AbsFloor * 0.7, _noiseFloor * CloseRatio);

            // Гейт с гистерезисом и удержанием
            if (rms > openThresh) _holdFrames = GateHold;
            else if (rms > closeThresh && _holdFrames > 0) _holdFrames = Math.Min(GateHold, _holdFrames + 1);
            else if (_holdFrames > 0) _holdFrames--;
            int gateTarget = _holdFrames > 0 ? 1 : 0;

            // Нормализация ТОЛЬКО по настоящей речи (rms выше порога открытия) —
            // иначе во время «удержания» гейта на паузе AGC адаптировался к шуму
            // и подкачивал его (ловил C#-харнес, сценарий D)
            if (gateTarget == 1 && rms > openThresh)
            {
                double desired = Math.Min(AgcMax, Math.Max(AgcMin, AgcTargetRms / Math.Max(rms, 1e-4)));
                _agcGain += (desired - _agcGain) * 0.05;
            }

            // Lookahead: наружу уходит ПРЕДЫДУЩИЙ буфер, обработанный коэффициентом,
            // вычисленным по ТЕКУЩЕМУ — начало речи не обрезается.
            double[] toEmit = _pending;
            _pending = f;

            if (toEmit == null || toEmit.Length != samples)
            {
                // Первый буфер (или редкая смена размера) — отдаём 20 мс тишины
                Array.Clear(buffer, 0, samples * 2);
                return;
            }

            // Применяем: гейт плавно между GateFloorGain и 1, затем AGC
            for (int i = 0; i < samples; i++)
            {
                double target = gateTarget == 1 ? 1.0 : GateFloorGain;
                _gateGain += (target - _gateGain) * (target > _gateGain ? _attack : _release);
                double s = toEmit[i] * _gateGain * _agcGain;
                if (s > 1.0) s = 1.0; else if (s < -1.0) s = -1.0;
                short v = (short)Math.Round(s * 32767.0);
                buffer[i * 2] = (byte)(v & 0xFF);
                buffer[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
            }
        }
    }
}
