using System;
using System.Collections.Generic;

namespace Tl.Ui
{
    public static class UiRowChanges
    {
        public static unsafe bool SameBytes<T>(T first, T second) where T : unmanaged
            => new ReadOnlySpan<byte>(&first, sizeof(T)).SequenceEqual(new ReadOnlySpan<byte>(&second, sizeof(T)));
    }

    public sealed class UiViewSlots
    {
        readonly Dictionary<long, int> _slotOfKey = new Dictionary<long, int>();
        long[] _keyOfSlot;
        int[] _frameOfSlot;
        bool[] _slotInUse;
        int[] _freeSlots;
        int _freeCount;
        int _slotCount;
        int _frame;

        public UiViewSlots(int capacity)
        {
            capacity = Math.Max(1, capacity);
            _keyOfSlot = new long[capacity];
            _frameOfSlot = new int[capacity];
            _slotInUse = new bool[capacity];
            _freeSlots = new int[capacity];
        }

        public int SlotCount => _slotCount;

        public int LiveCount => _slotOfKey.Count;

        public bool IsInUse(int slot) => slot >= 0 && slot < _slotCount && _slotInUse[slot];

        public bool TryGetSlot(long key, out int slot) => _slotOfKey.TryGetValue(key, out slot);

        void GrowTo(int capacity)
        {
            Array.Resize(ref _keyOfSlot, capacity);
            Array.Resize(ref _frameOfSlot, capacity);
            Array.Resize(ref _slotInUse, capacity);
            Array.Resize(ref _freeSlots, capacity);
        }

        public void BeginFrame() => _frame++;

        public int Touch(long key, out bool isNew)
        {
            if (_slotOfKey.TryGetValue(key, out var slot))
            {
                isNew = false;
                _frameOfSlot[slot] = _frame;
                return slot;
            }
            if (_freeCount > 0)
            {
                slot = _freeSlots[--_freeCount];
            }
            else
            {
                if (_slotCount == _keyOfSlot.Length)
                    GrowTo(_keyOfSlot.Length * 2);
                slot = _slotCount++;
            }
            _slotOfKey.Add(key, slot);
            _keyOfSlot[slot] = key;
            _frameOfSlot[slot] = _frame;
            _slotInUse[slot] = true;
            isNew = true;
            return slot;
        }

        public int ReleaseUnseen(Span<int> releasedSlots)
        {
            var released = 0;
            for (var slot = 0; slot < _slotCount && released < releasedSlots.Length; slot++)
            {
                if (!_slotInUse[slot] || _frameOfSlot[slot] == _frame)
                    continue;
                _slotOfKey.Remove(_keyOfSlot[slot]);
                _slotInUse[slot] = false;
                _freeSlots[_freeCount++] = slot;
                releasedSlots[released++] = slot;
            }
            return released;
        }
    }
}
