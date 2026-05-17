// Single source of truth for scene names. Every scene here must be in Build
// Settings. Scene chain: MainMenu -> Video01 -> Makievska -> Video02 -> Vest -> Video03 -> MainMenu.
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
    // its own NextScene, then clears it (used by the menu's video gallery).
    public static string NextSceneOverride;
}
