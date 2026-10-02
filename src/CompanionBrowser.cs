using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using Nivalis.Localization;
using NivalisMods.ModCompanion.Api;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NivalisMods.ModCompanion;

public sealed partial class CompanionMenu
{
    private TMP_InputField? _search;
    private Image? _inventoryRowArt;
    private Toggle? _developerToggle;

    [HideFromIl2Cpp]
    private void CreateBrowserSearch(Transform parent)
    {
        var source = Resources.FindObjectsOfTypeAll<TMP_InputField>().FirstOrDefault(f => f.name.StartsWith("P_Element_SearchInputField"));
        if (source == null) throw new InvalidOperationException("Native search field template is unavailable.");
        var clone = Object.Instantiate(source.gameObject, _templates!.transform); clone.SetActive(false);
        clone.name = "ModCompanion.Search";
        _search = clone.GetComponent<TMP_InputField>();
        _search.onValueChanged = new TMP_InputField.OnChangeEvent();
        _search.onEndEdit = new TMP_InputField.SubmitEvent();
        _search.onSubmit = new TMP_InputField.SubmitEvent();
        _search.onSelect = new TMP_InputField.SelectionEvent();
        _search.onDeselect = new TMP_InputField.SelectionEvent();
        _search.SetTextWithoutNotify("");
        _search.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(new Action<string>(_ => _pendingUiChange = BuildOverview)));
        foreach (var nav in clone.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        // The native search navigation retains inventory-specific neighbours.
        foreach (var component in clone.GetComponents<MonoBehaviour>())
            if (component.GetIl2CppType().Name == "InputFieldNavigation") Object.DestroyImmediate(component);
        foreach (var localized in clone.GetComponentsInChildren<LocalizedStaticUILabel>(true)) Object.DestroyImmediate(localized);
        var placeholder = _search.placeholder?.TryCast<TMP_Text>();
        if (placeholder != null) placeholder.text = "Search mods…";
        var clear = clone.transform.Find("ClearButton")?.GetComponent<Button>();
        if (clear != null) Click(clear, () => _search.text = "");
        clone.transform.SetParent(parent, false);
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(.49f, 1);
        rect.offsetMin = new Vector2(20, -45); rect.offsetMax = new Vector2(-20, -8);
        RestorePresentation(clone); clone.SetActive(true);
        var devRow = Object.Instantiate(_toggleTemplate!, _templates!.transform);
        devRow.SetActive(false);
        var native = devRow.GetComponent<Nivalis.ToggleSettingUI>();
        _developerToggle = native.toggle;
        Object.DestroyImmediate(native);
        NativeUiParts.Relabel(devRow, "Enable developer mode");
        _developerToggle.onValueChanged = new Toggle.ToggleEvent();
        _developerToggle.SetIsOnWithoutNotify(SettingsRegistry.DeveloperMode);
        _developerToggle.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<bool>>(new Action<bool>(enabled => Plugin.DeveloperModeSetting.Value = enabled)));
        foreach (var nav in devRow.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        devRow.transform.SetParent(parent, false);
        var devRect = devRow.GetComponent<RectTransform>();
        devRect.anchorMin = new Vector2(0, 0); devRect.anchorMax = new Vector2(.49f, 0);
        devRect.offsetMin = new Vector2(20, 0); devRect.offsetMax = new Vector2(-20, 42);
        RestorePresentation(devRow); devRow.SetActive(true);
        _scrolls[0].GetComponent<RectTransform>().offsetMin = new Vector2(20, 55);
        _inventoryRowArt = Resources.FindObjectsOfTypeAll<Image>().FirstOrDefault(i => i.name == "P_Element_ScrollView_InventoryItem");
    }

    [HideFromIl2Cpp]
    private void ModListRow(ModRegistration mod)
    {
        var rect = NativeUiParts.Rect("Mod." + mod.Id, _scrolls[0].content);
        NativeUiParts.Image(_inventoryRowArt ?? _buttonTemplate!.GetComponent<Image>(), rect.gameObject);
        var image = rect.GetComponent<Image>(); image.raycastTarget = true;
        var size = rect.gameObject.AddComponent<LayoutElement>();
        size.minHeight = size.preferredHeight = 62; size.flexibleHeight = 0;
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .84f, .55f, 1);
        colors.selectedColor = colors.highlightedColor; button.colors = colors;
        Click(button, () => _pendingUiChange = () => ShowDetails(mod));
        var iconRect = NativeUiParts.Rect("Icon", rect);
        iconRect.anchorMin = new Vector2(0, .5f); iconRect.anchorMax = iconRect.anchorMin;
        iconRect.pivot = new Vector2(0, .5f); iconRect.sizeDelta = new Vector2(48, 48); iconRect.anchoredPosition = new Vector2(8, 0);
        var sprite = LoadIcon(mod);
        if (sprite != null)
        {
            var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = sprite; icon.preserveAspect = true; icon.raycastTarget = false;
        }
        else
        {
            var initials = NativeUiParts.Text(iconRect, _font!, Initials(mod.Name));
            initials.fontSize = 18; initials.alignment = TextAlignmentOptions.Center;
        }
        var label = NativeUiParts.Text(rect, _font!, mod.Name);
        label.fontSize = 19; label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.offsetMin = new Vector2(70, 0); label.rectTransform.offsetMax = new Vector2(-12, 0);
    }

    [HideFromIl2Cpp]
    private static void ResetScroll(ScrollRect scroll)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        scroll.StopMovement();
        scroll.verticalNormalizedPosition = 1;
        scroll.content.anchoredPosition = Vector2.zero;
    }
}
