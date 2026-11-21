using Tl;

namespace Tl.Ui
{
    public struct UiSignal
    {
        public byte Code;
    }

    public struct UiFocus
    {
        public byte Id;
    }

    [TlClip("UI/Signal")]
    public struct SignalClip
    {
        public byte Code;
    }

    public struct SignalTrack : ITlTrack<SignalClip>
    {
        public static void OnClipStart(in TlFrame<SignalClip> frame, ref Events<UiSignal> signals)
        {
            if (frame.Direction > 0)
                signals.Raise(new UiSignal { Code = frame.Clip.Code });
        }
    }

    [TlClip("UI/Focus")]
    public struct FocusClip
    {
        public byte Id;
    }

    public struct FocusTrack : ITlTrack<FocusClip>
    {
        public static void OnClipStart(in TlFrame<FocusClip> frame, ref Events<UiFocus> focus)
        {
            if (frame.Direction > 0)
                focus.Raise(new UiFocus { Id = frame.Clip.Id });
        }
    }
}
