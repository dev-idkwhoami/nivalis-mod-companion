using HarmonyLib;

namespace NivalisMods.ModCompanion;

// Install the entry when its native panel is used. This also covers panels
// created before the plugin, without searching loaded objects during gameplay.
[HarmonyPatch(typeof(SettingsPanel), nameof(SettingsPanel.Show))]
internal static class SettingsEntry
{
    [HarmonyPostfix]
    private static void Postfix(SettingsPanel __instance) =>
        Plugin.Guard("Add Mod Companion entry", () => CompanionMenu.Instance?.AddEntryButton(__instance));
}
