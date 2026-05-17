using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlaneCrash))]
public class PlayerDeathRestart : MonoBehaviour
{
    // Exploded fires once for every way the player's run ends — shot down then
    // dived into the ground, or flown straight into terrain — so reload right
    // there. No delay/Invoke: PlaneCrash destroys this object the same frame,
    // which would cancel any pending timer (the bug this had before).
    void Awake() => GetComponent<PlaneCrash>().Exploded += Restart;

    void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
