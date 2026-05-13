using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlaneHealth))]
public class PlayerDeathRestart : MonoBehaviour
{
    public float RestartDelay = 5f;

    PlaneHealth _health;
    bool _scheduled;

    void Awake()
    {
        _health = GetComponent<PlaneHealth>();
        _health.Died += OnDied;
    }

    void OnDestroy()
    {
        if (_health != null) _health.Died -= OnDied;
    }

    void OnDied()
    {
        if (_scheduled) return;
        _scheduled = true;
        Invoke(nameof(Restart), RestartDelay);
    }

    void Restart()
    {
        var scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }
}
