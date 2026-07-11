/* ═══════════════════════════════════════════════════════════════
   Tebegram Web — мобильный PWA-клиент.
   Работает с тем же сервером и протоколом, что и десктопный клиент:
   разделитель полей ▫, сообщения Sender▫Reciver▫Type▫Time▫ServerAdress▫Text,
   реалтайм через WebSocket /Chat/ws.
   ═══════════════════════════════════════════════════════════════ */
'use strict';

const APP_VERSION = '1.0.10';
const SEP = '▫';
const MSG_SEP = '❂';
const WS_SEP = '▫#▫';

/* ─────────────────────────── Сервер ─────────────────────────── */
const Server = {
  address: null,

  // Тот же механизм, что у десктопного клиента: адрес devtunnel лежит в Adress.txt
  // в репозитории. Первый источник — ветка main-dev-Test (ВРЕМЕННО, для проверки
  // туннеля DrunkMan), дальше — основные пути в main (новая и старая раскладка).
  ADDRESS_SOURCES: [
    'https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main-dev-Test/Tebegram-client/Adress.txt',
    'https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegram-client/Adress.txt',
    'https://raw.githubusercontent.com/AWPMasterGames/Tebegram/refs/heads/main/Tebegrammmm/Adress.txt',
  ],

  async resolve() {
    // 1. Ручной адрес из настроек
    const override = localStorage.getItem('tbg.serverOverride');
    if (override) {
      this.address = override.replace(/\/+$/, '');
      return this.address;
    }

    // 2. Если веб-клиент открыт с самого сервера (путь /app) — API на том же домене
    if (location.pathname.startsWith('/app')) {
      try {
        const r = await fetchWithTimeout(`${location.origin}/Test`, 3000);
        if ((await r.text()) === 'HI!') {
          this.address = location.origin;
          return this.address;
        }
      } catch { /* не тот хост — идём дальше */ }
    }

    // 3. Adress.txt на GitHub
    for (const src of this.ADDRESS_SOURCES) {
      try {
        const r = await fetchWithTimeout(`${src}?t=${Date.now()}`, 5000);
        if (!r.ok) continue;
        const line = (await r.text()).split('\n')[0].trim();
        if (line) {
          this.address = line.replace(/\/+$/, '');
          return this.address;
        }
      } catch { /* пробуем следующий источник */ }
    }

    throw new Error('Не удалось определить адрес сервера');
  },

  wsUrl(path) {
    return this.address.replace(/^https:/, 'wss:').replace(/^http:/, 'ws:') + path;
  },
};

function fetchWithTimeout(url, ms, options = {}) {
  return fetch(url, { ...options, signal: AbortSignal.timeout(ms) });
}

/* ─────────────────────────── Состояние ─────────────────────────── */
const Store = {
  user: null,          // { id, login, name, username, avatar }
  contacts: [],        // [{ id, username, name, avatar, messages: [] }]
  folders: [],         // [{ name, icon, usernames: [] }] — папки, созданные в десктопе
  activeFolder: null,  // null = «Все чаты»
  activeContact: null,

  findContact(username) {
    return this.contacts.find(c => c.username === username) || null;
  },
};

/* ─────────────────────────── API ─────────────────────────── */
const Api = {
  async login(login, password) {
    const r = await fetchWithTimeout(
      `${Server.address}/login/${encodeURIComponent(login)}-${encodeURIComponent(password)}`, 10000);
    return r.text();
  },

  async register(login, password, username, name) {
    const r = await fetchWithTimeout(
      `${Server.address}/register/${encodeURIComponent(login)}-${encodeURIComponent(password)}-${encodeURIComponent(username)}-${encodeURIComponent(name)}`, 10000);
    return r.text();
  },

  async history(userId) {
    const r = await fetchWithTimeout(`${Server.address}/messages/${userId}`, 10000);
    return r.ok ? r.text() : '';
  },

  // Дублирующая запись сообщения (как в десктопе): WS рассылает, POST сохраняет
  async postMessage(raw) {
    await fetch(`${Server.address}/messages`, { method: 'POST', body: raw });
  },

  // Удаление сообщения: scope = "self" (только у себя) | "all" (у всех)
  async deleteMessage(ownerId, contactUsername, scope, time, text) {
    await fetch(`${Server.address}/message`, {
      method: 'DELETE',
      body: `${ownerId}${SEP}${contactUsername}${SEP}${scope}${SEP}${time}${SEP}${text}`,
    });
  },

  async addContact(myId, username, name) {
    const r = await fetch(`${Server.address}/Contact`, {
      method: 'POST',
      body: `${myId}${SEP}${username}${SEP}${name}`,
    });
    return { ok: r.ok, status: r.status, text: await r.text() };
  },

  async removeContact(myId, username) {
    await fetch(`${Server.address}/Contact`, { method: 'DELETE', body: `${myId}${SEP}${username}` });
  },

  async findUser(username) {
    const r = await fetchWithTimeout(`${Server.address}/UserName/${encodeURIComponent(username)}`, 8000);
    if (!r.ok) return null;
    const [id, name] = (await r.text()).split(SEP);
    return { id: parseInt(id, 10), name };
  },

  // Папки контактов: формат «Название▫Иконка▫user1&user2», папки через ❂
  async getFolders(userId) {
    try {
      const r = await fetchWithTimeout(`${Server.address}/Folders/${userId}`, 8000);
      if (!r.ok) return [];
      const text = (await r.text()).trim();
      if (!text) return [];
      return text.split(MSG_SEP).map(raw => {
        const f = raw.split(SEP);
        if (f.length < 3 || !f[0]) return null;
        return { name: f[0], icon: f[1], usernames: f[2].split('&').filter(Boolean) };
      }).filter(Boolean);
    } catch { return []; }
  },

  async avatarFileName(userId) {
    try {
      const r = await fetchWithTimeout(`${Server.address}/avatarsFileName/${userId}`, 8000);
      if (!r.ok) return '';
      return (await r.text()).trim();
    } catch { return ''; }
  },

  avatarUrl(fileName) {
    return `${Server.address}/avatars/${encodeURIComponent(fileName)}`;
  },

  async uploadFile(file) {
    const fd = new FormData();
    fd.append('file', file, file.name);
    const r = await fetch(`${Server.address}/upload`, { method: 'POST', body: fd });
    return r.text(); // сервер возвращает сохранённое имя файла
  },

  async uploadAvatar(userId, file) {
    const fd = new FormData();
    fd.append('file', file, file.name);
    const r = await fetch(`${Server.address}/avatars/${userId}`, { method: 'POST', body: fd });
    return r.text();
  },

  fileUrl(fileName) {
    return `${Server.address}/upload/${encodeURIComponent(fileName)}`;
  },
};

/* ─────────────────────── Голосовые звонки ───────────────────────
   Совместимы с ПК и Android: общий формат — PCM 16 бит, 48 кГц, моно.
   Сервер просто ретранслирует бинарные пакеты всем в комнате.
   - GET  /Voice/CreateRoom/{myId}-{username}   → токен комнаты
   - GET  /Voice/GetCallToken/{myId}            → "NotFound" или "caller▫token"
   - GET  /Voice/DeclineCall/{myId}-{token}
   - WSS  /Voice/ws?userId={id}&roomToken={t}   → бинарный PCM 48 кГц + текст "CloseConnection"
   ──────────────────────────────────────────────────────────────── */
const SAMPLE_RATE = 48000;

const Voice = {
  ENABLED: true,

  ws: null,
  ctx: null,
  stream: null,
  captureNode: null,
  playbackNode: null,
  micEnabled: true,
  active: false,
  contact: null,
  token: null,
  role: null,           // 'caller' | 'callee'
  timer: null,
  seconds: 0,

  async createRoom(contactUsername) {
    const r = await fetch(`${Server.address}/Voice/CreateRoom/${Store.user.id}-${encodeURIComponent(contactUsername)}`);
    return r.text();
  },

  async getIncomingCall() {
    try {
      const r = await fetch(`${Server.address}/Voice/GetCallToken/${Store.user.id}`);
      const t = await r.text();
      if (t === 'NotFound') return null;
      const [caller, token] = t.split(SEP);
      // Свой же токен исходящего звонка (без ▫) — это не входящий звонок
      if (!caller || !token) return null;
      return { caller, token };
    } catch { return null; }
  },

  async decline(token) {
    try {
      await fetch(`${Server.address}/Voice/DeclineCall/${Store.user.id}-${encodeURIComponent(token)}`);
    } catch { /* уже закрыто */ }
  },

  wsUrl(token) {
    return Server.wsUrl(`/Voice/ws?userId=${Store.user.id}&roomToken=${encodeURIComponent(token)}`);
  },

  // Запрос микрофона. ВАЖНО: вызывается ПЕРВЫМ, прямо из обработчика нажатия —
  // iOS/Android считают «активацию жеста» истёкшей после сетевых await (fetch/WS),
  // и getUserMedia после них молча отклонялся с Permission denied.
  async _getMic() {
    // getUserMedia работает только в безопасном контексте — по http://IP браузер
    // запрещает микрофон молча, поэтому объясняем явно
    if (!window.isSecureContext)
      throw new Error('звонки работают только по HTTPS. Открой приложение по ссылке туннеля (https://…devtunnels.ms/app), а не по http или IP-адресу');
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia)
      throw new Error('браузер не поддерживает доступ к микрофону');

    // Пробуем от САМОГО простого запроса к более «умному». Ведём именно с {audio:true}:
    // Samsung/Android-прошивки часто отвергают строгие ограничения (channelCount и т.п.),
    // причём НЕ как OverconstrainedError, а как NotFoundError — и старый код тогда сразу
    // говорил «микрофон не найден», ни разу не попробовав простой запрос.
    // Worklet берёт только канал 0, поэтому стерео-поток из {audio:true} нам подходит.
    const attempts = [
      { audio: true },
      { audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true } },
    ];

    let lastErr = null;
    for (let round = 0; round < 2; round++) {         // до 2 проходов (второй — «прогрев» железа)
      for (const constraints of attempts) {
        try {
          return await navigator.mediaDevices.getUserMedia(constraints);
        } catch (e) {
          lastErr = e;
          // Отказ в доступе — дальнейшие попытки бессмысленны, выходим сразу
          if (e && (e.name === 'NotAllowedError' || e.name === 'SecurityError')) round = 99;
          if (round === 99) break;
        }
      }
      // NotFoundError иногда транзиентный (микрофон «просыпается») — ждём и пробуем ещё раз
      if (lastErr && lastErr.name === 'NotFoundError' && round === 0)
        await new Promise(r => setTimeout(r, 500));
      else
        break;
    }

    const e = lastErr;
    if (e && (e.name === 'NotAllowedError' || e.name === 'SecurityError'))
      throw new Error(this._micDeniedHint());
    if (e && e.name === 'NotReadableError')
      throw new Error('микрофон занят другим приложением. Заверши телефонный звонок, закрой диктофон/другие вкладки с микрофоном и попробуй снова.');
    if (e && e.name === 'NotFoundError') {
      // Диагностика: реально ли устройство ввода не видно браузеру
      let inputs = -1;
      try { inputs = (await navigator.mediaDevices.enumerateDevices()).filter(d => d.kind === 'audioinput').length; } catch {}
      throw new Error(this._micNotFoundHint(inputs));
    }
    throw new Error(`микрофон недоступен (${(e && e.name) || 'ошибка'})`);
  },

  // Подсказка под платформу: Android блокирует навсегда после одного отказа
  // и больше НЕ показывает запрос — пользователь должен включить доступ руками
  _micDeniedHint() {
    const ua = navigator.userAgent;
    if (/android/i.test(ua))
      return 'нет доступа к микрофону. Нажми значок замка (или настроек) в адресной строке → Разрешения → Микрофон → Разрешить. Если запрос вообще не появлялся — проверь доступ к микрофону у браузера: Настройки Android → Приложения → Chrome/Samsung Internet → Разрешения';
    if (/iphone|ipad|ipod/i.test(ua))
      return 'нет доступа к микрофону. iPhone: Настройки → Приложения → Safari → Микрофон (или всплывающий запрос при звонке)';
    return 'нет доступа к микрофону. Разреши его в настройках сайта (значок замка в адресной строке)';
  },

  // NotFoundError: микрофон есть физически, но браузер его не отдал.
  // inputs — сколько аудио-входов видит браузер (0 = вообще не видит, >0 = есть, но захват сорвался).
  _micNotFoundHint(inputs) {
    const android = /android/i.test(navigator.userAgent);
    if (inputs === 0) {
      // Классика Samsung Internet в режиме установленного PWA — он не отдаёт устройства
      if (android)
        return 'браузер не видит микрофон, хотя разрешение выдано. Обычно помогает: (1) открыть приложение в Chrome, а не в Samsung Internet; (2) закрыть телефонный звонок/диктофон, если они держат микрофон; (3) если открыто как установленное приложение — открой ту же ссылку обычной вкладкой браузера. После смены — перезапусти звонок.';
      return 'браузер не видит микрофон. Проверь, что он не занят другим приложением, и попробуй Chrome.';
    }
    // Устройство есть в списке, но захват не удался — как правило, помогает перезапуск браузера
    return 'микрофон найден, но захват не удался. Закрой другие приложения, использующие микрофон, полностью закрой и открой браузер (или переоткрой приложение) и попробуй снова.';
  },

  // Инициатор звонка
  async startOutgoing(contact) {
    if (this.active) return;
    this.contact = contact;
    this.role = 'caller';
    UI.showCall(contact, 'outgoing');
    try {
      this.stream = await this._getMic(); // до сетевых запросов — см. _getMic
      await this._ensureAudio();          // аудиоконтекст тоже создаём в жесте (iOS)
      this.token = await this.createRoom(contact.username);
      await this._connectAudio(this.token);
      // Таймер НЕ стартуем: держим «Вызов…», пока собеседник не принял.
      // «Идёт разговор» + таймер включит его ПЕРВЫЙ аудио-кадр (см. _connectAudio) —
      // так время звонка на обоих телефонах начинается одновременно.
    } catch (e) {
      UI.toast('Не удалось начать звонок: ' + (e.message || e));
      this.hangup();
    }
  },

  // Приём входящего звонка
  async acceptIncoming() {
    if (!this._incoming) return;
    this.contact = Store.findContact(this._incoming.caller)
      || makeContact(0, this._incoming.caller, this._incoming.caller);
    this.token = this._incoming.token;
    this.role = 'callee';
    this._incoming = null;
    try {
      this.stream = await this._getMic(); // до сетевых запросов — см. _getMic
      await this._ensureAudio();          // аудиоконтекст тоже создаём в жесте (iOS)
      await this._connectAudio(this.token);
      // Таймер стартует по первому аудио-кадру собеседника (синхронно на обоих)
      UI.showCall(this.contact, 'connecting');
    } catch (e) {
      UI.toast('Не удалось подключиться: ' + (e.message || e));
      this.hangup();
    }
  },

  // Аудиоконтекст на РОДНОЙ частоте устройства + worklet + аудио-элемент вывода.
  // Вызывается прямо в жесте нажатия (iOS требует создавать/будить аудио в жесте).
  //
  // Три защиты от «тишины» на iPhone:
  // 1) sampleRate НЕ задаём: при несовпадении с частотой железа (часто 44100)
  //    WebKit молча отдаёт тишину из createMediaStreamSource. Пересэмплируем сами.
  // 2) Вывод звука идёт через <audio>-элемент (MediaStreamAudioDestinationNode):
  //    в режиме «микрофон+динамик» iOS прямой выход Web Audio может не звучать
  //    вовсе, а медиа-путь через <audio> работает и играет через громкоговоритель.
  // 3) При включении микрофона iOS переводит контекст в НЕСТАНДАРТНОЕ состояние
  //    'interrupted' (не 'suspended'!) и сам не будит — ловим statechange и будим.
  async _ensureAudio() {
    if (!this.ctx || this.ctx.state === 'closed') {
      const AC = window.AudioContext || window.webkitAudioContext;
      this.ctx = new AC();
      // ?v= — чтобы телефоны не держали старый worklet в HTTP-кэше
      await this.ctx.audioWorklet.addModule('./voice-worklet.js?v=1.0.10');

      // Автопробуждение: iOS «прерывает» контекст при смене аудио-сессии
      this.ctx.onstatechange = () => {
        if (this.ctx && this.ctx.state !== 'running' && this.ctx.state !== 'closed')
          this.ctx.resume().catch(() => {});
      };
    }
    // resume безусловно: state может быть и 'interrupted' (WebKit), не только 'suspended'
    if (this.ctx.state !== 'running') await this.ctx.resume().catch(() => {});

    // Выходной путь: worklet → MediaStreamDestination → <audio>. play() — в жесте.
    if (!this.playbackDest || this.playbackDest.context !== this.ctx)
      this.playbackDest = this.ctx.createMediaStreamDestination();
    if (!this._audioEl) {
      this._audioEl = new Audio();
      this._audioEl.setAttribute('playsinline', '');
      this._audioEl.autoplay = true;
    }
    if (this._audioEl.srcObject !== this.playbackDest.stream)
      this._audioEl.srcObject = this.playbackDest.stream;
    try {
      await this._audioEl.play();
      this._elPlaying = true;
    } catch {
      this._elPlaying = false; // автоплей не дали — подстрахуемся прямым выходом
    }
  },

  async _connectAudio(token) {
    // 1. Микрофон (если не запрошен заранее в обработчике жеста) + аудиоконтекст
    if (!this.stream) this.stream = await this._getMic();
    await this._ensureAudio();
    const deviceRate = this.ctx.sampleRate; // напр. 44100 на iPhone, 48000 на ПК

    // 2. WebSocket комнаты
    this.ws = new WebSocket(this.wsUrl(token));
    this.ws.binaryType = 'arraybuffer';
    await new Promise((res, rej) => {
      this.ws.onopen = res;
      this.ws.onerror = () => rej(new Error('соединение не установлено'));
    });

    // 3. Захват: Float32 (родная частота) → 48 кГц → Int16 → отправка
    const src = this.ctx.createMediaStreamSource(this.stream);
    this.captureNode = new AudioWorkletNode(this.ctx, 'tbg-capture');
    this.captureNode.port.onmessage = e => {
      if (!this.micEnabled || !this.ws || this.ws.readyState !== WebSocket.OPEN) return;
      this.ws.send(floatToInt16(resamplePcm(e.data, deviceRate, SAMPLE_RATE)).buffer);
    };
    src.connect(this.captureNode);
    // подключаем к выходу через нулевую громкость, чтобы worklet работал (без эха себя)
    const mute = this.ctx.createGain();
    mute.gain.value = 0;
    this.captureNode.connect(mute).connect(this.ctx.destination);

    // 4. Воспроизведение входящего: через <audio>-элемент (iOS), иначе — напрямую
    this.playbackNode = new AudioWorkletNode(this.ctx, 'tbg-playback');
    this.playbackNode.connect(this._elPlaying ? this.playbackDest : this.ctx.destination);

    // 5. Входящие пакеты: Int16 48 кГц → Float32 → родная частота устройства.
    //    Первый аудио-кадр собеседника = он реально в разговоре → стартуем таймер
    //    (на обоих устройствах почти одновременно).
    this._peerJoined = false;
    this.ws.onmessage = e => {
      if (typeof e.data === 'string') {
        if (e.data === 'CloseConnection') this.hangup(false);
        return;
      }
      if (!this._peerJoined) {
        this._peerJoined = true;
        UI.setCallState('active');
        this._startTimer();
      }
      const f32 = int16ToFloat32(new Int16Array(e.data));
      this.playbackNode.port.postMessage(resamplePcm(f32, SAMPLE_RATE, deviceRate));
    };
    this.ws.onclose = () => this.hangup(false);

    this.active = true;
  },

  toggleMic() {
    this.micEnabled = !this.micEnabled;
    UI.setMicUI(this.micEnabled);
  },

  async hangup(notifyServer = true) {
    if (!this.active && !this.contact) return;
    this.active = false;
    this._stopTimer();

    if (notifyServer && this.token) this.decline(this.token);

    try { this.ws && this.ws.close(); } catch {}
    try { this.stream && this.stream.getTracks().forEach(t => t.stop()); } catch {}
    try { this.captureNode && this.captureNode.disconnect(); } catch {}
    try { this.playbackNode && this.playbackNode.disconnect(); } catch {}
    try { if (this._audioEl) { this._audioEl.pause(); this._audioEl.srcObject = null; } } catch {}
    try { if (this.ctx) { this.ctx.onstatechange = null; this.ctx.close(); } } catch {}

    this.ws = this.ctx = this.stream = this.captureNode = this.playbackNode = null;
    this.playbackDest = null;
    this._peerJoined = false;
    this._elPlaying = false;
    this.contact = this.token = this.role = null;
    this.micEnabled = true;
    UI.hideCall();
  },

  _startTimer() {
    this.seconds = 0;
    UI.setCallTime('00:00');
    this.timer = setInterval(() => {
      this.seconds++;
      const m = String(Math.floor(this.seconds / 60)).padStart(2, '0');
      const s = String(this.seconds % 60).padStart(2, '0');
      UI.setCallTime(`${m}:${s}`);
    }, 1000);
  },
  _stopTimer() { clearInterval(this.timer); this.timer = null; },

  // Опрос входящих звонков (аналог десктопного GetCallToken)
  _incoming: null,
  startPolling() {
    this._poll = setInterval(async () => {
      if (this.active || this._incoming) return;
      const call = await this.getIncomingCall();
      if (call) {
        this._incoming = call;
        UI.showIncoming(call.caller);
      }
    }, 1800);
  },
  stopPolling() { clearInterval(this._poll); },
  declineIncoming() {
    if (!this._incoming) return;
    this.decline(this._incoming.token);
    this._incoming = null;
    UI.hideCall();
  },
};

// Линейный ресэмплер: протокол всегда 48 кГц, а аудиоконтекст работает на родной
// частоте устройства (см. Voice._ensureAudio) — переводим кадры туда и обратно.
// Для голоса линейной интерполяции достаточно.
function resamplePcm(f32, fromRate, toRate) {
  if (fromRate === toRate || f32.length === 0) return f32;
  const outLen = Math.max(1, Math.round(f32.length * toRate / fromRate));
  const out = new Float32Array(outLen);
  const step = (f32.length - 1) / Math.max(1, outLen - 1);
  for (let i = 0; i < outLen; i++) {
    const pos = i * step;
    const i0 = pos | 0;
    const i1 = Math.min(i0 + 1, f32.length - 1);
    out[i] = f32[i0] + (f32[i1] - f32[i0]) * (pos - i0);
  }
  return out;
}

// PCM-конвертация Float32 [-1..1] ↔ Int16
function floatToInt16(f32) {
  const i16 = new Int16Array(f32.length);
  for (let i = 0; i < f32.length; i++) {
    let s = Math.max(-1, Math.min(1, f32[i]));
    i16[i] = s < 0 ? s * 0x8000 : s * 0x7FFF;
  }
  return i16;
}
function int16ToFloat32(i16) {
  const f32 = new Float32Array(i16.length);
  for (let i = 0; i < i16.length; i++) f32[i] = i16[i] / 0x8000;
  return f32;
}

/* ─────────────────────── Разбор сообщений ─────────────────────── */
function parseMessage(raw) {
  const p = raw.split(SEP);
  if (p.length < 6) return null;
  return {
    sender: p[0],
    receiver: p[1],
    type: p[2],                     // "Text" | "File"
    time: p[3],
    serverAddress: p[4],
    text: p.slice(5).join(SEP),     // текст может содержать ▫
  };
}

function serializeMessage(m) {
  return `${m.sender}${SEP}${m.receiver}${SEP}${m.type}${SEP}${m.time}${SEP}${m.serverAddress || ''}${SEP}${m.text}`;
}

// Полная дата+время для хранения (dd.MM.yyyy HH:mm) — как в десктопном клиенте
function nowFull() {
  const d = new Date();
  const p = n => String(n).padStart(2, '0');
  return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

// Из строки времени вытаскиваем только ЧЧ:ММ (для показа в пузыре)
function timeShort(t) {
  const m = /(\d{1,2}:\d{2})\s*$/.exec(t || '');
  return m ? m[1] : (t || '');
}

// Ключ дня yyyymmdd (для разделителей). Пусто, если дата не распознана.
function dateKeyOf(t) {
  const m = /^(\d{2})\.(\d{2})\.(\d{4})/.exec(t || '');
  return m ? `${m[3]}${m[2]}${m[1]}` : '';
}

const RU_MONTHS = ['января', 'февраля', 'марта', 'апреля', 'мая', 'июня',
  'июля', 'августа', 'сентября', 'октября', 'ноября', 'декабря'];

// Дружелюбная подпись даты как в Telegram
function dateLabel(key) {
  const m = /^(\d{4})(\d{2})(\d{2})$/.exec(key || '');
  if (!m) return '';
  const d = new Date(+m[1], +m[2] - 1, +m[3]);
  const today = new Date(); today.setHours(0, 0, 0, 0);
  const diff = Math.round((today - d) / 86400000);
  if (diff === 0) return 'Сегодня';
  if (diff === 1) return 'Вчера';
  const base = `${d.getDate()} ${RU_MONTHS[d.getMonth()]}`;
  return d.getFullYear() === today.getFullYear() ? base : `${base} ${d.getFullYear()}`;
}

/* Распределение входящего сообщения по контактам (логика десктопного клиента) */
async function routeMessage(raw) {
  const m = parseMessage(raw);
  if (!m) return;

  let contactUsername;
  if (m.sender === Store.user.username) {
    m.outgoing = true;
    contactUsername = m.receiver;
  } else {
    m.outgoing = false;
    contactUsername = m.sender;
  }

  let contact = Store.findContact(contactUsername);
  if (!contact) {
    const info = await Api.findUser(contactUsername);
    if (!info) return;
    contact = makeContact(info.id, contactUsername, info.name);
    Store.contacts.push(contact);
    loadContactAvatar(contact);
  }

  contact.messages.push(m);
  UI.onMessageAdded(contact, m);
}

function makeContact(id, username, name) {
  return { id, username, name: name || username, avatar: '', messages: [] };
}

/* ── «Избранное» — чат с самим собой (как в Telegram) ── */
function isFavorites(c) {
  return !!Store.user && c.username === Store.user.username;
}

function favoritesAvatar(cls) {
  const div = document.createElement('div');
  div.className = `avatar avatar--fav ${cls || ''}`;
  // Закладка — статичная иконка, пользовательских данных нет
  div.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="M17 3H7c-1.1 0-2 .9-2 2v16l7-3 7 3V5c0-1.1-.9-2-2-2z"/></svg>';
  return div;
}

async function loadContactAvatar(contact) {
  const fn = await Api.avatarFileName(contact.id);
  if (fn && !fn.includes('не найден')) {
    contact.avatar = Api.avatarUrl(fn);
    UI.refreshAvatars(contact);
  }
}

/* ─────────────────────── WebSocket чата ─────────────────────── */
const Chat = {
  ws: null,
  closed: false,

  connect() {
    this.closed = false;
    this._loop();
  },

  disconnect() {
    this.closed = true;
    try { this.ws?.close(); } catch { /* уже закрыт */ }
  },

  async _loop() {
    while (!this.closed) {
      try {
        await this._connectOnce();
      } catch { /* обрыв — переподключимся */ }
      UI.setConnected(false);
      if (this.closed) return;
      await new Promise(res => setTimeout(res, 3000));
    }
  },

  _connectOnce() {
    return new Promise((resolve, reject) => {
      const ws = new WebSocket(Server.wsUrl(`/Chat/ws?userId=${Store.user.id}`));
      this.ws = ws;
      ws.onopen = () => UI.setConnected(true);
      ws.onmessage = e => {
        const data = String(e.data);
        // Команда удаления сообщения у собеседника
        if (data.startsWith(`DEL${WS_SEP}`)) handleDeleteNotification(data);
        else routeMessage(data);
      };
      ws.onerror = () => reject(new Error('ws error'));
      ws.onclose = () => resolve();
    });
  },

  send(text) {
    if (!this.ws || this.ws.readyState !== WebSocket.OPEN) return false;
    this.ws.send(text);
    return true;
  },
};

/* ─────────────────────── Удаление сообщений ─────────────────────── */
let _menuTarget = null;

function showMessageMenu(contact, m) {
  if (!contact || !m) return;
  _menuTarget = { contact, m };
  // В «Избранном» собеседник — ты сам, «удалить у всех» не имеет смысла
  $('msg-del-all').classList.toggle('hidden', isFavorites(contact));
  $('msg-menu').classList.remove('hidden');
}

function hideMessageMenu() {
  _menuTarget = null;
  $('msg-menu').classList.add('hidden');
}

async function deleteTargetMessage(scope) {
  const t = _menuTarget;
  hideMessageMenu();
  if (!t) return;
  UI.removeMessage(t.contact, t.m);
  try {
    await Api.deleteMessage(Store.user.id, t.contact.username, scope, t.m.time, t.m.text);
  } catch {
    UI.toast('Не удалось удалить на сервере — проверь соединение');
  }
}

// Сервер прислал «DEL▫#▫username▫#▫time▫#▫text» — собеседник удалил сообщение у всех
function handleDeleteNotification(raw) {
  const rest = raw.slice(`DEL${WS_SEP}`.length);
  const i1 = rest.indexOf(WS_SEP);
  if (i1 < 0) return;
  const username = rest.slice(0, i1);
  const r2 = rest.slice(i1 + WS_SEP.length);
  const i2 = r2.indexOf(WS_SEP);
  if (i2 < 0) return;
  const time = r2.slice(0, i2);
  const text = r2.slice(i2 + WS_SEP.length);

  const contact = Store.findContact(username);
  if (!contact) return;
  const m = contact.messages.find(x => x.time === time && x.text === text);
  if (m) UI.removeMessage(contact, m);
}

/* ─────────────────────── Отправка сообщений ─────────────────────── */
async function sendMessage(contact, text, type = 'Text', serverAddress = '') {
  const m = {
    sender: Store.user.username,
    receiver: contact.username,
    type,
    time: nowFull(),
    serverAddress,
    text,
  };
  const raw = serializeMessage(m);

  if (!Chat.send(`SEND${WS_SEP}0${WS_SEP}${contact.username}${WS_SEP}${raw}`)) {
    UI.toast('Нет соединения с сервером — попробуй ещё раз');
    return false;
  }
  // Дублируем в POST /messages для сохранения на сервере (как десктопный клиент)
  Api.postMessage(raw).catch(() => { /* история догрузится позже */ });
  return true;
}

/* ─────────────────────── Вход и загрузка данных ─────────────────────── */
function parseLoginResponse(payload, password) {
  // Формат: Id▫Login▫Name▫Username▫Avatar▫FolderName▫Icon▫IsCanRedact▫ContactsCount▫(id&username&name▫)*
  const p = payload.split(SEP);
  const user = {
    id: parseInt(p[0], 10),
    login: p[1],
    name: p[2],
    username: p[3],
    avatarFile: p[4],
    password,
  };
  const contacts = [];
  for (let i = 9; i < p.length - 1; i++) {
    const c = p[i].split('&');
    if (c.length >= 3) contacts.push(makeContact(parseInt(c[0], 10), c[1], c[2]));
  }
  return { user, contacts };
}

function isErrorResponse(text) {
  return text.startsWith('Пользователь с таким логином не существует')
    || text.startsWith('Неверный пароль')
    || text.startsWith('Ошибка')
    || text.startsWith('Все поля')
    || text.startsWith('Пользователь с таким логином уже существует')
    || text.startsWith('Пользователь с таким именем уже существует');
}

async function enterApp(payload, login, password) {
  const { user, contacts } = parseLoginResponse(payload, password);
  Store.user = user;
  Store.contacts = contacts;

  localStorage.setItem('tbg.login', login);
  localStorage.setItem('tbg.password', password);

  UI.showMain();
  UI.renderChatList();
  UI.renderSettings();

  // Папки контактов (создаются в десктопном клиенте, здесь работают как фильтры)
  Api.getFolders(user.id).then(folders => {
    Store.folders = folders;
    UI.renderFolderChips();
  });

  contacts.forEach(loadContactAvatar);
  if (user.avatarFile) {
    Store.user.avatar = Api.avatarUrl(user.avatarFile);
    UI.renderSettings();
  }

  // История, затем реалтайм
  try {
    const h = await Api.history(user.id);
    for (const raw of h.split(MSG_SEP)) {
      if (raw.trim()) await routeMessage(raw);
    }
    UI.renderChatList();
  } catch { /* без истории тоже работаем */ }

  Chat.connect();
  Voice.startPolling(); // слушаем входящие звонки
}

/* ═══════════════════════════ UI ═══════════════════════════ */
const $ = id => document.getElementById(id);

const UI = {
  screens: ['screen-login', 'screen-register', 'screen-main'],

  show(id) {
    this.screens.forEach(s => $(s).classList.toggle('hidden', s !== id));
  },
  showMain() { this.show('screen-main'); },

  toast(text, ms = 2600) {
    const t = $('toast');
    t.textContent = text;
    t.classList.remove('hidden');
    clearTimeout(this._toastTimer);
    this._toastTimer = setTimeout(() => t.classList.add('hidden'), ms);
  },

  setConnected(on) {
    $('conn-badge').classList.toggle('conn-badge--off', !on);
  },

  /* ── Аватары ── */
  avatarNode(contact, cls) {
    const div = document.createElement('div');
    div.className = `avatar ${cls || ''}`;
    if (contact.avatar) {
      const img = document.createElement('img');
      img.src = contact.avatar;
      img.alt = '';
      img.onerror = () => { img.remove(); div.textContent = initials(contact.name); };
      div.appendChild(img);
    } else {
      div.textContent = initials(contact.name);
    }
    return div;
  },

  refreshAvatars() {
    this.renderChatList();
    if (Store.activeContact) this.updateChatHeader();
  },

  /* ── Папки контактов ── */
  renderFolderChips() {
    const box = $('folder-chips');
    box.textContent = '';
    if (!Store.folders.length) {
      box.classList.add('hidden');
      return;
    }
    box.classList.remove('hidden');

    const makeChip = (label, folder) => {
      const chip = document.createElement('button');
      chip.className = 'folder-chip' + (Store.activeFolder === folder ? ' folder-chip--active' : '');
      chip.textContent = label;
      chip.addEventListener('click', () => {
        Store.activeFolder = folder;
        this.renderFolderChips();
        this.renderChatList();
      });
      box.appendChild(chip);
    };

    makeChip('💬 Все чаты', null);
    for (const f of Store.folders) makeChip(`${f.icon || '📁'} ${f.name}`, f);
  },

  /* ── Список чатов ── */
  renderChatList() {
    const list = $('chat-list');
    const q = $('chat-search').value.trim().toLowerCase();
    list.textContent = '';

    let contacts = Store.contacts;
    if (Store.activeFolder) {
      contacts = contacts.filter(c => Store.activeFolder.usernames.includes(c.username));
    }
    if (q) {
      const qq = q.replace(/^@/, ''); // @ отбрасываем и для поиска по своим
      contacts = contacts.filter(c =>
        c.name.toLowerCase().includes(qq) || c.username.toLowerCase().includes(qq));
    }

    if (!contacts.length) {
      // При @поиске пустоту не показываем — снизу появится «Глобальный поиск»
      if (q.startsWith('@')) return;
      const empty = document.createElement('div');
      empty.className = 'chat-list-empty';
      empty.textContent = q ? 'Ничего не найдено' : 'Пока нет чатов.\nНайди собеседника: введи @логин в поиске.';
      list.appendChild(empty);
      return;
    }

    // «Избранное» (чат с собой) — всегда первым
    contacts = [...contacts].sort((a, b) =>
      (isFavorites(b) ? 1 : 0) - (isFavorites(a) ? 1 : 0));

    for (const c of contacts) {
      const item = document.createElement('div');
      item.className = 'chat-item';
      item.appendChild(isFavorites(c) ? favoritesAvatar() : this.avatarNode(c));

      const body = document.createElement('div');
      body.className = 'chat-item-body';

      const top = document.createElement('div');
      top.className = 'chat-item-top';
      const name = document.createElement('span');
      name.className = 'chat-item-name';
      name.textContent = isFavorites(c) ? 'Избранное' : c.name;
      const time = document.createElement('span');
      time.className = 'chat-item-time';
      const last = c.messages[c.messages.length - 1];
      time.textContent = last ? timeShort(last.time) : '';
      top.append(name, time);

      const preview = document.createElement('div');
      preview.className = 'chat-item-preview';
      preview.textContent = last
        ? (last.type === 'File' ? '📎 Файл' : (last.outgoing ? 'Вы: ' : '') + last.text)
        : `@${c.username}`;

      body.append(top, preview);
      item.appendChild(body);
      item.addEventListener('click', () => this.openChat(c));
      list.appendChild(item);
    }
  },

  /* ── Экран переписки ── */
  openChat(contact) {
    Store.activeContact = contact;
    this.updateChatHeader();
    this.renderMessages();
    $('screen-chat').classList.remove('hidden');
    requestAnimationFrame(() => $('screen-chat').classList.add('open'));
  },

  closeChat() {
    Store.activeContact = null;
    $('screen-chat').classList.remove('open');
    setTimeout(() => $('screen-chat').classList.add('hidden'), 320);
    this.renderChatList();
  },

  updateChatHeader() {
    const c = Store.activeContact;
    const fav = isFavorites(c);
    $('chat-header-name').textContent = fav ? 'Избранное' : c.name;
    $('chat-header-username').textContent = fav ? 'ваши сохранённые сообщения' : `@${c.username}`;
    const holder = $('chat-header-avatar');
    const av = fav ? favoritesAvatar('avatar--sm') : this.avatarNode(c, 'avatar--sm');
    holder.replaceWith(Object.assign(av, { id: 'chat-header-avatar' }));
    // Звонок самому себе не нужен
    $('btn-call').style.display = fav ? 'none' : '';
  },

  renderMessages() {
    const box = $('messages');
    box.textContent = '';
    let lastKey = '';
    for (const m of Store.activeContact.messages) {
      const key = dateKeyOf(m.time);
      if (key && key !== lastKey) {
        box.appendChild(this.dateSeparator(key));
        lastKey = key;
      }
      box.appendChild(this.bubbleNode(m));
    }
    box.scrollTop = box.scrollHeight;
  },

  dateSeparator(key) {
    const el = document.createElement('div');
    el.className = 'messages-date';
    el.textContent = dateLabel(key);
    return el;
  },

  bubbleNode(m) {
    let el;
    if (m.type === 'File') {
      const url = m.serverAddress && m.serverAddress.startsWith('http')
        ? m.serverAddress
        : Api.fileUrl(m.text);
      el = document.createElement('a');
      el.href = url;
      el.target = '_blank';
      el.rel = 'noopener';
      el.className = `bubble bubble--file ${m.outgoing ? 'bubble--out' : 'bubble--in'}`;
      const name = document.createElement('span');
      name.className = 'file-name';
      name.textContent = `📎 ${m.text}`;
      // Фото — инлайн-превью, как в десктопном клиенте; при ошибке загрузки
      // остаётся обычная ссылка с именем файла
      if (/\.(png|jpe?g|gif|bmp|webp)$/i.test(m.text || '')) {
        // bubble--photo: узкая рамка + время плашкой поверх фото
        el.classList.add('bubble--photo');
        const img = document.createElement('img');
        img.className = 'bubble-photo';
        img.src = url;
        img.alt = m.text;
        img.loading = 'lazy';
        name.style.display = 'none';
        img.onerror = () => { img.remove(); name.style.display = ''; el.classList.remove('bubble--photo'); };
        el.appendChild(img);
        // Клик по фото — просмотр в лайтбоксе, а не скачивание
        // (сервер отдаёт /upload с Content-Disposition: attachment)
        el.addEventListener('click', e => {
          e.preventDefault();
          PhotoViewer.open(url, m.text);
        });
      }
      el.appendChild(name);
    } else {
      el = document.createElement('div');
      el.className = `bubble ${m.outgoing ? 'bubble--out' : 'bubble--in'}`;
      const text = document.createElement('span');
      text.textContent = m.text;
      el.appendChild(text);
    }
    const time = document.createElement('span');
    time.className = 'bubble-time';
    time.textContent = timeShort(m.time);
    el.appendChild(time);

    // Долгое нажатие (моб.) или правый клик (десктоп) — меню удаления
    let pressTimer;
    el.addEventListener('touchstart', () => {
      pressTimer = setTimeout(() => showMessageMenu(Store.activeContact, m), 500);
    }, { passive: true });
    el.addEventListener('touchend', () => clearTimeout(pressTimer));
    el.addEventListener('touchmove', () => clearTimeout(pressTimer));
    el.addEventListener('contextmenu', e => {
      e.preventDefault();
      showMessageMenu(Store.activeContact, m);
    });
    return el;
  },

  onMessageAdded(contact, m) {
    if (Store.activeContact === contact) {
      const box = $('messages');
      const atBottom = box.scrollHeight - box.scrollTop - box.clientHeight < 80;
      // Разделитель дня, если сообщение — первое за новый день
      const msgs = contact.messages;
      const idx = msgs.indexOf(m);
      const prev = idx > 0 ? msgs[idx - 1] : null;
      const key = dateKeyOf(m.time);
      if (key && (!prev || dateKeyOf(prev.time) !== key)) box.appendChild(this.dateSeparator(key));
      box.appendChild(this.bubbleNode(m));
      if (atBottom || m.outgoing) box.scrollTop = box.scrollHeight;
    }
    this.renderChatList();
  },

  removeMessage(contact, m) {
    const i = contact.messages.indexOf(m);
    if (i >= 0) contact.messages.splice(i, 1);
    if (Store.activeContact === contact) this.renderMessages();
    this.renderChatList();
  },

  /* ── Настройки ── */
  renderSettings() {
    const u = Store.user;
    if (!u) return;
    $('settings-name').textContent = u.name;
    $('settings-username').textContent = `@${u.username}`;
    const holder = $('settings-avatar');
    holder.replaceWith(Object.assign(
      this.avatarNode({ name: u.name, avatar: u.avatar }, 'avatar--xl'), { id: 'settings-avatar' }));
    $('server-override').value = localStorage.getItem('tbg.serverOverride') || '';
    $('app-version').textContent = APP_VERSION;
  },

  switchTab(tab) {
    $('tab-chats').classList.toggle('hidden', tab !== 'chats');
    $('tab-settings').classList.toggle('hidden', tab !== 'settings');
    document.querySelectorAll('.tabbar-item').forEach(b =>
      b.classList.toggle('tabbar-item--active', b.dataset.tab === tab));
  },

  /* ── Звонки ── */
  _showCallScreen(contact, name) {
    const holder = $('call-avatar');
    const av = isFavorites(contact) ? favoritesAvatar('avatar--call') : this.avatarNode(contact, 'avatar--call');
    holder.replaceWith(Object.assign(av, { id: 'call-avatar' }));
    $('call-name').textContent = name;
    $('screen-call').classList.remove('hidden');
  },

  showCall(contact, state) {
    this._showCallScreen(contact, contact.name);
    this.setCallState(state);
  },

  setCallState(state) {
    const incoming = state === 'incoming';
    const active = state === 'active';
    $('call-incoming-actions').classList.toggle('hidden', !incoming);
    $('call-active-actions').classList.toggle('hidden', incoming);
    if (state === 'outgoing') $('call-status').textContent = 'Вызов…';
    else if (state === 'connecting') $('call-status').textContent = 'Соединение…';
    else if (active) $('call-status').textContent = 'Идёт разговор';
    this.setMicUI(true);
  },

  showIncoming(callerUsername) {
    const c = Store.findContact(callerUsername) || makeContact(0, callerUsername, callerUsername);
    this._showCallScreen(c, c.name);
    $('call-status').textContent = 'Входящий звонок';
    $('call-incoming-actions').classList.remove('hidden');
    $('call-active-actions').classList.add('hidden');
  },

  hideCall() {
    $('screen-call').classList.add('hidden');
  },

  setCallTime(t) { $('call-status').textContent = t; },

  setMicUI(on) {
    $('btn-call-mic').classList.toggle('muted', !on);
  },
};

function initials(name) {
  return (name || '?').trim().split(/\s+/).slice(0, 2).map(w => w[0]).join('').toUpperCase() || '?';
}

/* ─────────────────────── Обработчики ─────────────────────── */
function showError(id, text) {
  const el = $(id);
  el.textContent = text;
  el.classList.remove('hidden');
}

async function doLogin() {
  const login = $('login-login').value.trim();
  const password = $('login-password').value;
  if (!login || !password) return showError('login-error', 'Заполни все поля');

  $('btn-login').disabled = true;
  $('btn-login').textContent = 'Входим…';
  try {
    const resp = await Api.login(login, password);
    if (isErrorResponse(resp)) return showError('login-error', resp);
    await enterApp(resp, login, password);
  } catch {
    showError('login-error', 'Сервер недоступен. Проверь соединение.');
  } finally {
    $('btn-login').disabled = false;
    $('btn-login').textContent = 'Войти';
  }
}

async function doRegister() {
  const name = $('reg-name').value.trim();
  const login = $('reg-login').value.trim();
  const p1 = $('reg-password').value;
  const p2 = $('reg-password2').value;

  if (!name || !login || !p1) return showError('reg-error', 'Заполни все поля');
  if (p1 !== p2) return showError('reg-error', 'Пароли не совпадают');
  if (p1.length < 4) return showError('reg-error', 'Пароль должен быть не менее 4 символов');
  if (login.length < 3) return showError('reg-error', 'Логин должен быть не менее 3 символов');
  if (/[-▫\s]/.test(login)) return showError('reg-error', 'Логин не может содержать пробелы и дефисы');

  $('btn-register').disabled = true;
  try {
    const resp = await Api.register(login, p1, login, name);
    if (isErrorResponse(resp)) return showError('reg-error', resp);
    await enterApp(resp, login, p1);
    UI.toast('Аккаунт создан. Добро пожаловать!');
  } catch {
    showError('reg-error', 'Сервер недоступен. Проверь соединение.');
  } finally {
    $('btn-register').disabled = false;
  }
}

async function doSend() {
  const input = $('msg-input');
  const text = input.value.trim();
  const contact = Store.activeContact;
  if (!text || !contact) return;
  if (await sendMessage(contact, text)) {
    input.value = '';
    input.style.height = 'auto';
  }
}

/* Нормализация фото перед отправкой:
   1) применяет EXIF-ориентацию (иначе на ПК-клиенте фото с телефона перевёрнуто —
      WPF не читает EXIF);
   2) пережимает в JPEG q0.92 с ограничением длинной стороны 2560px — качество
      сохраняется, а вес предсказуем. GIF и не-картинки не трогаем. */
async function normalizePhoto(file) {
  if (!/^image\//.test(file.type) || file.type === 'image/gif') return file;
  try {
    let bmp;
    try { bmp = await createImageBitmap(file, { imageOrientation: 'from-image' }); }
    catch { bmp = await createImageBitmap(file); } // старые Safari без опций
    const MAX = 2560;
    const k = Math.min(1, MAX / Math.max(bmp.width, bmp.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bmp.width * k);
    canvas.height = Math.round(bmp.height * k);
    canvas.getContext('2d').drawImage(bmp, 0, 0, canvas.width, canvas.height);
    bmp.close && bmp.close();
    const blob = await new Promise(r => canvas.toBlob(r, 'image/jpeg', 0.92));
    if (!blob) return file;
    const name = file.name.replace(/\.[^.]+$/, '') + '.jpg';
    return new File([blob], name, { type: 'image/jpeg' });
  } catch {
    return file; // не смогли обработать — шлём как есть
  }
}

async function doSendFile(file) {
  const contact = Store.activeContact;
  if (!file || !contact) return;
  UI.toast('Загружаем файл…');
  try {
    file = await normalizePhoto(file);
    const stored = await Api.uploadFile(file);
    await sendMessage(contact, stored, 'File', Api.fileUrl(stored));
  } catch {
    UI.toast('Не удалось загрузить файл');
  }
}

/* ─────────────── Глобальный поиск пользователей (через @) ───────────────
   Ищет среди ВСЕХ пользователей сервера по подстроке логина/имени
   (GET /Users/find/{q}), показывает секцию под своими контактами.
   Тап по результату добавляет контакт и открывает чат. */
const GlobalSearch = {
  _timer: null,
  _seq: 0,

  schedule() {
    clearTimeout(this._timer);
    const raw = $('chat-search').value.trim();
    if (!raw.startsWith('@') || raw.length < 3) return; // @ + минимум 2 символа
    this._timer = setTimeout(() => this.run(raw.slice(1)), 300);
  },

  async run(q) {
    const seq = ++this._seq;
    let text = '';
    try {
      const r = await fetchWithTimeout(`${Server.address}/Users/find/${encodeURIComponent(q)}`, 8000);
      text = r.ok ? (await r.text()).trim() : '';
    } catch { return; }

    // Пока ждали ответ, запрос мог измениться — не рисуем устаревшее
    if (seq !== this._seq) return;
    const raw = $('chat-search').value.trim();
    if (!raw.startsWith('@') || raw.slice(1) !== q) return;

    const list = $('chat-list');
    list.querySelectorAll('.gs-header, .gs-item').forEach(el => el.remove());
    if (!text) return;

    const header = document.createElement('div');
    header.className = 'gs-header';
    header.textContent = 'Глобальный поиск';
    list.appendChild(header);

    for (const rawU of text.split(MSG_SEP)) {
      const [, username, name] = rawU.split(SEP);
      if (!username || username === Store.user.username) continue;
      if (Store.findContact(username)) continue; // свои контакты уже выше

      const item = document.createElement('div');
      item.className = 'chat-item gs-item';

      const av = document.createElement('div');
      av.className = 'avatar';
      av.textContent = initials(name || username);
      item.appendChild(av);

      const body = document.createElement('div');
      body.className = 'chat-item-body';
      const top = document.createElement('div');
      top.className = 'chat-item-top';
      const nm = document.createElement('span');
      nm.className = 'chat-item-name';
      nm.textContent = name || username;
      top.appendChild(nm);
      const sub = document.createElement('div');
      sub.className = 'chat-item-preview';
      sub.textContent = `@${username}`;
      body.append(top, sub);
      item.appendChild(body);

      item.addEventListener('click', () => this.add(username));
      list.appendChild(item);
    }
  },

  async add(username) {
    try {
      const r = await Api.addContact(Store.user.id, username, '');
      if (!r.ok) { UI.toast('Пользователь не найден'); return; }
      const [id, srvName, srvUsername] = r.text.split(SEP);
      const contact = makeContact(parseInt(id, 10), srvUsername, srvName);
      Store.contacts.push(contact);
      loadContactAvatar(contact);
      $('chat-search').value = '';
      UI.renderChatList();
      UI.openChat(contact);
    } catch {
      UI.toast('Не удалось добавить контакт');
    }
  },
};

function doLogout() {
  localStorage.removeItem('tbg.login');
  localStorage.removeItem('tbg.password');
  Chat.disconnect();
  Voice.stopPolling();
  if (Voice.active) Voice.hangup();
  Store.user = null;
  Store.contacts = [];
  Store.folders = [];
  Store.activeFolder = null;
  Store.activeContact = null;
  $('screen-chat').classList.remove('open');
  $('screen-chat').classList.add('hidden');
  UI.show('screen-login');
}

/* ─────────────────────── Запуск ─────────────────────── */
function bindEvents() {
  $('btn-login').addEventListener('click', doLogin);
  $('login-password').addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
  $('btn-goto-register').addEventListener('click', () => UI.show('screen-register'));
  $('btn-goto-login').addEventListener('click', () => UI.show('screen-login'));
  $('btn-register').addEventListener('click', doRegister);

  $('chat-search').addEventListener('input', () => {
    UI.renderChatList();
    GlobalSearch.schedule(); // @логин — поиск среди всех пользователей сервера
  });
  $('btn-chat-back').addEventListener('click', () => UI.closeChat());
  $('btn-send').addEventListener('click', doSend);

  const msgInput = $('msg-input');
  msgInput.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); doSend(); }
  });
  msgInput.addEventListener('input', () => {
    msgInput.style.height = 'auto';
    msgInput.style.height = Math.min(msgInput.scrollHeight, 96) + 'px';
  });

  $('msg-file').addEventListener('change', e => {
    doSendFile(e.target.files[0]);
    e.target.value = '';
  });

  // Кнопки «+» больше нет: новые собеседники добавляются через поиск @логин
  // (секция «Глобальный поиск» под своими контактами)

  // Меню удаления сообщения
  $('msg-del-mine').addEventListener('click', () => deleteTargetMessage('self'));
  $('msg-del-all').addEventListener('click', () => deleteTargetMessage('all'));
  $('msg-menu-cancel').addEventListener('click', hideMessageMenu);
  $('msg-menu').addEventListener('click', e => { if (e.target.id === 'msg-menu') hideMessageMenu(); });

  document.querySelectorAll('.tabbar-item').forEach(b =>
    b.addEventListener('click', () => UI.switchTab(b.dataset.tab)));

  $('btn-logout').addEventListener('click', doLogout);
  $('btn-theme').addEventListener('click', () => Theme.cycle());

  $('server-override').addEventListener('change', e => {
    const v = e.target.value.trim();
    if (v) localStorage.setItem('tbg.serverOverride', v);
    else localStorage.removeItem('tbg.serverOverride');
    UI.toast('Адрес сохранён. Перезапусти приложение.');
  });

  // Смена аватара: сперва окно обрезки (как в десктопном клиенте), потом загрузка
  $('avatar-file').addEventListener('change', e => {
    const file = e.target.files[0];
    e.target.value = '';
    if (file) AvatarCrop.open(file);
  });
  $('btn-crop-cancel').addEventListener('click', () => AvatarCrop.close());
  $('btn-crop-ok').addEventListener('click', () => AvatarCrop.apply());
  $('crop-zoom').addEventListener('input', () => AvatarCrop.onZoom());

  // Звонки
  $('btn-call').addEventListener('click', () => {
    if (Store.activeContact && !isFavorites(Store.activeContact)) Voice.startOutgoing(Store.activeContact);
  });
  $('btn-call-accept').addEventListener('click', () => Voice.acceptIncoming());
  $('btn-call-decline').addEventListener('click', () => Voice.declineIncoming());
  $('btn-call-mic').addEventListener('click', () => Voice.toggleMic());
  $('btn-call-hangup').addEventListener('click', () => Voice.hangup());

  $('btn-hint-close').addEventListener('click', () => {
    $('install-hint').classList.add('hidden');
    localStorage.setItem('tbg.hintShown', '1');
  });

  // Просмотр фото: крестик или клик по фону закрывают (клик по самому фото — нет)
  $('pv-close').addEventListener('click', () => PhotoViewer.close());
  $('photo-viewer').addEventListener('click', e => {
    if (e.target === e.currentTarget) PhotoViewer.close();
  });

  // ── Свайп слева направо — жест «назад» (как в iOS) ──
  // Начинать можно из любой точки ЛЕВОЙ ПОЛОВИНЫ экрана, не только с края
  let edgeSwipe = null;
  document.addEventListener('touchstart', e => {
    if (e.touches.length !== 1) { edgeSwipe = null; return; }
    const t = e.touches[0];
    edgeSwipe = t.clientX < window.innerWidth * 0.5 ? { x: t.clientX, y: t.clientY } : null;
  }, { passive: true });
  document.addEventListener('touchend', e => {
    if (!edgeSwipe) return;
    const t = e.changedTouches[0];
    const dx = t.clientX - edgeSwipe.x;
    const dy = Math.abs(t.clientY - edgeSwipe.y);
    edgeSwipe = null;
    if (dx < 70 || dy > 60) return;
    // Приоритет «назад»: открытые модалки → экран чата
    if (PhotoViewer.isOpen()) { PhotoViewer.close(); return; }
    if (!$('modal-crop').classList.contains('hidden')) { AvatarCrop.close(); return; }
    if (!$('msg-menu').classList.contains('hidden')) { hideMessageMenu(); return; }
    if ($('screen-chat').classList.contains('open')) UI.closeChat();
  }, { passive: true });
}

/* ─────────────── Тема: Авто (системная) / Светлая / Тёмная ─────────────── */
const Theme = {
  MODES: ['auto', 'light', 'dark'],
  LABELS: { auto: 'Авто', light: 'Светлая', dark: 'Тёмная' },

  current() { return localStorage.getItem('tbg.theme') || 'auto'; },

  apply(mode) {
    if (!this.MODES.includes(mode)) mode = 'auto';
    if (mode === 'auto') document.documentElement.removeAttribute('data-theme');
    else document.documentElement.setAttribute('data-theme', mode);
    localStorage.setItem('tbg.theme', mode);
    const lbl = $('theme-label');
    if (lbl) lbl.textContent = this.LABELS[mode];
  },

  cycle() {
    this.apply(this.MODES[(this.MODES.indexOf(this.current()) + 1) % this.MODES.length]);
  },
};

/* ─────────────── Просмотр фото (как ImageViewerWindow на ПК) ─────────────── */
const PhotoViewer = {
  open(url, name) {
    $('pv-img').src = url;
    const dl = $('pv-download');
    dl.href = url;
    dl.setAttribute('download', name || 'photo');
    $('photo-viewer').classList.remove('hidden');
  },
  close() {
    $('photo-viewer').classList.add('hidden');
    $('pv-img').src = '';
  },
  isOpen() { return !$('photo-viewer').classList.contains('hidden'); },
};

/* ─────────────── Обрезка аватара (как AvatarCropWindow на ПК) ───────────────
   Круглый предпросмотр, перетаскивание фото и ползунок масштаба.
   «Поставить» рендерит квадрат 512px в canvas и грузит на сервер. */
const AvatarCrop = {
  VIEW: 280,   // размер области предпросмотра (px, совпадает с CSS)
  OUT: 1024,   // размер итогового аватара (как в десктопном клиенте)

  img: null, url: null,
  scale: 1, minScale: 1,
  x: 0, y: 0,          // смещение центра фото от центра области
  _drag: null,

  open(file) {
    this.url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      this.img = img;
      this.minScale = Math.max(this.VIEW / img.naturalWidth, this.VIEW / img.naturalHeight);
      this.scale = this.minScale;
      this.x = 0; this.y = 0;
      $('crop-zoom').value = '0';
      $('crop-img').src = this.url;
      $('modal-crop').classList.remove('hidden');
      this._apply();
    };
    img.onerror = () => UI.toast('Не удалось открыть изображение');
    img.src = this.url;

    // Перетаскивание (мышь и палец) — pointer events
    const area = $('crop-area');
    if (!area._cropBound) {
      area._cropBound = true;
      area.addEventListener('pointerdown', e => {
        this._drag = { x: e.clientX, y: e.clientY, startX: this.x, startY: this.y };
        area.setPointerCapture(e.pointerId);
      });
      area.addEventListener('pointermove', e => {
        if (!this._drag) return;
        this.x = this._drag.startX + (e.clientX - this._drag.x);
        this.y = this._drag.startY + (e.clientY - this._drag.y);
        this._clamp();
        this._apply();
      });
      const stop = () => { this._drag = null; };
      area.addEventListener('pointerup', stop);
      area.addEventListener('pointercancel', stop);
    }
  },

  onZoom() {
    const v = parseFloat($('crop-zoom').value); // 0..1
    this.scale = this.minScale * (1 + v * 3);   // до 4x от «вписанного»
    this._clamp();
    this._apply();
  },

  // Фото не должно уезжать за края круга
  _clamp() {
    const maxX = Math.max(0, (this.img.naturalWidth * this.scale - this.VIEW) / 2);
    const maxY = Math.max(0, (this.img.naturalHeight * this.scale - this.VIEW) / 2);
    this.x = Math.min(maxX, Math.max(-maxX, this.x));
    this.y = Math.min(maxY, Math.max(-maxY, this.y));
  },

  _apply() {
    $('crop-img').style.transform =
      `translate(calc(-50% + ${this.x}px), calc(-50% + ${this.y}px)) scale(${this.scale})`;
  },

  close() {
    $('modal-crop').classList.add('hidden');
    if (this.url) { URL.revokeObjectURL(this.url); this.url = null; }
    this.img = null;
  },

  async apply() {
    if (!this.img) return;
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = this.OUT;
    const ctx = canvas.getContext('2d');
    const k = this.OUT / this.VIEW;
    const w = this.img.naturalWidth * this.scale;
    const h = this.img.naturalHeight * this.scale;
    // Левый верх фото в координатах области предпросмотра → в канву с масштабом k
    ctx.drawImage(this.img,
      (this.VIEW / 2 + this.x - w / 2) * k,
      (this.VIEW / 2 + this.y - h / 2) * k,
      w * k, h * k);

    const blob = await new Promise(res => canvas.toBlob(res, 'image/png'));
    this.close();
    if (!blob) { UI.toast('Не удалось обрезать изображение'); return; }
    try {
      const file = new File([blob], `avatar_${Store.user.id}_${Date.now()}.png`, { type: 'image/png' });
      const stored = await Api.uploadAvatar(Store.user.id, file);
      // ?t= обходит кэш картинок — аватар обновляется сразу
      Store.user.avatar = Api.avatarUrl(stored) + `?t=${Date.now()}`;
      UI.renderSettings();
      UI.toast('Аватар обновлён');
    } catch {
      UI.toast('Не удалось загрузить аватар');
    }
  },
};

function maybeShowInstallHint() {
  const isIos = /iphone|ipad|ipod/i.test(navigator.userAgent);
  const standalone = window.matchMedia('(display-mode: standalone)').matches
    || window.navigator.standalone === true;
  if (isIos && !standalone && !localStorage.getItem('tbg.hintShown')) {
    $('install-hint').classList.remove('hidden');
  }
}

async function boot() {
  bindEvents();
  Theme.apply(Theme.current()); // сохранённая тема — до отрисовки экранов
  $('app-version').textContent = APP_VERSION;

  // Service worker — оффлайн-оболочка и «настоящее приложение» для iOS
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('./sw.js').catch(() => { /* не критично */ });
  }

  maybeShowInstallHint();

  try {
    await Server.resolve();
  } catch {
    UI.toast('Сервер сейчас недоступен', 4000);
    return; // остаёмся на экране входа — попробует ещё раз при входе
  }

  // Автовход
  const login = localStorage.getItem('tbg.login');
  const password = localStorage.getItem('tbg.password');
  if (login && password) {
    try {
      const resp = await Api.login(login, password);
      if (!isErrorResponse(resp)) {
        await enterApp(resp, login, password);
        return;
      }
    } catch { /* сервер недоступен — показываем экран входа */ }
  }
  UI.show('screen-login');
}

boot();
