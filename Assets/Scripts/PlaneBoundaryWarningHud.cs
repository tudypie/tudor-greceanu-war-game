using UnityEngine;

// Player-only map-boundary warning. The horizontal twin of
// PlaneCeilingWarningHud.
//
// As the plane flies out into the warning band of the scene's MapBoundary
// box it shows an amber "TURN BACK" caution while the pilot is still in
// control. Once the flight model takes over (PlaneFlightModel.OverBoundary)
// it flashes a red banner so the pilot understands the stick was overridden
// and the plane is being banked back toward the field automatically until it
// is well inside again.
//
// The boundary physics itself lives in PlaneFlightModel (driven by the scene
// MapBoundary) and applies to every plane; this component is purely the
// player's HUD feedback, so it only draws when a PlanePlayerInput is present
// and the plane is alive.
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneBoundaryWarningHud : MonoBehaviour
{
    public int FontSize = 26;
    // Sits just below the altitude-ceiling banner so the two never overlap
    // when (rarely) both fire at once.
    public float TopMargin = 150f;
    [Tooltip("Flashes per second while the autopilot is recovering.")]
    public float FlashHz = 4f;

    PlaneFlightModel _model;
    PlanePlayerInput _player;
    PlaneHealth _health;

    void Start()
    {
        _model = GetComponent<PlaneFlightModel>();
        _player = GetComponent<PlanePlayerInput>();
        _health = GetComponent<PlaneHealth>();
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        // Player plane only, and not while dead/crashed.
        if (_model == null || _player == null) return;
        if (_health != null && _health.IsDead) return;

        var prox = _model.BoundaryProximity;
        if (prox <= 0f && !_model.OverBoundary) return;

        string text;
        Color color;
        if (_model.OverBoundary)
        {
            // Autopilot has the stick — flash so it reads as urgent.
            if (Mathf.Repeat(Time.unscaledTime * FlashHz, 1f) >= 0.5f) return;
            text = "!! LEAVING COMBAT AREA - AUTO-TURN !!";
            color = new Color(1f, 0.25f, 0.2f, 1f);
        }
        else
        {
            // Heads-up while the pilot still has control.
            text = "LEAVING COMBAT AREA - TURN BACK";
            color = Color.Lerp(new Color(1f, 0.85f, 0.2f, 0.9f),
                               new Color(1f, 0.45f, 0.15f, 1f), prox);
        }

        var skin = GUI.skin.label;
        var prevSize = skin.fontSize;
        var prevAlign = skin.alignment;
        var prevColor = GUI.color;

        skin.fontSize = FontSize;
        skin.alignment = TextAnchor.UpperCenter;

        var h = FontSize + 8f;
        // Cheap drop shadow so it stays legible over sky or terrain.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(2f, TopMargin + 2f, Screen.width, h), text);
        GUI.color = color;
        GUI.Label(new Rect(0f, TopMargin, Screen.width, h), text);

        GUI.color = prevColor;
        skin.fontSize = prevSize;
        skin.alignment = prevAlign;
    }
}
