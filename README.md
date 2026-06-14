# AntiAFK

## 🇷🇺 Русский

### Описание
Windows-приложение для GTA V multiplayer, предотвращающее отключение персонажа по неактивности (AFK) через имитацию активности на маркетплейсе.

Стек: **C# / .NET 8**
- `AntiAfk.Core` — логика движка, координаты, состояние цикла
- `AntiAfk.Infrastructure` — WinAPI, захват экрана, конфиг, логи
- `AntiAfk.App` — трей (WinForms) + настройки (WPF) + Velopack обновления

### Требования
- **Windows 10/11 x64**
- **.NET 8 SDK** для сборки
- Игра в **16:9**, оконный режим без рамки (FHD / 2K / 4K — авто-масштабирование)

### Сборка и запуск (разработка)

```bash
git clone https://github.com/BxdiS/antiafk.git
cd antiafk
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

### Установка для пользователей

Скачай **`AntiAFK-win-Setup.exe`** из [GitHub Releases](https://github.com/BxdiS/antiafk/releases) и установи.

### Управление из трея

| Пункт | Действие |
|-------|----------|
| **Запустить / Остановить** | Старт/стоп бота с сохранением фазы цикла |
| **Иконка** | 🟢 работает · 🟡 ожидание игры · 🔴 остановлен · 🔵 обновление |
| **Обновить** | Появляется при доступном обновлении, применяет скачанный апдейт |
| **Настройки** | Язык (RU/EN), путь к лаунчеру игры |
| **Открыть лог** | `%AppData%\AntiAfk\logs\` |
| **Выход** | Полное закрытие приложения |

### Авто-обновления (Velopack)

- Проверка при запуске + каждые N часов (`config.json`)
- Скачивание в фоне автоматически
- Синяя иконка в трее + пункт **Обновить** в контекстном меню
- Если не скачалось во время работы бота — скачается при следующем запуске
- Обновления видны только после **публикации** релиза (не draft)

### Релизы (для разработчика)

```bash
git tag v1.0.1
git push origin v1.0.1
```

CI создаёт **draft** релиз → редактируешь changelog → **Publish**.

Подробно: [RELEASE.md](RELEASE.md)

### Конфигурация

`%AppData%\AntiAfk\config.json` · Состояние цикла: `engine_state.json`

### Roadmap
- [ ] Автозапуск с Windows
- [ ] Авто-запуск лаунчера игры по кнопке «Старт»
- [ ] Telegram-бот
- [ ] Планировщик
- [ ] Подпись кода (SignPath)

### Внимание ⚠️
Используй на свой риск. Может быть обнаружено системой защиты сервера.

---

## 🇬🇧 English

### Description
Windows app for GTA V multiplayer that prevents AFK disconnects via marketplace activity simulation.

### Build & Run

```bash
git clone https://github.com/BxdiS/antiafk.git
cd antiafk
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

### Install

Download **`AntiAFK-win-Setup.exe`** from [GitHub Releases](https://github.com/BxdiS/antiafk/releases).

### Tray

| Item | Action |
|------|--------|
| **Start / Stop** | Start/stop bot with cycle resume |
| **Icon** | 🟢 running · 🟡 waiting · 🔴 stopped · 🔵 update |
| **Update** | Appears when update is available, applies downloaded update |
| **Settings** | Language (RU/EN), game launcher path |
| **Open log** | `%AppData%\AntiAfk\logs\` |
| **Exit** | Quit app |

### Auto-updates

Velopack + GitHub Releases. Background download, blue tray icon, **Update** menu item. See [RELEASE.md](RELEASE.md).

### Warning ⚠️
Use at your own risk.

---

### Legacy
Old Python version: `legacy/python/`
