/* ═══════════════════════════════════════════════════════════════
   Tebegram Web — мобильный PWA-клиент.
   Работает с тем же сервером и протоколом, что и десктопный клиент:
   разделитель полей ▫, сообщения Sender▫Reciver▫Type▫Time▫ServerAdress▫Text,
   реалтайм через WebSocket /Chat/ws.
   ═══════════════════════════════════════════════════════════════ */
'use strict';

const APP_VERSION = '1.0.3';
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

  // Инициатор звонка
  async startOutgoing(contact) {
    if (this.active) return;
    this.contact = contact;
    this.role = 'caller';
    UI.showCall(contact, 'outgoing');
    try {
      this.token = await this.createRoom(contact.username);
      await this._connectAudio(this.token);
      UI.setCallState('active');
      this._startTimer();
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
      await this._connectAudio(this.token);
      UI.showCall(this.contact, 'active');
      this._startTimer();
    } catch (e) {
      UI.toast('Не удалось подключиться: ' + (e.message || e));
      this.hangup();
    }
  },

  async _connectAudio(token) {
    // 1. Микрофон + аудиоконтекст на 48 кГц
    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, channelCount: 1 },
    });
    this.ctx = new (window.AudioContext || window.webkitAudioContext)({ sampleRate: SAMPLE_RATE });
    if (this.ctx.state === 'suspended') await this.ctx.resume();
    await this.ctx.audioWorklet.addModule('./voice-worklet.js');

    // 2. WebSocket комнаты
    this.ws = new WebSocket(this.wsUrl(token));
    this.ws.binaryType = 'arraybuffer';
    await new Promise((res, rej) => {
      this.ws.onopen = res;
      this.ws.onerror = () => rej(new Error('соединение не установлено'));
    });

    // 3. Захват: Float32 → Int16 → отправка
    const src = this.ctx.createMediaStreamSource(this.stream);
    this.captureNode = new AudioWorkletNode(this.ctx, 'tbg-capture');
    this.captureNode.port.onmessage = e => {
      if (!this.micEnabled || !this.ws || this.ws.readyState !== WebSocket.OPEN) return;
      this.ws.send(floatToInt16(e.data).buffer);
    };
    src.connect(this.captureNode);
    // подключаем к выходу через нулевую громкость, чтобы worklet работал (без эха себя)
    const mute = this.ctx.createGain();
    mute.gain.value = 0;
    this.captureNode.connect(mute).connect(this.ctx.destination);

    // 4. Воспроизведение входящего
    this.playbackNode = new AudioWorkletNode(this.ctx, 'tbg-playback');
    this.playbackNode.connect(this.ctx.destination);

    // 5. Входящие пакеты
    this.ws.onmessage = e => {
      if (typeof e.data === 'string') {
        if (e.data === 'CloseConnection') this.hangup(false);
        return;
      }
      this.playbackNode.port.postMessage(int16ToFloat32(new Int16Array(e.data)));
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
    try { this.ctx && this.ctx.close(); } catch {}

    this.ws = this.ctx = this.stream = this.captureNode = this.playbackNode = null;
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
      contacts = contacts.filter(c =>
        c.name.toLowerCase().includes(q) || c.username.toLowerCase().includes(q));
    }

    if (!contacts.length) {
      const empty = document.createElement('div');
      empty.className = 'chat-list-empty';
      empty.textContent = q ? 'Ничего не найдено' : 'Пока нет чатов.\nНажми «+», чтобы добавить контакт.';
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
      el = document.createElement('a');
      el.href = m.serverAddress && m.serverAddress.startsWith('http')
        ? m.serverAddress
        : Api.fileUrl(m.text);
      el.target = '_blank';
      el.rel = 'noopener';
      el.className = `bubble bubble--file ${m.outgoing ? 'bubble--out' : 'bubble--in'}`;
      const name = document.createElement('span');
      name.className = 'file-name';
      name.textContent = `📎 ${m.text}`;
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

async function doSendFile(file) {
  const contact = Store.activeContact;
  if (!file || !contact) return;
  UI.toast('Загружаем файл…');
  try {
    const stored = await Api.uploadFile(file);
    await sendMessage(contact, stored, 'File', Api.fileUrl(stored));
  } catch {
    UI.toast('Не удалось загрузить файл');
  }
}

async function doAddContact() {
  const username = $('add-username').value.trim().replace(/^@/, '');
  const name = $('add-name').value.trim();
  if (!username) return showError('add-error', 'Укажи имя пользователя');

  const existing = Store.findContact(username);
  if (existing) {
    $('modal-add').classList.add('hidden');
    UI.openChat(existing);
    return;
  }

  try {
    const r = await Api.addContact(Store.user.id, username, name);
    if (!r.ok) return showError('add-error', 'Пользователь не найден');
    // ответ: id▫name▫username
    const [id, srvName, srvUsername] = r.text.split(SEP);
    const contact = makeContact(parseInt(id, 10), srvUsername, name || srvName);
    Store.contacts.push(contact);
    loadContactAvatar(contact);
    UI.renderChatList();
    $('modal-add').classList.add('hidden');
    UI.openChat(contact);
  } catch {
    showError('add-error', 'Сервер недоступен');
  }
}

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

  $('chat-search').addEventListener('input', () => UI.renderChatList());
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

  $('btn-add-contact').addEventListener('click', () => {
    $('add-username').value = '';
    $('add-name').value = '';
    $('add-error').classList.add('hidden');
    $('modal-add').classList.remove('hidden');
  });
  $('btn-add-cancel').addEventListener('click', () => $('modal-add').classList.add('hidden'));
  $('btn-add-ok').addEventListener('click', doAddContact);

  // Меню удаления сообщения
  $('msg-del-mine').addEventListener('click', () => deleteTargetMessage('self'));
  $('msg-del-all').addEventListener('click', () => deleteTargetMessage('all'));
  $('msg-menu-cancel').addEventListener('click', hideMessageMenu);
  $('msg-menu').addEventListener('click', e => { if (e.target.id === 'msg-menu') hideMessageMenu(); });

  document.querySelectorAll('.tabbar-item').forEach(b =>
    b.addEventListener('click', () => UI.switchTab(b.dataset.tab)));

  $('btn-logout').addEventListener('click', doLogout);

  $('server-override').addEventListener('change', e => {
    const v = e.target.value.trim();
    if (v) localStorage.setItem('tbg.serverOverride', v);
    else localStorage.removeItem('tbg.serverOverride');
    UI.toast('Адрес сохранён. Перезапусти приложение.');
  });

  $('avatar-file').addEventListener('change', async e => {
    const file = e.target.files[0];
    if (!file) return;
    try {
      const stored = await Api.uploadAvatar(Store.user.id, file);
      Store.user.avatar = Api.avatarUrl(stored);
      UI.renderSettings();
      UI.toast('Аватар обновлён');
    } catch {
      UI.toast('Не удалось загрузить аватар');
    }
    e.target.value = '';
  });

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
}

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
