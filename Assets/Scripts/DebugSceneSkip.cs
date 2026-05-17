using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Test shortcut: press N to jump straight to the next scene in the
// Build Settings order, which is the GameFlow chain. Auto-spawns itself
// in every scene so there is nothing to place by hand. Active in release
// builds too, on purpose.
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
