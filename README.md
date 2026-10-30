# Mod Companion

A shared settings menu and configuration API for **Nivalis Nights** mods, built
on the game's native UI and BepInEx 6 Unity IL2CPP.

**Version: 1.0.4**

## For players

Open Mod Companion with **F5** or **Settings → Mod Companion**. Select a mod to
change its settings, configure keyboard and controller shortcuts, or read its
help pages. Changes save automatically. Options marked as requiring a restart
show when a saved change is pending.

Only mods that integrate with Companion appear in the menu. Depending on the
mod, it can provide custom settings tabs, Controls, Developer and Info. Developer
settings are hidden until Developer Mode is enabled in Companion.

### Install or update

1. Install BepInEx 6 IL2CPP for the game and launch it once.
2. Close the game and extract the [latest release](https://github.com/dev-idkwhoami/nivalis-mod-companion/releases/latest)
   into the game directory.
3. Confirm the DLL is at `BepInEx/plugins/Nivalis.ModCompanion.dll`, then launch
   the game.

The release ZIP contains only that DLL in its installation folders. Some mods,
including HUD Overhaul, HOBA and Cigarette, bundle Companion already. Keep one
shared copy at the path above; do not install another copy in a mod subfolder.
If installing an older mod archive, keep the newer Companion DLL.

### What's new in 1.0.4

Retired settings are removed automatically from config files managed by
Companion, while current preferences and keyboard/controller bindings are kept.
Cleanup happens after mod registration completes and on subsequent saves or
reloads. No per-mod list of retired settings is needed.

Files owned by a mod and merely exposed through Companion keep their unexposed
settings. Config files belonging to absent mods are not scanned or deleted.
The existing 1.0.3 integration API remains compatible; Cigarette 1.4.0 and HOBA
1.1.2 were checked without changing their integration code.

### Configuration

Companion's own settings live in `BepInEx/config/Mod Companion.cfg`. Its old
`local.nivalis.modcompanion.cfg` is moved there automatically if the new file
does not exist. If both exist, the new file is used and the old one is preserved.

By default, registered mods use `BepInEx/config/<ModName>/<ModName>.cfg`; a mod
can choose a custom path or expose its existing config file. Changes save
immediately, failed writes are reported and retried, and files are not watched
for external edits. See [configuration and persistence](docs/configuration.md)
for ownership, cleanup and reload behavior.

## For mod authors

Register once on the Unity main thread during plugin `Load`, using a stable ID,
your metadata and typed settings. Complete registration synchronously before
plugin loading finishes. Companion handles the menu, validation and persistence.
Logos can be embedded in your DLL without loose image files.

```csharp
using NivalisMods.ModCompanion.Api;

var mod = SettingsRegistry.Register("example.my-mod",
    new ModMetadata("My Mod", "Your Name", "1.0.0"));
var features = mod.AddCategory("Features", "Features");
var enabled = features.Toggle("Enabled", "Enable feature", true);
// Keep this handle and read enabled.Value in your mod.
```

Start with the [integration guide](docs/getting-started.md). The
[documentation index](docs/README.md) covers settings, controls, metadata,
embedded icons, existing configs and storage behavior.

## Build and test

Requires .NET SDK 8, GNU Make, Python 3 for packaging, and a game installation
with BepInEx 6 IL2CPP and regenerated interop assemblies. Run from this repository:

```sh
make build GAME_PATH="/path/to/Nivalis Nights"
make test GAME_PATH="/path/to/Nivalis Nights"
make package GAME_PATH="/path/to/Nivalis Nights"
```

Use `DOTNET=/path/to/dotnet` if the SDK is not on PATH. The build writes
`bin/Nivalis.ModCompanion.dll`; packaging produces the versioned ZIP and SHA-256
file in `bin/`. Building and packaging do not install or publish anything.

The 75 configuration checks cover persistence, validation, bindings, obsolete
keys, external config ownership and failed-save recovery. Managed checks do not
replace in-game UI and controller testing.

## Development and releases

Use the next patch version after the latest release tag for work toward the next
release. Keep it unchanged across fixes, builds and local installs until tagged.
Keep the project version and `SettingsRegistry.PluginVersion` aligned.

From a clean, committed checkout, `make deploy GAME_PATH="/path/to/Nivalis Nights"`
builds and tests the package, creates the version tag if needed, uploads a draft
GitHub release and verifies the downloaded checksum before publishing. GitHub CLI
must be authenticated. Deployment pushes the tag; push the source branch separately.

## License

Licensed under [MIT](LICENSE). This is an unofficial mod and is not affiliated
with the game's developers.
