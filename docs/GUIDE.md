# Руководство AntiAFK

## Установка

1. Открой [Releases](https://github.com/BxdiS/antiafk/releases).
2. Скачай **`AntiAFK-win-Setup.exe`** из последнего опубликованного релиза (не draft).
3. Установи и запусти приложение.

> Авто-обновления работают только после установки через Setup.exe, не при запуске из Visual Studio.

## Управление (трей)

| Пункт | Действие |
|-------|----------|
| Запустить / Остановить | Старт и пауза бота. Состояние цикла сохраняется |
| Обновить | Появляется при доступном обновлении. Устанавливает скачанный апдейт |
| Настройки | Язык (RU/EN), путь к лаунчеру игры |
| Открыть лог | `%AppData%\AntiAfk\logs\` |
| Выход | Полное закрытие приложения |

### Обновления

- Проверка при запуске и каждые N часов (см. конфиг).
- Скачивание в фоне; синяя иконка в трее при доступном апдейте.
- Если бот работал и апдейт не успел скачаться — скачается при следующем запуске.

## Конфигурация

Файлы в `%AppData%\AntiAfk\`:

| Файл | Назначение |
|------|------------|
| `config.json` | Настройки приложения |
| `engine_state.json` | Состояние цикла (resume после остановки) |
| `logs/` | Логи |

Пример конфига: [config.example.json](config.example.json)

Создаётся автоматически при первом запуске. Основные поля:

```json
{
  "language": "ru",
  "launcherPath": "",
  "update": {
    "enabled": true,
    "gitHubOwner": "BxdiS",
    "gitHubRepo": "antiafk",
    "checkIntervalHours": 6
  }
}
```

- `launcherPath` — пустой = авто-поиск лаунчера в стандартных путях Windows.
- Координаты UI и тайминги зашиты в код, не редактируются через конфиг.

## Сборка из исходников

```bash
git clone https://github.com/BxdiS/antiafk.git
cd antiafk
dotnet build src/AntiAfk.App/AntiAfk.App.csproj
dotnet run --project src/AntiAfk.App/AntiAfk.App.csproj
```

### Структура проекта

```
src/
  AntiAfk.Core/           — движок, координаты, состояние
  AntiAfk.Infrastructure/ — WinAPI, экран, конфиг, логи
  AntiAfk.App/            — трей + настройки + обновления
```

## Релизы (для разработчика)

CI запускается при push тега `v*`.

### Обычный релиз

```bash
git push origin main
git tag v1.0.1
git push origin v1.0.1
```

GitHub Actions:
1. Собирает приложение (win-x64, self-contained).
2. Упаковывает Velopack (`packId: antiafk`).
3. Создаёт **draft** релиз `AntiAFK v1.0.1`.
4. Заполняет описание автогенерированными GitHub Release Notes.

Дальше вручную:
1. [Releases](https://github.com/BxdiS/antiafk/releases) → открыть draft.
2. Отредактировать changelog.
3. **Publish release**.

> Пока релиз **draft** — клиенты обновление не видят.

### Первый релиз (v1.0.0)

```bash
git push origin main
git tag v1.0.0
git push origin v1.0.0
```

Шаг `vpk download` на первом релизе может упасть — это нормально (`continue-on-error` в workflow).

### Локальная сборка Velopack (опционально)

```bash
dotnet publish src/AntiAfk.App/AntiAfk.App.csproj -c Release -o publish -r win-x64 --self-contained true
dotnet tool install -g vpk --version 1.2.0
vpk pack -u antiafk -v 1.0.0 -p publish --mainExe AntiAfk.exe --packTitle AntiAFK
```

Артефакты — в папке `Releases/`.

## Подпись кода (SignPath)

Для устранения предупреждений SmartScreen:

1. Проект под [GPL-3.0](../LICENSE) — OSI-совместимая лицензия, требование SignPath.
2. Подать заявку: [signpath.org](https://signpath.org/).
3. Подключить подпись в CI через `vpk pack --signTemplate ...`.

SignPath **не подписывает** проекты с проприетарными или non-OSI лицензиями.

## Troubleshooting

| Проблема | Решение |
|---------|---------|
| Игра не найдена | Окно процесса GTA5.exe или заголовок мультиплеер-клиента с версией в скобках |
| Обновления не работают | Релиз опубликован (не draft)? Установка через Setup.exe? |
| Workflow не запустился | Тег должен начинаться с `v` |
