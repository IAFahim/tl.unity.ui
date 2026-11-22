using Unity.Properties;
using UnityEngine;

namespace InventorySample
{
    [CreateAssetMenu(menuName = "Tl UI Samples/Shop Item", fileName = "Shop Item")]
    public sealed class ShopItem : ScriptableObject
    {
        [Tooltip("The name the shop shows and the bag stores.")]
        [CreateProperty]
        public string DisplayName = "Item";

        [Tooltip("The price the shop shows, in coins.")]
        [CreateProperty]
        [Min(0)]
        public int Price = 10;

        [Tooltip("What the shop row's tooltip says.")]
        [CreateProperty]
        public string Description = "";
    }
}
