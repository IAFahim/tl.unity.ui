using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TlUi
{
    public enum UiTimelineVerb : byte
    {
        Play,
        Stop,
        Pause,
        Resume,
        Restart,
    }

    [VisualElementComponent]
    public partial struct TlTarget
    {
        [UxmlAttribute]
        public int Id;
    }

    [VisualElementComponent]
    public partial struct TlSend
    {
        [UxmlAttribute]
        public int Code;

        [UxmlAttribute]
        public int Payload;

        [UxmlAttribute]
        public bool RowAsPayload;

        static void Send(in TlSend self, VisualElement owner, EventBase trigger)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel == null)
                return;
            panel.SendToGame(self.Code, self.RowAsPayload ? TlRow.IndexAround(owner) : self.Payload, -1);
            trigger.StopPropagation();
        }

        [OnComponentAdded]
        static void LetNavigationReachIt(ref TlSend self, VisualElement owner) => owner.focusable = true;

        [RegisterCallback(typeof(ClickEvent))]
        static void SendTheCodeOnClick(ref TlSend self, VisualElement owner, ClickEvent click) => Send(self, owner, click);

        [RegisterCallback(typeof(NavigationSubmitEvent))]
        static void SendTheCodeOnSubmit(ref TlSend self, VisualElement owner, NavigationSubmitEvent submit) => Send(self, owner, submit);
    }

    [VisualElementComponent]
    public partial struct TlSendValue
    {
        [UxmlAttribute]
        public int Code;

        [UxmlAttribute]
        public float Scale;

        [UxmlCreateInstanceMethod]
        public static TlSendValue ScaledByOne() => new TlSendValue { Scale = 1f };

        static void Send(int code, VisualElement owner, int payload)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel != null)
                panel.SendToGame(code, payload, -1);
        }

        [RegisterCallback(typeof(ChangeEvent<float>))]
        static void SendTheNewFloat(ref TlSendValue self, VisualElement owner, ChangeEvent<float> change) => Send(self.Code, owner, Mathf.RoundToInt(change.newValue * self.Scale));

        [RegisterCallback(typeof(ChangeEvent<int>))]
        static void SendTheNewInt(ref TlSendValue self, VisualElement owner, ChangeEvent<int> change) => Send(self.Code, owner, change.newValue);

        [RegisterCallback(typeof(ChangeEvent<bool>))]
        static void SendTheNewToggle(ref TlSendValue self, VisualElement owner, ChangeEvent<bool> change) => Send(self.Code, owner, change.newValue ? 1 : 0);

        [RegisterCallback(typeof(ChangeEvent<string>))]
        static void SendTheChosenIndex(ref TlSendValue self, VisualElement owner, ChangeEvent<string> change)
        {
            if (owner is DropdownField dropdown)
                Send(self.Code, owner, dropdown.index);
        }
    }

    [VisualElementComponent]
    [RequiresElementOfType(typeof(TextField))]
    public partial struct TlSendText
    {
        [UxmlAttribute]
        public int Code;

        [RegisterCallback(typeof(ChangeEvent<string>))]
        static void SendTheText(ref TlSendText self, TextField owner, ChangeEvent<string> change)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel != null)
                panel.SendText(self.Code, change.newValue);
        }
    }

    [VisualElementComponent]
    public partial struct TlPlay
    {
        [UxmlAttribute]
        public int Timeline;

        [UxmlAttribute]
        public UiTimelineVerb Verb;

        static void Request(in TlPlay self, VisualElement owner, EventBase trigger)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel == null)
                return;
            panel.RequestTimeline(self.Verb, self.Timeline);
            trigger.StopPropagation();
        }

        [OnComponentAdded]
        static void LetNavigationReachIt(ref TlPlay self, VisualElement owner) => owner.focusable = true;

        [RegisterCallback(typeof(ClickEvent))]
        static void RequestTheTimelineOnClick(ref TlPlay self, VisualElement owner, ClickEvent click) => Request(self, owner, click);

        [RegisterCallback(typeof(NavigationSubmitEvent))]
        static void RequestTheTimelineOnSubmit(ref TlPlay self, VisualElement owner, NavigationSubmitEvent submit) => Request(self, owner, submit);
    }

    [VisualElementComponent(false)]
    public partial struct TlRow
    {
        public int List;
        public int Index;

        public static int IndexAround(VisualElement element)
        {
            for (var ancestor = element; ancestor != null; ancestor = ancestor.hierarchy.parent)
                if (ancestor.HasComponent<TlRow>())
                    return ancestor.GetComponent<TlRow>().Index;
            return -1;
        }

        [RegisterCallback(typeof(ClickEvent))]
        static void SendTheRowClick(ref TlRow self, VisualElement owner, ClickEvent click)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel != null)
                panel.SendRowClick(self.List, self.Index);
        }

        [RegisterCallback(typeof(NavigationSubmitEvent))]
        static void SendTheRowSubmit(ref TlRow self, VisualElement owner, NavigationSubmitEvent submit)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel != null)
                panel.SendRowSubmit(self.List, self.Index);
        }
    }

    [VisualElementComponent(false)]
    [RequiresComponentOfType(typeof(TlRow))]
    public partial struct TlRowDrag
    {
        public const string DraggingClass = "tl-dragging";
        public const string DropTargetClass = "tl-drop-target";
        public const string GhostClass = "tl-drag-ghost";
        public const float PixelsBeforeTheRowLifts = 6f;

        Vector2 _pointerAtPress;
        Vector2 _ghostAtPressInTheDragLayer;
        bool _lifted;
        VisualElement _ghost;
        VisualElement _hovered;
        List<VisualElement> _underThePointer;

        static void Lift(ref TlRowDrag self, VisualElement row)
        {
            var panel = TlUiPanel.Owning(row);
            self._ghost = panel != null ? panel.DragGhostOf(row) : null;
            row.AddToClassList(DraggingClass);
            self._lifted = true;
            if (self._ghost == null)
                return;
            var layer = self._ghost.hierarchy.parent;
            self._ghostAtPressInTheDragLayer = layer.WorldToLocal(row.worldBound.position);
            self._ghost.style.width = row.layout.width;
            self._ghost.style.height = row.layout.height;
            self._ghost.style.display = DisplayStyle.Flex;
        }

        static void PutBack(ref TlRowDrag self, VisualElement row)
        {
            Hover(ref self, null);
            row.RemoveFromClassList(DraggingClass);
            if (self._ghost != null)
                self._ghost.style.display = DisplayStyle.None;
            self._ghost = null;
            self._lifted = false;
        }

        static void Hover(ref TlRowDrag self, VisualElement dropTarget)
        {
            if (dropTarget == self._hovered)
                return;
            self._hovered?.RemoveFromClassList(DropTargetClass);
            self._hovered = dropTarget;
            self._hovered?.AddToClassList(DropTargetClass);
        }

        static VisualElement DropTargetUnder(ref TlRowDrag self, VisualElement row, Vector2 panelPosition)
        {
            var panel = TlUiPanel.Owning(row);
            if (panel == null)
                return null;
            var draggedList = row.GetComponent<TlRow>().List;
            self._underThePointer ??= new List<VisualElement>(16);
            self._underThePointer.Clear();
            row.panel.PickAll(panelPosition, self._underThePointer);
            foreach (var picked in self._underThePointer)
            {
                if (picked == row || row.Contains(picked) || (self._ghost != null && (picked == self._ghost || self._ghost.Contains(picked))))
                    continue;
                for (var element = picked; element != null; element = element.hierarchy.parent)
                {
                    if (element.HasComponent<TlRow>())
                    {
                        var landedOn = element.GetComponent<TlRow>().List;
                        if (landedOn == draggedList ? panel.ReordersRows(landedOn) : panel.TakesDrops(landedOn))
                            return element;
                        break;
                    }
                    var list = panel.ListHolding(element);
                    if (list >= 0 && list != draggedList && panel.TakesDrops(list))
                        return element;
                }
            }
            return null;
        }

        [RegisterCallback(typeof(PointerDownEvent))]
        static void PressTheRow(ref TlRowDrag self, VisualElement owner, PointerDownEvent down)
        {
            if (down.button != 0)
                return;
            self._pointerAtPress = down.position;
            self._lifted = false;
            owner.CapturePointer(down.pointerId);
        }

        [RegisterCallback(typeof(PointerMoveEvent))]
        static void MoveTheRowWithThePointer(ref TlRowDrag self, VisualElement owner, PointerMoveEvent move)
        {
            if (!owner.HasPointerCapture(move.pointerId))
                return;
            var moved = (Vector2)move.position - self._pointerAtPress;
            if (!self._lifted)
            {
                if (moved.sqrMagnitude < PixelsBeforeTheRowLifts * PixelsBeforeTheRowLifts)
                    return;
                Lift(ref self, owner);
            }
            if (self._ghost != null)
            {
                self._ghost.style.left = self._ghostAtPressInTheDragLayer.x + moved.x;
                self._ghost.style.top = self._ghostAtPressInTheDragLayer.y + moved.y;
            }
            Hover(ref self, DropTargetUnder(ref self, owner, move.position));
        }

        [RegisterCallback(typeof(PointerCaptureOutEvent))]
        static void PutTheRowBackWhenCaptureIsLost(ref TlRowDrag self, VisualElement owner, PointerCaptureOutEvent lost)
        {
            if (self._lifted)
                PutBack(ref self, owner);
        }

        [RegisterCallback(typeof(PointerUpEvent))]
        static void DropTheRow(ref TlRowDrag self, VisualElement owner, PointerUpEvent up)
        {
            if (!owner.HasPointerCapture(up.pointerId))
                return;
            if (!self._lifted)
            {
                owner.ReleasePointer(up.pointerId);
                return;
            }
            var dropTarget = DropTargetUnder(ref self, owner, up.position);
            PutBack(ref self, owner);
            var panel = TlUiPanel.Owning(owner);
            var dragged = owner.GetComponent<TlRow>();
            owner.ReleasePointer(up.pointerId);
            if (panel == null || dropTarget == null)
                return;
            if (dropTarget.HasComponent<TlRow>())
            {
                var landedOn = dropTarget.GetComponent<TlRow>();
                if (landedOn.List == dragged.List)
                    panel.SendRowDrag(dragged.List, dragged.Index, landedOn.Index);
                else
                    panel.SendRowDrop(dragged.List, dragged.Index, landedOn.List, landedOn.Index);
                return;
            }
            panel.SendRowDrop(dragged.List, dragged.Index, panel.ListHolding(dropTarget), -1);
        }
    }
}
