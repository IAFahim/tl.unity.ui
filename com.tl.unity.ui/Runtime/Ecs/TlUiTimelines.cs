using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace TlUi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    [AddComponentMenu("Tl UI/Tl UI Timelines")]
    [HelpURL("https://github.com/IAFahim/tl.unity.ui/tree/main/com.tl.unity.ui")]
    public sealed class TlUiTimelines : MonoBehaviour
    {
        public const int MostTargetNames = 64;

        [Tooltip("Element names from your UXML that clips can target. A clip targeting a name drives every element with that name, in every Tl UI Panel. Right-click a UI clip and pick Tl UI Target to add names; the order is what clips store, so do not reorder or remove names clips use.")]
        public List<string> TargetNames = new List<string>();
    }
}
