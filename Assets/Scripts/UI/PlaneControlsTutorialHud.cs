using UnityEngine;

// Player-only takeoff controls tutorial; one hint per takeoff phase, fades
// out a few seconds after liftoff.
[RequireComponent(typeof(PlaneFlightModel))]
public class PlaneControlsTutorialHud : MonoBehaviour
{
    public int FontSize = 22;
    [Tooltip("Distance of the hint from the bottom of the screen.")]
    public float BottomMargin = 96f;
    [Tooltip("Seconds the movement hint stays up after takeoff.")]
    public float AirHintDuration = 7f;
    [Tooltip("Seconds the camera-pan hint stays up after the movement hint.")]
    public float CameraHintDuration = 6f;
    [Tooltip("Fade-out length for the last in-air hint (seconds).")]
    public float AirHintFade = 1.5f;

    PlaneFlightModel _model;
    PlanePlayerInput _player;
    PlaneHealth _health;

    bool _wasGrounded;
    bool _tookOff;
    float _takeoffTime;

    static readonly Color Hint = new Color(0.85f, 0.95f, 1f, 0.95f);
    static readonly Color Action = new Color(1f, 0.8f, 0.2f, 1f);

    void Start()
    {
        _model = GetComponent<PlaneFlightModel>();
        _player = GetComponent<PlanePlayerInput>();
        _health = GetComponent<PlaneHealth>();
        _wasGrounded = _model != null && _model.IsGrounded;
    }

    void Update()
    {
        if (_model == null) return;
        // Grounded -> airborne edge is the takeoff; air-start planes never
        // trip this, so they get no tutorial.
        if (_wasGrounded && !_model.IsGrounded)
        {
            _tookOff = true;
            _takeoffTime = Time.time;
        }
        _wasGrounded = _model.IsGrounded;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (_model == null || _player == null) return;
        if (_health != null && _health.IsDead) return;

        var stats = _model.Stats;
        if (stats == null) return;

        string text;
        var color = Hint;

        if (_model.IsGrounded)
        {
            if (_model.CurrentSpeed >= stats.RotationSpeed)
            {
                // Ready to rotate: pulse the prompt as the action to take now.
                text = "S to take off";
                color = Action;
                color.a *= 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            }
            else
            {
                text = "SPACE to accelerate     Q / E to steer";
            }
        }
        else
        {
            if (!_tookOff) return;
            var since = Time.time - _takeoffTime;
            var total = AirHintDuration + CameraHintDuration;
            if (since > total) return;
            text = since < AirHintDuration
                ? "W / S to pitch     A / D to roll     Q / E to steer"
                : "Hold RMB to pan the camera around";
            if (AirHintFade > 0f && since > total - AirHintFade)
                color.a *= Mathf.Clamp01((total - since) / AirHintFade);
        }

        var skin = GUI.skin.label;
        var prevSize = skin.fontSize;
        var prevAlign = skin.alignment;
        var prevColor = GUI.color;

        skin.fontSize = FontSize;
        skin.alignment = TextAnchor.MiddleCenter;

        var h = FontSize + 8f;
        var y = Screen.height - BottomMargin - h;
        // Drop shadow for legibility over sky or terrain.
        GUI.color = new Color(0f, 0f, 0f, 0.6f * color.a);
        GUI.Label(new Rect(2f, y + 2f, Screen.width, h), text);
        GUI.color = color;
        GUI.Label(new Rect(0f, y, Screen.width, h), text);

        GUI.color = prevColor;
        skin.fontSize = prevSize;
        skin.alignment = prevAlign;
    }
}
