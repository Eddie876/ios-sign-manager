namespace SignManager.Web.Services;

public sealed record WebUiOptions(
    string AppConfigPath = "data/config/apps.json",
    string AppStatePath = "data/state/state.json",
    string SettingsPath = "data/config/settings.json",
    string UploadTempDirectory = "data/uploads");
