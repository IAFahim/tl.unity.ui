using System;
using System.Collections.Generic;
using System.Text;
using Tl;
using Tl.Ui;
using Tl.Unity;
using Unity.Collections;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TlUi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    [DefaultExecutionOrder(TlUiOrder.Panel)]
    [AddComponentMenu("Tl UI/Tl UI Panel")]
    [HelpURL("https://github.com/IAFahim/tl.unity.ui/tree/main/com.tl.unity.ui")]
    [AutoStaticsCleanup]
    public sealed partial class TlUiPanel : MonoBehaviour
    {
        sealed class ListState
        {
            public ListState(int list, UiListBinding binding, ITlUiRows rows, VisualElement target)
            {
                List = list;
                Binding = binding;
                Rows = rows;
                Target = target;
                View = target as ListView;
            }

            public readonly int List;
            public readonly UiListBinding Binding;
            public readonly ITlUiRows Rows;
            public readonly VisualElement Target;
            public readonly ListView View;
            public readonly List<VisualElement> RowElements = new List<VisualElement>();
            public long ShownCountVersion = -1;
            public int ShownCount;
            public EventCallback<NavigationSubmitEvent> OnSubmit;
            public Action<IEnumerable<int>> OnSelect;
            public Func<HandleDragAndDropArgs, DragVisualMode> OnDragUpdate;
            public Func<HandleDragAndDropArgs, DragVisualMode> OnDrop;
            public TemplateContainer DragGhost;
        }

        sealed class ElementClasses : IUiClassList
        {
            public VisualElement Element;

            public bool HasClass(string className) => Element.ClassListContains(className);
        }

        [Tooltip("The Panel Renderer this component drives. Filled in when the component is added next to it.")]
        public PanelRenderer Panel;

        [Header("Address")]
        [Tooltip("Optional: Ids by selector, for many elements at once (.card, Button). Clips usually name their target instead, by the element's Name in UI Builder.")]
        public List<UiTarget> Targets = new List<UiTarget>();

        [Tooltip("USS classes Add Class and Remove Class clips name by slot, 0 to 7.")]
        public List<string> ClassSlots = new List<string>();

        [Tooltip("UXML templates Attach clips put inside their target, by slot, 0 to 7. The template is created the first time its clip starts, then shown while a clip is on and hidden (display none) while none is, so playing again allocates nothing.")]
        public List<VisualTreeAsset> TemplateSlots = new List<VisualTreeAsset>();

        [Tooltip("Texts Text clips show, by slot.")]
        [TextArea]
        public List<string> TextSlots = new List<string>();

        [Header("Events")]
        [Tooltip("What happens when a Signal clip starts, by code.")]
        public List<UiSignalReaction> Signals = new List<UiSignalReaction>();

        [Header("Input")]
        [Tooltip("What clicks, pointer moves, value changes and hotkeys do: start or stop UI timelines, send codes to game systems, or invoke events.")]
        public List<UiCommand> Commands = new List<UiCommand>();

        [Header("Lists")]
        [Tooltip("Rows from an asset list or an ECS buffer, shown in a ListView or a container, with row clicks and drags sent to game systems.")]
        public List<UiListBinding> Lists = new List<UiListBinding>();

        [Header("Data")]
        [Tooltip("The data source of the whole screen: a ScriptableObject, or a Tl UI Data component that shows an ECS singleton. Bind fields to it in UI Builder.")]
        public UnityEngine.Object DataSource;

        static List<TlUiPanel> s_Enabled;

        static List<TlUiPanel> PanelsEnabledNow => s_Enabled ??= new List<TlUiPanel>(4);

        readonly List<UiElementState> _elements = new List<UiElementState>();
        readonly Dictionary<VisualElement, UiElementState> _stateOf = new Dictionary<VisualElement, UiElementState>();
        readonly List<VisualElement> _matches = new List<VisualElement>();
        readonly List<Action> _unregisterCommands = new List<Action>();
        readonly List<(InputAction Action, Action<InputAction.CallbackContext> OnPerformed)> _hotkeys = new List<(InputAction, Action<InputAction.CallbackContext>)>();
        readonly List<int> _drainedCodes = new List<int>(16);
        readonly List<ListState> _lists = new List<ListState>();
        readonly Dictionary<string, string[]> _revealPrefixes = new Dictionary<string, string[]>();
        readonly ElementClasses _classes = new ElementClasses();
        readonly HashSet<VisualTreeAsset> _templatesWarnedAbout = new HashSet<VisualTreeAsset>();
        World _boundWorld;
        EntityQuery _switchesQuery;
        EntityQuery _channelsQuery;
        EntityQuery _signalsQuery;
        EntityQuery _focusQuery;
        EntityQuery _hostsQuery;
        EntityQuery _hostsWithoutTimelineQuery;
        EntityQuery _targetNamesQuery;
        Entity _host;
        readonly List<string> _targetNames = new List<string>();
        bool _targetNamesRead;
        readonly List<string> _previewNames = new List<string>();
        UiSwitches _previewSwitches;
        UiChannels _previewChannels;
        bool _previewing;
        PanelRenderer.VersionedUIReloadCallback _onPreviewReloaded;
        UiSwitches _shownSwitches;
        UiChannels _shownChannels;
        PanelRenderer.VersionedUIReloadCallback _onUiReloaded;
        VisualElement _resolvedRoot;
        int _resolvedVersion = -1;
        bool _resolveRequested;
        bool _applyEverything = true;
        bool _someListTakesDrops;
        bool _listsWaitForTargetNames;
        UiInputState _shownInput;
        bool _inputReported;

        public World BoundWorld => _boundWorld;

        public IReadOnlyList<UiElementState> Elements => _elements;

        public VisualElement Root => _resolvedRoot;

        public IReadOnlyList<string> TargetNames => _targetNames;

        public bool IsPreviewing => _previewing;

        public UiSwitches ShownSwitches => _shownSwitches;

        public UiChannels ShownChannels => _shownChannels;

        public static IReadOnlyList<TlUiPanel> EnabledPanels => PanelsEnabledNow;

        public static string DumpEveryPanel()
        {
            if (PanelsEnabledNow.Count == 0)
                return "No Tl UI Panel is enabled. Enter Play mode in a scene with one.";
            var dump = new StringBuilder();
            foreach (var panel in PanelsEnabledNow)
                dump.Append("== ").Append(panel.name).Append(panel._resolvedRoot == null ? " (no UI loaded yet)" : "").Append('\n').Append(panel.Dump());
            return dump.ToString();
        }

        public static TlUiPanel Owning(VisualElement element)
        {
            for (var ancestor = element; ancestor != null; ancestor = ancestor.hierarchy.parent)
                foreach (var panel in PanelsEnabledNow)
                    if (panel._resolvedRoot == ancestor)
                        return panel;
            return null;
        }

        public static bool HasTargetComponentUnder(VisualElement subtree)
        {
            if (subtree.HasComponent<TlTarget>())
                return true;
            for (var child = 0; child < subtree.hierarchy.childCount; child++)
                if (HasTargetComponentUnder(subtree.hierarchy[child]))
                    return true;
            return false;
        }

        void WarnAboutTargetsInside(VisualTreeAsset template, VisualElement instance, string usedAs)
        {
            if (template == null || _templatesWarnedAbout.Contains(template) || !HasTargetComponentUnder(instance))
                return;
            _templatesWarnedAbout.Add(template);
            Debug.LogWarning("Tl UI Panel: " + template.name + " is used as " + usedAs + " and has a TlTarget component inside. Ids come only from the panel's own UXML, so that TlTarget does nothing; TlSend and TlPlay do work inside templates.", this);
        }

        public VisualElement FirstElementOf(int id)
        {
            var idBit = id >= 0 && id <= UiIds.HighestId ? UiIds.BitOf((byte)id) : 0UL;
            foreach (var state in _elements)
                if ((state.Ids & idBit) != 0)
                    return state.Element;
            return null;
        }

        public IList<VisualElement> RowElementsOf(int list) => list >= 0 && list < _lists.Count ? _lists[list].RowElements : null;

        public void Bind(World world)
        {
            _boundWorld = world;
            var entityManager = world.EntityManager;
            _switchesQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiSwitches>());
            _channelsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiChannels>());
            _signalsQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimelineEvent<UiSignal>>());
            _focusQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimelineEvent<UiFocus>>());
            _hostsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>(), ComponentType.ReadWrite<TimelineRequest>());
            _hostsWithoutTimelineQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>(), ComponentType.ReadWrite<UiCommandInbox>());
            _targetNamesQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTargetName>());
            _targetNamesRead = false;
            _host = Entity.Null;
            _applyEverything = true;
        }

        public void ResolveTargetsAgain() => _resolveRequested = true;

        bool BoundWorldIsUsable()
        {
            if (_boundWorld != null && _boundWorld.IsCreated)
                return true;
            var defaultWorld = World.DefaultGameObjectInjectionWorld;
            if (defaultWorld == null || !defaultWorld.IsCreated)
                return false;
            Bind(defaultWorld);
            return true;
        }

        bool Matches(in UiSelectorStep step, VisualElement element)
        {
            _classes.Element = element;
            return step.Matches(element.GetType().Name, element.name ?? "", _classes);
        }

        bool MatchesWithAncestors(VisualElement root, VisualElement element, UiSelectorStep[] steps)
        {
            if (!Matches(steps[steps.Length - 1], element))
                return false;
            var step = steps.Length - 2;
            for (var ancestor = element.hierarchy.parent; step >= 0 && ancestor != null && ancestor != root.hierarchy.parent; ancestor = ancestor.hierarchy.parent)
                if (Matches(steps[step], ancestor))
                    step--;
            return step < 0;
        }

        void CollectMatchesUnder(VisualElement root, VisualElement parent, UiSelectorStep[] steps)
        {
            for (var child = 0; child < parent.hierarchy.childCount; child++)
            {
                var element = parent.hierarchy[child];
                if (MatchesWithAncestors(root, element, steps))
                    _matches.Add(element);
                CollectMatchesUnder(root, element, steps);
            }
        }

        void AddToId(VisualElement element, int id)
        {
            if (id < 0 || id > UiIds.HighestId)
            {
                Debug.LogWarning("Tl UI Panel: Id " + id + " on " + element.GetType().Name + " #" + element.name + " is outside 0 to " + UiIds.HighestId + ".", this);
                return;
            }
            if (!_stateOf.TryGetValue(element, out var state))
            {
                state = CaptureAuthoredState(element);
                _stateOf.Add(element, state);
                _elements.Add(state);
                if (element.HasBinding("style.display") || element.HasBinding("enabledSelf"))
                    Debug.LogWarning("Tl UI Panel: " + element.GetType().Name + " #" + element.name + " has an Id and a data binding on style.display or enabledSelf. Show, Hide, Enable and Disable clips write the same property, so whichever writes last wins.", this);
            }
            state.Ids |= UiIds.BitOf((byte)id);
        }

        void CollectNamedUnder(VisualElement parent, string name, int id)
        {
            for (var child = 0; child < parent.hierarchy.childCount; child++)
            {
                var element = parent.hierarchy[child];
                if (element.name == name)
                    AddToId(element, id);
                CollectNamedUnder(element, name, id);
            }
        }

        void ResolveTargets(VisualElement root, IReadOnlyList<string> targetNames)
        {
            foreach (var state in _elements)
                RestoreAuthoredState(state);
            _elements.Clear();
            _stateOf.Clear();
            foreach (var target in Targets)
            {
                var steps = UiSelector.Parse(target.Selector);
                if (steps.Length == 0 || target.Id < 0 || target.Id > UiIds.HighestId)
                    continue;
                _matches.Clear();
                CollectMatchesUnder(root, root, steps);
                foreach (var element in _matches)
                    AddToId(element, target.Id);
            }
            for (var id = 0; id < targetNames.Count && id <= UiIds.HighestId; id++)
                if (!string.IsNullOrEmpty(targetNames[id]))
                    CollectNamedUnder(root, targetNames[id], id);
            CollectAuthoredTargetsUnder(root);
        }

        void ReadTargetNames()
        {
            _targetNamesQuery.CompleteDependency();
            using var hosts = _targetNamesQuery.ToEntityArray(Allocator.Temp);
            var names = _boundWorld.EntityManager.GetBuffer<UiTargetName>(hosts[0], true);
            _targetNames.Clear();
            for (var id = 0; id < names.Length; id++)
                _targetNames.Add(names[id].Name.ToString());
            _targetNamesRead = true;
            if (_targetNames.Count > 0 || _listsWaitForTargetNames)
                _resolveRequested = true;
            _listsWaitForTargetNames = false;
        }

        public void ShowPreview(IReadOnlyList<string> targetNames, in UiSwitches switches, in UiChannels channels)
        {
            if (Application.isPlaying)
                return;
            if (Panel == null)
                Panel = GetComponent<PanelRenderer>();
            if (Panel == null)
                return;
            var namesChanged = targetNames.Count != _previewNames.Count;
            for (var id = 0; !namesChanged && id < targetNames.Count; id++)
                namesChanged = targetNames[id] != _previewNames[id];
            if (namesChanged)
            {
                _previewNames.Clear();
                _previewNames.AddRange(targetNames);
            }
            _previewSwitches = switches;
            _previewChannels = channels;
            if (!_previewing)
            {
                _previewing = true;
                _onPreviewReloaded ??= OnPreviewReloaded;
                Panel.RegisterUIReloadCallback(_onPreviewReloaded);
                return;
            }
            if (_resolvedRoot == null)
                return;
            if (namesChanged)
            {
                ResolveTargets(_resolvedRoot, _previewNames);
                _applyEverything = true;
            }
            ApplyPreview();
        }

        public void StopPreview()
        {
            if (!_previewing)
                return;
            _previewing = false;
            if (Panel != null && _onPreviewReloaded != null)
                Panel.UnregisterUIReloadCallback(_onPreviewReloaded);
            foreach (var state in _elements)
                RestoreAuthoredState(state);
            _elements.Clear();
            _stateOf.Clear();
            _resolvedRoot = null;
            _shownSwitches = default;
            _shownChannels = default;
            _applyEverything = true;
        }

        void OnPreviewReloaded(PanelRenderer panel, VisualElement root, int version)
        {
            if (!_previewing || Application.isPlaying)
                return;
            ResolveTargets(root, _previewNames);
            _resolvedRoot = root;
            _applyEverything = true;
            ApplyPreview();
        }

        void ApplyPreview()
        {
            ApplySwitches(_previewSwitches);
            ApplyChannels(_previewChannels);
            _applyEverything = false;
        }

        void CollectAuthoredTargetsUnder(VisualElement parent)
        {
            for (var child = 0; child < parent.hierarchy.childCount; child++)
            {
                var element = parent.hierarchy[child];
                if (element.HasComponent<TlTarget>())
                    AddToId(element, element.GetComponent<TlTarget>().Id);
                CollectAuthoredTargetsUnder(element);
            }
        }

        UiElementState CaptureAuthoredState(VisualElement element)
        {
            var state = new UiElementState(element)
            {
                AuthoredEnabled = element.enabledSelf,
                AuthoredText = element is TextElement text ? text.text : null,
                AuthoredValue = element is INotifyValueChanged<float> valued ? valued.value : 0f,
            };
            for (var slot = 0; slot < ClassSlots.Count && slot < UiIds.SlotCount; slot++)
                if (!string.IsNullOrEmpty(ClassSlots[slot]) && element.ClassListContains(ClassSlots[slot]))
                    state.AuthoredClasses |= (byte)(1 << slot);
            return state;
        }

        void RestoreAuthoredState(UiElementState state)
        {
            ApplyLook(state, default);
            ApplyChannelLook(state, default);
            foreach (var attached in state.Attached)
                attached?.RemoveFromHierarchy();
        }

        string PrefixOf(string text, int length)
        {
            if (length >= text.Length)
                return text;
            if (!_revealPrefixes.TryGetValue(text, out var prefixes))
                _revealPrefixes[text] = prefixes = new string[text.Length];
            return prefixes[length] ??= text.Substring(0, length);
        }

        string ShownText(UiElementState state, in UiChannelLook look)
        {
            var slot = look.TextSlot;
            var text = look.HasText && slot >= 0 && slot < TextSlots.Count ? TextSlots[slot] : state.AuthoredText;
            if (text == null || !look.Has(UiProperty.Reveal))
                return text;
            return PrefixOf(text, UiRules.RevealedLength(text.Length, look.Reveal));
        }

        void ShowOrHideAttachedTemplate(UiElementState state, int slot, bool attached)
        {
            var instance = state.Attached[slot];
            if (attached && instance == null && TemplateSlots[slot] != null)
            {
                instance = state.Attached[slot] = TemplateSlots[slot].Instantiate();
                WarnAboutTargetsInside(TemplateSlots[slot], instance, "an Attach template");
            }
            if (instance == null)
                return;
            if (attached && instance.hierarchy.parent != state.Element)
                state.Element.Add(instance);
            instance.style.display = attached ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        void ApplyLook(UiElementState state, in UiLook look)
        {
            var element = state.Element;
            element.style.display = look.Display == UiDisplay.Hidden ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : look.Display == UiDisplay.Shown ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                : new StyleEnum<DisplayStyle>(StyleKeyword.Null);
            element.style.visibility = look.Visibility == UiVisibility.Invisible ? new StyleEnum<Visibility>(Visibility.Hidden) : new StyleEnum<Visibility>(StyleKeyword.Null);
            element.SetEnabled(look.IsUsable(state.AuthoredEnabled));
            for (var slot = 0; slot < ClassSlots.Count && slot < UiIds.SlotCount; slot++)
                if (!string.IsNullOrEmpty(ClassSlots[slot]))
                    element.EnableInClassList(ClassSlots[slot], look.ClassIsOn(slot, (state.AuthoredClasses & (1 << slot)) != 0));
            for (var slot = 0; slot < TemplateSlots.Count && slot < UiIds.SlotCount; slot++)
                ShowOrHideAttachedTemplate(state, slot, look.TemplateIsAttached(slot));
            state.ShownLook = look;
        }

        void ApplyChannelLook(UiElementState state, in UiChannelLook look)
        {
            var element = state.Element;
            var shown = state.ShownChannels;
            if (look.Has(UiProperty.Opacity) != shown.Has(UiProperty.Opacity) || look.Opacity != shown.Opacity)
                element.style.opacity = look.Has(UiProperty.Opacity) ? new StyleFloat(look.Opacity) : new StyleFloat(StyleKeyword.Null);
            var translated = look.Has(UiProperty.MoveX) || look.Has(UiProperty.MoveY);
            var wasTranslated = shown.Has(UiProperty.MoveX) || shown.Has(UiProperty.MoveY);
            if (translated != wasTranslated || look.TranslateX != shown.TranslateX || look.TranslateY != shown.TranslateY)
                element.style.translate = translated ? new StyleTranslate(new Translate(new Length(look.TranslateX), new Length(look.TranslateY))) : new StyleTranslate(StyleKeyword.Null);
            if (look.Has(UiProperty.Scale) != shown.Has(UiProperty.Scale) || look.Scale != shown.Scale)
                element.style.scale = look.Has(UiProperty.Scale) ? new StyleScale(new Scale(new Vector2(look.Scale, look.Scale))) : new StyleScale(StyleKeyword.Null);
            if (look.Has(UiProperty.Rotate) != shown.Has(UiProperty.Rotate) || look.Rotate != shown.Rotate)
                element.style.rotate = look.Has(UiProperty.Rotate) ? new StyleRotate(new Rotate(new Angle(look.Rotate, AngleUnit.Degree))) : new StyleRotate(StyleKeyword.Null);
            if (look.Has(UiProperty.WidthPercent) != shown.Has(UiProperty.WidthPercent) || look.Width != shown.Width)
                element.style.width = look.Has(UiProperty.WidthPercent) ? new StyleLength(Length.Percent(look.Width)) : new StyleLength(StyleKeyword.Null);
            if (look.Has(UiProperty.HeightPercent) != shown.Has(UiProperty.HeightPercent) || look.Height != shown.Height)
                element.style.height = look.Has(UiProperty.HeightPercent) ? new StyleLength(Length.Percent(look.Height)) : new StyleLength(StyleKeyword.Null);
            if (element is TextElement textElement && (look.HasText || look.Has(UiProperty.Reveal) || shown.HasText || shown.Has(UiProperty.Reveal)))
            {
                var text = ShownText(state, look);
                if (!ReferenceEquals(textElement.text, text))
                    textElement.text = text;
            }
            if (element is INotifyValueChanged<float> valued && (look.Has(UiProperty.Value) != shown.Has(UiProperty.Value) || look.Value != shown.Value))
                valued.SetValueWithoutNotify(look.Has(UiProperty.Value) ? look.Value : state.AuthoredValue);
            state.ShownChannels = look;
        }

        public void SendToGame(int code, int payload, int target) => SendToGame(code, payload, target, -1);

        public void SendToGame(int code, int payload, int target, int fromList)
        {
            if (!TryFindHost(out var host))
                return;
            _boundWorld.EntityManager.GetBuffer<UiCommandInbox>(host).Add(new UiCommandInbox { Code = code, Payload = payload, Target = target, FromList = fromList });
        }

        public bool SomeoneIsTyping()
        {
            var focused = _resolvedRoot?.focusController?.focusedElement as VisualElement;
            return focused != null && (focused is TextField || focused.GetFirstAncestorOfType<TextField>() != null);
        }

        public void SendText(int code, string text)
        {
            if (!TryFindHost(out var host) || !_boundWorld.EntityManager.HasBuffer<UiTextInbox>(host))
                return;
            var fixedText = default(FixedString128Bytes);
            fixedText.CopyFromTruncated(text ?? "");
            _boundWorld.EntityManager.GetBuffer<UiTextInbox>(host).Add(new UiTextInbox { Code = code, Text = fixedText });
        }

        bool PointerIsOverUi()
        {
            var pointer = Pointer.current;
            var panel = _resolvedRoot?.panel;
            if (pointer == null || panel == null)
                return false;
            var screen = pointer.position.ReadValue();
            var picked = panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y)));
            return picked != null && picked != _resolvedRoot && _resolvedRoot.Contains(picked);
        }

        void ReportInputStateFromLastLayout()
        {
            var latest = new UiInputState { PointerOverUi = PointerIsOverUi(), Typing = SomeoneIsTyping() };
            if (latest.PointerOverUi == _shownInput.PointerOverUi && latest.Typing == _shownInput.Typing && _inputReported)
                return;
            if (!TryFindHostQuietly(out var host) || !_boundWorld.EntityManager.HasComponent<UiInputState>(host))
                return;
            _boundWorld.EntityManager.SetComponentData(host, latest);
            _shownInput = latest;
            _inputReported = true;
        }

        public void SendRowClick(int list, int row)
        {
            if (list >= 0 && list < _lists.Count && _lists[list].Binding.ClickCode >= 0)
                SendToGame(_lists[list].Binding.ClickCode, row, -1);
        }

        public void SendRowSubmit(int list, int row)
        {
            if (list >= 0 && list < _lists.Count && _lists[list].View == null)
                SendRowClick(list, row);
        }

        public void SendRowDrag(int list, int fromRow, int toRow)
        {
            if (ReordersRows(list))
                SendToGame(_lists[list].Binding.DragCode, fromRow, toRow, list);
        }

        public void SendRowDrop(int fromList, int fromRow, int toList, int toRow)
        {
            if (TakesDrops(toList))
                SendToGame(_lists[toList].Binding.DropCode, fromRow, toRow, fromList);
        }

        public bool ReordersRows(int list) => list >= 0 && list < _lists.Count && _lists[list].Binding.DragCode >= 0;

        public bool TakesDrops(int list) => list >= 0 && list < _lists.Count && _lists[list].Binding.DropCode >= 0 && _lists[list].Target != null && _lists[list].View == null;

        public VisualElement DragGhostOf(VisualElement row)
        {
            var list = row.HasComponent<TlRow>() ? row.GetComponent<TlRow>().List : -1;
            if (list < 0 || list >= _lists.Count || _lists[list].Binding.RowTemplate == null || _resolvedRoot == null)
                return null;
            var state = _lists[list];
            if (state.DragGhost == null)
            {
                state.DragGhost = state.Binding.RowTemplate.Instantiate();
                state.DragGhost.name = "tl-drag-ghost";
                state.DragGhost.AddToClassList(TlRowDrag.GhostClass);
                state.DragGhost.pickingMode = PickingMode.Ignore;
                state.DragGhost.style.position = Position.Absolute;
                state.DragGhost.style.display = DisplayStyle.None;
            }
            if (state.DragGhost.hierarchy.parent != _resolvedRoot)
                _resolvedRoot.Add(state.DragGhost);
            else
                state.DragGhost.BringToFront();
            state.DragGhost.dataSource = row.dataSource;
            state.DragGhost.dataSourcePath = row.dataSourcePath;
            return state.DragGhost;
        }

        public int ListHolding(VisualElement element)
        {
            for (var list = 0; list < _lists.Count; list++)
                if (_lists[list].Target != null && (_lists[list].Target == element || _lists[list].Target.contentContainer == element))
                    return list;
            return -1;
        }

        bool TryFindHostQuietly(out Entity host)
        {
            var entities = _boundWorld.EntityManager;
            if (_host != Entity.Null && entities.Exists(_host) && entities.HasBuffer<UiTimelineSlot>(_host))
            {
                host = _host;
                return true;
            }
            host = Entity.Null;
            var hostsQuery = _hostsQuery.IsEmptyIgnoreFilter ? _hostsWithoutTimelineQuery : _hostsQuery;
            if (hostsQuery.IsEmptyIgnoreFilter)
                return false;
            hostsQuery.CompleteDependency();
            using var hosts = hostsQuery.ToEntityArray(Allocator.Temp);
            host = _host = hosts[0];
            return true;
        }

        bool TryFindHost(out Entity host)
        {
            if (BoundWorldIsUsable() && TryFindHostQuietly(out host))
                return true;
            host = Entity.Null;
            Debug.LogWarning("Tl UI Panel: no object in the SubScene has a Tl UI Timelines component, so commands have nowhere to go.", this);
            return false;
        }

        public void RequestTimeline(UiTimelineVerb verb, int slot)
        {
            if (!TryFindHost(out var host))
                return;
            var entityManager = _boundWorld.EntityManager;
            var slots = entityManager.GetBuffer<UiTimelineSlot>(host, true);
            if (slot < 0 || slot >= slots.Length)
            {
                Debug.LogWarning("Tl UI Panel: timeline slot " + slot + " does not exist; the UI Sequence director has " + slots.Length + " (its Playable and Also Plays).", this);
                return;
            }
            if (!entityManager.HasBuffer<TimelineRequest>(host))
            {
                Debug.LogWarning("Tl UI Panel: timeline slot " + slot + " cannot play because the Playable Director next to Tl UI Timelines has no timeline. Assign a Tl timeline as its Playable.", this);
                return;
            }
            var timeline = slots[slot].Timeline;
            var requests = entityManager.GetBuffer<TimelineRequest>(host);
            switch (verb)
            {
                case UiTimelineVerb.Play:
                    requests.Start(timeline);
                    break;
                case UiTimelineVerb.Stop:
                    requests.Stop(timeline);
                    break;
                case UiTimelineVerb.Pause:
                    requests.Pause(timeline);
                    break;
                case UiTimelineVerb.Resume:
                    requests.Resume(timeline);
                    break;
                case UiTimelineVerb.Restart:
                    requests.Stop(timeline);
                    requests.Start(timeline);
                    break;
            }
        }

        void React(UiSignalReaction reaction)
        {
            switch (reaction.Do)
            {
                case UiCommandAction.InvokeEvent:
                    break;
                case UiCommandAction.SendToGame:
                    SendToGame(reaction.GameCode, reaction.Code, -1);
                    break;
                default:
                    RequestTimeline(VerbOf(reaction.Do), reaction.TimelineSlot);
                    break;
            }
            reaction.OnSignal?.Invoke();
        }

        static UiTimelineVerb VerbOf(UiCommandAction action) => action switch
        {
            UiCommandAction.StopTimeline => UiTimelineVerb.Stop,
            UiCommandAction.PauseTimeline => UiTimelineVerb.Pause,
            UiCommandAction.ResumeTimeline => UiTimelineVerb.Resume,
            UiCommandAction.RestartTimeline => UiTimelineVerb.Restart,
            _ => UiTimelineVerb.Play,
        };

        void Run(UiCommand command, int payload)
        {
            switch (command.Do)
            {
                case UiCommandAction.InvokeEvent:
                    command.OnInvoke?.Invoke();
                    break;
                case UiCommandAction.SendToGame:
                    SendToGame(command.GameCode, payload, -1);
                    break;
                default:
                    RequestTimeline(VerbOf(command.Do), command.TimelineSlot);
                    break;
            }
        }

        void RegisterCommand(UiCommand command, VisualElement element)
        {
            switch (command.On)
            {
                case UiCommandEvent.Click:
                    EventCallback<ClickEvent> onClick = _ => Run(command, command.GamePayload);
                    EventCallback<NavigationSubmitEvent> onSubmit = _ => Run(command, command.GamePayload);
                    element.RegisterCallback(onClick);
                    element.RegisterCallback(onSubmit);
                    _unregisterCommands.Add(() =>
                    {
                        element.UnregisterCallback(onClick);
                        element.UnregisterCallback(onSubmit);
                    });
                    break;
                case UiCommandEvent.PointerEnter:
                    EventCallback<PointerEnterEvent> onEnter = _ => Run(command, command.GamePayload);
                    element.RegisterCallback(onEnter);
                    _unregisterCommands.Add(() => element.UnregisterCallback(onEnter));
                    break;
                case UiCommandEvent.PointerLeave:
                    EventCallback<PointerLeaveEvent> onLeave = _ => Run(command, command.GamePayload);
                    element.RegisterCallback(onLeave);
                    _unregisterCommands.Add(() => element.UnregisterCallback(onLeave));
                    break;
                case UiCommandEvent.ValueChanged:
                    EventCallback<ChangeEvent<float>> onFloat = change => Run(command, Mathf.RoundToInt(change.newValue));
                    EventCallback<ChangeEvent<int>> onInt = change => Run(command, change.newValue);
                    EventCallback<ChangeEvent<bool>> onBool = change => Run(command, change.newValue ? 1 : 0);
                    element.RegisterCallback(onFloat);
                    element.RegisterCallback(onInt);
                    element.RegisterCallback(onBool);
                    _unregisterCommands.Add(() =>
                    {
                        element.UnregisterCallback(onFloat);
                        element.UnregisterCallback(onInt);
                        element.UnregisterCallback(onBool);
                    });
                    break;
                case UiCommandEvent.Cancel:
                    EventCallback<NavigationCancelEvent> onCancel = cancel =>
                    {
                        Run(command, command.GamePayload);
                        cancel.StopPropagation();
                    };
                    element.RegisterCallback(onCancel);
                    _unregisterCommands.Add(() => element.UnregisterCallback(onCancel));
                    break;
            }
        }

        VisualElement MakeRow(ListState list)
        {
            var row = list.Binding.RowTemplate.Instantiate();
            WarnAboutTargetsInside(list.Binding.RowTemplate, row, "a row template");
            row.AddComponent(new TlRow { List = list.List, Index = -1 });
            if (list.View != null)
                return row;
            row.focusable = true;
            if (list.Binding.DragCode >= 0 || _someListTakesDrops)
                row.AddComponent(new TlRowDrag());
            return row;
        }

        static void BindRow(ListState list, VisualElement row, int index)
        {
            row.GetComponent<TlRow>().Index = index;
            row.dataSource = list.Rows.Items[index];
            row.dataSourcePath = list.Rows.PathToRow;
        }

        void ChooseTheSelectedRow(ListState list)
        {
            var row = list.View.selectedIndex;
            if (row >= 0)
                SendToGame(list.Binding.ClickCode, row, -1);
        }

        DragVisualMode DropTheSelectedRow(ListState list, in HandleDragAndDropArgs drop)
        {
            var from = list.View.selectedIndex;
            var to = drop.dropPosition == DragAndDropPosition.OverItem || drop.insertAtIndex <= from ? drop.insertAtIndex : drop.insertAtIndex - 1;
            to = Mathf.Clamp(to, 0, Mathf.Max(0, list.Rows.Items.Count - 1));
            if (from >= 0 && to != from)
                SendToGame(list.Binding.DragCode, from, to, list.List);
            return DragVisualMode.Move;
        }

        void HookUpTheListView(ListState list)
        {
            var view = list.View;
            view.selectionType = SelectionType.Single;
            if (list.Binding.ClickCode >= 0)
            {
                list.OnSubmit = _ => ChooseTheSelectedRow(list);
                view.RegisterCallback(list.OnSubmit);
            }
            if (list.Binding.SelectCode >= 0)
            {
                list.OnSelect = _ => SendToGame(list.Binding.SelectCode, view.selectedIndex, -1);
                view.selectedIndicesChanged += list.OnSelect;
            }
            if (list.Binding.DragCode >= 0)
            {
                view.reorderable = true;
                list.OnDragUpdate = _ => DragVisualMode.Move;
                list.OnDrop = drop => DropTheSelectedRow(list, drop);
                view.dragAndDropUpdate += list.OnDragUpdate;
                view.handleDrop += list.OnDrop;
            }
        }

        static void UnhookTheListView(ListState list)
        {
            var view = list.View;
            if (list.OnSubmit != null)
                view.UnregisterCallback(list.OnSubmit);
            if (list.OnSelect != null)
                view.selectedIndicesChanged -= list.OnSelect;
            if (list.OnDragUpdate != null)
                view.dragAndDropUpdate -= list.OnDragUpdate;
            if (list.OnDrop != null)
                view.handleDrop -= list.OnDrop;
            view.itemsSource = null;
        }

        void ForgetLists()
        {
            foreach (var list in _lists)
            {
                if (list.View != null)
                {
                    UnhookTheListView(list);
                    continue;
                }
                foreach (var row in list.RowElements)
                    row.RemoveFromHierarchy();
                list.DragGhost?.RemoveFromHierarchy();
            }
            _lists.Clear();
        }

        void ResolveLists()
        {
            ForgetLists();
            _someListTakesDrops = false;
            foreach (var binding in Lists)
                _someListTakesDrops |= binding.DropCode >= 0;
            for (var index = 0; index < Lists.Count; index++)
            {
                var binding = Lists[index];
                var target = FirstElementOf(binding.TargetId);
                if (!(binding.Rows is ITlUiRows rows) || binding.RowTemplate == null || target == null)
                {
                    if (binding.Rows is ITlUiRows && binding.RowTemplate != null && !_targetNamesRead)
                    {
                        _listsWaitForTargetNames = true;
                        _lists.Add(new ListState(index, binding, null, null));
                        continue;
                    }
                    Debug.LogWarning("Tl UI Panel: list " + index + " needs Rows (Tl UI Asset Rows or a Tl UI Buffer Rows component), a Row Template and a Target Id that matches an element.", this);
                    _lists.Add(new ListState(index, binding, null, null));
                    continue;
                }
                var list = new ListState(index, binding, rows, target);
                if (list.View != null)
                {
                    list.View.makeItem = () => MakeRow(list);
                    list.View.bindItem = (row, rowIndex) => BindRow(list, row, rowIndex);
                    list.View.itemsSource = rows.Items;
                    list.ShownCountVersion = rows.CountVersion;
                    HookUpTheListView(list);
                }
                _lists.Add(list);
            }
        }

        void UpdateLists()
        {
            foreach (var list in _lists)
            {
                if (list.Rows == null)
                    continue;
                var countVersion = list.Rows.CountVersion;
                if (list.View != null)
                {
                    if (countVersion != list.ShownCountVersion)
                    {
                        list.View.RefreshItems();
                        list.ShownCountVersion = countVersion;
                    }
                    continue;
                }
                if (countVersion == list.ShownCountVersion)
                    continue;
                var count = list.Rows.Items.Count;
                while (list.RowElements.Count < count)
                {
                    var row = MakeRow(list);
                    list.Target.Add(row);
                    list.RowElements.Add(row);
                }
                for (var index = 0; index < list.RowElements.Count; index++)
                {
                    var row = list.RowElements[index];
                    var visible = index < count;
                    row.style.display = visible ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    if (visible)
                        BindRow(list, row, index);
                }
                list.ShownCount = count;
                list.ShownCountVersion = countVersion;
            }
        }

        void Resolve(VisualElement root)
        {
            foreach (var unregister in _unregisterCommands)
                unregister();
            _unregisterCommands.Clear();
            ResolveTargets(root, _targetNames);
            foreach (var command in Commands)
                foreach (var state in _elements)
                    if (command.On != UiCommandEvent.HotkeyOnly && (state.Ids & UiIds.BitOf((byte)command.TargetId)) != 0)
                        RegisterCommand(command, state.Element);
            if (DataSource is ITlUiDataSource dataBridge)
            {
                root.dataSource = dataBridge.Source;
                root.dataSourcePath = dataBridge.PathToData;
            }
            else if (DataSource != null)
            {
                root.dataSource = DataSource;
            }
            ResolveLists();
            _resolvedRoot = root;
            _resolveRequested = false;
            _applyEverything = true;
        }

        UiSwitches ReadSwitchesOfEveryHost()
        {
            var total = default(UiSwitches);
            if (_switchesQuery.IsEmptyIgnoreFilter)
                return total;
            _switchesQuery.CompleteDependency();
            using var columns = _switchesQuery.ToComponentDataArray<UiSwitches>(Allocator.Temp);
            for (var host = 0; host < columns.Length; host++)
                UiIds.Merge(ref total, columns[host]);
            return total;
        }

        UiChannels ReadChannelsOfEveryHost()
        {
            var total = default(UiChannels);
            if (_channelsQuery.IsEmptyIgnoreFilter)
                return total;
            _channelsQuery.CompleteDependency();
            using var columns = _channelsQuery.ToComponentDataArray<UiChannels>(Allocator.Temp);
            for (var host = 0; host < columns.Length; host++)
                UiRules.MergeChannels(ref total, columns[host]);
            return total;
        }

        void ApplySwitches(in UiSwitches latest)
        {
            var touchedIds = _applyEverything ? ulong.MaxValue : UiRules.TouchedIds(_shownSwitches, latest);
            if (touchedIds == 0)
                return;
            foreach (var state in _elements)
            {
                if ((state.Ids & touchedIds) == 0)
                    continue;
                var look = UiRules.LookOf(latest, state.Ids);
                if (_applyEverything || !look.Equals(state.ShownLook))
                    ApplyLook(state, look);
            }
            _shownSwitches = latest;
        }

        static UiChannelLook ChannelLookOf(UiElementState state, in UiChannels channels)
        {
            var look = default(UiChannelLook);
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                if ((channels.Live & (1 << slot)) == 0)
                    continue;
                var key = channels.Keys.Get(slot);
                if ((state.Ids & UiIds.BitOf(UiChannelKey.TargetOf(key))) != 0)
                    look.Set(UiChannelKey.PropertyOf(key), channels.Values.Get(slot));
            }
            return look;
        }

        void ApplyChannels(in UiChannels latest)
        {
            var touchedIds = _applyEverything ? ulong.MaxValue : UiRules.TouchedTargetsOfChannels(_shownChannels, latest);
            if (touchedIds == 0)
                return;
            foreach (var state in _elements)
            {
                if ((state.Ids & touchedIds) == 0)
                    continue;
                var look = ChannelLookOf(state, latest);
                if (_applyEverything || !look.Equals(state.ShownChannels))
                    ApplyChannelLook(state, look);
            }
            _shownChannels = latest;
        }

        void DrainSignals()
        {
            if (_signalsQuery.IsEmptyIgnoreFilter)
                return;
            _drainedCodes.Clear();
            var entityManager = _boundWorld.EntityManager;
            _signalsQuery.CompleteDependency();
            using (var hosts = _signalsQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var host in hosts)
                {
                    var signals = entityManager.GetBuffer<TimelineEvent<UiSignal>>(host);
                    for (var i = 0; i < signals.Length; i++)
                        _drainedCodes.Add(signals[i].Record.Value.Code);
                    signals.Clear();
                }
            }
            foreach (var code in _drainedCodes)
                foreach (var reaction in Signals)
                    if (reaction.Code == code)
                        React(reaction);
        }

        void DrainFocus()
        {
            if (_focusQuery.IsEmptyIgnoreFilter)
                return;
            _drainedCodes.Clear();
            var entityManager = _boundWorld.EntityManager;
            _focusQuery.CompleteDependency();
            using (var hosts = _focusQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (var host in hosts)
                {
                    var focus = entityManager.GetBuffer<TimelineEvent<UiFocus>>(host);
                    for (var i = 0; i < focus.Length; i++)
                        _drainedCodes.Add(focus[i].Record.Value.Id);
                    focus.Clear();
                }
            }
            foreach (var id in _drainedCodes)
                foreach (var state in _elements)
                    if ((state.Ids & UiIds.BitOf((byte)id)) != 0)
                    {
                        state.Element.Focus();
                        break;
                    }
        }

        void ListenToHotkeys()
        {
            foreach (var command in Commands)
            {
                var action = command.Hotkey != null ? command.Hotkey.action : null;
                if (action == null)
                    continue;
                Action<InputAction.CallbackContext> onPerformed = _ =>
                {
                    if (!SomeoneIsTyping())
                        Run(command, command.GamePayload);
                };
                action.performed += onPerformed;
                action.Enable();
                _hotkeys.Add((action, onPerformed));
            }
        }

        void StopListeningToHotkeys()
        {
            foreach (var (action, onPerformed) in _hotkeys)
                action.performed -= onPerformed;
            _hotkeys.Clear();
        }

        public string Dump()
        {
            var dump = new StringBuilder();
            dump.Append("switches show ").Append(_shownSwitches.Show).Append(" hide ").Append(_shownSwitches.Hide)
                .Append(" enable ").Append(_shownSwitches.Enable).Append(" disable ").Append(_shownSwitches.Disable)
                .Append(" | animating");
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                if ((_shownChannels.Live & (1 << slot)) == 0)
                    continue;
                var key = _shownChannels.Keys.Get(slot);
                var target = UiChannelKey.TargetOf(key);
                var property = UiChannelKey.PropertyOf(key);
                dump.Append(' ').Append(target < _targetNames.Count && !string.IsNullOrEmpty(_targetNames[target]) ? _targetNames[target] : "Id " + target)
                    .Append(' ').Append(property == UiChannelKey.TextProperty ? "Text" : ((UiProperty)property).ToString()).Append('=').Append(_shownChannels.Values.Get(slot))
                    .Append((_shownChannels.Held & (1 << slot)) != 0 ? " (held)" : "");
            }
            dump.Append('\n');
            foreach (var state in _elements)
            {
                var element = state.Element;
                dump.Append(element.GetType().Name).Append(" #").Append(element.name).Append(" ids ").Append(state.Ids)
                    .Append(" display ").Append(element.resolvedStyle.display).Append(" visibility ").Append(element.resolvedStyle.visibility)
                    .Append(" usable ").Append(element.enabledInHierarchy).Append(" opacity ").Append(element.resolvedStyle.opacity);
                for (var slot = 0; slot < ClassSlots.Count && slot < UiIds.SlotCount; slot++)
                    if (!string.IsNullOrEmpty(ClassSlots[slot]) && element.ClassListContains(ClassSlots[slot]))
                        dump.Append(" .").Append(ClassSlots[slot]);
                if (element is TextElement text)
                    dump.Append(" text \"").Append(text.text).Append('"');
                dump.Append('\n');
            }
            return dump.ToString();
        }

        void OnUiReloaded(PanelRenderer panel, VisualElement root, int version)
        {
            if (root == _resolvedRoot && version == _resolvedVersion)
                return;
            _resolvedVersion = version;
            Resolve(root);
        }

        void Reset()
        {
            Panel = GetComponent<PanelRenderer>();
        }

        void OnEnable()
        {
            if (Panel == null)
                Panel = GetComponent<PanelRenderer>();
            ListenToHotkeys();
            if (!PanelsEnabledNow.Contains(this))
                PanelsEnabledNow.Add(this);
            _onUiReloaded ??= OnUiReloaded;
            if (Panel != null)
                Panel.RegisterUIReloadCallback(_onUiReloaded);
        }

        void OnDisable()
        {
            StopPreview();
            PanelsEnabledNow.Remove(this);
            StopListeningToHotkeys();
            if (Panel != null && _onUiReloaded != null)
                Panel.UnregisterUIReloadCallback(_onUiReloaded);
            ForgetLists();
            foreach (var unregister in _unregisterCommands)
                unregister();
            _unregisterCommands.Clear();
            foreach (var state in _elements)
                RestoreAuthoredState(state);
            _elements.Clear();
            _stateOf.Clear();
            _resolvedRoot = null;
            _resolvedVersion = -1;
        }

        void Update()
        {
            if (_resolvedRoot == null || !BoundWorldIsUsable())
                return;
            ReportInputStateFromLastLayout();
            if (!_targetNamesRead && !_targetNamesQuery.IsEmptyIgnoreFilter)
                ReadTargetNames();
            if (_resolveRequested)
                Resolve(_resolvedRoot);
            ApplySwitches(ReadSwitchesOfEveryHost());
            ApplyChannels(ReadChannelsOfEveryHost());
            UpdateLists();
            DrainSignals();
            DrainFocus();
            _applyEverything = false;
        }
    }
}
