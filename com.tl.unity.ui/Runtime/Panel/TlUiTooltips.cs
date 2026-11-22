using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    [AddComponentMenu("Tl UI/Tl UI Tooltips")]
    [HelpURL("https://github.com/IAFahim/tl.unity/tree/main/com.tl.unity.ui")]
    public sealed class TlUiTooltips : MonoBehaviour
    {
        public const string TooltipClass = "tl-tooltip";

        [Tooltip("The Panel Renderer whose elements show their Tooltip text. Filled in when the component is added next to it.")]
        public PanelRenderer Panel;

        [Tooltip("How long the pointer rests on an element before its tooltip shows, in milliseconds. Keyboard and gamepad focus show it at once.")]
        [Min(0)]
        public int DelayMs = 400;

        [Tooltip("Space between the element and its tooltip, in panel pixels.")]
        public float Gap = 6f;

        PanelRenderer.VersionedUIReloadCallback _onUiReloaded;
        EventCallback<PointerOverEvent> _onPointerOver;
        EventCallback<PointerLeaveEvent> _onPointerLeave;
        EventCallback<PointerDownEvent> _onPointerDown;
        EventCallback<FocusInEvent> _onFocusIn;
        EventCallback<FocusOutEvent> _onFocusOut;
        VisualElement _root;
        Label _label;
        IVisualElementScheduledItem _showLater;
        VisualElement _waitingFor;
        VisualElement _shownFor;

        public Label Label => _label;

        public VisualElement ShownFor => _shownFor;

        static VisualElement WithTooltip(VisualElement element)
        {
            for (var ancestor = element; ancestor != null; ancestor = ancestor.hierarchy.parent)
                if (!string.IsNullOrEmpty(ancestor.tooltip))
                    return ancestor;
            return null;
        }

        void Show(VisualElement owner)
        {
            _waitingFor = null;
            _showLater?.Pause();
            if (owner == null || owner.panel == null || _label == null)
            {
                Hide();
                return;
            }
            _shownFor = owner;
            if (!ReferenceEquals(_label.text, owner.tooltip))
                _label.text = owner.tooltip;
            var bounds = owner.worldBound;
            var place = _root.WorldToLocal(new Vector2(bounds.xMin, bounds.yMax + Gap));
            _label.style.left = place.x;
            _label.style.top = place.y;
            _label.style.display = DisplayStyle.Flex;
            _label.BringToFront();
        }

        void Hide()
        {
            _waitingFor = null;
            _shownFor = null;
            _showLater?.Pause();
            if (_label != null)
                _label.style.display = DisplayStyle.None;
        }

        void ShowAfterTheDelay()
        {
            if (_waitingFor != null)
                Show(_waitingFor);
        }

        void OnPointerOver(PointerOverEvent over)
        {
            var owner = WithTooltip(over.target as VisualElement);
            if (owner == _shownFor && owner != null)
                return;
            Hide();
            if (owner == null)
                return;
            _waitingFor = owner;
            _showLater.ExecuteLater(DelayMs);
        }

        void OnFocusIn(FocusInEvent focus) => Show(WithTooltip(focus.target as VisualElement));

        void OnFocusOut(FocusOutEvent focus)
        {
            if (_shownFor != null && _shownFor == WithTooltip(focus.target as VisualElement))
                Hide();
        }

        void Detach()
        {
            if (_root == null)
                return;
            _root.UnregisterCallback(_onPointerOver);
            _root.UnregisterCallback(_onPointerLeave);
            _root.UnregisterCallback(_onPointerDown, TrickleDown.TrickleDown);
            _root.UnregisterCallback(_onFocusIn);
            _root.UnregisterCallback(_onFocusOut);
            _showLater?.Pause();
            _label?.RemoveFromHierarchy();
            _root = null;
            _label = null;
            _showLater = null;
            _waitingFor = null;
            _shownFor = null;
        }

        void OnUiReloaded(PanelRenderer panel, VisualElement root, int version)
        {
            if (root == _root)
                return;
            Detach();
            _root = root;
            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(TooltipClass);
            _label.style.position = Position.Absolute;
            _label.style.display = DisplayStyle.None;
            root.Add(_label);
            _showLater = root.schedule.Execute(ShowAfterTheDelay);
            _showLater.Pause();
            root.RegisterCallback(_onPointerOver);
            root.RegisterCallback(_onPointerLeave);
            root.RegisterCallback(_onPointerDown, TrickleDown.TrickleDown);
            root.RegisterCallback(_onFocusIn);
            root.RegisterCallback(_onFocusOut);
        }

        void Reset()
        {
            Panel = GetComponent<PanelRenderer>();
        }

        void OnEnable()
        {
            if (Panel == null)
                Panel = GetComponent<PanelRenderer>();
            _onPointerOver ??= OnPointerOver;
            _onPointerLeave ??= _ => Hide();
            _onPointerDown ??= _ => Hide();
            _onFocusIn ??= OnFocusIn;
            _onFocusOut ??= OnFocusOut;
            _onUiReloaded ??= OnUiReloaded;
            if (Panel != null)
                Panel.RegisterUIReloadCallback(_onUiReloaded);
        }

        void OnDisable()
        {
            if (Panel != null && _onUiReloaded != null)
                Panel.UnregisterUIReloadCallback(_onUiReloaded);
            Detach();
        }
    }
}
