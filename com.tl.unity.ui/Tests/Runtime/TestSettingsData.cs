using TlUi;
using Unity.Entities;
using Unity.Properties;

namespace TlUiPackage.Tests
{
    [GeneratePropertyBag]
    public struct TestSettingsData : IComponentData
    {
        public float Volume;
        public bool Muted;
        public int Coins;
    }

    public sealed class TestSettingsView : TlUiData<TestSettingsData>
    {
    }
}
