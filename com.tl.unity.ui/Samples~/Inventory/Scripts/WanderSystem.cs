using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace InventorySample
{
    [BurstCompile]
    public partial struct WanderSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var secondsThisFrame = SystemAPI.Time.DeltaTime;
            foreach (var (wander, placement, nameplate) in SystemAPI.Query<RefRW<Wander>, RefRW<LocalTransform>, RefRW<Nameplate>>())
            {
                wander.ValueRW.Angle += wander.ValueRO.RadiansPerSecond * secondsThisFrame;
                placement.ValueRW.Position = wander.ValueRO.Center + wander.ValueRO.Radius * new float3(math.cos(wander.ValueRO.Angle), 0f, math.sin(wander.ValueRO.Angle));
                nameplate.ValueRW.Health = math.max(0f, nameplate.ValueRO.Health - wander.ValueRO.HealthLostPerSecond * secondsThisFrame);
            }
        }
    }
}
