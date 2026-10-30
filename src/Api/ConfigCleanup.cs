using System.Reflection;
using BepInEx.Configuration;

namespace NivalisMods.ModCompanion.Api;

internal static class ConfigCleanup
{
    // BepInEx 6 has no public orphan-removal API. Keep this version-specific
    // access isolated; if its internals change, save normally without pruning.
    private static readonly PropertyInfo? Orphans = typeof(ConfigFile).GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? IoLock = typeof(ConfigFile).GetField("_ioLock", BindingFlags.Instance | BindingFlags.NonPublic);
    private static bool _reported;

    internal static void Save(ConfigFile file, bool prune)
    {
        if (!prune) { file.Save(); return; }
        if (Orphans?.GetValue(file) is not IDictionary<ConfigDefinition, string> entries || IoLock?.GetValue(file) is not object sync)
        {
            if (!_reported)
            {
                _reported = true;
                SettingsRegistry.Report("Obsolete setting cleanup is unavailable for this BepInEx version; existing keys are preserved.");
            }
            file.Save();
            return;
        }
        lock (sync)
        {
            var removed = entries.ToArray();
            entries.Clear();
            try { file.Save(); }
            catch
            {
                // Preserve unbound values in memory if the write failed so a
                // later bind or save retry can still recover them.
                foreach (var pair in removed) entries[pair.Key] = pair.Value;
                throw;
            }
        }
    }
}
