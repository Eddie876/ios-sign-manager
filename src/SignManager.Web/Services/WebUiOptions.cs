namespace SignManager.Web.Services;

public sealed record WebUiOptions(
    string AppConfigPath = "data/config/apps.json",
    string AppStatePath = "data/state/state.json",
    string SettingsPath = "data/config/settings.json",
    string SourceRootDirectory = "data/sources",
    string UploadTempDirectory = "data/uploads",
    string ShortcutTokenPath = "data/config/shortcut-tokens.json",
    long UploadMaxBytes = 2L * 1024 * 1024 * 1024,
    int UploadMaxEntries = 50_000,
    long UploadMaxTotalExpandedBytes = 4L * 1024 * 1024 * 1024,
    long UploadMaxSingleEntryBytes = 1L * 1024 * 1024 * 1024,
    double UploadMaxCompressionRatio = 200,
    int ShortcutPromptCooldownHours = 12,
    int ShortcutPromptOpportunityHours = 72,
    string ShortcutBootstrapToken = "");
