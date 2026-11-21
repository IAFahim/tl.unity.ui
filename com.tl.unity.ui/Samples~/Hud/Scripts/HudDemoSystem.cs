using TlUi;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace HudSample
{
    [BurstCompile]
    public partial struct HudDemoSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HudData>();
            state.RequireForUpdate<HudRules>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rules = SystemAPI.GetSingleton<HudRules>();
            var hud = SystemAPI.GetSingleton<HudData>();
            foreach (var inbox in SystemAPI.Query<DynamicBuffer<UiCommandInbox>>())
            {
                for (var command = 0; command < inbox.Length; command++)
                    if (inbox[command].Code == rules.HealCode)
                        hud.Health = math.min(hud.MaxHealth, hud.Health + rules.HealAmount);
                inbox.Clear();
            }
            if (!rules.Paused)
            {
                var secondsThisFrame = SystemAPI.Time.DeltaTime;
                hud.Seconds += secondsThisFrame;
                hud.Health = math.max(0f, hud.Health - rules.DrainPerSecond * secondsThisFrame);
                hud.Coins = (int)(hud.Seconds * rules.CoinsPerSecond);
            }
            hud.HealthFraction = hud.Health / hud.MaxHealth;
            hud.LowHealth = hud.HealthFraction < rules.LowHealthFraction;
            SystemAPI.SetSingleton(hud);
        }
    }
}
