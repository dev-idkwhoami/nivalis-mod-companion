using HarmonyLib;
using Nivalis;
using NivalisMods.ModCompanion.Api;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInputManager = Nivalis.PlayerInputManager;

namespace NivalisMods.ModCompanion;

public static class CompanionInput
{
    private static readonly Dictionary<string, InputAction> Actions = new();
    private static readonly Dictionary<IntPtr, KeyBindingSetting> Owned = new();
    internal static float ResumeAfter;
    internal static bool Rebinding;
    private static bool _syncing;
    private static InputActionAsset? _asset;

    internal static void Initialize()
    {
        PlayerInputManager.OnRebindStarted += (Il2CppSystem.Action)(() => Rebinding = true);
        PlayerInputManager.OnRebindComplete += (Il2CppSystem.Action)(() => { Capture(); End(); });
        PlayerInputManager.OnRebindCanceled += (Il2CppSystem.Action)End;
    }
    private static void End() { Rebinding = false; ResumeAfter = Time.unscaledTime + .3f; }

    internal static InputAction? Resolve(KeyBindingSetting setting)
    {
        var controls = PlayerInputManager._instance?.Input;
        if (controls == null) return null;
        if (setting.NativeAction != null) return controls.asset.FindAction(setting.NativeAction, false);
        if (Actions.TryGetValue(setting.ActionId, out var existing)) return existing;
        _asset ??= ScriptableObject.CreateInstance<InputActionAsset>();
        _asset.Disable();
        var map = new InputActionMap(setting.ActionId.Replace('/', '.'));
        var action = InputActionSetupExtensions.AddAction(map, "Action", InputActionType.Button, expectedControlLayout: "Button");
        InputActionSetupExtensions.AddActionMap(_asset, map);
        InputActionSetupExtensions.AddBinding(action, new InputBinding(setting.Keyboard!.DefaultValue, groups: controls.KeyboardScheme.bindingGroup));
        InputActionSetupExtensions.AddBinding(action, new InputBinding(setting.Gamepad!.DefaultValue, groups: controls.GamepadScheme.bindingGroup));
        Actions.Add(setting.ActionId, action); Owned.Add(action.Pointer, setting);
        setting.Keyboard.Changed += path => Apply(action, 0, path);
        setting.Gamepad.Changed += path => Apply(action, 1, path);
        Apply(action, 0, setting.Keyboard.Value);
        Apply(action, 1, setting.Gamepad.Value);
        _asset.Enable();
        return action;
    }

    private static void Apply(InputAction action, int index, string path)
    {
        if (!_syncing) InputActionRebindingExtensions.ApplyBindingOverride(action, index, path);
    }

    internal static void Capture()
    {
        _syncing = true;
        try
        {
            foreach (var action in Actions.Values)
            {
                var setting = Owned[action.Pointer];
                setting.Keyboard!.Value = action.bindings[0].effectivePath ?? "";
                setting.Gamepad!.Value = action.bindings[1].effectivePath ?? "";
            }
        }
        finally { _syncing = false; }
    }

    /// <summary>Gameplay polling; suppressed during rebinding and while a mouse/pause panel is open.</summary>
    public static bool WasPressed(KeyBindingSetting setting)
    {
        if (!Application.isFocused || Rebinding || Time.unscaledTime < ResumeAfter) return false;
        var ui = UIManager._instance;
        if (ui != null)
            foreach (var panel in ui._openPanels)
                if (panel != null && panel.IsVisible && (panel.requiresMouse || panel.pauseTimeWhenOpen)) return false;
        return Resolve(setting)?.WasPressedThisFrame() == true;
    }

    internal static bool IsOwned(InputAction? action) => action != null && Owned.ContainsKey(action.Pointer);
}

// The native row resolves references through the player's asset. Only our standalone
// actions bypass that lookup; they are deliberately absent from the game's save data.
[HarmonyPatch(typeof(PlayerInputManager), nameof(PlayerInputManager.GetActionForActionReference))]
internal static class ResolveCompanionAction
{
    [HarmonyPrefix]
    private static bool Prefix(InputActionReference __0, ref InputAction __result)
    {
        var action = __0?.action;
        if (!CompanionInput.IsOwned(action)) return true;
        __result = action!;
        return false;
    }
}

[HarmonyPatch(typeof(InputRebindUI), nameof(InputRebindUI.ResetBinding))]
internal static class SaveResetBinding
{
    [HarmonyPostfix]
    private static void Postfix(InputRebindUI __instance)
    {
        if (CompanionInput.IsOwned(__instance.action?.actionReference?.action)) CompanionInput.Capture();
    }
}

[HarmonyPatch(typeof(InputRebindUI), nameof(InputRebindUI.UpdateText))]
internal static class BindingLabel
{
    internal static readonly Dictionary<IntPtr, string> Labels = new();
    [HarmonyPostfix]
    private static void Postfix(InputRebindUI __instance)
    {
        if (Labels.TryGetValue(__instance.Pointer, out var label)) __instance.actionText.text = label;
    }
}
