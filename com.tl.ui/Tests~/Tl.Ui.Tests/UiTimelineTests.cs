using System.Runtime.InteropServices;
using Tl.Gen.Tlb;
using Tl.Ui;
using Xunit;

namespace Tl.Ui.Tests;

public sealed class UiTimelineTests
{
    const int TicksPerSecond = 60;
    const int TimelineTicks = 120;
    const ushort BasicsTimeline = 1;

    static byte[] BytesOf<T>(T clip) where T : unmanaged => MemoryMarshal.AsBytes(new Span<T>(ref clip)).ToArray();

    static TypedTrack TrackOf<T>(uint startTick, uint endTick, T clip) where T : unmanaged
    {
        var track = new TypedTrack { Type = typeof(TlNoSettings<T>) };
        track.Clips.Add(new TypedClip { Type = typeof(T), Start = startTick, End = endTick, Data = BytesOf(clip) });
        return track;
    }

    static TlCurve RampFromZeroToOne()
    {
        var curve = default(TlCurve);
        for (var sample = 0; sample < TlCurve.Samples; sample++)
            curve[sample] = sample / (float)(TlCurve.Samples - 1);
        return curve;
    }

    static UiTargetRef Target(byte id) => new() { Id = id };

    static readonly AnimateClip FadeInAndHold = new() { Target = Target(6), Property = UiProperty.Opacity, From = 0f, To = 1f, Ease = RampFromZeroToOne(), Hold = true };
    static readonly AnimateClip SlideInWhileOn = new() { Target = Target(7), Property = UiProperty.MoveX, From = -300f, To = 0f, Ease = RampFromZeroToOne(), Hold = false };
    static readonly TextClip CaptionWhileOn = new() { Target = Target(5), Text = new UiTextRef { Slot = 4 } };

    static byte[] BasicsLibrary()
    {
        var basics = new TypedTimeline
        {
            Name = "basics",
            Duration = TimelineTicks,
            Tracks =
            {
                TrackOf(0, 60, new ShowClip { Target = Target(0) }),
                TrackOf(40, 50, new HideClip { Target = Target(0) }),
                TrackOf(30, 90, new HideClip { Target = Target(1), KeepSpace = true }),
                TrackOf(10, 20, new AddClassClip { Target = Target(2), Class = new UiClassRef { Slot = 3 } }),
                TrackOf(15, 25, new RemoveClassClip { Target = Target(2), Class = new UiClassRef { Slot = 3 } }),
                TrackOf(20, 70, new DisableClip { Target = Target(4) }),
                TrackOf(50, 60, new EnableClip { Target = Target(4) }),
                TrackOf(70, 80, new AttachClip { Target = Target(5), Template = new UiTemplateRef { Slot = 1 } }),
                TrackOf(0, 60, FadeInAndHold),
                TrackOf(20, 40, SlideInWhileOn),
                TrackOf(60, 90, CaptionWhileOn),
                TrackOf(45, 46, new SignalClip { Code = 7 }),
                TrackOf(50, 51, new FocusClip { Target = Target(3) }),
            },
        };
        var manifest = LibraryBake.Manifest(new List<string> { basics.Name! }, [], TicksPerSecond);
        var kinds = LibraryBake.Kinds([typeof(UiSwitches).Assembly]);
        var tlb = LibraryBake.BakeTyped(basics, manifest);
        var part = new LibraryPart { Name = basics.Name!, Tlb = tlb, Kinds = LibraryBake.KindPayload(basics.Name!, tlb, kinds), Next = (ushort)LibraryAssembler.NextOf(tlb) };
        return LibraryAssembler.Assemble([part], TicksPerSecond, LibrarySettings.Default);
    }

    static bool On(ulong mask, int id) => (mask >> id & 1UL) != 0;

    static bool Within(int tick, int startTick, int endTick) => tick >= startTick && tick < endTick;

    readonly struct TickRecord
    {
        public TickRecord(int tick, UiSwitches switches, UiChannels channels, int[] signalCodes, int[] focusIds)
        {
            Tick = tick;
            Switches = switches;
            Channels = channels;
            SignalCodes = signalCodes;
            FocusIds = focusIds;
        }

        public readonly int Tick;
        public readonly UiSwitches Switches;
        public readonly UiChannels Channels;
        public readonly int[] SignalCodes;
        public readonly int[] FocusIds;
    }

    static List<TickRecord> PlayBasics(int steps, int replayAtStep, out long allocatedAfterReplay)
    {
        using var world = new TimelineWorld(BasicsLibrary());
        using var players = new PlaybackTable(1);
        using var kinds = new KindStore(world, players);
        using var signals = new Events<UiSignal>(16);
        using var focus = new Events<UiFocus>(16);
        var switchColumn = new UiSwitches[1];
        var channelColumn = new UiChannels[1];
        var records = new List<TickRecord>(steps);
        players.Start(0, BasicsTimeline);
        allocatedAfterReplay = 0;
        for (var step = 1; step <= steps; step++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            world.Tick(players, true);
            new Tl.Generated.TlAuto_Tl_Ui { C0_UiChannels = channelColumn, C1_UiSwitches = switchColumn, E0_UiSignal = signals.Writer, E1_UiFocus = focus.Writer }.Run(world, players, true);
            var drainedSignals = signals.Drain();
            var drainedFocus = focus.Drain();
            var allocatedThisStep = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            if (replayAtStep > 0 && step > replayAtStep)
                allocatedAfterReplay += allocatedThisStep;
            if (step == replayAtStep)
                players.Start(0, BasicsTimeline);
            var signalCodes = new int[drainedSignals.Length];
            for (var i = 0; i < signalCodes.Length; i++)
                signalCodes[i] = drainedSignals[i].Value.Code;
            var focusIds = new int[drainedFocus.Length];
            for (var i = 0; i < focusIds.Length; i++)
                focusIds[i] = drainedFocus[i].Value.Id;
            records.Add(new TickRecord(step - 1, switchColumn[0], channelColumn[0], signalCodes, focusIds));
        }
        return records;
    }

    [Fact]
    public void EverySwitchClipIsOnExactlyOnItsTicksAndTheColumnRestsAfterTheEnd()
    {
        foreach (var record in PlayBasics(TimelineTicks + 10, 0, out _))
        {
            var tick = record.Tick;
            var switches = record.Switches;
            var playing = tick < TimelineTicks;
            Assert.Equal(playing && Within(tick, 0, 60), On(switches.Show, 0));
            Assert.Equal(playing && Within(tick, 40, 50), On(switches.Hide, 0));
            Assert.Equal(playing && Within(tick, 30, 90), On(switches.Invisible, 1));
            Assert.Equal(playing && Within(tick, 10, 20), On(switches.Add3, 2));
            Assert.Equal(playing && Within(tick, 15, 25), On(switches.Remove3, 2));
            Assert.Equal(playing && Within(tick, 20, 70), On(switches.Disable, 4));
            Assert.Equal(playing && Within(tick, 50, 60), On(switches.Enable, 4));
            Assert.Equal(playing && Within(tick, 70, 80), On(switches.Attach1, 5));
            if (!playing)
                Assert.Equal(0UL, UiRules.TouchedIds(default, switches));
        }
    }

    [Fact]
    public void TheLookOfATargetFollowsHideBeatsShowDisableBeatsEnableAndRemoveBeatsAdd()
    {
        foreach (var record in PlayBasics(TimelineTicks, 0, out _))
        {
            var tick = record.Tick;
            var shownPanel = UiRules.LookOf(record.Switches, 1UL << 0);
            var expectedDisplay = Within(tick, 40, 50) ? UiDisplay.Hidden : Within(tick, 0, 60) ? UiDisplay.Shown : UiDisplay.Authored;
            Assert.Equal(expectedDisplay, shownPanel.Display);
            var styledCard = UiRules.LookOf(record.Switches, 1UL << 2);
            Assert.Equal(Within(tick, 10, 15), styledCard.ClassIsOn(3, authored: false));
            Assert.Equal(!Within(tick, 15, 25), styledCard.ClassIsOn(3, authored: true));
            var button = UiRules.LookOf(record.Switches, 1UL << 4);
            Assert.Equal(!Within(tick, 20, 70), button.IsUsable(authored: true));
            var hint = UiRules.LookOf(record.Switches, 1UL << 1);
            Assert.Equal(Within(tick, 30, 90) ? UiVisibility.Invisible : UiVisibility.Authored, hint.Visibility);
            var slot = UiRules.LookOf(record.Switches, 1UL << 5);
            Assert.Equal(Within(tick, 70, 80), slot.TemplateIsAttached(1));
        }
    }

    static bool TryRead(in UiChannels channels, ushort key, out float value, out bool held)
    {
        var slot = UiChannelSlots.Find(channels.Live, channels.Keys, key);
        value = slot >= 0 ? channels.Values.Get(slot) : 0f;
        held = slot >= 0 && (channels.Held & (1 << slot)) != 0;
        return slot >= 0;
    }

    [Fact]
    public void AnimateMovesItsTargetsPropertyFromFromToToAndHoldsWhileTextAndUnheldPropertiesRestWhenTheirClipEnds()
    {
        var records = PlayBasics(TimelineTicks + 10, 0, out _);
        var fade = UiChannelKey.Of(FadeInAndHold.Target, UiProperty.Opacity);
        var slide = UiChannelKey.Of(SlideInWhileOn.Target, UiProperty.MoveX);
        var caption = UiChannelKey.OfText(CaptionWhileOn.Target);
        foreach (var record in records)
        {
            var tick = record.Tick;
            var channels = record.Channels;
            var playing = tick < TimelineTicks;
            Assert.True(TryRead(channels, fade, out var opacity, out var fadeHeld));
            Assert.True(fadeHeld);
            var expectedOpacity = tick < 59 ? FadeInAndHold.Ease.Evaluate(tick / 60f) : FadeInAndHold.Ease.Evaluate(1f);
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedOpacity), BitConverter.SingleToInt32Bits(opacity));
            Assert.Equal(playing && Within(tick, 20, 40), TryRead(channels, slide, out var moveX, out var slideHeld));
            if (playing && Within(tick, 20, 40))
            {
                Assert.False(slideHeld);
                Assert.Equal(-300f + 300f * SlideInWhileOn.Ease.Evaluate((tick - 20) / 20f), moveX, 3);
            }
            Assert.Equal(playing && Within(tick, 60, 90), TryRead(channels, caption, out var textSlot, out _));
            if (playing && Within(tick, 60, 90))
                Assert.Equal(4f, textSlot);
        }
    }

    [Fact]
    public void ChannelKeysCarryTheTargetAndThePropertySoTwoClipsOnOneTargetDoNotCollide()
    {
        var opacity = UiChannelKey.Of(Target(9), UiProperty.Opacity);
        var scale = UiChannelKey.Of(Target(9), UiProperty.Scale);
        Assert.NotEqual(opacity, scale);
        Assert.Equal(9, UiChannelKey.TargetOf(scale));
        Assert.Equal((byte)UiProperty.Scale, UiChannelKey.PropertyOf(scale));
        Assert.Equal(UiChannelKey.TextProperty, UiChannelKey.PropertyOf(UiChannelKey.OfText(Target(63))));
        Assert.Equal(63, UiChannelKey.TargetOf(UiChannelKey.OfText(Target(63))));
    }

    [Fact]
    public void SignalAndFocusAreRaisedOnceOnTheTickTheirClipStarts()
    {
        var records = PlayBasics(TimelineTicks, 0, out _);
        Assert.Equal(new[] { 45 }, records.Where(r => r.SignalCodes.Length > 0).Select(r => r.Tick).ToArray());
        Assert.Equal(new[] { 7 }, records.SelectMany(r => r.SignalCodes).ToArray());
        Assert.Equal(new[] { 50 }, records.Where(r => r.FocusIds.Length > 0).Select(r => r.Tick).ToArray());
        Assert.Equal(new[] { 3 }, records.SelectMany(r => r.FocusIds).ToArray());
    }

    [Fact]
    public void ASecondPlayOfTheBasicsTimelineReplaysEveryClipAndAllocatesNothing()
    {
        const int replayAtStep = TimelineTicks + 5;
        var records = PlayBasics(replayAtStep + TimelineTicks, replayAtStep, out var allocatedDuringReplay);
        var replay = records.Where(r => r.Tick >= replayAtStep).ToList();
        Assert.Equal(60, replay.Count(r => On(r.Switches.Show, 0)));
        Assert.Equal(new[] { 7 }, replay.SelectMany(r => r.SignalCodes).ToArray());
        Assert.Equal(new[] { 3 }, replay.SelectMany(r => r.FocusIds).ToArray());
        Assert.Equal(0L, allocatedDuringReplay);
    }
}
