using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    public sealed class UiRowDrag : PointerManipulator
    {
        public const string DraggingClass = "tl-dragging";
        public const string DropTargetClass = "tl-drop-target";

        readonly TlUiPanel _panel;
        readonly UiRowHandle _row;
        VisualElement _hoveredRow;

        public UiRowDrag(TlUiPanel panel, UiRowHandle row)
        {
            _panel = panel;
            _row = row;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        VisualElement RowUnder(Vector2 panelPosition, out UiRowHandle handle)
        {
            handle = null;
            for (var element = target.panel?.Pick(panelPosition); element != null; element = element.hierarchy.parent)
                if (element.userData is UiRowHandle candidate && candidate.List == _row.List)
                {
                    handle = candidate;
                    return element;
                }
            return null;
        }

        void Hover(VisualElement row)
        {
            if (row == _hoveredRow)
                return;
            _hoveredRow?.RemoveFromClassList(DropTargetClass);
            _hoveredRow = row;
            _hoveredRow?.AddToClassList(DropTargetClass);
        }

        void OnPointerDown(PointerDownEvent down)
        {
            if (!CanStartManipulation(down))
                return;
            target.CapturePointer(down.pointerId);
            target.AddToClassList(DraggingClass);
        }

        void OnPointerMove(PointerMoveEvent move)
        {
            if (!target.HasPointerCapture(move.pointerId))
                return;
            var row = RowUnder(move.position, out _);
            Hover(row == target ? null : row);
        }

        void OnPointerUp(PointerUpEvent up)
        {
            if (!target.HasPointerCapture(up.pointerId))
                return;
            target.ReleasePointer(up.pointerId);
            target.RemoveFromClassList(DraggingClass);
            Hover(null);
            RowUnder(up.position, out var dropped);
            if (dropped != null && dropped.Index != _row.Index)
                _panel.SendRowDrag(_row.List, _row.Index, dropped.Index);
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        }
    }
}
