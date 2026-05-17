using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Press N to skip to the next scene in Build Settings order. Auto-spawns in
// every scene; active in release builds too, on purpose.
public class DebugSceneSkip : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Spawn()
    {
        var go = new GameObject(nameof(DebugSceneSkip));
        go.AddComponent<DebugSceneSkip>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.nKey.wasPressedThisFrame) return;

        int count = SceneManager.sceneCountInBuildSettings;
        int next = (SceneManager.GetActiveScene().buildIndex + 1) % count;
        SceneManager.LoadScene(next);
    }
}
