using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Nivalis;
using Nivalis.Localization;
using Nivalis.UI;
using Nivalis.UI.Concrete;
using NivalisMods.ModCompanion.Api;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using PlayerInputManager = Nivalis.PlayerInputManager;

namespace NivalisMods.ModCompanion;

public sealed partial class CompanionMenu : MonoBehaviour
{
    internal const string RootName = "ModCompanion.Settings";
    internal static CompanionMenu? Instance;
    private SettingsPanel? _panel;
    private GameObject? _templates;
    private GameObject? _toggleTemplate, _stepTemplate, _sliderTemplate, _buttonTemplate, _bindingTemplate;
    private TMP_Text? _font;
    private ControlsSettingsUI? _controls;
    private ScrollRect[] _scrolls = Array.Empty<ScrollRect>();
    private readonly List<Action> _refresh = new();
    private ModRegistration? _mod;
    private ModSection? _section;
    private int _hostPage;
    private int _revision = -1;
    private float _nextRefresh, _nextSave;
    private SettingsPanel? _returnTo;
    private int _showFrame = -1, _returnFrame = -1;
    private bool _wasVisible;
    private bool _constructionFailed;
    private GameObject? _lastSelection;
    private Action? _pendingUiChange;
    internal static InputAction? MenuAction;
    public CompanionMenu(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;
    public void Update() => Plugin.Guard("Mod Companion menu", Tick);

    [HideFromIl2Cpp]
    private void Tick()
    {
        // Rebuild outside native button/event dispatch; destroying its sender in
        // a Unity callback can leave the native event system holding a dead object.
        if (_pendingUiChange != null && !CompanionInput.Rebinding)
        {
            var pending = _pendingUiChange; _pendingUiChange = null; pending();
        }
        if (Time.unscaledTime >= _nextSave)
        {
            _nextSave = Time.unscaledTime + 1;
            foreach (var mod in SettingsRegistry.RegisteredMods)
                if (mod.IsDirty) mod.Save();
        }
        if (_returnFrame >= 0 && Time.frameCount >= _returnFrame)
        {
            _returnFrame = -1;
            if (_returnTo != null) _returnTo.Show();
            _returnTo = null;
        }
        if (_showFrame >= 0 && Time.frameCount >= _showFrame)
        {
            _showFrame = -1;
            if (_panel != null)
            {
                MenuTrace.Write("Deferred show: calling native SettingsPanel.Show");
                _panel.Show();
                SelectMod(_mod);
                MenuTrace.Write("Deferred show: complete");
                _panel.GetComponent<CanvasGroup>().alpha = 1;
            }
        }
        var visible = _panel != null && _panel.IsVisible;
        if (_wasVisible && !visible)
        {
            foreach (var mod in SettingsRegistry.RegisteredMods) if (mod.IsDirty) mod.Save();
            CompanionInput.ResumeAfter = Time.unscaledTime + .3f;
            if (_returnTo != null) _returnFrame = Time.frameCount + 1;
        }
        _wasVisible = visible;
        if (Application.isFocused && _search?.isFocused != true && !CompanionInput.Rebinding && Time.unscaledTime >= CompanionInput.ResumeAfter && MenuAction?.WasPressedThisFrame() == true)
        {
            if (visible) _panel!.Hide();
            else Open();
        }
        if (!visible) return;
        if (_revision != SettingsRegistry.Revision && !CompanionInput.Rebinding)
        {
            BuildModList(); SelectMod(_mod);
            _revision = SettingsRegistry.Revision;
        }
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + .15f;
            foreach (var refresh in _refresh) SettingsRegistry.Notify(refresh);
            ConfigureNavigation();
            RefreshScrollbars();
        }
        KeepSelectionVisible();
    }

    [HideFromIl2Cpp]
    public void Open(SettingsPanel? origin = null)
    {
        if (_constructionFailed || CompanionInput.Rebinding || PlayerInputManager._instance?.Input == null || UIManager._instance == null) return;
        if (origin == null)
        {
            foreach (var panel in UIManager._instance._openPanels)
                if (panel != null && panel.IsVisible && panel.requiresMouse) return;
        }
        MenuTrace.Write("Open requested");
        if (_panel == null) Create();
        if (_panel == null) return;
        if (origin != null)
        {
            origin.Hide();
            // Do not bypass a native pending-changes confirmation.
            if (origin.IsVisible) return;
            _returnTo = origin;
        }
        _panel!.transform.SetAsLastSibling();
        _showFrame = Time.frameCount + 1;
    }

    [HideFromIl2Cpp]
    internal static bool IsCompanion(Component component) => component.GetComponentInParent<SettingsPanel>()?.name == RootName;
    [HideFromIl2Cpp]
    internal static void Click(Button button, Action callback)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(() => Plugin.Guard("Settings action", callback)));
        button.interactable = true;
    }

    [HideFromIl2Cpp]
    internal void AddEntryButton(SettingsPanel settings)
    {
        if (settings == null || settings.name == RootName || !settings.gameObject.scene.IsValid()) return;
        var wrapper = settings.transform.Find("FrameWrapper/GameplaySettings/ButtonsWrapper");
        if (wrapper == null) return;
        if (wrapper.Find("ModCompanion.Open") != null) { NativeNavigation.LinkEntry(settings); return; }
        var source = wrapper.Find("Apply")?.GetComponent<Button>();
        if (source == null) return;
        var button = Object.Instantiate(source.gameObject, wrapper);
        button.name = "ModCompanion.Open";
        NativeUiParts.Relabel(button, "Mod Companion");
        Click(button.GetComponent<Button>(), () => Open(settings));
        foreach (var navigation in button.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(navigation);
        RestorePresentation(button);
        var manager = button.GetComponentInParent<CanvasBehaviourManager>();
        if (manager != null)
        {
            var hidden = !manager.GetCanvasIsEnabled();
            foreach (var behaviour in button.GetComponentsInChildren<Behaviour>(true))
                if (behaviour is Graphic || behaviour is LayoutGroup || behaviour is Selectable)
                    manager.RegisterElementToDisable(behaviour, true, hidden);
        }
        button.SetActive(true);
        NativeNavigation.LinkEntry(settings);
    }

    [HideFromIl2Cpp]
    private void Create()
    {
        MenuTrace.Write("Create: locating native settings source");
        var source = Resources.FindObjectsOfTypeAll<SettingsPanel>().FirstOrDefault(s => s != null && s.gameObject.scene.IsValid() && s.name != RootName && s.togglesGroup?.toggles.Length == 4);
        if (source == null) throw new InvalidOperationException("Native settings window is unavailable.");
        var staging = new GameObject("ModCompanion.Templates"); staging.SetActive(false);
        GameObject? root = null;
        try
        {
            MenuTrace.Constructing = true;
            MenuTrace.Write($"Create: cloning source {source.name}");
            root = Object.Instantiate(source.gameObject, staging.transform); root.SetActive(false); root.name = RootName;
            MenuTrace.Write("Create: clone returned; locating native controllers");
            _panel = root.GetComponent<SettingsPanel>();
            _controls = root.GetComponentInChildren<ControlsSettingsUI>(true);
            if (_controls == null) throw new InvalidOperationException("Native Controls section is unavailable.");
            _templates = staging;
            GameObject Copy(GameObject obj)
            {
                MenuTrace.Write("Create: copying template " + obj.name);
                var copy = Object.Instantiate(obj, staging.transform);
                MenuTrace.Write("Create: copied template " + obj.name);
                return copy;
            }
            _toggleTemplate = Copy(root.GetComponentsInChildren<ToggleSettingUI>(true).First(t => t.name == "P_DisableHeadBob").gameObject);
            _stepTemplate = Copy(root.GetComponentInChildren<ResolutionSettingUI>(true).gameObject);
            _sliderTemplate = Copy(root.GetComponentsInChildren<SliderSettingUI>(true).First(s => s.name == "P_MouseSensitivity").gameObject);
            // Native SettingsTabPanel caches are not available on an inactive clone.
            // Resolve the serialized hierarchy instead of dereferencing applyButton.
            MenuTrace.Write("Create: resolving Controls hierarchy references");
            T RequireControl<T>(string path) where T : Component
            {
                var component = _controls.transform.Find(path)?.GetComponent<T>();
                return component ?? throw new InvalidOperationException($"Native Controls/{path} has no {typeof(T).Name}.");
            }
            _controls.applyButton = RequireControl<Button>("ButtonsWrapper/Apply");
            _controls.resetButton = RequireControl<Button>("ButtonsWrapper/Reset");
            _controls.restoreDefaultsButton = RequireControl<Button>("ButtonsWrapper/Restore");
            _controls.scroll = RequireControl<ScrollRect>("Scrollview");
            _controls.rebindOverlay = RequireControl<UIPanel>("RebindOverlay");
            _controls.controls = new Il2CppSystem.Collections.Generic.List<InputRebindUI>();
            _buttonTemplate = Copy(_controls.applyButton.gameObject);
            _bindingTemplate = Copy(_controls.GetComponentsInChildren<InputRebindUI>(true).First(r => r.name == "Inventory").gameObject);
            Clear(_controls.scroll.content);
            ConfigureLayout(_controls.scroll.content);
            _controls.scroll.content.GetComponent<VerticalLayoutGroup>().spacing = 24;
            _font = _toggleTemplate.GetComponentInChildren<TMP_Text>(true);
            _categoryTemplate = Copy(_panel.togglesGroup.toggles[0].gameObject);
            var scrollTemplate = Copy(root.transform.Find("FrameWrapper/GameplaySettings/MainSettings/Scrollview").gameObject);
            var controller = _panel.togglesGroup;
            var oldPanels = controller.panels.ToArray();
            var newPanels = new UISubPanel[4];
            _scrolls = new ScrollRect[4];
            // Keep Controls as the third tab, including overlay, conflict confirmation and device listeners.
            for (var i = 0; i < 4; i++)
            {
                MenuTrace.Write("Create: building page " + i);
                if (i == 2) { newPanels[i] = _controls; _scrolls[i] = _controls.scroll; continue; }
                var rect = NativeUiParts.Rect("ModCompanion.Page" + i, oldPanels[0].transform.parent);
                var originalRect = oldPanels[0].GetComponent<RectTransform>();
                rect.anchorMin = originalRect.anchorMin; rect.anchorMax = originalRect.anchorMax;
                rect.pivot = originalRect.pivot; rect.sizeDelta = originalRect.sizeDelta; rect.anchoredPosition = originalRect.anchoredPosition;
                rect.gameObject.SetActive(true);
                rect.gameObject.AddComponent<CanvasGroup>();
                var page = rect.gameObject.AddComponent<UISubPanel>();
                // AddComponent does not populate the serialized Optional objects that
                // native FadeShow/FadeIn/FadeOut dereference. Preserve the source's
                // presentation defaults before the controller can display this page.
                page.fadeInTimeOverride = oldPanels[0].fadeInTimeOverride ?? new OptionalFloat(0f, false);
                page.fadeOutTimeOverride = oldPanels[0].fadeOutTimeOverride ?? new OptionalFloat(0f, false);
                page.ease = oldPanels[0].ease ?? new Optional<DG.Tweening.Ease>(DG.Tweening.Ease.Linear);
                page.startVisible = i == 0;
                MenuTrace.Write($"Create: page {i} animation settings initialized");
                var scrollObject = Object.Instantiate(scrollTemplate, rect);
                var scrollRect = scrollObject.GetComponent<RectTransform>();
                scrollRect.anchorMin = Vector2.zero; scrollRect.anchorMax = Vector2.one;
                scrollRect.offsetMin = new Vector2(20, 15); scrollRect.offsetMax = new Vector2(-20, -15);
                var scroll = scrollObject.GetComponent<ScrollRect>();
                scroll.onValueChanged = new ScrollRect.ScrollRectEvent(); Clear(scroll.content);
                ConfigureLayout(scroll.content);
                newPanels[i] = page; _scrolls[i] = scroll; scrollObject.SetActive(true);
            }
            MenuTrace.Write("Create: replacing tab controller arrays");
            controller.panels = newPanels;
            controller._currentlyShownPanel = null!;
            controller._currentlyOnToggle = 0;
            _panel._LastSelected_k__BackingField = null!;
            controller.events = new Il2CppSystem.Collections.Generic.List<UnityEvent>();
            for (var i = 0; i < 4; i++)
            {
                controller.events.Add(new UnityEvent());
                controller.toggles[i].onValueChanged = new Toggle.ToggleEvent();
                NativeUiParts.Relabel(controller.toggles[i].gameObject, new[] { "Mods", "Settings", "Controls", "Info" }[i]);
            }
            foreach (var page in oldPanels)
                if (page.Pointer != _controls.Pointer)
                {
                    MenuTrace.Write("Create: destroying replaced page " + page.name);
                    Object.DestroyImmediate(page.gameObject);
                }
            MenuTrace.Write("Create: configuring retained Controls section");
            _panel.settingsTabPanels = new Il2CppSystem.Collections.Generic.List<SettingsTabPanel>();
            _panel.settingsTabPanels.Add(_controls);
            _controls.transform.Find("ButtonsWrapper")?.gameObject.SetActive(false);
            // Prevent the original native restore/apply actions from touching game configuration.
            foreach (var button in new[] { _controls.applyButton, _controls.resetButton, _controls.restoreDefaultsButton })
                if (button != null) button.onClick = new Button.ButtonClickedEvent();
            _panel._startVisible = false;
            _panel.requiresMouse = _panel.pauseTimeWhenOpen = _panel.pauseTimeCompletely = true;
            // Native MenuControls/Pause includes O, which must not close Companion.
            // Escape and controller Back are handled by Cancel; F5 uses MenuAction.
            _panel.closeWithCancel = true;
            _panel.closeWithPause = false;
            _panel.firstSelected = controller.toggles[0].gameObject;
            var title = root.transform.Find("FrameWrapper/P_Element_TitleFrame/Title");
            foreach (var localized in title.GetComponents<LocalizedStaticUILabel>()) Object.DestroyImmediate(localized);
            title.GetComponent<TMP_Text>().text = "Mod Companion";
            Click(root.transform.Find("FrameWrapper/P_Element_CloseBtn/CloseBtn").GetComponent<Button>(), () => _panel.Hide());
            MenuTrace.Write("Create: removing inherited canvas managers");
            foreach (var manager in root.GetComponentsInChildren<CanvasBehaviourManager>(true)) Object.DestroyImmediate(manager);
            foreach (var canvas in root.GetComponentsInChildren<NestedCanvas>(true)) Object.DestroyImmediate(canvas);
            var canvasRoot = root.GetComponent<Canvas>() ?? root.AddComponent<Canvas>();
            canvasRoot.enabled = true; canvasRoot.overrideSorting = true; canvasRoot.sortingOrder = 200;
            var raycaster = root.GetComponent<GraphicRaycaster>() ?? root.AddComponent<GraphicRaycaster>(); raycaster.enabled = true;
            CreateShell(source);
            MenuTrace.Write("Create: building mod list");
            BuildModList();
            MenuTrace.Write("Create: populating initial mod");
            SelectMod(null);
            _revision = SettingsRegistry.Revision;
            MenuTrace.Write("Create: restoring presentation");
            RestorePresentation(root);
            root.transform.SetParent(source.transform.parent, false);
            MenuTrace.Write("Create: activating root and native Awake callbacks");
            root.SetActive(true);
            MenuTrace.Write("Create: root activated; hiding initial panel");
            _panel.Hide();
            MenuTrace.Write("Create: complete");
            Plugin.Logger.LogInfo("Mod Companion native settings window created, including Controls and rebinding panels.");
        }
        catch (Exception exception)
        {
            MenuTrace.Write("Create: managed failure " + exception);
            // Do not tear down an incompletely initialized native settings hierarchy
            // from this failure path. Quarantine one inactive tree and disable retries.
            _constructionFailed = true;
            _showFrame = -1;
            if (root != null)
            {
                root.SetActive(false);
                root.transform.SetParent(staging.transform, false);
            }
            Object.DontDestroyOnLoad(staging);
            _panel = null; _templates = staging; _refresh.Clear(); BindingLabel.Labels.Clear();
            MenuTrace.Write("Create: failed hierarchy retained inactive; menu disabled until restart");
            Plugin.Logger.LogError($"Mod Companion menu construction failed; see ModCompanion/menu-trace.log. Menu disabled until restart. {exception}");
        }
        finally { MenuTrace.Constructing = false; }
    }

    [HideFromIl2Cpp]
    private void Render(Transform parent, SettingDefinition setting)
    {
        if (setting.Kind == ControlKind.Text) { Text(parent, setting.Label); return; }
        MenuTrace.Write($"Render setting: {setting.Key} ({setting.Kind})");
        GameObject row;
        Action refresh;
        if (setting.Kind == ControlKind.Toggle)
        {
            row = Object.Instantiate(_toggleTemplate!, parent); row.SetActive(false);
            var native = row.GetComponent<ToggleSettingUI>(); var toggle = native.toggle;
            Object.DestroyImmediate(native); NativeUiParts.Relabel(row, setting.Label);
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<bool>>(new Action<bool>(value => Plugin.Guard("Change setting", () => setting.Write(value)))));
            refresh = () => toggle.SetIsOnWithoutNotify((bool)setting.Read()!);
        }
        else if (setting.Kind == ControlKind.Slider)
        {
            row = Object.Instantiate(_sliderTemplate!, parent); row.SetActive(false);
            var native = row.GetComponent<SliderSettingUI>(); var slider = native.slider; var valueText = native.valueText;
            Object.DestroyImmediate(native); NativeUiParts.Relabel(row, setting.Label);
            slider.minValue = setting.Minimum; slider.maxValue = setting.Maximum;
            slider.onValueChanged = new Slider.SliderEvent();
            slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(new Action<float>(value => Plugin.Guard("Change slider", () => setting.Write(value)))));
            refresh = () => { var value = (float)setting.Read()!; slider.SetValueWithoutNotify(value); valueText.text = value.ToString("0.##"); };
        }
        else if (setting.Kind == ControlKind.Button)
        {
            row = Button(parent, setting.Label, () => setting.Click!()); refresh = () => { };
        }
        else
        {
            void Adjust(int direction)
            {
                if (setting.Kind == ControlKind.Stepper)
                    setting.Write((int)Math.Clamp((long)(int)setting.Read()! + direction * (long)setting.IntegerStep, setting.IntegerMinimum, setting.IntegerMaximum));
                else
                {
                    var index = setting.Choices.ToList().FindIndex(c => Equals(c.Value, setting.Read()));
                    setting.Write(setting.Choices[(index + direction + setting.Choices.Count) % setting.Choices.Count].Value);
                }
            }
            var text = StepRow(parent, setting.Label, () => Adjust(-1), () => Adjust(1));
            row = text.transform.parent.gameObject;
            refresh = () => text.text = setting.Kind == ControlKind.Choice
                ? setting.Choices.First(c => Equals(c.Value, setting.Read())).Label : setting.Read()!.ToString();
        }
        // Old template navigation points to native rows that no longer exist.
        foreach (var nav in row.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        foreach (var autoScroll in row.GetComponentsInChildren<GamepadAutoScroll>(true)) Object.DestroyImmediate(autoScroll);
        RestorePresentation(row); row.SetActive(true);
        var note = Text(parent, "");
        note.fontSize = 15; note.fontStyle = FontStyles.Normal;
        note.color = new Color(.60f, .59f, .57f, 1);
        note.margin = new Vector4(8, 0, 8, 5);
        void Refresh()
        {
            refresh();
            var enabled = setting.EnabledWhen?.Invoke() ?? true;
            foreach (var selectable in row.GetComponentsInChildren<Selectable>(true)) selectable.interactable = enabled;
            note.text = string.Join("  ", new[] { setting.Hint ?? setting.Description, !enabled ? setting.DisabledReason : "", setting.PendingRestart ? "Restart required to apply this change." : setting.RequiresRestart ? "Requires restart." : "" }.Where(s => !string.IsNullOrWhiteSpace(s)));
            note.gameObject.SetActive(note.text.Length > 0);
            AlignHint(note, row.GetComponentInChildren<TMP_Text>(true));
        }
        _refresh.Add(Refresh); Refresh();
    }

    [HideFromIl2Cpp]
    private void BuildBindings(SettingsCategory category)
    {
        if (_controls == null) return;
        foreach (var old in _controls.controls) if (old != null) BindingLabel.Labels.Remove(old.Pointer);
        _controls.lastSelected = null!;
        _controls.controls.Clear(); Clear(_controls.scroll.content);
        foreach (var setting in category.Entries)
        {
            if (setting is not KeyBindingSetting entry) { Render(_controls.scroll.content, setting); continue; }
            AddBinding(entry);
        }
        _controls.firstSelected = _controls.controls.Count > 0 ? _controls.controls[0].gameObject : ActiveTab.gameObject;
        if (_panel!.gameObject.activeInHierarchy) _controls.ControllerConnected();
    }

    [HideFromIl2Cpp]
    private void AddBinding(KeyBindingSetting entry)
    {
        MenuTrace.Write("Binding row: " + entry.Key);
        var action = CompanionInput.Resolve(entry);
        if (action == null) { Text(_controls!.scroll.content, entry.Label + " — action not available"); return; }
        var clone = Object.Instantiate(_bindingTemplate!, _templates!.transform); clone.SetActive(false);
        clone.name = "ModCompanion.Binding." + entry.Key;
        var row = clone.GetComponent<InputRebindUI>();
        row.action = new InputDisplayUI.ActionDisplay
        {
            actionReference = InputActionReference.Create(action), compositeIndex = new[] { 0 },
            description = row.action.description, displayInputText = row.action.displayInputText
        };
        row.action.Init();
        BindingLabel.Labels[row.Pointer] = entry.Label;
        foreach (var nav in clone.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        foreach (var autoScroll in clone.GetComponentsInChildren<GamepadAutoScroll>(true)) Object.DestroyImmediate(autoScroll);
        _controls!.controls.Add(row);
        // Start wires initial rows. Later rebuilds need that subscription explicitly.
        if (_panel!.gameObject.activeInHierarchy)
            row.OnSelected += (Il2CppSystem.Action<GameObject>)(selected => _controls.Selected(selected));
        clone.transform.SetParent(_controls.scroll.content, false);
        var size = clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>();
        size.layoutPriority = 100; size.minHeight = size.preferredHeight = 44; size.flexibleHeight = 0;
        var group = clone.GetComponent<CanvasGroup>();
        if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
        RestorePresentation(clone); clone.SetActive(true);
        row.actionText.text = entry.Label;
        var hint = Text(_controls.scroll.content, entry.Hint ?? entry.Description);
        hint.fontSize = 15; hint.fontStyle = FontStyles.Normal; hint.color = new Color(.60f, .59f, .57f, 1);
        hint.gameObject.SetActive(hint.text.Length > 0);
        _refresh.Add(() =>
        {
            if (!CompanionInput.Rebinding) row.UpdateText();
            hint.text = entry.Hint ?? entry.Description;
            hint.gameObject.SetActive(hint.text.Length > 0);
            AlignHint(hint, row.actionText);
        });
    }

    [HideFromIl2Cpp]
    private TMP_Text StepRow(Transform parent, string label, Action left, Action right)
    {
        var row = Object.Instantiate(_stepTemplate!, parent); row.SetActive(false);
        Object.DestroyImmediate(row.GetComponent<ResolutionSettingUI>());
        NativeUiParts.Relabel(row, label);
        var leftButton = row.transform.Find("ButtonLeft").GetComponent<Button>();
        var rightButton = row.transform.Find("ButtonRight").GetComponent<Button>();
        Click(leftButton, left); Click(rightButton, right);
        // Native selector root handles left/right through its removed controller.
        // Replace that routing so controllers can adjust the whole selected row.
        var trigger = row.AddComponent<EventTrigger>();
        var move = new EventTrigger.Entry { eventID = EventTriggerType.Move };
        move.callback.AddListener(DelegateSupport.ConvertDelegate<UnityAction<BaseEventData>>(new Action<BaseEventData>(data =>
        {
            var axis = data.TryCast<AxisEventData>();
            if (axis == null || CompanionInput.Rebinding) return;
            if (axis.moveDir == MoveDirection.Left) { Plugin.Guard("Previous choice", left); data.Use(); }
            if (axis.moveDir == MoveDirection.Right) { Plugin.Guard("Next choice", right); data.Use(); }
        })));
        trigger.triggers = new Il2CppSystem.Collections.Generic.List<EventTrigger.Entry>();
        trigger.triggers.Add(move);
        RestorePresentation(row); row.SetActive(true);
        return row.transform.Find("ValueText").GetComponent<TMP_Text>();
    }
    [HideFromIl2Cpp]
    private GameObject Button(Transform parent, string label, Action click)
    {
        var row = Object.Instantiate(_buttonTemplate!, parent); row.SetActive(false);
        NativeUiParts.Relabel(row, label); Click(row.GetComponent<Button>(), click);
        foreach (var nav in row.GetComponentsInChildren<ManualUINavigation>(true)) Object.DestroyImmediate(nav);
        var size = row.GetComponent<LayoutElement>() ?? row.AddComponent<LayoutElement>();
        size.layoutPriority = 100; size.minHeight = size.preferredHeight = 38; size.flexibleHeight = 0;
        RestorePresentation(row); row.SetActive(true); return row;
    }
    [HideFromIl2Cpp]
    private TMP_Text Text(Transform parent, string value)
    {
        var text = NativeUiParts.Text(parent, _font!, value); text.fontSize = 18; text.fontStyle = FontStyles.Normal;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Overflow;
        text.margin = new Vector4(8, 8, 8, 8);
        var layout = text.gameObject.AddComponent<LayoutElement>(); layout.minHeight = 24; layout.flexibleHeight = 0;
        return text;
    }
    [HideFromIl2Cpp]
    private static void Clear(Transform parent)
    { for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject); }
    [HideFromIl2Cpp]
    private static void ConfigureLayout(Transform content)
    {
        var rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = Vector2.zero; rect.anchoredPosition = Vector2.zero;
        var fitter = content.GetComponent<ContentSizeFitter>() ?? content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = content.GetComponent<VerticalLayoutGroup>() ?? content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 18, 0, 0);
        layout.childControlWidth = layout.childForceExpandWidth = layout.childControlHeight = true;
        layout.childForceExpandHeight = false; layout.spacing = 8; layout.childAlignment = TextAnchor.UpperLeft;
    }
    [HideFromIl2Cpp]
    internal static void RestorePresentation(GameObject root)
    {
        foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true)) ConfigureScroll(scroll);
        foreach (var mask in root.GetComponentsInChildren<Mask>(true)) mask.enabled = true;
        foreach (var mask in root.GetComponentsInChildren<RectMask2D>(true)) mask.enabled = true;
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.enabled = true;
        // Native binding rows intentionally disable this placeholder sprite. The key
        // text (including controller glyphs) is a separate child and remains visible.
        foreach (var binding in root.GetComponentsInChildren<InputRebindUI>(true))
        {
            var placeholder = binding.transform.Find("Wrapper/Keyboard/KeyboardKey")?.GetComponent<Image>();
            if (placeholder != null) placeholder.enabled = false;
        }
        foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = true;
        foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = true;
        foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
        {
            selectable.enabled = true;
            var nav = selectable.navigation; nav.mode = Navigation.Mode.Automatic; selectable.navigation = nav;
        }
    }
    [HideFromIl2Cpp]
    private void KeepSelectionVisible()
    {
        if (CompanionInput.Rebinding) return;
        var selected = EventSystem.current?.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(_panel!.transform) || !selected.activeInHierarchy || selected.GetComponent<Selectable>()?.IsInteractable() == false)
        { ActiveTab.Select(); return; }
        if (selected == _lastSelection) return;
        _lastSelection = selected;
        foreach (var scroll in AllScrolls)
        {
            if (scroll == null || !selected.transform.IsChildOf(scroll.content)) continue;
            Canvas.ForceUpdateCanvases();
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            var viewport = scroll.viewport.rect;
            var offset = bounds.max.y > viewport.yMax ? viewport.yMax - bounds.max.y : bounds.min.y < viewport.yMin ? viewport.yMin - bounds.min.y : 0;
            var position = scroll.content.anchoredPosition;
            if (scroll.vertical) position.y += offset;
            if (scroll.horizontal)
                position.x += bounds.min.x < viewport.xMin ? viewport.xMin - bounds.min.x : bounds.max.x > viewport.xMax ? viewport.xMax - bounds.max.x : 0;
            scroll.content.anchoredPosition = position;
        }
    }
    public void OnDestroy()
    {
        foreach (var mod in SettingsRegistry.RegisteredMods) if (mod.IsDirty) mod.Save();
        if (_panel != null) Object.Destroy(_panel.gameObject);
        if (_templates != null && !_constructionFailed) Object.Destroy(_templates);
        foreach (var icon in _icons.Values) { Object.Destroy(icon.texture); Object.Destroy(icon); }
        BindingLabel.Labels.Clear(); Instance = null;
    }
}
