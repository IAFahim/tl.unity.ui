using Tl;

namespace Tl.Ui
{
    [TlState]
    public partial struct UiSwitches
    {
        public ulong Show;
        public ulong Hide;
        public ulong Invisible;
        public ulong Enable;
        public ulong Disable;
        public ulong Add0;
        public ulong Add1;
        public ulong Add2;
        public ulong Add3;
        public ulong Add4;
        public ulong Add5;
        public ulong Add6;
        public ulong Add7;
        public ulong Remove0;
        public ulong Remove1;
        public ulong Remove2;
        public ulong Remove3;
        public ulong Remove4;
        public ulong Remove5;
        public ulong Remove6;
        public ulong Remove7;
        public ulong Attach0;
        public ulong Attach1;
        public ulong Attach2;
        public ulong Attach3;
        public ulong Attach4;
        public ulong Attach5;
        public ulong Attach6;
        public ulong Attach7;
    }

    public static class UiIds
    {
        public const int HighestId = 63;
        public const int SlotCount = 8;

        public static ulong BitOf(byte id) => id <= HighestId ? 1UL << id : 0UL;

        public static ulong AddMask(in UiSwitches switches, int slot)
        {
            switch (slot)
            {
                case 0: return switches.Add0;
                case 1: return switches.Add1;
                case 2: return switches.Add2;
                case 3: return switches.Add3;
                case 4: return switches.Add4;
                case 5: return switches.Add5;
                case 6: return switches.Add6;
                case 7: return switches.Add7;
                default: return 0UL;
            }
        }

        public static ulong RemoveMask(in UiSwitches switches, int slot)
        {
            switch (slot)
            {
                case 0: return switches.Remove0;
                case 1: return switches.Remove1;
                case 2: return switches.Remove2;
                case 3: return switches.Remove3;
                case 4: return switches.Remove4;
                case 5: return switches.Remove5;
                case 6: return switches.Remove6;
                case 7: return switches.Remove7;
                default: return 0UL;
            }
        }

        public static ulong AttachMask(in UiSwitches switches, int slot)
        {
            switch (slot)
            {
                case 0: return switches.Attach0;
                case 1: return switches.Attach1;
                case 2: return switches.Attach2;
                case 3: return switches.Attach3;
                case 4: return switches.Attach4;
                case 5: return switches.Attach5;
                case 6: return switches.Attach6;
                case 7: return switches.Attach7;
                default: return 0UL;
            }
        }

        public static void Merge(ref UiSwitches total, in UiSwitches next)
        {
            total.Show |= next.Show;
            total.Hide |= next.Hide;
            total.Invisible |= next.Invisible;
            total.Enable |= next.Enable;
            total.Disable |= next.Disable;
            total.Add0 |= next.Add0;
            total.Add1 |= next.Add1;
            total.Add2 |= next.Add2;
            total.Add3 |= next.Add3;
            total.Add4 |= next.Add4;
            total.Add5 |= next.Add5;
            total.Add6 |= next.Add6;
            total.Add7 |= next.Add7;
            total.Remove0 |= next.Remove0;
            total.Remove1 |= next.Remove1;
            total.Remove2 |= next.Remove2;
            total.Remove3 |= next.Remove3;
            total.Remove4 |= next.Remove4;
            total.Remove5 |= next.Remove5;
            total.Remove6 |= next.Remove6;
            total.Remove7 |= next.Remove7;
            total.Attach0 |= next.Attach0;
            total.Attach1 |= next.Attach1;
            total.Attach2 |= next.Attach2;
            total.Attach3 |= next.Attach3;
            total.Attach4 |= next.Attach4;
            total.Attach5 |= next.Attach5;
            total.Attach6 |= next.Attach6;
            total.Attach7 |= next.Attach7;
        }
    }
}
