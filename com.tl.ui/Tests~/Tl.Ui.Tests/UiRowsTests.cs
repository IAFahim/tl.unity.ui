using Tl.Ui;
using Xunit;

namespace Tl.Ui.Tests;

public sealed class UiRowsTests
{
    struct Slot
    {
        public int Item;
        public float Count;
    }

    [Fact]
    public void SameBytesComparesEveryByteOfARow()
    {
        Assert.True(UiRowChanges.SameBytes(new Slot { Item = 3, Count = 2f }, new Slot { Item = 3, Count = 2f }));
        Assert.False(UiRowChanges.SameBytes(new Slot { Item = 3, Count = 2f }, new Slot { Item = 3, Count = 2.5f }));
        Assert.False(UiRowChanges.SameBytes(new Slot { Item = 3 }, new Slot { Item = 4 }));
    }

    [Fact]
    public void ViewSlotsKeepAKeysSlotReuseFreedSlotsAndReleaseOnlyUnseenKeys()
    {
        var slots = new UiViewSlots(2);
        Span<int> released = stackalloc int[8];
        slots.BeginFrame();
        Assert.Equal(0, slots.Touch(100, out var firstIsNew));
        Assert.Equal(1, slots.Touch(200, out _));
        Assert.Equal(2, slots.Touch(300, out _));
        Assert.True(firstIsNew);
        Assert.Equal(0, slots.ReleaseUnseen(released));

        slots.BeginFrame();
        Assert.Equal(0, slots.Touch(100, out var stillThere));
        Assert.False(stillThere);
        Assert.Equal(2, slots.Touch(300, out _));
        Assert.Equal(1, slots.ReleaseUnseen(released));
        Assert.Equal(1, released[0]);
        Assert.False(slots.IsInUse(1));
        Assert.Equal(2, slots.LiveCount);

        slots.BeginFrame();
        slots.Touch(100, out _);
        slots.Touch(300, out _);
        Assert.Equal(1, slots.Touch(400, out var reused));
        Assert.True(reused);
        Assert.Equal(3, slots.SlotCount);
    }

    [Fact]
    public void ViewSlotsAllocateNothingOnceWarm()
    {
        var slots = new UiViewSlots(64);
        Span<int> released = stackalloc int[64];
        for (var warmUp = 0; warmUp < 3; warmUp++)
        {
            slots.BeginFrame();
            for (long key = 0; key < 50; key++)
                slots.Touch(key, out _);
            slots.ReleaseUnseen(released);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var frame = 0; frame < 100; frame++)
        {
            slots.BeginFrame();
            for (long key = frame % 2; key < 50; key += 1 + frame % 2)
                slots.Touch(key, out _);
            slots.ReleaseUnseen(released);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0L, allocated);
    }
}
