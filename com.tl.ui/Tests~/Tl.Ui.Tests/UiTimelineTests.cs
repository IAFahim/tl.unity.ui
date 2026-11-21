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

    static readonly AnimateClip FadeInAndHold = new() { Channel = 0, Value = RampFromZeroToOne(), Hold = true };
    static readonly AnimateClip HalfWhileOn = new() { Channel = 1, Value = TlCurve.Constant(0.5f), Hold = false };

    static byte[] BasicsLibrary()
    {
        var basics = new TypedTimeline
        {
            Name = "basics",
            Duration = TimelineTicks,
            Tracks =
            {
                TrackOf(0, 60, new ShowClip { Id = 0 }),
                TrackOf(40, 50, new HideClip { Id = 0 }),
                TrackOf(30, 90, new HideClip { Id = 1, KeepSpace = true }),
                TrackOf(10, 20, new AddClassClip { Id = 2, ClassSlot = 3 }),
                TrackOf(15, 25, new RemoveClassClip { Id = 2, ClassSlot = 3 }),
                TrackOf(20, 70, new DisableClip { Id = 4 }),
                TrackOf(50, 60, new EnableClip { Id = 4 }),
                TrackOf(70, 80, new AttachClip { Id = 5, TemplateSlot = 1 }),
                TrackOf(0, 60, FadeInAndHold),
                TrackOf(20, 40, HalfWhileOn),
                TrackOf(60, 90, new TextClip { Channel = 2, TextSlot = 4 }),
                TrackOf(45, 46, new SignalClip { Code = 7 }),
                TrackOf(50, 51, new FocusClip { Id = 3 }),
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

    [Fact]
    public void AnimateFollowsItsCurveAndHoldsWhileTextAndUnheldChannelsRestWhenTheirClipEnds()
    {
        var records = PlayBasics(TimelineTicks + 10, 0, out _);
        var lastFadeValue = FadeInAndHold.Value.Evaluate(59 / 60f);
        foreach (var record in records)
        {
            var tick = record.Tick;
            var channels = record.Channels;
            var playing = tick < TimelineTicks;
            Assert.True((channels.Live & 1) != 0);
            var expectedFade = tick < 60 ? FadeInAndHold.Value.Evaluate(tick / 60f) : lastFadeValue;
            Assert.Equal(BitConverter.SingleToInt32Bits(expectedFade), BitConverter.SingleToInt32Bits(channels.Values.Get(0)));
            Assert.Equal(playing && Within(tick, 20, 40), (channels.Live & 2) != 0);
            if ((channels.Live & 2) != 0)
                Assert.Equal(0.5f, channels.Values.Get(1));
            Assert.Equal(playing && Within(tick, 60, 90), (channels.Live & 4) != 0);
            if ((channels.Live & 4) != 0)
                Assert.Equal(4f, channels.Values.Get(2));
            Assert.Equal(1, channels.Held);
        }
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
