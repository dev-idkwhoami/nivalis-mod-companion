using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using NivalisMods.ModCompanion.Api;

namespace NivalisMods.ModCompanion;

[BepInPlugin(SettingsRegistry.PluginId, "Mod Companion", SettingsRegistry.PluginVersion)]
public sealed class Plugin : BasePlugin
{
    internal static BepInEx.Configuration.ConfigEntry<bool> DeveloperModeSetting = null!;
    internal static ManualLogSource Logger = null!;
    public override void Load()
    {
        Logger = Log;
        MenuTrace.Write("=== Mod Companion compact binding rows build 7 loaded ===");
        SettingsRegistry.Report = message => Log.LogWarning(message);
        DeveloperModeSetting = Config.Bind("Menu", "DeveloperMode", false, "Show developer settings and test actions for registered mods.");
        SettingsRegistry.DeveloperMode = DeveloperModeSetting.Value;
        DeveloperModeSetting.SettingChanged += (_, _) => SettingsRegistry.DeveloperMode = DeveloperModeSetting.Value;
        CompanionInput.Initialize();
        new Harmony(SettingsRegistry.PluginId).PatchAll(typeof(Plugin).Assembly);
        AddComponent<CompanionMenu>();
        CompanionMenu.MenuAction = new UnityEngine.InputSystem.InputAction("OpenModCompanion",
            UnityEngine.InputSystem.InputActionType.Button,
            Config.Bind("Menu", "Keyboard", "<Keyboard>/f5", "Open the Mod Companion browser.").Value);
        UnityEngine.InputSystem.InputActionSetupExtensions.AddBinding(CompanionMenu.MenuAction,
            Config.Bind("Menu", "Gamepad", "", "Optional controller binding path for opening Mod Companion.").Value);
        CompanionMenu.MenuAction.Enable();
        if (Config.Bind("Development", "ShowExample", false, "Register a sample mod covering every control. Restart to apply.").Value)
            ExampleSettings.Register();
        Log.LogInfo($"Mod Companion {SettingsRegistry.PluginVersion} loaded. F5 or native Settings > Mod Companion.");
    }
    internal static void Guard(string operation, Action action)
    {
        try { action(); } catch (Exception e) { Logger.LogError($"{operation}: {e}"); }
    }
}

internal static class ExampleSettings
{
    internal static void Register()
    {
        var mod = SettingsRegistry.Register("local.nivalis.modcompanion.example",
            new ModMetadata("Example Mod", "Mod Companion", "1.0", "Demonstrates all supported settings and controller navigation."));
        mod.Developer.AddVerboseLogging();
        mod.Info.AddHeading("Example Mod");
        mod.Info.AddParagraph("A sample integration for mod authors.");
        var general = mod.Category("General", "General");
        general.Toggle("Enabled", "Enable example", true);
        general.Choice("Mode", "Mode", "normal", new[] { new Choice<string>("normal", "Normal"), new Choice<string>("relaxed", "Relaxed") });
        general.Slider("Scale", "Scale", 1f, .5f, 2f, .1f);
        general.Stepper("Count", "Count", 5, 1, 20);
        general.Stepper("Restart", "Restart example", 1, 1, 3, requiresRestart: true);
        general.Text("Explanation", "These settings demonstrate the API and have no gameplay effects.");
        general.Button("Log", "Write example log message", () => Plugin.Logger.LogInfo("Example button pressed."));
        mod.Controls.AddInput("ExampleAction", "Example action", "<Keyboard>/f10", "<Gamepad>/rightStickPress");
        var scrolling = mod.Category("Scrolling", "Scrolling");
        for (var i = 1; i <= 20; i++) scrolling.Toggle("Row" + i, "Scrolling test " + i, true);
    }
}
