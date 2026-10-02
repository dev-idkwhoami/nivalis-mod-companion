using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NivalisMods.ModCompanion;

public sealed partial class CompanionMenu
{
    private int _layoutTraceFrames;
    public void LateUpdate()
    {
        if (_layoutTraceFrames <= 0 || _panel?.IsVisible != true) return;
        if (--_layoutTraceFrames != 0) return;
        Plugin.Guard("Inspect companion layout", () =>
        {
            var scroll = _scrolls[_hostPage];
            MenuTrace.Write($"Layout page {_hostPage}: scroll enabled={scroll.enabled}, viewport={scroll.viewport.rect}, content={scroll.content.rect}, children={scroll.content.childCount}");
            if (_hostPage == 2)
                foreach (var row in _controls!.controls)
                    MenuTrace.Write($"Binding {row.name}: active={row.gameObject.activeInHierarchy}, rect={row.GetComponent<RectTransform>().rect}, alpha={row.GetComponent<CanvasGroup>()?.alpha}, input={row.input?.text}");
        });
    }

    [HideFromIl2Cpp]
    private static void ConfigureScroll(ScrollRect scroll)
    {
        scroll.enabled = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        if (!scroll.vertical || scroll.viewport == null || scroll.viewport == scroll.transform) return;
        // Controls' serialized viewport is zero-sized; the original ScrollRect drives it.
        // Use explicit geometry so visibility does not depend on a copied layout cache.
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        var viewport = scroll.viewport;
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero; viewport.offsetMax = new Vector2(-18, 0);
    }

    [HideFromIl2Cpp]
    private static void AlignHint(TMP_Text hint, TMP_Text? label)
    {
        if (label == null || !hint.gameObject.activeInHierarchy) return;
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(hint.rectTransform, label.rectTransform);
        var left = Mathf.Max(0, bounds.min.x - hint.rectTransform.rect.xMin + label.margin.x);
        hint.margin = new Vector4(left, 0, 8, 5);
    }

    [HideFromIl2Cpp]
    private void RefreshScrollbars()
    {
        foreach (var scroll in AllScrolls)
        {
            if (scroll == null || !scroll.vertical || !scroll.gameObject.activeInHierarchy) continue;
            if (scroll.verticalScrollbar != null)
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }
    }
}
