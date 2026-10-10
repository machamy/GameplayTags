#if UNITY_EDITOR
using UnityEditor;

public static class EditorState
{
    private static bool isPlayingCache;
    
    public static bool IsPlaying
    {
        get
        {
            return isPlayingCache;
        }
    }
    
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            case PlayModeStateChange.EnteredEditMode:
                isPlayingCache = false;
                break;
            case PlayModeStateChange.ExitingEditMode:
                isPlayingCache = false;
                break;
            case PlayModeStateChange.EnteredPlayMode:
                isPlayingCache = true;
                break;
            case PlayModeStateChange.ExitingPlayMode:
                isPlayingCache = false;
                break;
        }
    }

    [UnityEditor.InitializeOnLoadMethod]
    private static void Initialize()
    {
        isPlayingCache = EditorApplication.isPlaying;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }
}
#endif