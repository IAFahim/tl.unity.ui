using Tl;

namespace Tl.Ui
{
    public struct UiChannelFeed
    {
        public ushort Fed;
        public ushort Holds;
        public UiChannelValues Values;

        public static UiChannelFeed One(byte channel, float value, bool hold)
        {
            var feed = default(UiChannelFeed);
            if (channel >= UiChannelValues.Count)
                return feed;
            var bit = (ushort)(1 << channel);
            feed.Fed = bit;
            feed.Holds = hold ? bit : (ushort)0;
            feed.Values.Set(channel, value);
            return feed;
        }
    }

    [TlState]
    public partial struct UiChannels
    {
        public ushort Live;
        public ushort Held;
        public UiChannelValues Values;
    }

    [TlKind]
    public struct UiChannelKind
    {
        public static void Combine(ref UiChannelFeed total, in UiChannelFeed next)
        {
            for (var channel = 0; channel < UiChannelValues.Count; channel++)
                if ((next.Fed & (1 << channel)) != 0)
                    total.Values.Set(channel, next.Values.Get(channel));
            total.Holds = (ushort)((total.Holds & ~next.Fed) | (next.Holds & next.Fed));
            total.Fed |= next.Fed;
        }

        public static void Apply(in UiChannelFeed total, ref UiChannels channels)
        {
            for (var channel = 0; channel < UiChannelValues.Count; channel++)
                if ((total.Fed & (1 << channel)) != 0)
                    channels.Values.Set(channel, total.Values.Get(channel));
            channels.Held = (ushort)((channels.Held & ~total.Fed) | (total.Holds & total.Fed));
            channels.Live = (ushort)(total.Fed | channels.Held);
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

        public float Get(int channel)
        {
            switch (channel)
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

        public void Set(int channel, float value)
        {
            switch (channel)
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
}
