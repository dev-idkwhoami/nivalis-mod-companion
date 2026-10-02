# Integrating a mod

Images may be PNG/JPG bytes embedded in the mod DLL, or optional local files; Companion preserves their aspect ratio. Omitting the image uses initials.

Reference `Nivalis.ModCompanion.dll` (do not bundle another copy) and declare a BepInEx dependency so the companion loads first:

```csharp
using BepInEx;
using BepInEx.Unity.IL2CPP;
using NivalisMods.ModCompanion;
using NivalisMods.ModCompanion.Api;

[BepInPlugin("example.mod", "Example Mod", "1.0.0")]
[BepInDependency(SettingsRegistry.PluginId, SettingsRegistry.PluginVersion)]
public sealed class Plugin : BasePlugin
{
    // Keep these handles so your gameplay code can read the saved settings
    // and poll the action later.
    private Setting<int> _mode = null!;
    private KeyBindingSetting _action = null!;

    // This example applies the mode only at startup.
    private int _startupMode;

    public override void Load()
    {
        // Register once, using a stable ID and your mod's display metadata.
        // The config path is relative to BepInEx/config.
        var metadata = new ModMetadata(
            Name: "Example Mod",
            Author: "Example Author",
            Version: "1.0.0",
            Description: "Example settings and actions."
        );

        var mod = SettingsRegistry.Register(
            "example.mod",
            metadata,
            "ExampleMod/settings.cfg"
        );

        // Create a custom settings tab.
        // The first argument is its storage key; the second is its display label.
        var general = mod.AddCategory("General", "General");

        // A choice stores the numeric value, not the displayed label.
        // Keep these values stable if you rename options later.
        _mode = general.Choice(
            key: "Mode",
            label: "Mode",
            defaultValue: 1,
            choices: new[]
            {
                new Choice<int>(1, "Standard"),
                new Choice<int>(2, "Extended")
            },
            description: "Selects the operating mode.",
            requiresRestart: true
        );

        // React to changes immediately when a feature supports live updates.
        // Companion saves the changed value automatically.
        var enabled = general.Toggle(
            key: "Enabled",
            label: "Enable feature",
            defaultValue: true
        );

        enabled.Changed += value =>
        {
            Log.LogInfo($"Feature enabled: {value}");
        };

        // Use a slider for a fractional value within a bounded range.
        general.Slider(
            key: "Scale",
            label: "Scale",
            defaultValue: 1f,
            min: 0.5f,
            max: 2f,
            step: 0.1f
        );

        // Use a stepper for whole numbers. Hint adds help below this input.
        var count = general.Stepper(
            key: "Count",
            label: "Count",
            defaultValue: 5,
            min: 1,
            max: 20
        );

        count.Hint = "How many items to include.";

        // Text explains a setting; a button runs a callback.
        // Neither creates a persisted configuration value.
        general.Text(
            "Help",
            "Changes marked as requiring restart apply next launch."
        );

        general.Button(
            "Report",
            "Write diagnostic message",
            () => Log.LogInfo("Diagnostic requested")
        );

        // Adding an input creates the optional Controls tab automatically.
        // Keyboard/mouse and gamepad bindings are stored separately.
        _action = mod.Controls.AddInput(
            key: "Inspect",
            label: "Inspect",
            keyboard: "<Keyboard>/f10",
            gamepad: "<Gamepad>/rightStickPress"
        );

        // Developer settings are hidden unless developer mode is enabled.
        // Your mod decides how to use verbose.Value when writing logs.
        var verbose = mod.Developer.AddVerboseLogging();

        // Info is an optional page of headings and paragraphs.
        mod.Info.AddHeading("Example Mod");

        mod.Info.AddParagraph(
            "Configure settings and the inspect shortcut in the tabs above."
        );

        // Saved values are already available; opening the menu is unnecessary.
        // requiresRestart only adds a UI notice, so capture the value yourself
        // when gameplay should keep using it until the next launch.
        _startupMode = _mode.Value;
    }
}
```

Keep typed handles and read `.Value` from gameplay code. Assigning `.Value` uses the same validation and notifications as the menu. Alternatively, use `mod.Get<int>("General", "Mode")`. Call `CompanionInput.WasPressed(binding)` from a Unity main-thread update to poll a registered action; it suppresses gameplay polling while menus or rebinding are active.

Register and access the API on the Unity main thread. Register once during plugin Load. Registrations persist for the process lifetime; dynamic plugin unload is not supported. The API currently exposes boolean, integer stepper, float slider, typed choice, text, button, and button-action key binding controls. Composite movement bindings and text-entry fields are not currently supported.

A restart flag describes the setting and shows when the saved value differs from the startup value. It does **not** delay reads or callbacks. For restart-only features, capture the value once at startup.

Per-setting `Hint` text appears directly below its input, in smaller muted type. Existing `description` arguments serve as the default hint; set `Hint = ""` to suppress the visible hint while retaining the configuration description.

## Project reference

Add a reference to the Companion DLL with `Private="false"`. Distribute your own DLL and declare Mod Companion as a separate requirement; do not ship another copy.

```xml
<Reference
    Include="Nivalis.ModCompanion"
    HintPath="path/to/Nivalis.ModCompanion.dll"
    Private="false"
/>
```

The dependency version constant matches the Companion build used to compile your mod. Rebuild integrations after incompatible API changes. Read BepInEx startup errors if an installed version is rejected.

Next: [settings and sections](settings-api.md), [storage](configuration.md), [icons](icons.md).
