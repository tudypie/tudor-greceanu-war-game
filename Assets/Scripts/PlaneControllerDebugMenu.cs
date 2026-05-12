using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneControllerDebugMenu : MonoBehaviour
{
    public PlaneController Target;
    public Key ToggleKey = Key.F1;
    public bool VisibleOnStart = false;

    bool _visible;
    Rect _window = new Rect(20, 20, 360, 0);
    Snapshot _initial;
    string _exportStatus;
    float _exportStatusUntil;

    struct Snapshot
    {
        public float NormalThrust;
        public float MaxThrust;
        public float ThrustAgilityMultiplier;
        public float PitchIncreaseSpeed;
        public bool InvertPitch;
        public float RollIncreaseSpeed;
        public float BarrelRollSpeed;
        public float RollAutoLevelSpeed;
        public float BankTurnSpeed;
        public float CameraSpring;
    }

    void Start()
    {
        _visible = VisibleOnStart;
        if (Target == null) Target = FindFirstObjectByType<PlaneController>();
        if (Target != null) _initial = Capture(Target);
    }

    static Snapshot Capture(PlaneController t) => new Snapshot
    {
        NormalThrust = t.NormalThrust,
        MaxThrust = t.MaxThrust,
        ThrustAgilityMultiplier = t.ThrustAgilityMultiplier,
        PitchIncreaseSpeed = t.PitchIncreaseSpeed,
        InvertPitch = t.InvertPitch,
        RollIncreaseSpeed = t.RollIncreaseSpeed,
        BarrelRollSpeed = t.BarrelRollSpeed,
        RollAutoLevelSpeed = t.RollAutoLevelSpeed,
        BankTurnSpeed = t.BankTurnSpeed,
        CameraSpring = t.CameraSpring,
    };

    static void Apply(PlaneController t, Snapshot s)
    {
        t.NormalThrust = s.NormalThrust;
        t.MaxThrust = s.MaxThrust;
        t.ThrustAgilityMultiplier = s.ThrustAgilityMultiplier;
        t.PitchIncreaseSpeed = s.PitchIncreaseSpeed;
        t.InvertPitch = s.InvertPitch;
        t.RollIncreaseSpeed = s.RollIncreaseSpeed;
        t.BarrelRollSpeed = s.BarrelRollSpeed;
        t.RollAutoLevelSpeed = s.RollAutoLevelSpeed;
        t.BankTurnSpeed = s.BankTurnSpeed;
        t.CameraSpring = s.CameraSpring;
    }

    static string Format(Snapshot s) =>
        $"NormalThrust = {s.NormalThrust:0.##}\n" +
        $"MaxThrust = {s.MaxThrust:0.##}\n" +
        $"ThrustAgilityMultiplier = {s.ThrustAgilityMultiplier:0.##}\n" +
        $"PitchIncreaseSpeed = {s.PitchIncreaseSpeed:0.##}\n" +
        $"InvertPitch = {s.InvertPitch}\n" +
        $"RollIncreaseSpeed = {s.RollIncreaseSpeed:0.##}\n" +
        $"BarrelRollSpeed = {s.BarrelRollSpeed:0.##}\n" +
        $"RollAutoLevelSpeed = {s.RollAutoLevelSpeed:0.##}\n" +
        $"BankTurnSpeed = {s.BankTurnSpeed:0.##}\n" +
        $"CameraSpring = {s.CameraSpring:0.###}";

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard[ToggleKey].wasPressedThisFrame)
        {
            _visible = !_visible;
        }
    }

    void OnGUI()
    {
        if (!_visible || Target == null) return;

        _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "Plane Settings (press " + ToggleKey + " to toggle)");
    }

    void DrawWindow(int id)
    {
        Target.NormalThrust = Slider("Normal Thrust", Target.NormalThrust, _initial.NormalThrust, 100f, 2000f);
        Target.MaxThrust = Slider("Max Thrust (Space)", Target.MaxThrust, _initial.MaxThrust, 100f, 3000f);
        Target.ThrustAgilityMultiplier = Slider("Thrust Agility Mult.", Target.ThrustAgilityMultiplier, _initial.ThrustAgilityMultiplier, 1f, 4f);

        GUILayout.Space(6);

        Target.PitchIncreaseSpeed = Slider("Pitch Speed", Target.PitchIncreaseSpeed, _initial.PitchIncreaseSpeed, 50f, 800f);
        Target.InvertPitch = Toggle("Invert Pitch (W = climb)", Target.InvertPitch, _initial.InvertPitch);

        GUILayout.Space(6);

        Target.RollIncreaseSpeed = Slider("Roll Speed (A/D)", Target.RollIncreaseSpeed, _initial.RollIncreaseSpeed, 50f, 900f);
        Target.BarrelRollSpeed = Slider("Barrel Roll Speed (Q/E)", Target.BarrelRollSpeed, _initial.BarrelRollSpeed, 100f, 1500f);
        Target.RollAutoLevelSpeed = Slider("Roll Auto-Level Speed", Target.RollAutoLevelSpeed, _initial.RollAutoLevelSpeed, 0f, 360f);
        Target.BankTurnSpeed = Slider("Bank Turn Speed", Target.BankTurnSpeed, _initial.BankTurnSpeed, 0f, 360f);

        GUILayout.Space(6);

        Target.CameraSpring = Slider("Camera Spring", Target.CameraSpring, _initial.CameraSpring, 0f, 1f);

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
        {
            Apply(Target, _initial);
        }
        if (GUILayout.Button("Export"))
        {
            var text = Format(Capture(Target));
            GUIUtility.systemCopyBuffer = text;
            Debug.Log("Plane settings:\n" + text);
            _exportStatus = "Copied to clipboard + logged";
            _exportStatusUntil = Time.unscaledTime + 2f;
        }
        if (GUILayout.Button("Close")) _visible = false;
        GUILayout.EndHorizontal();

        if (_exportStatus != null && Time.unscaledTime < _exportStatusUntil)
        {
            GUILayout.Label(_exportStatus);
        }

        GUI.DragWindow(new Rect(0, 0, 10000, 20));
    }

    static float Slider(string label, float value, float defaultValue, float min, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150));
        value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(100));
        GUILayout.Label(value.ToString("0.##"), GUILayout.Width(45));
        if (GUILayout.Button("↺", GUILayout.Width(26))) value = defaultValue;
        GUILayout.EndHorizontal();
        return value;
    }

    static bool Toggle(string label, bool value, bool defaultValue)
    {
        GUILayout.BeginHorizontal();
        value = GUILayout.Toggle(value, " " + label);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("↺", GUILayout.Width(26))) value = defaultValue;
        GUILayout.EndHorizontal();
        return value;
    }
}
