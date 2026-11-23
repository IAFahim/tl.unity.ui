using System;
using Tl.Ui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TlUi
{
    public sealed class TlUiTargetIdAttribute : PropertyAttribute
    {
    }

    public sealed class TlUiTimelineSlotAttribute : PropertyAttribute
    {
    }

    [Serializable]
    public struct UiTarget
    {
        [Tooltip("The Id clips use for this target, 0 to 63.")]
        [Range(0, UiIds.HighestId)]
        public int Id;

        [Tooltip("UI Toolkit selector: #name is one element, .class every element with that class, Button every button; words combine as descendants (.shop-card Button).")]
        public string Selector;
    }

    [Serializable]
    public sealed class UiSignalReaction
    {
        [Tooltip("The code a Signal clip, or a game system through UiSignals.Raise, raises.")]
        [Range(0, 255)]
        public int Code;

        [Tooltip("What the signal does besides On Signal: play, stop, pause, resume or restart a UI timeline slot, or send a code back to game systems. Invoke Event runs On Signal only.")]
        public UiCommandAction Do = UiCommandAction.InvokeEvent;

        [Tooltip("The UI timeline slot the play, stop, pause, resume and restart actions use: 0 is the director's Playable, 1 and up its Also Plays list.")]
        [TlUiTimelineSlot]
        public int TimelineSlot;

        [Tooltip("The code Send To Game appends to the UI Command Inbox.")]
        public int GameCode;

        [Tooltip("Runs for every signal with this code: play a sound, start particles, call a method.")]
        public UnityEvent OnSignal = new UnityEvent();
    }

    [Serializable]
    public sealed class UiListBinding
    {
        [Tooltip("The element that shows the rows, by name: a ListView (rows are virtualized) or any container (one row element per row, kept and reused).")]
        [TlUiTargetId]
        public int TargetId;

        [Tooltip("Where the rows come from: a Tl UI Asset Rows asset, or a component that shows an ECS buffer (a Tl UI Buffer Rows subclass).")]
        public UnityEngine.Object Rows;

        [Tooltip("The UXML made for each row. Its data source is the row, so bind the row's fields by name.")]
        public VisualTreeAsset RowTemplate;

        [Tooltip("Send To Game code when a row is chosen: clicked, or Submit (Enter, gamepad South) while it has focus or is selected in a ListView. The row number is the payload. -1 sends nothing.")]
        public int ClickCode = -1;

        [Tooltip("ListView only: Send To Game code when the selection moves (pointer or Up and Down navigation), with the selected row as the payload, -1 when nothing is selected. -1 sends nothing.")]
        public int SelectCode = -1;

        [Tooltip("Send To Game code for dropping a row on another row of this list (swap, reorder): the dragged row is the payload, the row it lands on the target. A ListView uses UI Toolkit's own drag and its insertion line; in a container the row lifts and follows the pointer. -1: rows stay put inside this list.")]
        public int DragCode = -1;

        [Tooltip("Send To Game code for a row from another list dropped on this one (hand to table, bag to equipment): the dragged row is the payload, the row it lands on the target (-1 for empty space), and From List the list it came from. Containers only. -1: this list takes no drops. While any list takes drops, container rows can be lifted.")]
        public int DropCode = -1;
    }

    public enum UiCommandEvent
    {
        Click,
        PointerEnter,
        PointerLeave,
        ValueChanged,
        HotkeyOnly,
        Cancel
    }

    public enum UiCommandAction
    {
        PlayTimeline,
        StopTimeline,
        PauseTimeline,
        ResumeTimeline,
        RestartTimeline,
        SendToGame,
        InvokeEvent
    }

    [Serializable]
    public sealed class UiCommand
    {
        [Tooltip("The element that sends this command, by name.")]
        [TlUiTargetId]
        public int TargetId;

        [Tooltip("Which element event sends it. Hotkey Only listens to the hotkey alone.")]
        public UiCommandEvent On;

        [Tooltip("Play, stop, pause, resume or restart one of the UI timelines; send a code to game systems; or invoke the event below.")]
        public UiCommandAction Do;

        [Tooltip("For timeline actions: 0 is the UI Sequence director's Playable, 1 and up its Also Plays list in order.")]
        [TlUiTimelineSlot]
        public int TimelineSlot;

        [Tooltip("For Send To Game: the code game systems read from the UI Command Inbox buffer.")]
        public int GameCode;

        [Tooltip("For Send To Game: the payload sent with the code. A Value Changed event sends the new value rounded instead.")]
        public int GamePayload;

        [Tooltip("Optional: an input action that sends this command too, for keyboard and gamepad.")]
        public InputActionReference Hotkey;

        [Tooltip("For Invoke Event: what happens.")]
        public UnityEvent OnInvoke = new UnityEvent();
    }
}
