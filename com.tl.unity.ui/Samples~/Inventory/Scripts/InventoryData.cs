using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Properties;

namespace InventorySample
{
    [GeneratePropertyBag]
    public struct InventorySlot : IBufferElementData
    {
        public FixedString32Bytes Name;
        public int Count;
    }

    public struct ShopEntry : IBufferElementData
    {
        public FixedString32Bytes Name;
    }

    public struct BagRules : IComponentData
    {
        public int UseCode;
        public int MoveCode;
        public int BuyCode;
    }

    [GeneratePropertyBag]
    public struct Nameplate : IComponentData
    {
        public FixedString32Bytes Name;
        public float Health;
    }

    public struct Wander : IComponentData
    {
        public float3 Center;
        public float Radius;
        public float RadiansPerSecond;
        public float Angle;
        public float HealthLostPerSecond;
    }
}
