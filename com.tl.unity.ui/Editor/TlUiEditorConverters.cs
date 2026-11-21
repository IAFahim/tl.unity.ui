using UnityEditor;

namespace TlUi.Editor
{
    [InitializeOnLoad]
    public static class TlUiEditorConverters
    {
        static TlUiEditorConverters()
        {
            TlUiConverters.Register();
        }
    }
}
