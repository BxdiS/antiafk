# 🎮 Marketplace Anti-AFK | Маркетплейс Anti-AFK

## 🇷🇺 Русский

### Описание
Приложение для Majestic Multiplayer (Windows), предотвращающее отключение персонажа по неактивности (AFK) через имитацию активности на маркетплейсе.

Переписано на **C# / .NET 8** с архитектурой для масштабирования:
- `AntiAfk.Core` — логика движка, координаты, состояние цикла
- `AntiAfk.Infrastructure` — WinAPI, захват экрана, конфиг, логи
- `AntiAfk.App` — трей (WinForms) + настройки (WPF)

### Требования
- **Windows 10/11 x64**
- Для сборки: **.NET 8 SDK** (или новее)
- Игра в **16:9**, оконный режим без рамки (FHD / 2K / 4K — авто-масштабирование)

### Сборка и запуск

```bash
git clone https://github.com/BxdiS/antiafk-majestic.git
cd antiafk-majestic
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

### Публикация (один self-contained .exe)

```bash
dotnet publish src/AntiAfk.App/AntiAfk.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Готовый файл: `src/AntiAfk.App/bin/Release/net8.0-windows/win-x64/publish/AntiAfk.exe`

### Управление из трея
| Пункт | Действие |
|-------|----------|
| **Запустить / Остановить** | Старт/стоп бота. При остановке сохраняется фаза цикла и продолжение с того же места |
| **Иконка** | 🟢 работает · 🟡 ожидание игры · 🔴 остановлен / ошибка |
| **Настройки** | Язык (RU/EN), путь к Majestic Launcher, тайминги |
| **Открыть лог** | `%AppData%\AntiAfk\logs\` |
| **Выход** | Полное закрытие приложения |

### Конфигурация
Файл: `%AppData%\AntiAfk\config.json` (plain JSON)

Состояние цикла (resume): `%AppData%\AntiAfk\engine_state.json`

Координаты UI **не редактируются** — зашиты в коде (`GameConstants`), масштабируются по размеру окна игры.

### Как это работает
- Находит окно `Grand Theft Auto V` / `Majestic Multiplayer`
- Если игра не найдена — запускает **Majestic Launcher** (путь в настройках или авто-поиск)
- Фоновые клики по маркетплейсу через `PostMessage` (без захвата фокуса)
- Активные действия: ходьба, повороты, ESC, восстановление UI по пикселям
- При смене разрешения окна — уведомление пользователю
- При падении worker — авторестарт через 3 сек

### Авто-обновления (план)
Рекомендуемый стек для **бесшовных** обновлений без участия пользователя:

1. **[Velopack](https://github.com/velopack/velopack)** — современная замена Squirrel для .NET
2. **GitHub Releases** — CI публикует `RELEASES` + delta-пакеты
3. **Фоновая проверка** каждые N часов (настройка в `config.json`)
4. **Применение при перезапуске** или тихий restart в трее (Velopack умеет apply-on-exit)

Схема:
```
GitHub Actions (tag v1.2.3)
  → dotnet publish
  → vpk pack + upload to Release
  → клиент: Velopack.CheckForUpdatesAsync()
  → скачать в фоне → ApplyUpdatesAndRestart() при простое бота
```

Пользователь ничего не делает: обновление скачивается в фоне, применяется при следующем перезапуске приложения или когда бот остановлен.

### Подпись кода / SmartScreen
Бесплатных **доверенных** сертификатов для физлиц практически нет. Варианты:

| Вариант | Стоимость | SmartScreen |
|---------|-----------|-------------|
| **SignPath Foundation** | Бесплатно для open-source | ✅ Доверенная подпись |
| Self-signed | Бесплатно | ❌ Предупреждение остаётся |
| Накопление репутации | Бесплатно | ⚠️ Со временем смягчается при одном издателе |

**Рекомендация для этого репозитория:** подать заявку в [SignPath](https://signpath.org/) (OSS, бесплатно) → подписывать релизы в CI → SmartScreen перестанет ругаться после нескольких скачиваний.

### Идеи на будущее (Roadmap)
- [ ] Автозапуск бота при старте Windows
- [ ] Авто-запуск Majestic Launcher по кнопке «Старт» → последний сервер
- [ ] Автовыбор персонажа и спавн в заданной точке
- [ ] Telegram-бот (уведомления + удалённое управление)
- [ ] Планировщик (работа только в заданные часы)
- [ ] Velopack авто-обновления из GitHub Releases
- [ ] Расширенное логирование и debug-режим

### Внимание ⚠️
- Используй на свой риск
- Может быть обнаружено системой защиты сервера

### Legacy
Старая Python-версия: `legacy/python/`

---

## 🇬🇧 English

### Description
Windows app for Majestic Multiplayer that prevents AFK disconnects by simulating marketplace activity.

Rewritten in **C# / .NET 8** with scalable architecture:
- `AntiAfk.Core` — engine logic, coordinates, cycle state
- `AntiAfk.Infrastructure` — WinAPI, screen capture, config, logging
- `AntiAfk.App` — tray (WinForms) + settings (WPF)

### Requirements
- **Windows 10/11 x64**
- **.NET 8 SDK** for building
- Game in **16:9 borderless windowed** (FHD / 2K / 4K auto-scaling)

### Build & Run

```bash
git clone https://github.com/BxdiS/antiafk-majestic.git
cd antiafk-majestic
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

### Publish (self-contained single .exe)

```bash
dotnet publish src/AntiAfk.App/AntiAfk.App.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Output: `src/AntiAfk.App/bin/Release/net8.0-windows/win-x64/publish/AntiAfk.exe`

### Tray controls
| Item | Action |
|------|--------|
| **Start / Stop** | Start/stop bot. Stops save cycle phase for resume |
| **Icon** | 🟢 running · 🟡 waiting for game · 🔴 stopped / error |
| **Settings** | Language (RU/EN), Majestic Launcher path, timings |
| **Open log** | `%AppData%\AntiAfk\logs\` |
| **Exit** | Fully quit the app |

### Configuration
`%AppData%\AntiAfk\config.json` (plain JSON)

Cycle resume state: `%AppData%\AntiAfk\engine_state.json`

UI coordinates are **developer-defined** in code (`GameConstants`), scaled to game window size.

### Auto-updates (planned)
Recommended stack for **seamless** user-free updates:

1. **[Velopack](https://github.com/velopack/velopack)** + **GitHub Releases**
2. Background check every N hours
3. Download in background, apply on app restart or when bot is idle

### Code signing / SmartScreen
For open-source projects, apply for free signing via **[SignPath Foundation](https://signpath.org/)**. Self-signed certs do not bypass SmartScreen.

### Roadmap
- [ ] Auto-start with Windows
- [ ] Launch Majestic Launcher on Start → last server
- [ ] Auto character select + spawn point
- [ ] Telegram bot
- [ ] Scheduler
- [ ] Velopack auto-updates
- [ ] Advanced logging

### Warning ⚠️
- Use at your own risk
- May be detected by server anti-cheat

### Legacy
Old Python version: `legacy/python/`

---

### 📝 License
Created for Majestic RP community
