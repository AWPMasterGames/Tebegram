/* AudioWorklet-процессоры голосовой связи Tebegram.
   Работают в аудиопотоке (отдельно от основного), общий формат — 48 кГц моно.
   Захват отдаёт кадры Float32 в основной поток, воспроизведение берёт их из очереди. */
'use strict';

// ── Захват микрофона: копит ~20 мс и отправляет в основной поток ──
class CaptureProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    // 20 мс на РОДНОЙ частоте контекста (глобальная sampleRate этого worklet-скоупа):
    // контекст больше не форсируется в 48 кГц (баг тишины WebKit на iPhone),
    // в протокольные 48 кГц кадр пересэмплирует основной поток (resamplePcm)
    this._frame = Math.round(sampleRate * 0.02);
    this._buf = new Float32Array(this._frame);
    this._pos = 0;
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || !input[0]) return true;
    const ch = input[0];
    for (let i = 0; i < ch.length; i++) {
      this._buf[this._pos++] = ch[i];
      if (this._pos >= this._frame) {
        // копия, т.к. буфер переиспользуется
        this.port.postMessage(this._buf.slice(0));
        this._pos = 0;
      }
    }
    return true;
  }
}

// ── Воспроизведение: очередь входящих кадров, плавно отдаётся на выход ──
class PlaybackProcessor extends AudioWorkletProcessor {
  constructor() {
    super();
    this._queue = [];   // массив Float32Array
    this._cur = null;
    this._curPos = 0;
    this.port.onmessage = e => {
      if (e.data === 'flush') { this._queue = []; this._cur = null; return; }
      this._queue.push(e.data);
      // защита от разрастания задержки, если накопилось >1 сек
      if (this._queue.length > 50) this._queue.splice(0, this._queue.length - 50);
    };
  }

  // ВНИМАНИЕ: сигнатура process(inputs, outputs, params) — outputs ВТОРОЙ аргумент.
  // Раньше стояло process(outputs) — туда приходили INPUTS (пустые у узла без
  // входа), out был undefined, и процессор молча выходил: воспроизведение
  // не работало НИГДЕ (тишина в звонках при живом транспорте).
  process(inputs, outputs) {
    const out = outputs[0][0];
    if (!out) return true;
    for (let i = 0; i < out.length; i++) {
      if (!this._cur || this._curPos >= this._cur.length) {
        this._cur = this._queue.shift() || null;
        this._curPos = 0;
      }
      out[i] = this._cur ? this._cur[this._curPos++] : 0;
    }
    return true;
  }
}

registerProcessor('tbg-capture', CaptureProcessor);
registerProcessor('tbg-playback', PlaybackProcessor);
