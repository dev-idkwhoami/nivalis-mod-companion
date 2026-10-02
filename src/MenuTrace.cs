using System.Reflection;
using BepInEx;
using HarmonyLib;
using Nivalis;
using Nivalis.UI.Concrete;
using Nivalis.UI.InGameMenu;
using UnityEngine;

namespace NivalisMods.ModCompanion;

// Native process failures bypass managed exception handlers and buffered logs.
// Flush each construction boundary so the last started operation survives a crash.
internal static class MenuTrace
{
    internal static bool Constructing;
    internal static void Write(string step)
    {
        try
        {
            var directory = Path.Combine(Paths.ConfigPath, "ModCompanion");
            Directory.CreateDirectory(directory);
            using var file = new FileStream(Path.Combine(directory, "menu-trace.log"), FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var bytes = System.Text.Encoding.UTF8.GetBytes($"{DateTime.UtcNow:O} {step}{Environment.NewLine}");
            file.Write(bytes);
            file.Flush(true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

[HarmonyPatch]
internal static class TraceMenuLifecycle
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(SettingsPanel), nameof(SettingsPanel.Awake));
        yield return AccessTools.Method(typeof(SettingsTabPanel), nameof(SettingsTabPanel.Awake));
        yield return AccessTools.Method(typeof(ControlsSettingsUI), nameof(ControlsSettingsUI.Start));
        yield return AccessTools.Method(typeof(WindowToggleController), nameof(WindowToggleController.Awake));
        yield return AccessTools.Method(typeof(WindowToggleController), nameof(WindowToggleController.Start));
        yield return AccessTools.Method(typeof(UISubPanel), nameof(UISubPanel.Awake));
        yield return AccessTools.Method(typeof(RebindOverlayUI), nameof(RebindOverlayUI.Awake));
        yield return AccessTools.Method(typeof(RebindConfirmationUI), nameof(RebindConfirmationUI.Awake));
        yield return AccessTools.Method(typeof(InputRebindUI), nameof(InputRebindUI.Rebind));
        yield return AccessTools.Method(typeof(InputRebindUI), nameof(InputRebindUI.Awake));
        yield return AccessTools.Method(typeof(InputRebindUI), nameof(InputRebindUI.OnEnable));
        yield return AccessTools.Method(typeof(SettingsPanel), nameof(SettingsPanel.Show));
        yield return AccessTools.Method(typeof(SettingsPanel), nameof(SettingsPanel.Hide));
    }

    [HarmonyPrefix]
    private static void Before(Component __instance, MethodBase __originalMethod, out bool __state)
    {
        __state = MenuTrace.Constructing || CompanionMenu.IsCompanion(__instance);
        if (__state) MenuTrace.Write($"ENTER {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} ({__instance.name})");
    }

    [HarmonyPostfix]
    private static void After(MethodBase __originalMethod, bool __state)
    {
        if (__state) MenuTrace.Write($"EXIT {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}");
    }
}
