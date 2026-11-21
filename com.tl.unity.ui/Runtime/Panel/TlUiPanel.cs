using System;
using System.Collections.Generic;
using System.Text;
using Tl;
using Tl.Ui;
using Tl.Unity;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    [DefaultExecutionOrder(TlUiOrder.Panel)]
    [AddComponentMenu("Tl UI/Tl UI Panel")]
    [HelpURL("https://github.com/IAFahim/tl.unity/tree/main/com.tl.unity.ui")]
    public sealed class TlUiPanel : MonoBehaviour
    {
        sealed class ElementClasses : IUiClassList
        {
            public VisualElement Element;

            public bool HasClass(string className) => Element.ClassListContains(className);
        }

        [Tooltip("The Panel Renderer this component drives. Filled in when the component is added next to it.")]
        public PanelRenderer Panel;

        [Header("Address")]
        [Tooltip("What each Id points at. Clips, channels and commands name Ids; this table turns them into elements.")]
        public List<UiTarget> Targets = new List<UiTarget>();

        [Tooltip("USS classes Add Class and Remove Class clips name by slot, 0 to 7.")]
        public List<string> ClassSlots = new List<string>();

        [Tooltip("UXML templates Attach clips put inside their target, by slot, 0 to 7. The template is created the first time its clip starts, then shown while a clip is on and hidden (display none) while none is, so playing again allocates nothing.")]
        public List<VisualTreeAsset> TemplateSlots = new List<VisualTreeAsset>();

        [Tooltip("Texts Text clips show, by slot.")]
        [TextArea]
        public List<string> TextSlots = new List<string>();

        [Tooltip("What each channel moves: Animate and Text clips write channels, this table says which target and property.")]
        public List<UiChannelBinding> Channels = new List<UiChannelBinding>();

        [Header("Events")]
        [Tooltip("What happens when a Signal clip starts, by code.")]
        public List<UiSignalReaction> Signals = new List<UiSignalReaction>();

        [Header("Input")]
        [Tooltip("What clicks, pointer moves, value changes and hotkeys do: start or stop UI timelines, send codes to game systems, or invoke events.")]
        public List<UiCommand> Commands = new List<UiCommand>();

        [Header("Data")]
        [Tooltip("The data source of the whole screen: a ScriptableObject, or a Tl UI Data component that shows an ECS singleton. Bind fields to it in UI Builder.")]
        public UnityEngine.Object DataSource;

        readonly List<UiElementState> _elements = new List<UiElementState>();
        readonly Dictionary<VisualElement, UiElementState> _stateOf = new Dictionary<VisualElement, UiElementState>();
        readonly List<VisualElement> _matches = new List<VisualElement>();
        readonly List<Action> _unregisterCommands = new List<Action>();
        readonly List<int> _drainedCodes = new List<int>(16);
        readonly Dictionary<string, string[]> _revealPrefixes = new Dictionary<string, string[]>();
        readonly ElementClasses _classes = new ElementClasses();
        World _boundWorld;
        EntityQuery _switchesQuery;
        EntityQuery _channelsQuery;
        EntityQuery _signalsQuery;
        EntityQuery _focusQuery;
        EntityQuery _hostsQuery;
        UiSwitches _shownSwitches;
        UiChannels _shownChannels;
        PanelRenderer.VersionedUIReloadCallback _onUiReloaded;
        VisualElement _resolvedRoot;
        int _resolvedVersion = -1;
        bool _resolveRequested;
        bool _applyEverything = true;

        public World BoundWorld => _boundWorld;

        public IReadOnlyList<UiElementState> Elements => _elements;

        public VisualElement Root => _resolvedRoot;

        public UiSwitches ShownSwitches => _shownSwitches;

        public UiChannels ShownChannels => _shownChannels;

        public void Bind(World world)
        {
            _boundWorld = world;
            var entityManager = world.EntityManager;
            _switchesQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiSwitches>());
            _channelsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiChannels>());
            _signalsQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimelineEvent<UiSignal>>());
            _focusQuery = entityManager.CreateEntityQuery(ComponentType.ReadWrite<TimelineEvent<UiFocus>>());
            _hostsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<UiTimelineSlot>(), ComponentType.ReadWrite<TimelineRequest>());
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
            var text = look.Has(UiChannelProperty.Text) && slot >= 0 && slot < TextSlots.Count ? TextSlots[slot] : state.AuthoredText;
            if (text == null || !look.Has(UiChannelProperty.Reveal))
                return text;
            return PrefixOf(text, UiRules.RevealedLength(text.Length, look.Reveal));
        }

        void ShowOrHideAttachedTemplate(UiElementState state, int slot, bool attached)
        {
            var instance = state.Attached[slot];
            if (attached && instance == null && TemplateSlots[slot] != null)
                instance = state.Attached[slot] = TemplateSlots[slot].Instantiate();
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
            if (look.Has(UiChannelProperty.Opacity) != shown.Has(UiChannelProperty.Opacity) || look.Opacity != shown.Opacity)
                element.style.opacity = look.Has(UiChannelProperty.Opacity) ? new StyleFloat(look.Opacity) : new StyleFloat(StyleKeyword.Null);
            var translated = look.Has(UiChannelProperty.TranslateX) || look.Has(UiChannelProperty.TranslateY);
            var wasTranslated = shown.Has(UiChannelProperty.TranslateX) || shown.Has(UiChannelProperty.TranslateY);
            if (translated != wasTranslated || look.TranslateX != shown.TranslateX || look.TranslateY != shown.TranslateY)
                element.style.translate = translated ? new StyleTranslate(new Translate(new Length(look.TranslateX), new Length(look.TranslateY))) : new StyleTranslate(StyleKeyword.Null);
            if (look.Has(UiChannelProperty.Scale) != shown.Has(UiChannelProperty.Scale) || look.Scale != shown.Scale)
                element.style.scale = look.Has(UiChannelProperty.Scale) ? new StyleScale(new Scale(new Vector2(look.Scale, look.Scale))) : new StyleScale(StyleKeyword.Null);
            if (look.Has(UiChannelProperty.Rotate) != shown.Has(UiChannelProperty.Rotate) || look.Rotate != shown.Rotate)
                element.style.rotate = look.Has(UiChannelProperty.Rotate) ? new StyleRotate(new Rotate(new Angle(look.Rotate, AngleUnit.Degree))) : new StyleRotate(StyleKeyword.Null);
            if (look.Has(UiChannelProperty.WidthPercent) != shown.Has(UiChannelProperty.WidthPercent) || look.Width != shown.Width)
                element.style.width = look.Has(UiChannelProperty.WidthPercent) ? new StyleLength(Length.Percent(look.Width)) : new StyleLength(StyleKeyword.Null);
            if (look.Has(UiChannelProperty.HeightPercent) != shown.Has(UiChannelProperty.HeightPercent) || look.Height != shown.Height)
                element.style.height = look.Has(UiChannelProperty.HeightPercent) ? new StyleLength(Length.Percent(look.Height)) : new StyleLength(StyleKeyword.Null);
            if (element is TextElement textElement)
            {
                var text = ShownText(state, look);
                if (!ReferenceEquals(textElement.text, text))
                    textElement.text = text;
            }
            if (element is INotifyValueChanged<float> valued && (look.Has(UiChannelProperty.Value) != shown.Has(UiChannelProperty.Value) || look.Value != shown.Value))
                valued.SetValueWithoutNotify(look.Has(UiChannelProperty.Value) ? look.Value : state.AuthoredValue);
            state.ShownChannels = look;
        }

        void SendToGame(int code, int payload)
        {
            if (!TryFindHost(out var host))
                return;
            _boundWorld.EntityManager.GetBuffer<UiCommandInbox>(host).Add(new UiCommandInbox { Code = code, Payload = payload });
        }

        bool TryFindHost(out Entity host)
        {
            host = Entity.Null;
            if (!BoundWorldIsUsable() || _hostsQuery.IsEmptyIgnoreFilter)
            {
                Debug.LogWarning("Tl UI Panel: no object in the SubScene has a Tl UI Timelines component, so commands have nowhere to go.", this);
                return false;
            }
            _hostsQuery.CompleteDependency();
            using var hosts = _hostsQuery.ToEntityArray(Allocator.Temp);
            host = hosts[0];
            return true;
        }

        void RequestTimeline(UiCommandAction action, int slot)
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
            var timeline = slots[slot].Timeline;
            var requests = entityManager.GetBuffer<TimelineRequest>(host);
            switch (action)
            {
                case UiCommandAction.PlayTimeline:
                    requests.Start(timeline);
                    break;
                case UiCommandAction.StopTimeline:
                    requests.Stop(timeline);
                    break;
                case UiCommandAction.PauseTimeline:
                    requests.Pause(timeline);
                    break;
                case UiCommandAction.ResumeTimeline:
                    requests.Resume(timeline);
                    break;
                case UiCommandAction.RestartTimeline:
                    requests.Stop(timeline);
                    requests.Start(timeline);
                    break;
            }
        }

        void Run(UiCommand command, int payload)
        {
            switch (command.Do)
            {
                case UiCommandAction.InvokeEvent:
                    command.OnInvoke?.Invoke();
                    break;
                case UiCommandAction.SendToGame:
                    SendToGame(command.GameCode, payload);
                    break;
                default:
                    RequestTimeline(command.Do, command.TimelineSlot);
                    break;
            }
        }

        void RegisterCommand(UiCommand command, VisualElement element)
        {
            switch (command.On)
            {
                case UiCommandEvent.Click:
                    EventCallback<ClickEvent> onClick = _ => Run(command, command.GamePayload);
                    element.RegisterCallback(onClick);
                    _unregisterCommands.Add(() => element.UnregisterCallback(onClick));
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
            }
        }

        void Resolve(VisualElement root)
        {
            foreach (var unregister in _unregisterCommands)
                unregister();
            _unregisterCommands.Clear();
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
                {
                    if (!_stateOf.TryGetValue(element, out var state))
                    {
                        state = CaptureAuthoredState(element);
                        _stateOf.Add(element, state);
                        _elements.Add(state);
                    }
                    state.Ids |= UiIds.BitOf((byte)target.Id);
                }
            }
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

        UiChannelLook ChannelLookOf(UiElementState state, in UiChannels channels)
        {
            var look = default(UiChannelLook);
            foreach (var binding in Channels)
                if ((channels.Live & (1 << binding.Channel)) != 0 && (state.Ids & UiIds.BitOf((byte)binding.TargetId)) != 0)
                    look.Set(binding.Property, channels.Values.Get(binding.Channel));
            return look;
        }

        void ApplyChannels(in UiChannels latest)
        {
            var touchedChannels = _applyEverything ? ushort.MaxValue : UiRules.TouchedChannels(_shownChannels, latest);
            if (touchedChannels == 0)
                return;
            var touchedIds = 0UL;
            foreach (var binding in Channels)
                if ((touchedChannels & (1 << binding.Channel)) != 0)
                    touchedIds |= UiIds.BitOf((byte)binding.TargetId);
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
                        reaction.OnSignal?.Invoke();
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

        void RunHotkeys()
        {
            foreach (var command in Commands)
                if (command.Hotkey != null && command.Hotkey.action != null && command.Hotkey.action.WasPerformedThisFrame())
                    Run(command, command.GamePayload);
        }

        public string Dump()
        {
            var dump = new StringBuilder();
            dump.Append("switches show ").Append(_shownSwitches.Show).Append(" hide ").Append(_shownSwitches.Hide)
                .Append(" enable ").Append(_shownSwitches.Enable).Append(" disable ").Append(_shownSwitches.Disable)
                .Append(" | channels live ").Append(_shownChannels.Live).Append(" held ").Append(_shownChannels.Held).Append('\n');
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
            foreach (var command in Commands)
                if (command.Hotkey != null && command.Hotkey.action != null)
                    command.Hotkey.action.Enable();
            _onUiReloaded ??= OnUiReloaded;
            if (Panel != null)
                Panel.RegisterUIReloadCallback(_onUiReloaded);
        }

        void OnDisable()
        {
            if (Panel != null && _onUiReloaded != null)
                Panel.UnregisterUIReloadCallback(_onUiReloaded);
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
            if (_resolveRequested)
                Resolve(_resolvedRoot);
            ApplySwitches(ReadSwitchesOfEveryHost());
            ApplyChannels(ReadChannelsOfEveryHost());
            DrainSignals();
            DrainFocus();
            RunHotkeys();
            _applyEverything = false;
        }
    }
}
