/* Service worker Tebegram Web.
   Кэширует оболочку приложения (app shell) — благодаря этому PWA
   запускается мгновенно и открывается даже без сети.
   Запросы к API (другой origin) не трогаем. */
'use strict';

const CACHE = 'tebegram-shell-v1.0.10';
const SHELL = [
  './',
  './index.html',
  './styles.css',
  './app.js',
  './voice-worklet.js',
  './manifest.webmanifest',
  './icons/icon-192.png',
  './icons/icon-512.png',
  './icons/apple-touch-icon.png',
];

self.addEventListener('install', e => {
  e.waitUntil(caches.open(CACHE).then(c => c.addAll(SHELL)).then(() => self.skipWaiting()));
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys()
      .then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // Только свои статические файлы; API и raw.githubusercontent идут напрямую
  if (e.request.method !== 'GET' || url.origin !== location.origin) return;
  // На сервере приложение живёт под /app — это тоже наш origin, но API-пути не кэшируем
  if (!SHELL.some(p => url.pathname.endsWith(p.replace('./', '/')) || url.pathname.endsWith('/'))) {
    return;
  }

  // Сначала сеть (чтобы обновления доезжали), при неудаче — кэш
  e.respondWith(
    fetch(e.request)
      .then(resp => {
        const copy = resp.clone();
        caches.open(CACHE).then(c => c.put(e.request, copy));
        return resp;
      })
      .catch(() => caches.match(e.request, { ignoreSearch: true }))
  );
});
