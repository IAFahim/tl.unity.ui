using Tl;

namespace Tl.Ui
{
    [TlClip("UI/Animate")]
    public struct AnimateClip
    {
        public UiTargetRef Target;
        public UiProperty Property;
        public float From;
        public float To;
        public TlCurve Ease;
        public bool Hold;
    }

    public struct AnimateTrack : ITlTrack<AnimateClip>
    {
        static float PropertyValueAt(in AnimateClip clip, float progress)
        {
            var eased = clip.Ease.Evaluate(progress);
            return clip.From + (clip.To - clip.From) * eased;
        }

        [Feeds(typeof(UiChannelKind))]
        public static UiChannelFeed MoveThePropertyFromFromToTo(in TlFrame<AnimateClip> frame)
            => UiChannelFeed.One(UiChannelKey.Of(frame.Clip.Target, frame.Clip.Property), PropertyValueAt(frame.Clip, frame.Progress), frame.Clip.Hold);

        [Pulse(typeof(UiChannelKind), On = ClipEvent.End)]
        public static UiChannelFeed HoldTheLastValueWhenTheClipEnds(in TlFrame<AnimateClip> frame)
            => frame.Clip.Hold
                ? UiChannelFeed.One(UiChannelKey.Of(frame.Clip.Target, frame.Clip.Property), PropertyValueAt(frame.Clip, frame.Direction > 0 ? 1f : 0f), true)
                : default;
    }

    [TlClip("UI/Text")]
    public struct TextClip
    {
        public UiTargetRef Target;
        public UiTextRef Text;
    }

    public struct TextTrack : ITlTrack<TextClip>
    {
        [Feeds(typeof(UiChannelKind))]
        public static UiChannelFeed ShowTheTextWhileOn(in TlFrame<TextClip> frame)
            => UiChannelFeed.One(UiChannelKey.OfText(frame.Clip.Target), frame.Clip.Text.Slot, false);
    }
}
