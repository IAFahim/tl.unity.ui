using System;
using System.Collections.Generic;
using TlUi;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace InventorySample
{
    [AddComponentMenu("Tl UI Samples/Bag")]
    public sealed class BagAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct StartingItem
        {
            [Tooltip("The item's name, as the shop and the bag show it.")]
            public string Name;

            [Tooltip("How many of it the bag starts with.")]
            [Min(1)]
            public int Count;
        }

        [Tooltip("How many slots the bag has.")]
        [Min(1)]
        public int SlotCount = 6;

        [Tooltip("What the bag holds when the game starts, from the first slot on.")]
        public List<StartingItem> StartingItems = new List<StartingItem>();

        [Tooltip("The shop's rows; the same asset the Tl UI Panel's shop list shows, so a buy code's row number names the item.")]
        public TlUiAssetRows Shop;

        [Header("Codes the Tl UI Panel's lists send")]
        [Tooltip("Click Code of the bag list: use one of the clicked slot's item.")]
        public int UseCode = 1;

        [Tooltip("Drag Code of the bag list: swap the dragged slot with the one it was dropped on.")]
        public int MoveCode = 2;

        [Tooltip("Click Code of the shop list: put the clicked shop item in the bag.")]
        public int BuyCode = 3;

        sealed class Baker : Baker<BagAuthoring>
        {
            public override void Bake(BagAuthoring authoring)
            {
                var bag = GetEntity(TransformUsageFlags.None);
                var slots = AddBuffer<InventorySlot>(bag);
                for (var slot = 0; slot < authoring.SlotCount; slot++)
                {
                    var starting = slot < authoring.StartingItems.Count ? authoring.StartingItems[slot] : default;
                    slots.Add(string.IsNullOrEmpty(starting.Name) ? default : new InventorySlot { Name = new FixedString32Bytes(starting.Name), Count = starting.Count });
                }
                var shop = AddBuffer<ShopEntry>(bag);
                if (authoring.Shop == null)
                {
                    Debug.LogError("Bag: assign the Shop rows asset, the same one the shop list on the Tl UI Panel shows.", authoring);
                }
                else
                {
                    DependsOn(authoring.Shop);
                    foreach (var row in authoring.Shop.Items)
                    {
                        DependsOn(row);
                        shop.Add(new ShopEntry { Name = row is ShopItem item ? new FixedString32Bytes(item.DisplayName) : default });
                    }
                }
                AddComponent(bag, new BagRules { UseCode = authoring.UseCode, MoveCode = authoring.MoveCode, BuyCode = authoring.BuyCode });
            }
        }
    }
}
