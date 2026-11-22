using Tl.Ui;
using Tl.Unity;
using TlUi;
using Unity.Burst;
using Unity.Entities;

namespace TlUiPackage.Tests
{
    public struct LevelUpEvents : IComponentData
    {
        public byte Code;
    }

    [DisableAutoCreation]
    [BurstCompile]
    public partial struct RaiseLevelUpSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var gameEvents = SystemAPI.GetSingletonEntity<LevelUpEvents>();
            var levelUpCode = SystemAPI.GetSingleton<LevelUpEvents>().Code;
            SystemAPI.GetBuffer<TimelineEvent<UiSignal>>(gameEvents).Raise(levelUpCode);
        }
    }
}
