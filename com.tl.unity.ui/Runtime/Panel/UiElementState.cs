using System;
using Tl.Ui;
using UnityEngine.UIElements;

namespace TlUi
{
    public struct UiChannelLook : IEquatable<UiChannelLook>
    {
        public ushort Present;
        public float Opacity;
        public float TranslateX;
        public float TranslateY;
        public float Scale;
        public float Rotate;
        public float Width;
        public float Height;
        public float Reveal;
        public float Value;
        public int TextSlot;

        public bool Has(UiChannelProperty property) => (Present & (1 << (int)property)) != 0;

        public void Set(UiChannelProperty property, float value)
        {
            Present |= (ushort)(1 << (int)property);
            switch (property)
            {
                case UiChannelProperty.Opacity: Opacity = value; break;
                case UiChannelProperty.TranslateX: TranslateX = value; break;
                case UiChannelProperty.TranslateY: TranslateY = value; break;
                case UiChannelProperty.Scale: Scale = value; break;
                case UiChannelProperty.Rotate: Rotate = value; break;
                case UiChannelProperty.WidthPercent: Width = value; break;
                case UiChannelProperty.HeightPercent: Height = value; break;
                case UiChannelProperty.Text: TextSlot = (int)value; break;
                case UiChannelProperty.Reveal: Reveal = value; break;
                case UiChannelProperty.Value: Value = value; break;
            }
        }

        public bool Equals(UiChannelLook other) => Present == other.Present && Opacity == other.Opacity && TranslateX == other.TranslateX
            && TranslateY == other.TranslateY && Scale == other.Scale && Rotate == other.Rotate && Width == other.Width && Height == other.Height
            && Reveal == other.Reveal && Value == other.Value && TextSlot == other.TextSlot;

        public override bool Equals(object obj) => obj is UiChannelLook other && Equals(other);

        public override int GetHashCode() => Present ^ TextSlot << 16 ^ Opacity.GetHashCode() ^ Value.GetHashCode();
    }

    public sealed class UiElementState
    {
        public UiElementState(VisualElement element)
        {
            Element = element;
        }

        public readonly VisualElement Element;
        public readonly TemplateContainer[] Attached = new TemplateContainer[UiIds.SlotCount];
        public ulong Ids;
        public bool AuthoredEnabled;
        public string AuthoredText;
        public float AuthoredValue;
        public byte AuthoredClasses;
        public UiLook ShownLook;
        public UiChannelLook ShownChannels;
    }
}
