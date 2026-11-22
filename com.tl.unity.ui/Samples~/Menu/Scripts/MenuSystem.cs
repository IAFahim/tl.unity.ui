using TlUi;
using Unity.Burst;
using Unity.Entities;

namespace MenuSample
{
    [BurstCompile]
    public partial struct MenuSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MenuSettings>();
            state.RequireForUpdate<MenuCodes>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var codes = SystemAPI.GetSingleton<MenuCodes>();
            var settings = SystemAPI.GetSingleton<MenuSettings>();
            var changed = false;
            foreach (var inbox in SystemAPI.Query<DynamicBuffer<UiCommandInbox>>())
            {
                for (var command = 0; command < inbox.Length; command++)
                {
                    if (inbox[command].Code == codes.Start)
                        settings.Started = true;
                    else if (inbox[command].Code == codes.TogglePause)
                        settings.Paused = !settings.Paused;
                    changed = true;
                }
                inbox.Clear();
            }
            if (changed)
                SystemAPI.SetSingleton(settings);
        }
    }
}
