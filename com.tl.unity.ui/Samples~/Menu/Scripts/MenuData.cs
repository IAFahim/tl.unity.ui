using Unity.Entities;
using Unity.Properties;

namespace MenuSample
{
    [GeneratePropertyBag]
    public struct MenuSettings : IComponentData
    {
        public float Volume;
        public int Quality;
        public bool Muted;
        public bool Paused;
        public bool Started;
    }

    public struct MenuCodes : IComponentData
    {
        public int Start;
        public int TogglePause;
    }
}
