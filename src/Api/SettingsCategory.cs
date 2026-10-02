using BepInEx.Configuration;

namespace NivalisMods.ModCompanion.Api;

public enum ControlKind { Toggle, Choice, Slider, Stepper, Text, Button, KeyBinding }
public sealed record Choice<T>(T Value, string Label);

public abstract class SettingDefinition
{
    public string Key { get; }
    public string Label { get; }
    public string Description { get; init; } = "";
    /// <summary>Optional help shown directly beneath this setting in small, muted text.</summary>
    public string? Hint { get; set; }
    public bool RequiresRestart { get; init; }
    public Func<bool>? EnabledWhen { get; set; }
    public string DisabledReason { get; set; } = "";
    public ControlKind Kind { get; }
    public abstract bool PendingRestart { get; }
    internal float Minimum, Maximum, Increment;
    internal int IntegerMinimum, IntegerMaximum, IntegerStep;
    internal IReadOnlyList<(object Value, string Label)> Choices = Array.Empty<(object, string)>();
    internal Action? Click;
    internal SettingDefinition(string key, string label, ControlKind kind) { Key = key; Label = label; Kind = kind; }
    internal abstract object? Read();
    internal abstract void Write(object value);
    internal abstract void Validate();
    public abstract void Reset();
}

public sealed class Setting<T> : SettingDefinition
{
    private readonly ConfigEntry<T> _entry;
    private readonly Func<T, T> _normalize;
    private readonly T _startup;
    private T _last;
    private bool _validating;
    public T Value { get => _entry.Value; set => _entry.Value = _normalize(value); }
    public T DefaultValue => (T)_entry.DefaultValue;
    /// <summary>Raised after a changed value is validated, including changes made through the original ConfigEntry.</summary>
    public event Action<T>? Changed;
    public override bool PendingRestart => RequiresRestart && !EqualityComparer<T>.Default.Equals(_startup, Value);

    internal Setting(string key, string label, ControlKind kind, ConfigEntry<T> entry, Func<T, T>? normalize = null,
        string description = "", bool restart = false) : base(key, label, kind)
    {
        _entry = entry; _normalize = normalize ?? (value => value);
        Description = description; RequiresRestart = restart;
        _entry.Value = _normalize(_entry.Value);
        _startup = _last = _entry.Value;
        _entry.SettingChanged += (_, _) =>
        {
            if (_validating) return;
            Validate();
            var current = Value;
            if (EqualityComparer<T>.Default.Equals(_last, current)) return;
            _last = current;
            if (Changed != null)
                foreach (Action<T> handler in Changed.GetInvocationList()) SettingsRegistry.Notify(() => handler(current));
        };
    }
    internal override object? Read() => Value;
    internal override void Write(object value) => Value = (T)value;
    internal override void Validate()
    {
        _validating = true;
        try { Value = _entry.Value; } finally { _validating = false; }
    }
    public override void Reset() => Value = DefaultValue;
}

internal sealed class DisplaySetting : SettingDefinition
{
    internal DisplaySetting(string key, string label, ControlKind kind) : base(key, label, kind) { }
    public override bool PendingRestart => false;
    internal override object? Read() => null;
    internal override void Write(object value) { }
    internal override void Validate() { }
    public override void Reset() { }
}

/// <summary>Keyboard/mouse and gamepad bindings are persisted separately. NativeAction is an adapter for game-owned actions.</summary>
public sealed class KeyBindingSetting : SettingDefinition
{
    public Setting<string>? Keyboard { get; }
    public Setting<string>? Gamepad { get; }
    public string? NativeAction { get; }
    internal string ActionId { get; }
    internal ModRegistration Owner { get; }
    internal KeyBindingSetting(ModRegistration owner, string category, string key, string label,
        Setting<string>? keyboard, Setting<string>? gamepad, string? nativeAction) : base(key, label, ControlKind.KeyBinding)
    {
        Owner = owner; ActionId = owner.Id + "/" + category + "/" + key;
        Keyboard = keyboard; Gamepad = gamepad; NativeAction = nativeAction;
    }
    public override bool PendingRestart => false;
    internal override object? Read() => null;
    internal override void Write(object value) { }
    internal override void Validate() { Keyboard?.Validate(); Gamepad?.Validate(); }
    public override void Reset() { Keyboard?.Reset(); Gamepad?.Reset(); }
}

public sealed class SettingsCategory
{
    private readonly ModRegistration _mod;
    private readonly List<SettingDefinition> _entries = new();
    public string Key { get; }
    public string Label { get; }
    public SectionKind Kind { get; }
    public IReadOnlyList<SettingDefinition> Entries => _entries.AsReadOnly();
    internal SettingsCategory(ModRegistration mod, string key, string label, SectionKind kind) { _mod = mod; Key = key; Label = label; Kind = kind; }

    private T Add<T>(T setting) where T : SettingDefinition
    {
        _mod.Add(this, setting); _entries.Add(setting); return setting;
    }
    private ConfigEntry<T> Entry<T>(string key, T value, string description)
    {
        if (_entries.Any(e => e.Key == key)) throw new ArgumentException($"Duplicate setting '{key}'.");
        return _mod.Config.Bind(Key, key, value, description);
    }
    private void CheckFile<T>(ConfigEntry<T> entry)
    {
        if (!ReferenceEquals(entry.ConfigFile, _mod.Config)) throw new ArgumentException("Entry belongs to a different configuration file.");
    }
    public Setting<bool> Toggle(string key, string label, bool defaultValue, string description = "", bool requiresRestart = false) =>
        BindToggle(key, label, Entry(key, defaultValue, description), description, requiresRestart);
    public Setting<bool> BindToggle(string key, string label, ConfigEntry<bool> entry, string description = "", bool requiresRestart = false)
    {
        CheckFile(entry);
        return Add(new Setting<bool>(key, label, ControlKind.Toggle, entry, description: description, restart: requiresRestart));
    }
    public Setting<int> Stepper(string key, string label, int defaultValue, int min, int max, int step = 1, string description = "", bool requiresRestart = false) =>
        BindStepper(key, label, Entry(key, defaultValue, description), min, max, step, description, requiresRestart);
    public Setting<int> BindStepper(string key, string label, ConfigEntry<int> entry, int min, int max, int step = 1, string description = "", bool requiresRestart = false)
    {
        CheckFile(entry);
        if (min > max || step <= 0) throw new ArgumentException("Invalid numeric range or step.");
        return Add(new Setting<int>(key, label, ControlKind.Stepper, entry, v => Math.Clamp(v, min, max), description, requiresRestart)
        { IntegerMinimum = min, IntegerMaximum = max, IntegerStep = step });
    }
    public Setting<float> Slider(string key, string label, float defaultValue, float min, float max, float step = 0.01f, string description = "", bool requiresRestart = false)
    {
        if (!float.IsFinite(min) || !float.IsFinite(max) || !float.IsFinite(step) || min >= max || step <= 0)
            throw new ArgumentException("Slider bounds and step must be finite, with min < max and step > 0.");
        float Normalize(float v) => Math.Clamp(min + MathF.Round((Math.Clamp(float.IsFinite(v) ? v : defaultValue, min, max) - min) / step) * step, min, max);
        if (!float.IsFinite(defaultValue)) throw new ArgumentException("Default must be finite.");
        return Add(new Setting<float>(key, label, ControlKind.Slider, Entry(key, defaultValue, description), Normalize, description, requiresRestart)
        { Minimum = min, Maximum = max, Increment = step });
    }
    public Setting<T> Choice<T>(string key, string label, T defaultValue, IEnumerable<Choice<T>> choices, string description = "", bool requiresRestart = false) =>
        BindChoice(key, label, Entry(key, defaultValue, description), choices, description, requiresRestart);
    public Setting<T> BindChoice<T>(string key, string label, ConfigEntry<T> entry, IEnumerable<Choice<T>> choices, string description = "", bool requiresRestart = false)
    {
        CheckFile(entry);
        var options = choices.ToArray();
        if (options.Length == 0 || options.Select(o => o.Value).Distinct().Count() != options.Length || !options.Any(o => Equals(o.Value, entry.DefaultValue)))
            throw new ArgumentException("Choices must be unique and include the default value.");
        return Add(new Setting<T>(key, label, ControlKind.Choice, entry,
            v => options.Any(o => Equals(o.Value, v)) ? v : (T)entry.DefaultValue, description, requiresRestart)
        { Choices = options.Select(o => ((object)o.Value!, o.Label)).ToArray() });
    }
    public SettingDefinition Text(string key, string text) => Add(new DisplaySetting(key, text, ControlKind.Text));
    public SettingDefinition Button(string key, string label, Action clicked) => Add(new DisplaySetting(key, label, ControlKind.Button) { Click = clicked });
    public KeyBindingSetting KeyBinding(string key, string label, string keyboard = "", string gamepad = "")
    {
        var kb = new Setting<string>(key + ".Keyboard", label, ControlKind.Text, Entry(key + ".Keyboard", keyboard, "Keyboard/mouse binding path."));
        var pad = new Setting<string>(key + ".Gamepad", label, ControlKind.Text, Entry(key + ".Gamepad", gamepad, "Gamepad binding path."));
        return Add(new KeyBindingSetting(_mod, Key, key, label, kb, pad, null));
    }
    public KeyBindingSetting NativeKeyBinding(string key, string label, string actionName) =>
        Add(new KeyBindingSetting(_mod, Key, key, label, null, null, actionName));
    public void ResetDefaults() { foreach (var entry in _entries) entry.Reset(); }
}
