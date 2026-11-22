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

        [RegisterCallback(typeof(ClickEvent))]
        static void SendTheCodeOnClick(ref TlSend self, VisualElement owner, ClickEvent click)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel == null)
                return;
            panel.SendToGame(self.Code, self.RowAsPayload ? TlRow.IndexAround(owner) : self.Payload, -1);
            click.StopPropagation();
        }
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
    }

    [VisualElementComponent]
    public partial struct TlPlay
    {
        [UxmlAttribute]
        public int Timeline;

        [UxmlAttribute]
        public UiTimelineVerb Verb;

        [RegisterCallback(typeof(ClickEvent))]
        static void RequestTheTimelineOnClick(ref TlPlay self, VisualElement owner, ClickEvent click)
        {
            var panel = TlUiPanel.Owning(owner);
            if (panel == null)
                return;
            panel.RequestTimeline(self.Verb, self.Timeline);
            click.StopPropagation();
        }
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
    }

    [VisualElementComponent(false)]
    [RequiresComponentOfType(typeof(TlRow))]
    public partial struct TlRowDrag
    {
        public const string DraggingClass = "tl-dragging";
        public const string DropTargetClass = "tl-drop-target";

        static VisualElement s_HoveredRow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetTheHoveredRowOfThePreviousPlay() => s_HoveredRow = null;

        static VisualElement RowUnder(VisualElement dragged, Vector2 panelPosition)
        {
            var list = dragged.GetComponent<TlRow>().List;
            for (var element = dragged.panel?.Pick(panelPosition); element != null; element = element.hierarchy.parent)
                if (element.HasComponent<TlRow>() && element.GetComponent<TlRow>().List == list)
                    return element;
            return null;
        }

        static void Hover(VisualElement row)
        {
            if (row == s_HoveredRow)
                return;
            s_HoveredRow?.RemoveFromClassList(DropTargetClass);
            s_HoveredRow = row;
            s_HoveredRow?.AddToClassList(DropTargetClass);
        }

        [RegisterCallback(typeof(PointerDownEvent))]
        static void StartDragging(ref TlRowDrag self, VisualElement owner, PointerDownEvent down)
        {
            if (down.button != 0)
                return;
            owner.CapturePointer(down.pointerId);
            owner.AddToClassList(DraggingClass);
        }

        [RegisterCallback(typeof(PointerMoveEvent))]
        static void MarkTheRowUnderThePointer(ref TlRowDrag self, VisualElement owner, PointerMoveEvent move)
        {
            if (!owner.HasPointerCapture(move.pointerId))
                return;
            var row = RowUnder(owner, move.position);
            Hover(row == owner ? null : row);
        }

        [RegisterCallback(typeof(PointerCaptureOutEvent))]
        static void StopDraggingWhenCaptureIsLost(ref TlRowDrag self, VisualElement owner, PointerCaptureOutEvent lost)
        {
            owner.RemoveFromClassList(DraggingClass);
            Hover(null);
        }

        [RegisterCallback(typeof(PointerUpEvent))]
        static void DropOnTheRowUnderThePointer(ref TlRowDrag self, VisualElement owner, PointerUpEvent up)
        {
            if (!owner.HasPointerCapture(up.pointerId))
                return;
            owner.ReleasePointer(up.pointerId);
            owner.RemoveFromClassList(DraggingClass);
            Hover(null);
            var dropped = RowUnder(owner, up.position);
            if (dropped == null || dropped == owner)
                return;
            var from = owner.GetComponent<TlRow>();
            var panel = TlUiPanel.Owning(owner);
            if (panel != null)
                panel.SendRowDrag(from.List, from.Index, dropped.GetComponent<TlRow>().Index);
        }
    }
}
