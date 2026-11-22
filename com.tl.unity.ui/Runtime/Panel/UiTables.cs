using System;
using Tl.Ui;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace TlUi
{
    [Serializable]
    public struct UiTarget
    {
        [Tooltip("The Id clips use for this target, 0 to 63.")]
        [Range(0, UiIds.HighestId)]
        public int Id;

        [Tooltip("UI Toolkit selector: #name is one element, .class every element with that class, Button every button; words combine as descendants (.shop-card Button).")]
        public string Selector;
    }

    public enum UiChannelProperty
    {
        Opacity,
        TranslateX,
        TranslateY,
        Scale,
        Rotate,
        WidthPercent,
        HeightPercent,
        Text,
        Reveal,
        Value
    }

    [Serializable]
    public struct UiChannelBinding
    {
        [Tooltip("The channel Animate and Text clips write, 0 to 15.")]
        [Range(0, UiChannelValues.Count - 1)]
        public int Channel;

        [Tooltip("The target Id whose elements this channel moves.")]
        [Range(0, UiIds.HighestId)]
        public int TargetId;

        [Tooltip("What the channel value drives. Opacity 0 to 1, Translate in pixels, Scale as a factor, Rotate in degrees, Width and Height in percent, Text as a text slot number, Reveal 0 to 1 of the shown text, Value for sliders, progress bars and custom elements.")]
        public UiChannelProperty Property;
    }

    [Serializable]
    public sealed class UiSignalReaction
    {
        [Tooltip("The code Signal clips raise.")]
        [Range(0, 255)]
        public int Code;

        [Tooltip("What happens when a Signal clip with this code starts: play a sound, start particles, call a method.")]
        public UnityEvent OnSignal = new UnityEvent();
    }

    [Serializable]
    public sealed class UiListBinding
    {
        [Tooltip("The target Id of the element that shows the rows: a ListView (rows are virtualized) or any container (one row element per row, kept and reused).")]
        [Range(0, UiIds.HighestId)]
        public int TargetId;

        [Tooltip("Where the rows come from: a Tl UI Asset Rows asset, or a component that shows an ECS buffer (a Tl UI Buffer Rows subclass).")]
        public UnityEngine.Object Rows;

        [Tooltip("The UXML made for each row. Its data source is the row, so bind the row's fields by name.")]
        public VisualTreeAsset RowTemplate;

        [Tooltip("Send To Game code when a row is chosen: clicked, or Submit (Enter, gamepad South) while it has focus or is selected in a ListView. The row number is the payload. -1 sends nothing.")]
        public int ClickCode = -1;

        [Tooltip("ListView only: Send To Game code when the selection moves (pointer or Up and Down navigation), with the selected row as the payload, -1 when nothing is selected. -1 sends nothing.")]
        public int SelectCode = -1;

        [Tooltip("Send To Game code for dragging a row onto another, with the dragged row as the payload and the row it lands on as the target. A ListView uses UI Toolkit's own drag and its insertion line; a container uses Tl UI's row drag. -1 turns dragging off.")]
        public int DragCode = -1;
    }

    public enum UiCommandEvent
    {
        Click,
        PointerEnter,
        PointerLeave,
        ValueChanged,
        HotkeyOnly
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
        [Tooltip("The target Id whose elements send this command.")]
        [Range(0, UiIds.HighestId)]
        public int TargetId;

        [Tooltip("Which element event sends it. Hotkey Only listens to the hotkey alone.")]
        public UiCommandEvent On;

        [Tooltip("Play, stop, pause, resume or restart one of the UI timelines; send a code to game systems; or invoke the event below.")]
        public UiCommandAction Do;

        [Tooltip("For timeline actions: 0 is the UI Sequence director's Playable, 1 and up its Also Plays list in order.")]
        [Min(0)]
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
