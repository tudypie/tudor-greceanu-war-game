using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mission-2 (Vest) win condition: won once every enemy PlaneSpawner has
// emitted its full squadron and none remain alive, player still alive.
public class EliminationObjective : MonoBehaviour
{
    [Tooltip("Scene loaded on victory. Must be in Build Settings.")]
    public string WinScene = GameFlow.Video03;

    [Tooltip("Seconds the WIN banner holds before the next scene loads.")]
    public float WinDelay = 4f;

    [Tooltip("Enemy spawners to clear. Empty = auto-find every enemy PlaneSpawner in the scene.")]
    public List<PlaneSpawner> EnemySpawners = new();

    public bool DebugReadout = true;

    PlaneHealth _player;
    bool _won;
    float _winAt;
    bool _loading;

    void Start()
    {
        if (EnemySpawners.Count == 0)
            foreach (var s in FindObjectsByType<PlaneSpawner>(FindObjectsSortMode.None))
                if (s.SpawnFaction == PlaneFaction.Enemy)
                    EnemySpawners.Add(s);

        var input = FindFirstObjectByType<PlanePlayerInput>();
        if (input != null) _player = input.GetComponent<PlaneHealth>();
    }

    void Update()
    {
        if (_won)
        {
            if (!_loading && Time.time >= _winAt)
            {
                _loading = true;
                SceneManager.LoadScene(WinScene);
            }
            return;
        }

        if (_player != null && _player.IsDead) return; // loss handled elsewhere
        if (EnemySpawners.Count == 0) return;

        foreach (var s in EnemySpawners)
            if (s == null || !s.Finished) return;

        _won = true;
        _winAt = Time.time + Mathf.Max(0f, WinDelay);
        Debug.Log("All enemies destroyed - MISSION 2 COMPLETE");
    }

    int EnemiesAlive()
    {
        var n = 0;
        foreach (var s in EnemySpawners)
            if (s != null) n += s.AliveCount;
        return n;
    }

    void OnGUI()
    {
        if (!DebugReadout || !HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;

        var prevAlign = GUI.skin.label.alignment;
        var prevSize = GUI.skin.label.fontSize;
        var prevColor = GUI.color;

        if (_won)
        {
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUI.skin.label.fontSize = 28;
            GUI.color = new Color(0.4f, 1f, 0.5f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.4f, Screen.width, 40f),
                "MISSION COMPLETE");
        }
        else
        {
            GUI.skin.label.alignment = TextAnchor.UpperCenter;
            GUI.skin.label.fontSize = 18;
            GUI.color = new Color(1f, 0.6f, 0.35f, 1f);
            GUI.Label(new Rect(0f, 40f, Screen.width, 26f),
                $"ENEMIES REMAINING: {EnemiesAlive()}");
        }

        GUI.color = prevColor;
        GUI.skin.label.alignment = prevAlign;
        GUI.skin.label.fontSize = prevSize;
    }
}
