using TlUi;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace InventorySample
{
    [BurstCompile]
    public partial struct BagSystem : ISystem
    {
        static bool IsSlot(DynamicBuffer<InventorySlot> slots, int slot) => slot >= 0 && slot < slots.Length;

        static void UseOne(DynamicBuffer<InventorySlot> slots, int slot)
        {
            var used = slots[slot];
            if (used.Count == 0)
                return;
            used.Count--;
            if (used.Count == 0)
                used.Name = default;
            slots[slot] = used;
        }

        static void Swap(DynamicBuffer<InventorySlot> slots, int dragged, int droppedOn)
        {
            var draggedSlot = slots[dragged];
            slots[dragged] = slots[droppedOn];
            slots[droppedOn] = draggedSlot;
        }

        static void PutIn(DynamicBuffer<InventorySlot> slots, FixedString32Bytes name)
        {
            for (var slot = 0; slot < slots.Length; slot++)
                if (slots[slot].Count > 0 && slots[slot].Name == name)
                {
                    var stack = slots[slot];
                    stack.Count++;
                    slots[slot] = stack;
                    return;
                }
            for (var slot = 0; slot < slots.Length; slot++)
                if (slots[slot].Count == 0)
                {
                    slots[slot] = new InventorySlot { Name = name, Count = 1 };
                    return;
                }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<BagRules>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rules, slots, shop, inbox) in SystemAPI.Query<RefRO<BagRules>, DynamicBuffer<InventorySlot>, DynamicBuffer<ShopEntry>, DynamicBuffer<UiCommandInbox>>())
            {
                for (var index = 0; index < inbox.Length; index++)
                {
                    var command = inbox[index];
                    if (command.Code == rules.ValueRO.UseCode && IsSlot(slots, command.Payload))
                        UseOne(slots, command.Payload);
                    else if (command.Code == rules.ValueRO.MoveCode && IsSlot(slots, command.Payload) && IsSlot(slots, command.Target))
                        Swap(slots, command.Payload, command.Target);
                    else if (command.Code == rules.ValueRO.BuyCode && command.Payload >= 0 && command.Payload < shop.Length)
                        PutIn(slots, shop[command.Payload].Name);
                }
                inbox.Clear();
            }
        }
    }
}
