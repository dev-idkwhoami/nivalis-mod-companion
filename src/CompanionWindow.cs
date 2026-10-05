using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Nivalis.UI;
using Nivalis.UI.Concrete;
using NivalisMods.ModCompanion.Api;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NivalisMods.ModCompanion;

public sealed partial class CompanionMenu
{
    private GameObject? _categoryTemplate, _iconTemplate;
    private ScrollRect _modBar = null!, _categoryBar = null!, _details = null!;
    private TMP_Text _title = null!;
    private readonly List<Toggle> _modTabs = new(), _categoryTabs = new();
    private readonly List<ModRegistration?> _modOrder = new();
    private readonly List<ModSection?> _sections = new();
    private readonly Dictionary<string, string> _rememberedSections = new();
    private readonly Dictionary<string, Sprite> _icons = new();
    private ModRegistration? _overviewSelection;
    private int _selectedSection;
    private Selectable ActiveTab { [HideFromIl2Cpp] get => _categoryTabs.Count > 0 ? _categoryTabs[Math.Clamp(_selectedSection, 0, _categoryTabs.Count - 1)] : _modTabs[Math.Max(0, _modOrder.IndexOf(_mod!))]; }
    private IEnumerable<ScrollRect> AllScrolls { [HideFromIl2Cpp] get => _scrolls.Concat(new[] { _modBar, _categoryBar, _details }); }

    [HideFromIl2Cpp]
    private void CreateShell(SettingsPanel source)
    {
        var frame = _panel!.transform.Find("FrameWrapper").GetComponent<RectTransform>();
        frame.anchorMin = new Vector2(.07f, .035f); frame.anchorMax = new Vector2(.93f, .85f);
        frame.offsetMin = frame.offsetMax = Vector2.zero;
        // The native tab controller only switches our pages; Companion owns the window lifecycle.
        // Its four implementation tabs are replaced visually by our two navigation rows.
        var nativeTabs = _pages!.GetComponent<RectTransform>();
        nativeTabs.anchoredPosition = new Vector2(0, -10000);
        foreach (var tab in _pages!.toggles) tab.interactable = false;
        _title = frame.Find("P_Element_TitleFrame/Title").GetComponent<TMP_Text>();
        var titleFrame = _title.transform.parent.GetComponent<RectTransform>();
        titleFrame.anchorMin = new Vector2(.15f, 1); titleFrame.anchorMax = new Vector2(.85f, 1);
        titleFrame.sizeDelta = new Vector2(0, 45); titleFrame.anchoredPosition = new Vector2(0, -25);
        foreach (var page in _pages!.panels)
        {
            var rect = page.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(25, 20); rect.offsetMax = new Vector2(-25, -120);
        }
        var inventory = source.transform.parent.Find("P_InGameMenu/TabSelection/TabToggle_Inventory");
        inventory ??= Resources.FindObjectsOfTypeAll<Toggle>().FirstOrDefault(t => t.name == "TabToggle_Inventory")?.transform;
        // The main menu may be opened before inventory assets have been loaded.
        // It still gets the same navigation using the native settings tab artwork.
        _iconTemplate = Object.Instantiate(inventory != null ? inventory.gameObject : _categoryTemplate!, _templates!.transform);
        MenuTrace.Write("Window shell: " + (inventory != null ? "inventory icon template" : "settings tab fallback"));
        _modBar = HorizontalBar("ModCompanion.Mods", _panel.transform, .07f, .865f, .93f, .985f);
        _categoryBar = HorizontalBar("ModCompanion.Categories", frame, 0, 1, 1, 1);
        var categoryRect = _categoryBar.GetComponent<RectTransform>();
        categoryRect.offsetMin = new Vector2(30, -110); categoryRect.offsetMax = new Vector2(-30, -58);
        var overview = _scrolls[0].GetComponent<RectTransform>();
        overview.anchorMax = new Vector2(.49f, 1);
        _details = Object.Instantiate(_scrolls[3].gameObject, overview.parent).GetComponent<ScrollRect>();
        _details.name = "ModCompanion.ModDetails";
        var detailsRect = _details.GetComponent<RectTransform>();
        detailsRect.anchorMin = new Vector2(.51f, 0); detailsRect.anchorMax = Vector2.one;
        detailsRect.offsetMin = new Vector2(10, 15); detailsRect.offsetMax = new Vector2(-20, -15);
        Clear(_details.content);
        _details.onValueChanged = new ScrollRect.ScrollRectEvent();
        _details.gameObject.SetActive(true);
        ConfigureLayout(_details.content);
        CreateBrowserSearch(overview.parent);
        overview.offsetMax = new Vector2(-20, -58);
        _pages!.panels[0].GetComponent<RectTransform>().offsetMax = new Vector2(-25, -62);
        var controlsRect = _controls!.scroll.GetComponent<RectTransform>();
        controlsRect.anchorMin = new Vector2(.20f, 0); controlsRect.anchorMax = new Vector2(.80f, 1);
        controlsRect.offsetMin = new Vector2(20, 10); controlsRect.offsetMax = new Vector2(-20, -85);
        // Scalar settings and their hints share one readable-width column.
        var settingsRect = _scrolls[1].GetComponent<RectTransform>();
        settingsRect.anchorMin = new Vector2(.20f, 0); settingsRect.anchorMax = new Vector2(.80f, 1);
        _panel.firstSelected = _modBar.gameObject;
    }

    [HideFromIl2Cpp]
    private ScrollRect HorizontalBar(string name, Transform parent, float left, float bottom, float right, float top)
    {
        var rect = NativeUiParts.Rect(name, parent);
        rect.anchorMin = new Vector2(left, bottom); rect.anchorMax = new Vector2(right, top);
        rect.gameObject.AddComponent<RectMask2D>();
        var scroll = rect.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = true; scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.viewport = rect;
        var content = NativeUiParts.Rect("Content", rect);
        content.anchorMin = Vector2.zero; content.anchorMax = new Vector2(0, 1); content.pivot = new Vector2(0, .5f);
        var layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = 14;
        layout.padding = new RectOffset(12, 12, 8, 8);
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        return scroll;
    }

    [HideFromIl2Cpp]
    private Toggle Tab(GameObject template, Transform parent, string label, float width, float height, Action action)
    {
        var clone = Object.Instantiate(template, _templates!.transform); clone.SetActive(false);
        clone.name = "ModCompanion.Tab." + label;
        foreach (var nav in clone.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        var toggle = clone.GetComponent<Toggle>();
        toggle.group = null;
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.SetIsOnWithoutNotify(false);
        toggle.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<bool>>(new Action<bool>(value =>
        {
            if (!CompanionInput.Rebinding) _pendingUiChange = action;
            // Clicking the selected tab must not leave it visually unchecked.
            if (!value) toggle.SetIsOnWithoutNotify(true);
        })));
        clone.transform.SetParent(parent, false);
        var layout = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
        layout.minWidth = layout.preferredWidth = width; layout.flexibleWidth = 0;
        layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0;
        RestorePresentation(clone); clone.SetActive(true);
        return toggle;
    }

    [HideFromIl2Cpp]
    private void BuildModList()
    {
        EventSystem.current?.SetSelectedGameObject(null);
        Clear(_modBar.content); _modTabs.Clear(); _modOrder.Clear();
        _modOrder.Add(null); // Built-in browser, never a plugin registration.
        _modOrder.AddRange(SettingsRegistry.RegisteredMods.OrderBy(m => m.Name));
        foreach (var mod in _modOrder)
        {
            var tab = Tab(_iconTemplate!, _modBar.content, mod?.Name ?? "Mod Companion", 100, 90, () => SelectMod(mod));
            // Preserve the native angled border/selection art; replace the inventory glyph.
            foreach (var label in tab.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
            tab.transform.Find("SelectedIcon")?.gameObject.SetActive(false);
            tab.transform.Find("Icon")?.gameObject.SetActive(false);
            var better = tab.TryCast<TheraBytes.BetterUi.BetterToggle>();
            if (better != null)
            {
                better.betterTransitions?.Clear(); better.betterToggleTransitions?.Clear();
                better.betterTransitionsWhenOn?.Clear(); better.betterTransitionsWhenOff?.Clear();
            }
            var sprite = mod == null ? null : LoadIcon(mod);
            if (sprite != null)
            {
                var iconRect = NativeUiParts.Rect("ModIcon", tab.transform);
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(.5f, .5f);
                iconRect.sizeDelta = new Vector2(52, 52);
                var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = sprite;
                icon.preserveAspect = true; icon.raycastTarget = false;
            }
            // Inventory's normal-state sprite includes its glyph. Use its border-only selected
            // background as the neutral frame, with the native selection graphic layered above.
            var border = tab.transform.Find("SelectedBackground")?.GetComponent<Image>();
            var background = tab.GetComponent<Image>();
            if (border != null && background != null)
            {
                background.sprite = border.sprite;
                tab.graphic = border;
            }
            if (sprite == null)
            {
                var initials = NativeUiParts.Text(tab.transform, _font!, mod == null ? "MC" : Initials(mod.Name));
                initials.alignment = TextAlignmentOptions.Center; initials.fontSize = 27;
                initials.rectTransform.offsetMin = new Vector2(10, 12); initials.rectTransform.offsetMax = new Vector2(-10, -12);
            }
            _modTabs.Add(tab);
        }
        _panel!.firstSelected = _modTabs[0].gameObject;
        BuildOverview();
    }

    [HideFromIl2Cpp]
    private static string Initials(string name) => string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3).Select(word => char.ToUpperInvariant(word[0])));

    [HideFromIl2Cpp]
    private Sprite? LoadIcon(ModRegistration mod)
    {
        if (mod.Icon == null && string.IsNullOrWhiteSpace(mod.IconPath)) return null;
        if (_icons.TryGetValue(mod.Id, out var existing))
        {
            if (existing != null && existing.texture != null) return existing;
            // A managed cache entry can outlive its Unity asset after a save switch.
            if (existing != null) Object.Destroy(existing);
            _icons.Remove(mod.Id);
        }
        Texture2D? texture = null;
        try
        {
            texture = new Texture2D(2, 2) { hideFlags = HideFlags.DontUnloadUnusedAsset };
            if (!ImageConversion.LoadImage(texture, mod.Icon?.Data ?? File.ReadAllBytes(mod.IconPath!))) throw new IOException("Unsupported image.");
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
            sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _icons.Add(mod.Id, sprite);
            return sprite;
        }
        catch (Exception error)
        {
            if (texture != null) Object.Destroy(texture);
            Plugin.Logger.LogWarning($"Cannot load icon for {mod.Name}: {error.Message}");
            return null;
        }
    }

    [HideFromIl2Cpp]
    private void BuildOverview()
    {
        Clear(_scrolls[0].content);
        var query = _search?.text?.Trim() ?? "";
        var mods = SettingsRegistry.RegisteredMods.Where(m =>
            m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            m.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            m.Author.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            m.Description.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(m => m.Name).ToArray();
        foreach (var mod in mods) ModListRow(mod);
        if (mods.Length == 0)
        {
            Text(_scrolls[0].content, query.Length == 0 ? "No mods have registered settings." : "No matching mods.");
            Clear(_details.content);
            _overviewSelection = null;
        }
        else ShowDetails(mods.Contains(_overviewSelection) ? _overviewSelection! : mods[0]);
        ResetScroll(_scrolls[0]);
        ConfigureNavigation();
    }

    [HideFromIl2Cpp]
    private void ShowDetails(ModRegistration mod)
    {
        _overviewSelection = mod;
        Clear(_details.content);
        Heading(_details.content, mod.Name);
        Text(_details.content, "By " + mod.Author + " · Version " + mod.Version);
        if (mod.Description.Length > 0) Text(_details.content, mod.Description);
        Button(_details.content, "Open settings", () => _pendingUiChange = () => SelectMod(mod));
        ResetScroll(_details);
        ConfigureNavigation();
    }

    [HideFromIl2Cpp]
    private void SelectMod(ModRegistration? mod)
    {
        MenuTrace.Write("Select mod: " + (mod?.Id ?? "browser"));
        _mod = mod;
        EventSystem.current?.SetSelectedGameObject(null);
        _panel!._LastSelected_k__BackingField = null!;
        _title.text = mod?.Name ?? "Mod Companion";
        Clear(_categoryBar.content); _categoryTabs.Clear(); _sections.Clear();
        _categoryBar.gameObject.SetActive(mod != null);
        if (mod != null) _sections.AddRange(mod.Sections);
        for (var i = 0; i < _sections.Count; i++)
        {
            var index = i; var label = _sections[i]?.Label ?? "Overview";
            var tab = Tab(_categoryTemplate!, _categoryBar.content, label, Math.Clamp(60 + label.Length * 10, 140, 260), 36, () => SelectSection(index));
            NativeUiParts.Relabel(tab.gameObject, label);
            _categoryTabs.Add(tab);
        }
        for (var i = 0; i < _modTabs.Count; i++) _modTabs[i].SetIsOnWithoutNotify(_modOrder[i] == mod);
        var remembered = _rememberedSections.GetValueOrDefault(mod?.Id ?? "", "");
        var selected = _sections.FindIndex(section => (section?.Key ?? "") == remembered);
        SelectSection(Math.Max(0, selected));
    }

    [HideFromIl2Cpp]
    private void SelectSection(int index)
    {
        MenuTrace.Write("Select section: " + (_mod?.Id ?? "browser") + "/" + index);
        _selectedSection = index;
        _section = _sections.Count > 0 ? _sections[index] : null;
        _rememberedSections[_mod?.Id ?? ""] = _section?.Key ?? "";
        _refresh.Clear();
        for (var i = 0; i < _categoryTabs.Count; i++) _categoryTabs[i].SetIsOnWithoutNotify(i == index);
        if (_mod == null) _hostPage = 0;
        else if (_section?.Kind == SectionKind.Info)
        {
            _hostPage = 3; Clear(_scrolls[3].content);
            foreach (var block in _mod!.Info.Blocks)
                if (block.Kind == InfoBlockKind.Heading) Heading(_scrolls[3].content, block.Text);
                else Text(_scrolls[3].content, block.Text);
        }
        else
        {
            _hostPage = _section?.Category?.Entries.Any(e => e.Kind == ControlKind.KeyBinding) == true ? 2 : 1;
            if (_hostPage == 2) BuildBindings(_section!.Category!);
            else
            {
                Clear(_scrolls[1].content);
                if (_section?.Category != null)
                    foreach (var entry in _section.Category.Entries) Render(_scrolls[1].content, entry);
                else Text(_scrolls[1].content, "This mod has no settings.");
            }
            var error = Text(_scrolls[_hostPage].content, "");
            error.fontSize = 15;
            _refresh.Add(() => { error.text = _mod?.SaveError == null ? "" : "Could not save: " + _mod.SaveError; error.gameObject.SetActive(error.text.Length > 0); });
        }
        ConfigureScroll(_scrolls[_hostPage]);
        ResetScroll(_scrolls[_hostPage]);
        _layoutTraceFrames = 2;
        _lastSelection = null;
        foreach (var host in _pages!.panels) host.firstSelected = ActiveTab.gameObject;
        if (_panel!.gameObject.activeInHierarchy && _panel.IsVisible)
        {
            _pages!.SwitchToPanel(_hostPage);
            ActiveTab.Select();
        }
        ConfigureNavigation();
    }

    [HideFromIl2Cpp]
    private void Heading(Transform parent, string value)
    {
        var text = Text(parent, value); text.fontSize = 25; text.fontStyle = FontStyles.Bold;
        text.color = new Color(.83f, .66f, .40f, 1);
    }

    [HideFromIl2Cpp]
    internal void ChangeMod(int direction)
    {
        if (_panel?.IsVisible != true || CompanionInput.Rebinding || _modOrder.Count == 0) return;
        var index = Math.Max(0, _modOrder.IndexOf(_mod!));
        _pendingUiChange = () => SelectMod(_modOrder[(index + direction + _modOrder.Count) % _modOrder.Count]);
    }

    [HideFromIl2Cpp]
    private List<Selectable> Rows(Transform content)
    {
        var rows = new List<Selectable>();
        for (var i = 0; i < content.childCount; i++)
        {
            var child = content.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            var select = child.GetComponent<Selectable>() ?? child.GetComponentInChildren<Selectable>();
            if (select == null || !select.IsInteractable()) continue;
            rows.Add(select);
            if (child.GetComponent<EventTrigger>() != null)
                foreach (var button in child.GetComponentsInChildren<Button>(true))
                    if (button.Pointer != select.Pointer) { var nav = button.navigation; nav.mode = Navigation.Mode.None; button.navigation = nav; }
        }
        return rows;
    }

    [HideFromIl2Cpp]
    private static void Link(Selectable select, Selectable? up, Selectable? down, Selectable? left, Selectable? right)
    {
        var nav = select.navigation; nav.mode = Navigation.Mode.Explicit;
        nav.selectOnUp = up; nav.selectOnDown = down; nav.selectOnLeft = left; nav.selectOnRight = right;
        select.navigation = nav;
    }

    [HideFromIl2Cpp]
    internal void ConfigureNavigation()
    {
        if (_modTabs.Count == 0) return;
        var active = ActiveTab;
        var top = _modTabs[Math.Max(0, _modOrder.IndexOf(_mod!))];
        var rows = Rows(_scrolls[_hostPage].content);
        var scrollbar = _scrolls[_hostPage].verticalScrollbar;
        if (rows.Count == 0 && scrollbar != null && scrollbar.gameObject.activeInHierarchy) rows.Add(scrollbar);
        if (_hostPage == 0 && _search != null) rows.Insert(0, _search);
        if (_hostPage == 0 && _developerToggle != null)
        {
            _developerToggle.SetIsOnWithoutNotify(SettingsRegistry.DeveloperMode);
            rows.Add(_developerToggle);
        }
        var first = rows.FirstOrDefault() ?? active;
        for (var i = 0; i < _modTabs.Count; i++)
            Link(_modTabs[i], null, _categoryTabs.Count > 0 ? active : first, _modTabs[(i + _modTabs.Count - 1) % _modTabs.Count], _modTabs[(i + 1) % _modTabs.Count]);
        for (var i = 0; i < _categoryTabs.Count; i++)
            Link(_categoryTabs[i], top, first, _categoryTabs[(i + _categoryTabs.Count - 1) % _categoryTabs.Count], _categoryTabs[(i + 1) % _categoryTabs.Count]);
        var detailRows = _hostPage == 0 ? Rows(_details.content) : new List<Selectable>();
        for (var i = 0; i < rows.Count; i++)
        {
            var select = rows[i];
            if (select.TryCast<Scrollbar>() != null)
            {
                Link(select, null, null, active, active);
                continue;
            }
            var horizontal = select.TryCast<Slider>() != null ? null : select;
            Link(select, i > 0 ? rows[i - 1] : active, i + 1 < rows.Count ? rows[i + 1] : active, horizontal, detailRows.FirstOrDefault() ?? horizontal);
        }
        foreach (var select in detailRows) Link(select, active, active, rows.FirstOrDefault() ?? active, null);
    }
}
