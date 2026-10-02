namespace NivalisMods.ModCompanion.Api;

public enum SectionKind { Custom, Controls, Developer, Info }
public enum InfoBlockKind { Heading, Paragraph }
public sealed record InfoBlock(InfoBlockKind Kind, string Text);

/// <summary>A visible tab. Empty sections never appear here.</summary>
public sealed record ModSection(string Key, string Label, SectionKind Kind, SettingsCategory? Category);

public sealed class ControlsSection
{
    private readonly ModRegistration _mod;
    internal ControlsSection(ModRegistration mod) => _mod = mod;
    private SettingsCategory Category => _mod.StandardCategory("Controls", SectionKind.Controls);
    public KeyBindingSetting AddInput(string key, string label, string keyboard = "", string gamepad = "") =>
        Category.KeyBinding(key, label, keyboard, gamepad);
    /// <summary>Expose a game-owned action; its bindings remain owned by the game.</summary>
    public KeyBindingSetting AddNativeInput(string key, string label, string actionName) => Category.NativeKeyBinding(key, label, actionName);
    public SettingDefinition AddParagraph(string key, string text) => Category.Text(key, text);
}

public sealed class DeveloperSection
{
    private readonly ModRegistration _mod;
    internal DeveloperSection(ModRegistration mod) => _mod = mod;
    /// <summary>Add arbitrary test settings or actions here; visibility follows global developer mode.</summary>
    public SettingsCategory Settings => _mod.StandardCategory("Developer", SectionKind.Developer);
    public Setting<bool> AddVerboseLogging(bool defaultValue = false, string description = "Write detailed diagnostics to BepInEx/LogOutput.log.") =>
        _mod.StandardCategory("Developer", SectionKind.Developer).Toggle("Verbose", "Verbose logging", defaultValue, description);
}

public sealed class InfoSection
{
    private readonly List<InfoBlock> _blocks = new();
    public IReadOnlyList<InfoBlock> Blocks => _blocks.AsReadOnly();
    public void AddHeading(string text) => Add(InfoBlockKind.Heading, text);
    public void AddParagraph(string text) => Add(InfoBlockKind.Paragraph, text);
    private void Add(InfoBlockKind kind, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Info content cannot be empty.", nameof(text));
        _blocks.Add(new InfoBlock(kind, text));
        SettingsRegistry.Revision++;
    }
}
