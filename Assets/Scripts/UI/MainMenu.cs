using UnityEngine;
using UnityEngine.SceneManagement;

// IMGUI front-end menu: Play runs the campaign, Videos replays cutscenes,
// Levels jumps into a mission, Quit exits.
public class MainMenu : MonoBehaviour
{
    public string Title = "Pentru Cine?";
    [Tooltip("Scene loaded by Play (the intro cutscene). Must be added to Build Settings.")]
    public string PlayScene = GameFlow.Video01;

    [Header("Layout")]
    public float ButtonWidth = 320f;
    public float ButtonHeight = 56f;
    public float ButtonSpacing = 18f;

    enum Page { Main, Videos, Levels }
    Page _page;

    GUIStyle _titleStyle;
    GUIStyle _buttonStyle;

    static readonly (string label, string scene)[] VideoEntries =
    {
        ("Makievska Briefing", GameFlow.Video01),
        ("Schimbare de Planuri", GameFlow.Video02),
        ("Pentru Cine?", GameFlow.Video03),
        ("Interviu Tudor Greceanu", GameFlow.Video04),
    };

    static readonly (string label, string scene)[] LevelEntries =
    {
        ("Makievska", GameFlow.Mission1),
        ("Turda", GameFlow.Mission2),
    };

    void OnGUI()
    {
        _titleStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 64,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.92f, 0.95f, 1f, 1f) }
        };
        _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 24 };

        var cx = Screen.width * 0.5f;

        var titleRect = new Rect(0f, Screen.height * 0.18f, Screen.width, 90f);
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(3f, titleRect.y + 3f, Screen.width, 90f), Title, _titleStyle);
        GUI.color = prev;
        GUI.Label(titleRect, Title, _titleStyle);

        var y = Screen.height * 0.42f;
        switch (_page)
        {
            case Page.Videos:
                foreach (var (label, scene) in VideoEntries)
                    if (Button(cx, ref y, label)) PlayCutscene(scene);
                if (Button(cx, ref y, "Back")) _page = Page.Main;
                break;

            case Page.Levels:
                foreach (var (label, scene) in LevelEntries)
                    if (Button(cx, ref y, label)) SceneManager.LoadScene(scene);
                if (Button(cx, ref y, "Back")) _page = Page.Main;
                break;

            default:
                if (Button(cx, ref y, "Play")) SceneManager.LoadScene(PlayScene);
                if (Button(cx, ref y, "Videos")) _page = Page.Videos;
                if (Button(cx, ref y, "Levels")) _page = Page.Levels;
                if (Button(cx, ref y, "Quit")) Quit();
                break;
        }

        // Esc backs out of a sub-page (and is harmless on the main page).
        if (_page != Page.Main && Event.current.type == EventType.KeyDown
            && Event.current.keyCode == KeyCode.Escape)
        {
            _page = Page.Main;
            Event.current.Use();
        }
    }

    // Replay a cutscene, then return to the menu instead of the campaign.
    void PlayCutscene(string scene)
    {
        GameFlow.NextSceneOverride = GameFlow.MainMenu;
        SceneManager.LoadScene(scene);
    }

    bool Button(float centerX, ref float y, string label)
    {
        var rect = new Rect(centerX - ButtonWidth * 0.5f, y, ButtonWidth, ButtonHeight);
        y += ButtonHeight + ButtonSpacing;
        return GUI.Button(rect, label, _buttonStyle);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
