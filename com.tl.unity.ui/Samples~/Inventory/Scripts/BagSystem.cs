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

        static void Equip(DynamicBuffer<InventorySlot> slots, DynamicBuffer<EquippedItem> equipment, int bagSlot, int droppedOnEquipmentRow)
        {
            var item = slots[bagSlot];
            if (item.Count == 0 || equipment.Length == 0)
                return;
            var into = droppedOnEquipmentRow >= 0 && droppedOnEquipmentRow < equipment.Length ? droppedOnEquipmentRow : FirstEmptyEquipment(equipment);
            var worn = equipment[into];
            slots[bagSlot] = worn.Count > 0 ? new InventorySlot { Name = worn.Name, Count = worn.Count } : default;
            worn.Name = item.Name;
            worn.Count = item.Count;
            equipment[into] = worn;
        }

        static int FirstEmptyEquipment(DynamicBuffer<EquippedItem> equipment)
        {
            for (var row = 0; row < equipment.Length; row++)
                if (equipment[row].Count == 0)
                    return row;
            return 0;
        }

        static void Unequip(DynamicBuffer<InventorySlot> slots, DynamicBuffer<EquippedItem> equipment, int equipmentRow, int droppedOnBagSlot)
        {
            var worn = equipment[equipmentRow];
            if (worn.Count == 0)
                return;
            if (IsSlot(slots, droppedOnBagSlot))
            {
                var there = slots[droppedOnBagSlot];
                slots[droppedOnBagSlot] = new InventorySlot { Name = worn.Name, Count = worn.Count };
                worn.Name = there.Name;
                worn.Count = there.Count;
                equipment[equipmentRow] = worn;
                return;
            }
            for (var slot = 0; slot < slots.Length; slot++)
                if (slots[slot].Count == 0)
                {
                    slots[slot] = new InventorySlot { Name = worn.Name, Count = worn.Count };
                    worn.Name = default;
                    worn.Count = 0;
                    equipment[equipmentRow] = worn;
                    return;
                }
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<BagRules>();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (rules, slots, equipment, shop, inbox) in SystemAPI.Query<RefRO<BagRules>, DynamicBuffer<InventorySlot>, DynamicBuffer<EquippedItem>, DynamicBuffer<ShopEntry>, DynamicBuffer<UiCommandInbox>>())
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
                    else if (command.Code == rules.ValueRO.EquipCode && IsSlot(slots, command.Payload))
                        Equip(slots, equipment, command.Payload, command.Target);
                    else if (command.Code == rules.ValueRO.UnequipCode && command.Payload >= 0 && command.Payload < equipment.Length)
                        Unequip(slots, equipment, command.Payload, command.Target);
                }
                inbox.Clear();
            }
        }
    }
}
