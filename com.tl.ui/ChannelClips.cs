using Tl;

namespace Tl.Ui
{
    [TlClip("UI/Animate")]
    public struct AnimateClip
    {
        public byte Channel;
        public TlCurve Value;
        public bool Hold;
    }

    public struct AnimateTrack : ITlTrack<AnimateClip>
    {
        [Feeds(typeof(UiChannelKind))]
        public static UiChannelFeed MoveTheChannelAlongTheCurve(in TlFrame<AnimateClip> frame)
            => UiChannelFeed.One(frame.Clip.Channel, frame.Clip.Value.Evaluate(frame.Progress), frame.Clip.Hold);

        [Pulse(typeof(UiChannelKind), On = ClipEvent.End)]
        public static UiChannelFeed HoldTheLastKeyWhenTheClipEnds(in TlFrame<AnimateClip> frame)
            => frame.Clip.Hold
                ? UiChannelFeed.One(frame.Clip.Channel, frame.Clip.Value.Evaluate(frame.Direction > 0 ? 1f : 0f), true)
                : default;
    }

    [TlClip("UI/Text")]
    public struct TextClip
    {
        public byte Channel;
        public byte TextSlot;
    }

    public struct TextTrack : ITlTrack<TextClip>
    {
        [Feeds(typeof(UiChannelKind))]
        public static UiChannelFeed ShowTheTextSlotWhileOn(in TlFrame<TextClip> frame)
            => UiChannelFeed.One(frame.Clip.Channel, frame.Clip.TextSlot, false);
    }
}
