# Mod Companion

A shared settings menu and public configuration API for **Nivalis Nights** mods, built on the game's native UI and BepInEx 6 Unity IL2CPP.

**Version: 1.0.1.**

Companion's own settings are stored in `BepInEx/config/Mod Companion.cfg`.
The old `local.nivalis.modcompanion.cfg` is moved automatically on startup if the
new file does not exist. If both exist, the new file is used and the old one is
left untouched.

The native Settings entry is attached when that panel opens. Companion does not
scan loaded objects on a timer while playing; input-device navigation changes
are handled by the native navigation callback.

## Integration

Register your name, author and version, add typed settings, and read their values directly. Companion handles the menu and persistence. Logos can be embedded in your DLL without loose image files.

```csharp
var mod = SettingsRegistry.Register("example.my-mod",
    new ModMetadata("My Mod", "Your Name", "1.0.0"));
var features = mod.AddCategory("Features", "Features");
var enabled = features.Toggle("Enabled", "Enable feature", true);
// Read enabled.Value in your mod; changes save automatically.
```

Start with the [integration guide](docs/getting-started.md). The [documentation index](docs/README.md) covers the full API, controls, developer actions, metadata, icons, existing configs and storage behavior.

## License

Licensed under [MIT](LICENSE). This is an unofficial mod and is not affiliated with the game's developers.
