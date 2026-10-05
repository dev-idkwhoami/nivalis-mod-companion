using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using UnityEngine;

namespace NivalisMods.ModCompanion;

public sealed partial class CompanionMenu
{
    // SettingsPanel used to provide this lock. Keep it local so settings-specific
    // patches cannot intercept Companion's open, close, or pending-changes flow.
    [HideFromIl2Cpp]
    internal void UpdateRebindingLock()
    {
        if (_panel == null || _pages == null) return;
        var blocked = CompanionInput.Rebinding || Time.unscaledTime < CompanionInput.ResumeAfter;
        foreach (var confirmation in _rebindConfirmations)
            blocked |= confirmation != null && confirmation.IsVisible;
        _pages.Blocked = blocked;
        _panel.closeWithCancel = !blocked;
    }

    [HideFromIl2Cpp]
    private void CloseWindow()
    {
        UpdateRebindingLock();
        if (_panel != null && _pages?.Blocked == false) _panel.Hide();
    }
}
