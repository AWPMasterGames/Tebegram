# Tebegram — Animation Reference

> Документ для backend-разработчика: когда и как срабатывают анимации на клиенте,
> особенно те, что связаны с сетевыми событиями.

---

## Структура окон

```
MessengerWindow   — основное окно чата
VoiceRoom         — окно голосового / видео звонка
```

---

## MessengerWindow

Большинство переходов здесь **мгновенные** — без плавной анимации, просто смена `Visibility`.

### Загрузка сообщений при открытии чата

```
[открытие чата] ──► GetMessages() (async)
                         │
                    ┌────▼────────────────────────────────┐
                    │  MessagesLoadingOverlay  Visible     │  ← крутилка "Загрузка..."
                    └────────────────────────────────────┘
                         │
                    [API вернул сообщения]
                         │
                    ┌────▼────────────────────────────────┐
                    │  MessagesLoadingOverlay  Collapsed   │  ← мгновенно
                    │  LBMessages заполнен                 │
                    │  ScrollToBottom()                    │
                    └────────────────────────────────────┘
```

**Важно для бэка:** страница меняется сразу после получения ответа от API, никаких задержек нет.

---

### Выбор чата из списка

```
[клик по LBChats]
        │
        ├─► GridMessege.Visibility     = Visible   (панель ввода)
        └─► GridContactPanel.Visibility = Visible   (шапка с контактом)
```

Оба элемента скрыты до первого выбора чата. Переход мгновенный.

---

### Авто-скролл к последнему сообщению

Вызывается в 5 местах:
- при открытии чата
- при отправке сообщения
- при получении входящего сообщения (push от сервера)

```csharp
// MessengerWindow.xaml.cs
ScrollMessagesToBottom()  →  ScrollViewer.ScrollToBottom()
```

---

### Hover / press на кнопках

| Состояние | Opacity |
|-----------|---------|
| Normal    | 1.0     |
| Hover     | 0.82    |
| Pressed   | 0.62    |

Реализовано через `ControlTemplate` trigger, без кода.

---

## VoiceRoom

Здесь полноценные анимации с таймингами, easing и Completed-callbacks.
Все анимации используют `CubicEase`.

---

### При открытии окна (Loaded)

```
[окно загружено]
        │
        ▼  400ms, EaseOut
┌───────────────────────────┐
│  ContentGrid              │
│  Opacity:    0 ──► 1      │
│  TranslateY: 12 ──► 0     │  ← появляется снизу
└───────────────────────────┘
```

---

### Кнопки управления окном (свернуть / закрыть)

```
[мышь вошла в окно]  ──►  WindowControls  Opacity 0 ──► 1  (150ms)
[мышь вышла из окна] ──►  WindowControls  Opacity 1 ──► 0  (250ms)
```

Реализовано в XAML через `EventTrigger` + `Storyboard`, без кода.

---

### Принятие входящего звонка

Самая сложная анимация. Состоит из трёх последовательных фаз.

```
[нажата кнопка "Принять"]
        │
        ▼ ФАЗА 1 — выход (220ms, EaseIn)
┌──────────────────────────────────────┐
│  DefaultButtonsPanel                 │
│  Opacity:    1 ──► 0                 │
│  TranslateY: 0 ──► 60               │  ← уезжает вниз
│                                      │
│  DefoultVoiceRoom                    │
│  Opacity:    1 ──► 0                 │
└──────────────────────────────────────┘
        │
        ▼ [Completed callback — здесь реально меняется страница]
        │  DefoultVoiceRoom.Visibility = Collapsed
        │
        ▼ ФАЗА 2 — вход (320ms, EaseOut)
┌──────────────────────────────────────┐
│  ActiveVoiceRoom.Visibility = Visible│  ← теперь показан
│  ActiveVoiceRoom                     │
│  Opacity:    0 ──► 1                 │
└──────────────────────────────────────┘
        │
        ▼ ФАЗА 3 — staggered-кнопки (задержка i × 50ms каждая)
┌──────────────────────────────────────────────────────┐
│  i=0  ScreenShare   Opacity 0►1, TranslateY 60►0     │  delay:   0ms
│  i=1  Camera        Opacity 0►1, TranslateY 60►0     │  delay:  50ms
│  i=2  Hangup        Opacity 0►1, TranslateY 60►0     │  delay: 100ms
│  i=3  Mic           Opacity 0►1, TranslateY 60►0     │  delay: 150ms
│  i=4  AddUser       Opacity 0►1, TranslateY 60►0     │  delay: 200ms
└──────────────────────────────────────────────────────┘
        каждая кнопка: 320ms, EaseOut
```

**Итого от нажатия до полного появления кнопок: ~740ms**

---

### Завершение звонка (Hangup)

```
[нажата кнопка "Завершить"]
        │
        ▼  250ms, EaseIn
┌──────────────────────────────────┐
│  ButtonsPanel                    │
│  TranslateY:   0 ──► 70          │  ← уезжает вниз
│  Opacity:      1 ──► 0           │
│                                  │
│  ActiveVoiceRoom                 │
│  Opacity:      1 ──► 0           │
└──────────────────────────────────┘
        │
        ▼ [Completed callback]
           Window.Close()           ← окно закрывается здесь
```

**Важно для бэка:** сигнал об окончании звонка нужно отправлять до анимации,
не ждать закрытия окна.

---

### Микрофон (локальный) — кнопка Mute/Unmute

```
[нажата кнопка микрофона]
        │
        ├─► [МГНОВЕННО] BtnMic.Background  = DangerMutedBrush / Normal
        ├─► [МГНОВЕННО] MicIconPath.Fill   = DangerBrush / Normal
        │
        └─► [220ms, EaseOut] MicSlashLine
            StrokeDashOffset: 26 ──► 0   (mute:   линия появляется)
            StrokeDashOffset:  0 ──► 26  (unmute: линия исчезает)
```

Перечёркивающая линия рисуется через stroke dash offset — нет отдельного изображения,
просто анимируется длина видимой части линии.

---

### Микрофон (удалённый) — по WebSocket-событию

```
[сервер отправил "MicMuted" / "MicUnmuted"]
        │
        ├─► [МГНОВЕННО] MicButtonBorder.Background  меняется
        ├─► [МГНОВЕННО] MicAvatarPath.Fill           меняется
        ├─► [МГНОВЕННО] MicAvatarSlashLine.Stroke    меняется
        │
        └─► [220ms, EaseOut] MicAvatarSlashLine
            StrokeDashOffset: 26 ──► 0   (muted)
            StrokeDashOffset:  0 ──► 26  (unmuted)
```

**Важно для бэка:** ожидаемые WebSocket-события: `MicMuted` и `MicUnmuted`.
Клиент сразу обновляет состояние, анимация идёт поверх.

---

## Сводная таблица таймингов

| Анимация | Длительность | Easing |
|----------|-------------|--------|
| Window controls fade in | 150ms | — |
| Window controls fade out | 250ms | — |
| ContentGrid entrance | 400ms | CubicEase EaseOut |
| Incoming → Active (exit phase) | 220ms | CubicEase EaseIn |
| Incoming → Active (enter phase) | 320ms | CubicEase EaseOut |
| Active buttons stagger (per button) | 320ms + i×50ms delay | CubicEase EaseOut |
| Call close | 250ms | CubicEase EaseIn |
| Mic toggle animation | 220ms | CubicEase EaseOut |
| Remote mic badge animation | 220ms | CubicEase EaseOut |

---

## Когда страница реально меняется

| Событие | Момент смены состояния |
|---------|----------------------|
| Загрузка сообщений | После получения ответа API (async) |
| Выбор чата | Мгновенно (нет анимации) |
| Принятие звонка | В `Completed` callback фазы 1 (через 220ms) |
| Завершение звонка | В `Completed` callback финальной анимации (через 250ms) |
| Mute/Unmute (своя кнопка) | Мгновенно (цвет), анимация идёт параллельно |
| Mute/Unmute (удалённый) | Мгновенно после WebSocket-события |

---

## Используемые технологии

- **WPF `DoubleAnimation`** — анимация числовых свойств (Opacity, TranslateTransform.Y, StrokeDashOffset)
- **WPF `Storyboard`** — декларативные анимации в XAML
- **`EventTrigger`** — привязка анимации к событиям мыши без кода
- **`ControlTemplate` triggers** — hover/press эффекты
- **`TranslateTransform`** — смещение элементов по Y
- **`CubicEase`** — смягчение для всех code-behind анимаций
- Сторонние библиотеки анимации **не используются**
