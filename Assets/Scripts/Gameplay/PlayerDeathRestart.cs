using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlaneCrash))]
public class PlayerDeathRestart : MonoBehaviour
{
    // No delay/Invoke: PlaneCrash destroys this object the same frame, cancelling any pending timer.
    void Awake() => GetComponent<PlaneCrash>().Exploded += Restart;

    void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
