using System;

namespace Tl.Ui
{
    public enum UiDisplay : byte
    {
        Authored,
        Shown,
        Hidden
    }

    public enum UiVisibility : byte
    {
        Authored,
        Invisible
    }

    public enum UiUsable : byte
    {
        Authored,
        Usable,
        Unusable
    }

    public readonly struct UiLook : IEquatable<UiLook>
    {
        public UiLook(UiDisplay display, UiVisibility visibility, UiUsable usable, byte classesAdded, byte classesRemoved, byte templatesAttached)
        {
            Display = display;
            Visibility = visibility;
            Usable = usable;
            ClassesAdded = classesAdded;
            ClassesRemoved = classesRemoved;
            TemplatesAttached = templatesAttached;
        }

        public readonly UiDisplay Display;
        public readonly UiVisibility Visibility;
        public readonly UiUsable Usable;
        public readonly byte ClassesAdded;
        public readonly byte ClassesRemoved;
        public readonly byte TemplatesAttached;

        public bool ClassIsOn(int slot, bool authored)
        {
            var bit = 1 << slot;
            if ((ClassesRemoved & bit) != 0)
                return false;
            return (ClassesAdded & bit) != 0 || authored;
        }

        public bool TemplateIsAttached(int slot) => (TemplatesAttached & (1 << slot)) != 0;

        public bool IsUsable(bool authored) => Usable == UiUsable.Authored ? authored : Usable == UiUsable.Usable;

        public bool Equals(UiLook other) => Display == other.Display && Visibility == other.Visibility && Usable == other.Usable
            && ClassesAdded == other.ClassesAdded && ClassesRemoved == other.ClassesRemoved && TemplatesAttached == other.TemplatesAttached;

        public override bool Equals(object obj) => obj is UiLook other && Equals(other);

        public override int GetHashCode() => (int)Display | (int)Visibility << 2 | (int)Usable << 3 | ClassesAdded << 8 | ClassesRemoved << 16 | TemplatesAttached << 24;
    }

    public static class UiRules
    {
        public static UiLook LookOf(in UiSwitches switches, ulong targetIds)
        {
            var display = (switches.Hide & targetIds) != 0 ? UiDisplay.Hidden : (switches.Show & targetIds) != 0 ? UiDisplay.Shown : UiDisplay.Authored;
            var visibility = (switches.Invisible & targetIds) != 0 ? UiVisibility.Invisible : UiVisibility.Authored;
            var usable = (switches.Disable & targetIds) != 0 ? UiUsable.Unusable : (switches.Enable & targetIds) != 0 ? UiUsable.Usable : UiUsable.Authored;
            var classesAdded = 0;
            var classesRemoved = 0;
            var templatesAttached = 0;
            for (var slot = 0; slot < UiIds.SlotCount; slot++)
            {
                if ((UiIds.AddMask(switches, slot) & targetIds) != 0)
                    classesAdded |= 1 << slot;
                if ((UiIds.RemoveMask(switches, slot) & targetIds) != 0)
                    classesRemoved |= 1 << slot;
                if ((UiIds.AttachMask(switches, slot) & targetIds) != 0)
                    templatesAttached |= 1 << slot;
            }
            return new UiLook(display, visibility, usable, (byte)classesAdded, (byte)classesRemoved, (byte)templatesAttached);
        }

        public static ulong TouchedIds(in UiSwitches before, in UiSwitches after)
        {
            var touched = (before.Show ^ after.Show) | (before.Hide ^ after.Hide) | (before.Invisible ^ after.Invisible)
                | (before.Enable ^ after.Enable) | (before.Disable ^ after.Disable);
            for (var slot = 0; slot < UiIds.SlotCount; slot++)
                touched |= (UiIds.AddMask(before, slot) ^ UiIds.AddMask(after, slot))
                    | (UiIds.RemoveMask(before, slot) ^ UiIds.RemoveMask(after, slot))
                    | (UiIds.AttachMask(before, slot) ^ UiIds.AttachMask(after, slot));
            return touched;
        }

        public static ulong TouchedTargetsOfChannels(in UiChannels before, in UiChannels after)
        {
            var touched = 0UL;
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                var wasLive = (before.Live & (1 << slot)) != 0;
                var isLive = (after.Live & (1 << slot)) != 0;
                if (!wasLive && !isLive)
                    continue;
                if (wasLive && isLive && before.Keys.Get(slot) == after.Keys.Get(slot) && before.Values.Get(slot) == after.Values.Get(slot))
                    continue;
                if (wasLive)
                    touched |= UiIds.BitOf(UiChannelKey.TargetOf(before.Keys.Get(slot)));
                if (isLive)
                    touched |= UiIds.BitOf(UiChannelKey.TargetOf(after.Keys.Get(slot)));
            }
            return touched;
        }

        public static void MergeChannels(ref UiChannels total, in UiChannels next)
        {
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                if ((next.Live & (1 << slot)) == 0)
                    continue;
                var key = next.Keys.Get(slot);
                var into = UiChannelSlots.FindOrTakeFree(total.Live, total.Keys, key);
                if (into < 0)
                    continue;
                var intoBit = (ushort)(1 << into);
                total.Keys.Set(into, key);
                total.Values.Set(into, next.Values.Get(slot));
                total.Live |= intoBit;
                if ((next.Held & (1 << slot)) != 0)
                    total.Held |= intoBit;
            }
        }

        public static int RevealedLength(int textLength, float reveal)
        {
            if (reveal <= 0f)
                return 0;
            if (reveal >= 1f)
                return textLength;
            return (int)(textLength * reveal + 0.5f);
        }
    }
}
