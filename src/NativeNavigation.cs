using HarmonyLib;
using Nivalis;
using Nivalis.UI.Concrete;
using UnityEngine;
using UnityEngine.UI;

namespace NivalisMods.ModCompanion;

internal static class NativeNavigation
{
    // Native gameplay navigation is rebuilt when the input device changes.
    // Reconnect our entry after that rebuild, including when native Apply/Reset are disabled.
    internal static void LinkEntry(SettingsPanel? panel)
    {
        if (panel == null || panel.name == CompanionMenu.RootName) return;
        var section = panel.transform.Find("FrameWrapper/GameplaySettings");
        var button = section?.Find("ButtonsWrapper/ModCompanion.Open")?.GetComponent<Button>();
        var content = section?.Find("MainSettings/Scrollview/Viewport/Content");
        if (button == null || content == null) return;
        Selectable? last = null;
        for (var i = content.childCount - 1; i >= 0 && last == null; i--)
        {
            var row = content.GetChild(i);
            if (!row.gameObject.activeInHierarchy) continue;
            var candidate = row.GetComponent<Selectable>();
            if (candidate != null && candidate.IsInteractable()) last = candidate;
        }
        if (last == null) return;
        var down = last.navigation; down.mode = Navigation.Mode.Explicit; down.selectOnDown = button; last.navigation = down;
        // ManualUINavigation otherwise overrides the selectable's explicit down link.
        var manual = last.GetComponent<ManualUINavigation>();
        if (manual != null)
        {
            manual.lockSelectOnDown = new Optional<Selectable>(button);
        }
        var nav = button.navigation; nav.mode = Navigation.Mode.Explicit; nav.selectOnUp = last;
        nav.selectOnLeft = nav.selectOnRight = null;
        button.navigation = nav;
    }
}

[HarmonyPatch(typeof(GameplaySettingsUI), nameof(GameplaySettingsUI.UpdateManualUINavigation))]
internal static class NativeEntryNavigationPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameplaySettingsUI __instance) =>
        Plugin.Guard("Link Mod Companion entry", () => NativeNavigation.LinkEntry(__instance.GetComponentInParent<SettingsPanel>()));
}

// The native settings host still receives the game's tab actions. Route those to
// the outer mod row; d-pad/stick navigation reaches the inner category row.
[HarmonyPatch(typeof(Nivalis.UI.InGameMenu.WindowToggleController), nameof(Nivalis.UI.InGameMenu.WindowToggleController.TabLeftPressed))]
internal static class CompanionPreviousModPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Nivalis.UI.InGameMenu.WindowToggleController __instance)
    {
        if (!CompanionMenu.IsCompanion(__instance)) return true;
        CompanionMenu.Instance?.ChangeMod(-1);
        return false;
    }
}

[HarmonyPatch(typeof(Nivalis.UI.InGameMenu.WindowToggleController), nameof(Nivalis.UI.InGameMenu.WindowToggleController.TabRightPressed))]
internal static class CompanionNextModPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Nivalis.UI.InGameMenu.WindowToggleController __instance)
    {
        if (!CompanionMenu.IsCompanion(__instance)) return true;
        CompanionMenu.Instance?.ChangeMod(1);
        return false;
    }
}

[HarmonyPatch(typeof(ControlsSettingsUI), nameof(ControlsSettingsUI.UpdateManualUINavigation))]
internal static class CompanionControlsNavigationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ControlsSettingsUI __instance)
    {
        if (!CompanionMenu.IsCompanion(__instance)) return true;
        // The native algorithm assumes its original rows and Apply/Reset footer.
        // Companion categories may mix binding rows with scalar settings and help.
        CompanionMenu.Instance?.ConfigureNavigation();
        return false;
    }
}
