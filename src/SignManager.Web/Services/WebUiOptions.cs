namespace SignManager.Web.Services;

public sealed record WebUiOptions(
    string AppConfigPath = "data/config/apps.json",
    string AppStatePath = "data/state/state.json",
    string SettingsPath = "data/config/settings.json",
    string UploadTempDirectory = "data/uploads",
    string ShortcutTokenPath = "data/config/shortcut-tokens.json",
    int ShortcutPromptCooldownHours = 12,
    int ShortcutPromptOpportunityHours = 72,
    string ShortcutBootstrapToken = "dev-shortcut-token");
