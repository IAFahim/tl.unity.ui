using Unity.Entities;
using UnityEngine;

namespace MenuSample
{
    [AddComponentMenu("Tl UI Samples/Menu Settings")]
    public sealed class MenuAuthoring : MonoBehaviour
    {
        [Tooltip("Starting volume, 0 to 1.")]
        [Range(0f, 1f)]
        public float Volume = 0.8f;

        [Tooltip("Starting quality: 0 Low, 1 Medium, 2 High.")]
        [Range(0, 2)]
        public int Quality = 1;

        [Tooltip("The Send To Game code the Play button sends; it must match its TlSend in Menu.uxml.")]
        public int StartCode = 1;

        [Tooltip("The Send To Game code the Pause hotkey sends; it must match the Commands row on the Tl UI Panel.")]
        public int TogglePauseCode = 2;

        sealed class Baker : Baker<MenuAuthoring>
        {
            public override void Bake(MenuAuthoring authoring)
            {
                var menu = GetEntity(TransformUsageFlags.None);
                AddComponent(menu, new MenuSettings { Volume = authoring.Volume, Quality = authoring.Quality });
                AddComponent(menu, new MenuCodes { Start = authoring.StartCode, TogglePause = authoring.TogglePauseCode });
            }
        }
    }
}
