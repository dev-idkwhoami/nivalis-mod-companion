# Settings and sections

- `mod.Controls.AddInput(key, label, keyboard, gamepad)` adds a persisted button action to Controls. `AddNativeInput(key, label, actionName)` adapts an existing game-owned action. `AddParagraph(key, text)` adds help beside bindings.
- `mod.Developer.AddVerboseLogging(defaultValue: false, description: "...")` returns `Setting<bool>`. The mod reads `.Value` to decide what to log; Companion does not intercept its logger. Storage is `[Developer] Verbose`.
- `mod.Info.AddHeading(text)` and `AddParagraph(text)` build an ordered read-only page. Info text is presentation metadata, not saved user configuration.
- `mod.AddCategory(key, label)` defines custom tabs with the usual `Toggle`, `Choice`, `Slider`, `Stepper`, `Text`, `Button`, `KeyBinding` and `NativeKeyBinding` methods. `Category` remains an alias. Bindings in custom categories stay beside that category's other settings.

Accessing a section property does not create a visible tab. Empty custom and standard sections are hidden; Controls help alone does not create a tab without an input. Tabs appear in custom registration order, followed by Controls, Developer, Info when populated. The keys `Controls`, `Developer` and `Info` are reserved, case-insensitively; use the dedicated APIs for these sections. `mod.Sections` exposes the visible tab descriptions; `mod.Categories` exposes settings-backed categories, including standard ones.

`SettingsRegistry.Register(id, new ModMetadata(name, author, version, description, iconPath))` requires nonblank name, author and version. Metadata is immutable after registration. The author and version appear in the browser details; search also matches author. `iconPath` is an optional absolute image-file path (PNG/JPG, preferably square); missing/invalid images fall back to initials. Metadata alone does not create an Info tab. There are no automatically added Save, Reload or Reset buttons, nor a saved-status footer. `Save()` and `Reload()` are developer APIs; mods may explicitly add their own actions if needed. Save failures alone appear as an error.

Developer sections are hidden unless Companion's global developer mode is enabled (off by default). Hiding the tab does not reset its values or disable actions already running. Use `mod.Developer.Settings` for ordinary settings and test buttons, for example:

```csharp
mod.Developer.Settings.Toggle("ShowBounds", "Show debug bounds", false);
mod.Developer.Settings.Button("Diagnostics", "Run diagnostics", () => RunDiagnostics());
```

## Available controls

| Method | When to use it |
| --- | --- |
| `Toggle(key, label, defaultValue)` | Enable or disable a feature. |
| `Stepper(key, label, defaultValue, min, max, step)` | Choose a bounded whole-number value; step defaults to 1. |
| `Slider(key, label, defaultValue, min, max, step)` | Adjust a numeric range such as scale or intensity, with quantized increments. |
| `Choice<T>(key, label, defaultValue, choices)` | Select from a fixed set of named options; persist the typed value. |
| `Text(key, text)` | Add explanatory text within a settings category; no persisted value. |
| `Button(key, label, clicked)` | Run an explicit callback such as a diagnostic action; no persisted value. |
| `KeyBinding(key, label, keyboard, gamepad)` | Button binding in a custom category |
| `NativeKeyBinding(key, label, actionName)` | Adapter for an existing action in a custom category |

Keys must be unique within a category. Keep them stable across releases; labels can change. Numeric and choice methods accept optional descriptions and restart flags. `Hint` overrides visible help. `EnabledWhen` and `DisabledReason` control availability.

## Controls

Use `mod.Controls.AddInput` for companion-owned button actions. Poll with `CompanionInput.WasPressed(binding)` from your Unity update. Keyboard/mouse and gamepad paths are separate. `AddNativeInput` references an existing named action whose owner remains responsible for registration and persistence. Keep explanatory guides in Info. The API also exposes `Controls.AddParagraph` for integrations that need inline help.

The native capture overlay and conflict confirmation are retained. Companion-owned actions use a separate asset and are not inserted into the game save packet. Cross-mod binding conflict detection has not been verified. Composite movement bindings and free-text entry are not supported.

## Developer visibility

`SettingsRegistry.DeveloperMode` exposes the current global flag for reading. The checkbox controls visibility, not execution permission: hidden settings retain their saved values. A developer test feature that must stop when the mode is off should also check this flag in gameplay code.

## Info

`mod.Info.AddHeading("About")` and `mod.Info.AddParagraph("Description")` append blocks in order. Metadata alone does not create an Info tab.

See [registration](getting-started.md) and [configuration](configuration.md).
