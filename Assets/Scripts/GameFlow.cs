// Single source of truth for the scene-flow chain. Used as the default
// target for every navigation point so there are no scattered scene strings:
//
//   MainMenu --[Play]--> Video01 --> Makievska --[mission 1 win]-->
//   Video02 --> Vest --[mission 2 win]--> Video03 --> MainMenu
//
// Every scene here must be in Build Settings (ProjectSettings/EditorBuildSettings).
public static class GameFlow
{
    public const string MainMenu = "MainMenu";
    public const string Video01  = "Video01";
    public const string Mission1 = "Makievska";
    public const string Video02  = "Video02";
    public const string Mission2 = "Vest";
    public const string Video03  = "Video03";
    public const string Video04  = "Video04";

    // One-shot: when set, the next VideoSceneLoader advances here instead of
    // its own NextScene, then clears it. Lets the menu's video gallery play a
    // cutscene and return straight back to the menu instead of the campaign.
    public static string NextSceneOverride;
}
