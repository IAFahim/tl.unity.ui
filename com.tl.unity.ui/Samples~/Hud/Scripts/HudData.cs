using Unity.Entities;
using Unity.Properties;

namespace HudSample
{
    [GeneratePropertyBag]
    public struct HudData : IComponentData
    {
        public float Health;
        public float MaxHealth;
        public float HealthFraction;
        public int Coins;
        public float Seconds;
        public bool LowHealth;
    }

    public struct HudRules : IComponentData
    {
        public float DrainPerSecond;
        public float HealAmount;
        public int HealCode;
        public float CoinsPerSecond;
        public float LowHealthFraction;
        public bool Paused;
    }
}
