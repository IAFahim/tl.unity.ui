using Unity.Properties;
using UnityEngine;

public sealed class UiTestSettings : ScriptableObject
{
    [CreateProperty]
    public float Volume = 0.45f;

    [CreateProperty]
    public bool Muted = true;
}
