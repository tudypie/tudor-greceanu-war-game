using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Drop in a cutscene scene: advances to NextScene when the VideoPlayer
// finishes, or after FallbackSeconds if there is no clip. Skippable once
// MinShowSeconds has passed.
public class VideoSceneLoader : MonoBehaviour
{
    [Tooltip("Scene loaded when the video ends or is skipped. Must be in Build Settings.")]
    public string NextScene = GameFlow.MainMenu;

    [Tooltip("Used only when the scene has no VideoPlayer or its clip is unset.")]
    public float FallbackSeconds = 8f;

    [Tooltip("Skip input is ignored for this long so a key still held from the " +
             "previous scene can't blow straight past the cutscene.")]
    public float MinShowSeconds = 0.75f;

    VideoPlayer _player;
    float _startTime;
    float _autoAdvanceAt; // 0 = no timer (waiting on the VideoPlayer event)
    bool _advancing;

    void Start()
    {
        _startTime = Time.unscaledTime;
        _player = FindFirstObjectByType<VideoPlayer>();

        if (_player != null && _player.clip != null)
        {
            // loopPointReached never fires on a looping player, so it gets a
            // one-pass timer; the non-looping path also keeps a safety timer.
            if (_player.isLooping)
                _autoAdvanceAt = _startTime + (float)_player.clip.length;
            else
            {
                _player.loopPointReached += OnVideoFinished;
                _autoAdvanceAt = _startTime + (float)_player.clip.length + 1f;
            }
        }
        else
        {
            _autoAdvanceAt = _startTime + Mathf.Max(0.1f, FallbackSeconds);
        }
    }

    void OnDestroy()
    {
        if (_player != null) _player.loopPointReached -= OnVideoFinished;
    }

    void Update()
    {
        if (_advancing) return;

        if (_autoAdvanceAt > 0f && Time.unscaledTime >= _autoAdvanceAt)
        {
            Advance();
            return;
        }

        if (Time.unscaledTime - _startTime >= MinShowSeconds && SkipPressed())
            Advance();
    }

    void OnVideoFinished(VideoPlayer vp) => Advance();

    static bool SkipPressed()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    void Advance()
    {
        if (_advancing) return;
        _advancing = true;
        var target = GameFlow.NextSceneOverride ?? NextScene;
        GameFlow.NextSceneOverride = null;
        SceneManager.LoadScene(target);
    }

    void OnGUI()
    {
        if (_advancing || Time.unscaledTime - _startTime < MinShowSeconds) return;
        if (Event.current.type != EventType.Repaint) return;

        var prevColor = GUI.color;
        var prevAlign = GUI.skin.label.alignment;
        var prevSize = GUI.skin.label.fontSize;

        GUI.color = new Color(1f, 1f, 1f, 0.5f);
        GUI.skin.label.alignment = TextAnchor.LowerRight;
        GUI.skin.label.fontSize = 16;
        GUI.Label(new Rect(0f, 0f, Screen.width - 24f, Screen.height - 18f),
            "Press Space to skip");

        GUI.color = prevColor;
        GUI.skin.label.alignment = prevAlign;
        GUI.skin.label.fontSize = prevSize;
    }
}
