using Tl;

namespace Tl.Ui
{
    public static class UiChannelKey
    {
        public const int PropertyBits = 4;
        public const byte TextProperty = 15;

        public static ushort Of(UiTargetRef target, UiProperty property) => (ushort)(target.Id << PropertyBits | (byte)property);

        public static ushort OfText(UiTargetRef target) => (ushort)(target.Id << PropertyBits | TextProperty);

        public static byte TargetOf(ushort key) => (byte)(key >> PropertyBits);

        public static byte PropertyOf(ushort key) => (byte)(key & ((1 << PropertyBits) - 1));
    }

    public static class UiChannelSlots
    {
        public static int Find(ushort used, in UiChannelKeys keys, ushort key)
        {
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
                if ((used & (1 << slot)) != 0 && keys.Get(slot) == key)
                    return slot;
            return -1;
        }

        public static int FindOrTakeFree(ushort used, in UiChannelKeys keys, ushort key)
        {
            var found = Find(used, keys, key);
            if (found >= 0)
                return found;
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
                if ((used & (1 << slot)) == 0)
                    return slot;
            return -1;
        }

        public static bool Write(ref UiChannels channels, ushort key, float value, bool hold)
        {
            var slot = FindOrTakeFree(channels.Live, channels.Keys, key);
            if (slot < 0)
                return false;
            var bit = (ushort)(1 << slot);
            channels.Keys.Set(slot, key);
            channels.Values.Set(slot, value);
            channels.Live |= bit;
            channels.Held = hold ? (ushort)(channels.Held | bit) : (ushort)(channels.Held & ~bit);
            return true;
        }
    }

    public struct UiChannelFeed
    {
        public ushort Fed;
        public ushort Holds;
        public UiChannelKeys Keys;
        public UiChannelValues Values;

        public static UiChannelFeed One(ushort key, float value, bool hold)
        {
            var feed = default(UiChannelFeed);
            feed.Fed = 1;
            feed.Holds = hold ? (ushort)1 : (ushort)0;
            feed.Keys.Set(0, key);
            feed.Values.Set(0, value);
            return feed;
        }
    }

    [TlState]
    public partial struct UiChannels
    {
        public ushort Live;
        public ushort Held;
        public UiChannelKeys Keys;
        public UiChannelValues Values;
    }

    [TlKind(Accepts = KindEntries.Both)]
    public struct UiChannelKind
    {
        public static void Combine(ref UiChannelFeed total, in UiChannelFeed next)
        {
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                if ((next.Fed & (1 << slot)) == 0)
                    continue;
                var key = next.Keys.Get(slot);
                var into = UiChannelSlots.FindOrTakeFree(total.Fed, total.Keys, key);
                if (into < 0)
                    continue;
                var intoBit = (ushort)(1 << into);
                total.Keys.Set(into, key);
                total.Values.Set(into, next.Values.Get(slot));
                total.Fed |= intoBit;
                total.Holds = (next.Holds & (1 << slot)) != 0 ? (ushort)(total.Holds | intoBit) : (ushort)(total.Holds & ~intoBit);
            }
        }

        public static void Apply(in UiChannelFeed total, ref UiChannels channels)
        {
            var live = channels.Held;
            for (var slot = 0; slot < UiChannelValues.Count; slot++)
            {
                if ((total.Fed & (1 << slot)) == 0)
                    continue;
                var key = total.Keys.Get(slot);
                var into = UiChannelSlots.FindOrTakeFree(live, channels.Keys, key);
                if (into < 0)
                    continue;
                var intoBit = (ushort)(1 << into);
                channels.Keys.Set(into, key);
                channels.Values.Set(into, total.Values.Get(slot));
                live |= intoBit;
                channels.Held = (total.Holds & (1 << slot)) != 0 ? (ushort)(channels.Held | intoBit) : (ushort)(channels.Held & ~intoBit);
            }
            channels.Live = live;
        }

        public static void Idle(ref UiChannels channels) => channels.Live = channels.Held;
    }

    public struct UiChannelValues
    {
        public const int Count = 16;

        public float C0;
        public float C1;
        public float C2;
        public float C3;
        public float C4;
        public float C5;
        public float C6;
        public float C7;
        public float C8;
        public float C9;
        public float C10;
        public float C11;
        public float C12;
        public float C13;
        public float C14;
        public float C15;

        public float Get(int slot)
        {
            switch (slot)
            {
                case 0: return C0;
                case 1: return C1;
                case 2: return C2;
                case 3: return C3;
                case 4: return C4;
                case 5: return C5;
                case 6: return C6;
                case 7: return C7;
                case 8: return C8;
                case 9: return C9;
                case 10: return C10;
                case 11: return C11;
                case 12: return C12;
                case 13: return C13;
                case 14: return C14;
                case 15: return C15;
                default: return 0f;
            }
        }

        public void Set(int slot, float value)
        {
            switch (slot)
            {
                case 0: C0 = value; break;
                case 1: C1 = value; break;
                case 2: C2 = value; break;
                case 3: C3 = value; break;
                case 4: C4 = value; break;
                case 5: C5 = value; break;
                case 6: C6 = value; break;
                case 7: C7 = value; break;
                case 8: C8 = value; break;
                case 9: C9 = value; break;
                case 10: C10 = value; break;
                case 11: C11 = value; break;
                case 12: C12 = value; break;
                case 13: C13 = value; break;
                case 14: C14 = value; break;
                case 15: C15 = value; break;
            }
        }
    }

    public struct UiChannelKeys
    {
        public ushort K0;
        public ushort K1;
        public ushort K2;
        public ushort K3;
        public ushort K4;
        public ushort K5;
        public ushort K6;
        public ushort K7;
        public ushort K8;
        public ushort K9;
        public ushort K10;
        public ushort K11;
        public ushort K12;
        public ushort K13;
        public ushort K14;
        public ushort K15;

        public ushort Get(int slot)
        {
            switch (slot)
            {
                case 0: return K0;
                case 1: return K1;
                case 2: return K2;
                case 3: return K3;
                case 4: return K4;
                case 5: return K5;
                case 6: return K6;
                case 7: return K7;
                case 8: return K8;
                case 9: return K9;
                case 10: return K10;
                case 11: return K11;
                case 12: return K12;
                case 13: return K13;
                case 14: return K14;
                case 15: return K15;
                default: return 0;
            }
        }

        public void Set(int slot, ushort key)
        {
            switch (slot)
            {
                case 0: K0 = key; break;
                case 1: K1 = key; break;
                case 2: K2 = key; break;
                case 3: K3 = key; break;
                case 4: K4 = key; break;
                case 5: K5 = key; break;
                case 6: K6 = key; break;
                case 7: K7 = key; break;
                case 8: K8 = key; break;
                case 9: K9 = key; break;
                case 10: K10 = key; break;
                case 11: K11 = key; break;
                case 12: K12 = key; break;
                case 13: K13 = key; break;
                case 14: K14 = key; break;
                case 15: K15 = key; break;
            }
        }
    }
}
