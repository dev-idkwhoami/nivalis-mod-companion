using BepInEx;
using BepInEx.Configuration;

namespace NivalisMods.ModCompanion.Api;

/// <summary>Required mod identity and optional description/icon, supplied at registration.</summary>
public sealed record ModMetadata(string Name, string Author, string Version, string Description = "", string? IconPath = null, ModIcon? Icon = null);

/// <summary>Register on the Unity main thread during plugin Load, before opening the menu.</summary>
public static class SettingsRegistry
{
    public const string PluginId = "local.nivalis.modcompanion";
    public const string PluginVersion = "1.0.3";
    private static readonly List<ModRegistration> Mods = new();
    public static IReadOnlyList<ModRegistration> RegisteredMods => Mods.AsReadOnly();
    internal static string ConfigRoot = Paths.ConfigPath;
    internal static Action<string> Report = _ => { };
    internal static int Revision;
    private static bool _developerMode;
    public static bool DeveloperMode
    {
        get => _developerMode;
        internal set { if (_developerMode == value) return; _developerMode = value; Revision++; }
    }

    public static ModRegistration Register(string id, ModMetadata metadata, string? configPath = null)
    {
        ValidateIdentity(id, metadata);
        var root = Path.GetFullPath(ConfigRoot) + Path.DirectorySeparatorChar;
        // A display name such as "HUD Overhaul" defaults to HUDOverhaul/HUDOverhaul.cfg.
        // Callers can pin a custom relative path independently of future display-name changes.
        var stem = string.Concat(metadata.Name.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (stem.Length == 0) stem = id;
        var path = Path.GetFullPath(Path.Combine(root, configPath ?? Path.Combine(stem, stem + ".cfg")));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".cfg")
            throw new ArgumentException("Config path must be a .cfg file inside BepInEx/config.", nameof(configPath));
        if (Mods.Any(m => string.Equals(m.Config.ConfigFilePath, path, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Another registration already owns this configuration file.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var file = new ConfigFile(path, false) { SaveOnConfigSet = false };
        return Add(id, metadata, file, true);
    }

    /// <summary>Expose existing entries without changing ownership or SaveOnConfigSet.</summary>
    public static ModRegistration Register(string id, ModMetadata metadata, ConfigFile config)
    {
        ValidateIdentity(id, metadata);
        if (Mods.Any(m => string.Equals(m.Config.ConfigFilePath, config.ConfigFilePath, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Another registration already owns this configuration file.");
        return Add(id, metadata, config, false);
    }

    private static void ValidateIdentity(string id, ModMetadata metadata)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(c => !((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) && c is not '.' and not '-' and not '_'))
            throw new ArgumentException("Use a stable mod ID containing letters, numbers, dots, hyphens or underscores.");
        if (metadata == null) throw new ArgumentNullException(nameof(metadata));
        if (string.IsNullOrWhiteSpace(metadata.Name)) throw new ArgumentException("Mod name is required.");
        if (string.IsNullOrWhiteSpace(metadata.Author)) throw new ArgumentException("Mod author is required.");
        if (string.IsNullOrWhiteSpace(metadata.Version)) throw new ArgumentException("Mod version is required.");
        if (metadata.Description == null) throw new ArgumentException("Description cannot be null.");
        if (metadata.Icon != null && metadata.IconPath != null)
            throw new ArgumentException("Supply either an embedded icon or an icon path, not both.");
        if (metadata.IconPath != null && (string.IsNullOrWhiteSpace(metadata.IconPath) || !Path.IsPathFullyQualified(metadata.IconPath)))
            throw new ArgumentException("IconPath must be an absolute image file path, or null.");
        if (Mods.Any(m => m.Id == id)) throw new ArgumentException($"Mod '{id}' is already registered.");
    }

    private static ModRegistration Add(string id, ModMetadata metadata, ConfigFile config, bool owned)
    {
        var mod = new ModRegistration(id, metadata, config, owned);
        Mods.Add(mod);
        Revision++;
        return mod;
    }

    internal static void Notify(Action callback)
    {
        try { callback(); }
        catch (Exception e) { Report($"Settings callback failed: {e}"); }
    }
}

public sealed class ModRegistration
{
    private readonly List<SettingsCategory> _categories = new();
    private readonly Dictionary<string, SettingDefinition> _settings = new(StringComparer.Ordinal);
    internal ConfigFile Config { get; }
    internal bool OwnsConfig { get; }
    private bool _reloading;
    internal DateTime ChangedAt { get; private set; }
    public string Id { get; }
    public ModMetadata Metadata { get; }
    public string Name => Metadata.Name;
    public string Author => Metadata.Author;
    public ControlsSection Controls { get; }
    public DeveloperSection Developer { get; }
    public InfoSection Info { get; } = new();
    public string Version => Metadata.Version;
    public string Description => Metadata.Description;
    /// <summary>Optional absolute PNG/JPG path. Missing icons use the mod's initials.</summary>
    public string? IconPath => Metadata.IconPath;
    public ModIcon? Icon => Metadata.Icon;
    public IReadOnlyList<ModSection> Sections => _categories.Where(c => c.Entries.Count > 0 &&
            (c.Kind != SectionKind.Controls || c.Entries.Any(e => e.Kind == ControlKind.KeyBinding)) &&
            (c.Kind != SectionKind.Developer || SettingsRegistry.DeveloperMode))
        .OrderBy(c => c.Kind).Select(c => new ModSection(c.Key, c.Label, c.Kind, c))
        .Concat(Info.Blocks.Count > 0 ? new[] { new ModSection("Info", "Info", SectionKind.Info, null) } : Array.Empty<ModSection>()).ToArray();
    public string ConfigPath => Config.ConfigFilePath;
    public bool IsDirty { get; private set; }
    public string? SaveError { get; private set; }
    public IReadOnlyList<SettingsCategory> Categories => _categories.AsReadOnly();

    internal ModRegistration(string id, ModMetadata metadata, ConfigFile config, bool owned)
    {
        Id = id; Metadata = metadata; Config = config; OwnsConfig = owned;
        Controls = new ControlsSection(this); Developer = new DeveloperSection(this);
        config.SettingChanged += (_, _) => { MarkDirty(); if (!_reloading) Save(); };
    }

    public SettingsCategory Category(string key, string label) => AddCategory(key, label);

    public SettingsCategory AddCategory(string key, string label)
    {
        if (new[] { "Controls", "Developer", "Info" }.Contains(key, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Controls, Developer and Info are reserved. Use the corresponding section API.", nameof(key));
        return CreateCategory(key, label, SectionKind.Custom);
    }

    internal SettingsCategory StandardCategory(string key, SectionKind kind) =>
        _categories.FirstOrDefault(c => c.Kind == kind) ?? CreateCategory(key, key, kind);

    private SettingsCategory CreateCategory(string key, string label, SectionKind kind)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Category key and label are required.");
        if (_categories.Any(c => c.Key == key)) throw new ArgumentException($"Duplicate category '{key}'.");
        var category = new SettingsCategory(this, key, label, kind);
        _categories.Add(category); SettingsRegistry.Revision++;
        return category;
    }

    internal void Add(SettingsCategory category, SettingDefinition setting)
    {
        var key = category.Key + "/" + setting.Key;
        if (!_settings.TryAdd(key, setting)) throw new ArgumentException($"Duplicate setting '{key}'.");
        MarkDirty(); SettingsRegistry.Revision++;
    }

    public Setting<T> Get<T>(string category, string key) =>
        _settings.TryGetValue(category + "/" + key, out var entry) && entry is Setting<T> typed
            ? typed : throw new KeyNotFoundException($"No setting of type {typeof(T).Name} at '{category}/{key}'.");

    internal void MarkDirty() { IsDirty = true; ChangedAt = DateTime.UtcNow; }

    public bool Save()
    {
        try { Config.Save(); IsDirty = false; SaveError = null; return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (SaveError != e.Message) SettingsRegistry.Report($"Cannot save {Id}: {e.Message}");
            SaveError = e.Message;
            return false;
        }
    }

    /// <summary>Discards unsaved edits. Call explicitly; files are not watched.</summary>
    public void Reload()
    {
        _reloading = true;
        try
        {
            Config.Reload();
            foreach (var setting in _settings.Values) setting.Validate();
        }
        finally { _reloading = false; }
        MarkDirty();
        Save();
    }

    public void ResetDefaults()
    {
        foreach (var category in _categories) category.ResetDefaults();
    }
}
