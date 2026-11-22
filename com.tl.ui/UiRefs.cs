using System;

namespace Tl.Ui
{
    [Serializable]
    public struct UiTargetRef
    {
        public byte Id;

        public ulong Bit => UiIds.BitOf(Id);
    }

    [Serializable]
    public struct UiClassRef
    {
        public byte Slot;
    }

    [Serializable]
    public struct UiTemplateRef
    {
        public byte Slot;
    }

    [Serializable]
    public struct UiTextRef
    {
        public byte Slot;
    }

    public enum UiProperty : byte
    {
        Opacity,
        MoveX,
        MoveY,
        Scale,
        Rotate,
        WidthPercent,
        HeightPercent,
        Reveal,
        Value
    }
}
