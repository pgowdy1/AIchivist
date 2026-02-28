namespace ArchiveSearch.API.Models;

/// <summary>Tracks whether the app is in first-run setup mode (no API key configured).</summary>
public class SetupState(bool isSetupMode, string localSettingsPath)
{
    private volatile bool _isSetupMode = isSetupMode;
    public bool IsSetupMode { get => _isSetupMode; set => _isSetupMode = value; }
    public string LocalSettingsPath { get; } = localSettingsPath;
}
