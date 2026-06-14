using System.Text.Json;

namespace AntiAfk.Infrastructure.Localization;

public sealed class LocalizationService
{
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new(StringComparer.OrdinalIgnoreCase);
    private string _language = "ru";

    public LocalizationService()
    {
        Load("ru", """
        {
          "tray.start": "Запустить",
          "tray.stop": "Остановить",
          "tray.update": "Обновить",
          "tray.update_pending": "Доступно обновление",
          "tray.update_downloading": "Скачивание обновления...",
          "tray.update_ready": "Обновление готово к установке",
          "tray.settings": "Настройки",
          "tray.open_log": "Открыть лог",
          "tray.exit": "Выход",
          "tray.running": "Anti-AFK работает",
          "tray.stopped": "Anti-AFK остановлен",
          "tray.waiting_game": "Ожидание игры",
          "tray.error": "Ошибка",
          "notify.resolution_changed": "Разрешение окна изменилось. Используйте 16:9, оконный режим без рамки.",
          "notify.engine_restarted": "Движок перезапущен после сбоя.",
          "notify.already_running": "Anti-AFK уже запущен.",
          "settings.title": "Настройки Anti-AFK",
          "settings.language": "Язык",
          "settings.launcher_path": "Путь к лаунчеру игры",
          "settings.save": "Сохранить",
          "settings.cancel": "Отмена",
          "settings.browse": "Обзор"
        }
        """);

        Load("en", """
        {
          "tray.start": "Start",
          "tray.stop": "Stop",
          "tray.update": "Update",
          "tray.update_pending": "Update available",
          "tray.update_downloading": "Downloading update...",
          "tray.update_ready": "Update ready to install",
          "tray.settings": "Settings",
          "tray.open_log": "Open log",
          "tray.exit": "Exit",
          "tray.running": "Anti-AFK is running",
          "tray.stopped": "Anti-AFK is stopped",
          "tray.waiting_game": "Waiting for game",
          "tray.error": "Error",
          "notify.resolution_changed": "Window resolution changed. Use 16:9 borderless windowed mode.",
          "notify.engine_restarted": "Engine restarted after a crash.",
          "notify.already_running": "Anti-AFK is already running.",
          "settings.title": "Anti-AFK Settings",
          "settings.language": "Language",
          "settings.launcher_path": "Game launcher path",
          "settings.save": "Save",
          "settings.cancel": "Cancel",
          "settings.browse": "Browse"
        }
        """);
    }

    public void SetLanguage(string language)
    {
        _language = _translations.ContainsKey(language) ? language : "ru";
    }

    public string Get(string key)
    {
        if (_translations.TryGetValue(_language, out var table) && table.TryGetValue(key, out var value))
        {
            return value;
        }

        if (_translations.TryGetValue("en", out var fallback) && fallback.TryGetValue(key, out var english))
        {
            return english;
        }

        return key;
    }

    public IReadOnlyList<string> SupportedLanguages => ["ru", "en"];

    private void Load(string language, string json)
    {
        var dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        _translations[language] = dictionary;
    }
}
