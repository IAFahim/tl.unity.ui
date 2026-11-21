using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace InventorySample
{
    [AddComponentMenu("Tl UI Samples/Enemy")]
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Tooltip("The name its nameplate shows.")]
        public string DisplayName = "Enemy";

        [Tooltip("Starting health, in health points.")]
        [Min(0f)]
        public float Health = 100f;

        [Tooltip("Radius of the circle it walks around its start position, in metres.")]
        [Min(0f)]
        public float Radius = 1f;

        [Tooltip("How fast it walks the circle, in radians per second.")]
        public float RadiansPerSecond = 1f;

        [Tooltip("Health it loses every second, in health points.")]
        [Min(0f)]
        public float HealthLostPerSecond = 2f;

        sealed class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var enemy = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(enemy, new Nameplate { Name = new FixedString32Bytes(authoring.DisplayName), Health = authoring.Health });
                AddComponent(enemy, new Wander
                {
                    Center = authoring.transform.position,
                    Radius = authoring.Radius,
                    RadiansPerSecond = authoring.RadiansPerSecond,
                    HealthLostPerSecond = authoring.HealthLostPerSecond,
                });
            }
        }
    }
}
