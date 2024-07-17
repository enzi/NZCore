using UnityEditor;

namespace NZCore.Editor
{
    [InitializeOnLoad]
    public class RefreshOnPlay
    {
        static RefreshOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AssetDatabase.Refresh(ImportAssetOptions.Default);
            }
        }
    }
}