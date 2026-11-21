using Unity.Entities;
using UnityEngine;

namespace HudSample
{
    [AddComponentMenu("Tl UI Samples/Hud Demo")]
    public sealed class HudDemoAuthoring : MonoBehaviour
    {
        [Header("Health")]
        [Tooltip("Starting and maximum health, in health points.")]
        [Min(1f)]
        public float MaxHealth = 100f;

        [Tooltip("Health lost every second, in health points.")]
        [Min(0f)]
        public float DrainPerSecond = 12f;

        [Tooltip("Health one Heal command gives back, in health points.")]
        [Min(0f)]
        public float HealAmount = 30f;

        [Tooltip("The Send To Game code the Heal button sends; it must match the command on the Tl UI Panel.")]
        public int HealCode = 1;

        [Tooltip("Below this fraction of max health the low-health banner shows.")]
        [Range(0f, 1f)]
        public float LowHealthFraction = 0.3f;

        [Header("Coins")]
        [Tooltip("Coins earned every second.")]
        [Min(0f)]
        public float CoinsPerSecond = 3f;

        sealed class Baker : Baker<HudDemoAuthoring>
        {
            public override void Bake(HudDemoAuthoring authoring)
            {
                var hud = GetEntity(TransformUsageFlags.None);
                AddComponent(hud, new HudData { Health = authoring.MaxHealth, MaxHealth = authoring.MaxHealth, HealthFraction = 1f });
                AddComponent(hud, new HudRules
                {
                    DrainPerSecond = authoring.DrainPerSecond,
                    HealAmount = authoring.HealAmount,
                    HealCode = authoring.HealCode,
                    CoinsPerSecond = authoring.CoinsPerSecond,
                    LowHealthFraction = authoring.LowHealthFraction,
                });
            }
        }
    }
}
