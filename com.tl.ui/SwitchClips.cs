using Tl;

namespace Tl.Ui
{
    [TlClip("UI/Show")]
    public struct ShowClip
    {
        public UiTargetRef Target;
    }

    public struct ShowTrack : ITlTrack<ShowClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches ShowTheTargetWhileOn(in TlFrame<ShowClip> frame)
            => new UiSwitches { Show = frame.Clip.Target.Bit };
    }

    [TlClip("UI/Hide")]
    public struct HideClip
    {
        public UiTargetRef Target;
        public bool KeepSpace;
    }

    public struct HideTrack : ITlTrack<HideClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches HideTheTargetWhileOn(in TlFrame<HideClip> frame)
            => frame.Clip.KeepSpace
                ? new UiSwitches { Invisible = frame.Clip.Target.Bit }
                : new UiSwitches { Hide = frame.Clip.Target.Bit };
    }

    [TlClip("UI/Enable")]
    public struct EnableClip
    {
        public UiTargetRef Target;
    }

    public struct EnableTrack : ITlTrack<EnableClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches EnableTheTargetWhileOn(in TlFrame<EnableClip> frame)
            => new UiSwitches { Enable = frame.Clip.Target.Bit };
    }

    [TlClip("UI/Disable")]
    public struct DisableClip
    {
        public UiTargetRef Target;
    }

    public struct DisableTrack : ITlTrack<DisableClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches DisableTheTargetWhileOn(in TlFrame<DisableClip> frame)
            => new UiSwitches { Disable = frame.Clip.Target.Bit };
    }

    [TlClip("UI/Add Class")]
    public struct AddClassClip
    {
        public UiTargetRef Target;
        public UiClassRef Class;
    }

    public struct AddClassTrack : ITlTrack<AddClassClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches AddTheClassWhileOn(in TlFrame<AddClassClip> frame)
        {
            var target = frame.Clip.Target.Bit;
            switch (frame.Clip.Class.Slot)
            {
                case 0: return new UiSwitches { Add0 = target };
                case 1: return new UiSwitches { Add1 = target };
                case 2: return new UiSwitches { Add2 = target };
                case 3: return new UiSwitches { Add3 = target };
                case 4: return new UiSwitches { Add4 = target };
                case 5: return new UiSwitches { Add5 = target };
                case 6: return new UiSwitches { Add6 = target };
                case 7: return new UiSwitches { Add7 = target };
                default: return default;
            }
        }
    }

    [TlClip("UI/Remove Class")]
    public struct RemoveClassClip
    {
        public UiTargetRef Target;
        public UiClassRef Class;
    }

    public struct RemoveClassTrack : ITlTrack<RemoveClassClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches RemoveTheClassWhileOn(in TlFrame<RemoveClassClip> frame)
        {
            var target = frame.Clip.Target.Bit;
            switch (frame.Clip.Class.Slot)
            {
                case 0: return new UiSwitches { Remove0 = target };
                case 1: return new UiSwitches { Remove1 = target };
                case 2: return new UiSwitches { Remove2 = target };
                case 3: return new UiSwitches { Remove3 = target };
                case 4: return new UiSwitches { Remove4 = target };
                case 5: return new UiSwitches { Remove5 = target };
                case 6: return new UiSwitches { Remove6 = target };
                case 7: return new UiSwitches { Remove7 = target };
                default: return default;
            }
        }
    }

    [TlClip("UI/Attach")]
    public struct AttachClip
    {
        public UiTargetRef Target;
        public UiTemplateRef Template;
    }

    public struct AttachTrack : ITlTrack<AttachClip>
    {
        [Feeds(typeof(While<UiSwitches>))]
        public static UiSwitches AttachTheTemplateWhileOn(in TlFrame<AttachClip> frame)
        {
            var target = frame.Clip.Target.Bit;
            switch (frame.Clip.Template.Slot)
            {
                case 0: return new UiSwitches { Attach0 = target };
                case 1: return new UiSwitches { Attach1 = target };
                case 2: return new UiSwitches { Attach2 = target };
                case 3: return new UiSwitches { Attach3 = target };
                case 4: return new UiSwitches { Attach4 = target };
                case 5: return new UiSwitches { Attach5 = target };
                case 6: return new UiSwitches { Attach6 = target };
                case 7: return new UiSwitches { Attach7 = target };
                default: return default;
            }
        }
    }
}
