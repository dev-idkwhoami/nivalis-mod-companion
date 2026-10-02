# Configuration and persistence

- Every mod has a unique ID and one config file; no two registrations own the same file.
- Relative custom paths must stay inside `BepInEx/config/` and use `.cfg`. The default is `<mod-name>/<mod-name>.cfg`, using the display name with spaces and punctuation removed (letters, digits, hyphens and underscores are retained). For example, `Example Mod` uses `ExampleMod/ExampleMod.cfg`. An empty sanitized name falls back to the mod ID. Use a custom path to keep it independent of display-name changes.
- Category/key pairs are stable storage identifiers. Display labels and choice labels are not saved as identifiers. Choice values are saved, never list positions.
- Reads are typed and available immediately. Missing values use defaults; invalid choice values use the declared default, numeric values are bounded, and sliders quantize to their declared increment.
- Companion-owned files disable BepInEx's write-on-every-change behavior. Every value change saves synchronously, including changes made through typed handles and captured bindings. Failed saves retain dirty state and retry once per second; initial defaults are flushed by the menu service. Reload suppresses intermediate writes and saves once after validation. Existing ConfigFile instances keep their SaveOnConfigSet flag; Companion also saves exposed changes.
- `Save()` returns success/failure. Failures retain `IsDirty` and expose `SaveError`; the menu shows the error and retries. `Reload()` explicitly discards unsaved edits, reloads, validates and notifies; there is no file watcher.
- `Reset()` on a typed setting, `category.ResetDefaults()`, or `mod.ResetDefaults()` restores declared defaults. Native action bindings use their native per-row reset instead.
- `Changed` notifications run on the thread making the change; one failing subscriber is logged without preventing later subscribers.

Companion-owned button bindings use a separate input asset. Keyboard/mouse and gamepad paths are persisted as `<Key>.Keyboard` and `<Key>.Gamepad` entries in the mod's config. A narrowly scoped resolver patch makes these actions available to the native binding rows. They are not inserted into the game's action asset or native save packet. Native conflict confirmation is retained; conflict detection between two companion-owned actions has not been verified.

## Existing configuration

Use `SettingsRegistry.Register(id, metadata, existingConfigFile)` and `BindToggle`, `BindStepper`, or `BindChoice` to wrap existing `ConfigEntry` instances. Existing callbacks and file ownership remain unchanged. All exposed entries for a registration must belong to that same ConfigFile. For example:

```csharp
var mod = SettingsRegistry.Register("example.existing", new ModMetadata("Existing Mod", "Example Author", "1.0.0"), Config);
var section = mod.Category("Features", "Features");
var option = section.BindToggle("Enabled", "Enable feature", existingEntry);
option.EnabledWhen = () => featureIsAvailable;
option.DisabledReason = "Another mod provides this feature.";
```

For a game-owned input action, use `mod.Controls.AddNativeInput(key, label, actionName)`. Its registration and persistence remain the owning mod's responsibility.

Direct API use normally requires the companion. Optional integration needs a soft dependency, a presence check and a separate no-inline registration method so companion types are not resolved when its DLL is absent. Keep Companion types out of your normal startup path until the presence check succeeds.
