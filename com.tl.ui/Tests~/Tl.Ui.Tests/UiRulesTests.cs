using Tl.Ui;
using Xunit;

namespace Tl.Ui.Tests;

public sealed class UiRulesTests
{
    sealed class ClassNames : IUiClassList
    {
        readonly HashSet<string> _names;

        public ClassNames(params string[] names) => _names = new HashSet<string>(names);

        public bool HasClass(string className) => _names.Contains(className);
    }

    [Fact]
    public void ATargetAddressedByTwoIdsTakesTheStrongerSwitchOfEach()
    {
        var switches = new UiSwitches { Show = 1UL << 3, Hide = 1UL << 9, Enable = 1UL << 3, Disable = 1UL << 9, Add2 = 1UL << 3, Remove2 = 1UL << 9 };
        var addressedByThreeAndNine = UiRules.LookOf(switches, 1UL << 3 | 1UL << 9);
        Assert.Equal(UiDisplay.Hidden, addressedByThreeAndNine.Display);
        Assert.Equal(UiUsable.Unusable, addressedByThreeAndNine.Usable);
        Assert.False(addressedByThreeAndNine.ClassIsOn(2, authored: true));
        var addressedByThreeOnly = UiRules.LookOf(switches, 1UL << 3);
        Assert.Equal(UiDisplay.Shown, addressedByThreeOnly.Display);
        Assert.True(addressedByThreeOnly.IsUsable(authored: false));
        Assert.True(addressedByThreeOnly.ClassIsOn(2, authored: false));
        var untouched = UiRules.LookOf(switches, 1UL << 20);
        Assert.Equal(default(UiLook), untouched);
        Assert.True(untouched.IsUsable(authored: true));
        Assert.False(untouched.IsUsable(authored: false));
    }

    [Fact]
    public void TouchedIdsNamesExactlyTheIdsWhoseSwitchesChanged()
    {
        var before = new UiSwitches { Show = 0b0011, Attach7 = 1UL << 40 };
        var after = new UiSwitches { Show = 0b0110, Attach7 = 1UL << 40, Remove5 = 1UL << 63 };
        Assert.Equal(0b0101UL | 1UL << 63, UiRules.TouchedIds(before, after));
        Assert.Equal(0UL, UiRules.TouchedIds(after, after));
    }

    [Fact]
    public void TouchedTargetsOfChannelsNamesTheTargetsWhosePropertiesStartStopOrChange()
    {
        var before = default(UiChannels);
        before.Live = 0b011;
        before.Keys.Set(0, UiChannelKey.Of(new UiTargetRef { Id = 1 }, UiProperty.Opacity));
        before.Values.Set(0, 1f);
        before.Keys.Set(1, UiChannelKey.Of(new UiTargetRef { Id = 2 }, UiProperty.Scale));
        before.Values.Set(1, 2f);
        var after = before;
        after.Live = 0b110;
        after.Values.Set(1, 3f);
        after.Keys.Set(2, UiChannelKey.Of(new UiTargetRef { Id = 5 }, UiProperty.MoveY));
        after.Values.Set(2, 9f);
        Assert.Equal(1UL << 1 | 1UL << 2 | 1UL << 5, UiRules.TouchedTargetsOfChannels(before, after));
        var quietChange = after;
        quietChange.Values.Set(0, 42f);
        Assert.Equal(0UL, UiRules.TouchedTargetsOfChannels(after, quietChange));
    }

    [Fact]
    public void MergingTwoHostsKeepsOnePropertyPerTargetAndBothHostsValues()
    {
        var first = default(UiChannels);
        first.Live = 1;
        first.Held = 1;
        first.Keys.Set(0, UiChannelKey.Of(new UiTargetRef { Id = 3 }, UiProperty.Opacity));
        first.Values.Set(0, 0.25f);
        var second = default(UiChannels);
        second.Live = 0b100;
        second.Keys.Set(2, UiChannelKey.Of(new UiTargetRef { Id = 4 }, UiProperty.Rotate));
        second.Values.Set(2, 90f);
        var total = default(UiChannels);
        UiRules.MergeChannels(ref total, first);
        UiRules.MergeChannels(ref total, second);
        Assert.Equal((ushort)0b11, total.Live);
        Assert.Equal((ushort)0b01, total.Held);
        Assert.Equal(0.25f, total.Values.Get(UiChannelSlots.Find(total.Live, total.Keys, UiChannelKey.Of(new UiTargetRef { Id = 3 }, UiProperty.Opacity))));
        Assert.Equal(90f, total.Values.Get(UiChannelSlots.Find(total.Live, total.Keys, UiChannelKey.Of(new UiTargetRef { Id = 4 }, UiProperty.Rotate))));
    }

    [Fact]
    public void RevealedLengthRoundsAndClamps()
    {
        Assert.Equal(0, UiRules.RevealedLength(10, -1f));
        Assert.Equal(5, UiRules.RevealedLength(10, 0.5f));
        Assert.Equal(10, UiRules.RevealedLength(10, 2f));
        Assert.Equal(3, UiRules.RevealedLength(10, 0.26f));
    }

    [Fact]
    public void SelectorsParseIntoStepsThatMatchTypeNameAndClasses()
    {
        var steps = UiSelector.Parse("  .shop-card   Button#buy.primary ");
        Assert.Equal(2, steps.Length);
        Assert.True(steps[0].Matches("VisualElement", "card7", new ClassNames("shop-card", "big")));
        Assert.False(steps[0].Matches("VisualElement", "card7", new ClassNames("big")));
        Assert.True(steps[1].Matches("Button", "buy", new ClassNames("primary")));
        Assert.False(steps[1].Matches("Label", "buy", new ClassNames("primary")));
        Assert.False(steps[1].Matches("Button", "sell", new ClassNames("primary")));
        Assert.Empty(UiSelector.Parse(""));
    }
}
