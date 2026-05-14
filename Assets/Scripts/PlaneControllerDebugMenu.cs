using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneControllerDebugMenu : MonoBehaviour
{
    public PlaneFlightModel Target;
    public PlaneCameraFollow CameraFollow;
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
        public float RollAutoLevelSpeed;
        public float YawSpeed;
        public float BankTurnSpeed;
        public float CameraSpring;
    }

    void Start()
    {
        _visible = VisibleOnStart;
        if (Target == null) Target = FindFirstObjectByType<PlaneFlightModel>();
        if (Target != null && CameraFollow == null) CameraFollow = Target.GetComponent<PlaneCameraFollow>();
        if (Target != null) _initial = Capture(Target, CameraFollow);
    }

    static Snapshot Capture(PlaneFlightModel t, PlaneCameraFollow cam)
    {
        var s = new Snapshot();
        if (t != null && t.Stats != null)
        {
            s.NormalThrust = t.Stats.NormalThrust;
            s.MaxThrust = t.Stats.MaxThrust;
            s.ThrustAgilityMultiplier = t.Stats.ThrustAgilityMultiplier;
            s.PitchIncreaseSpeed = t.Stats.PitchIncreaseSpeed;
            s.InvertPitch = t.Stats.InvertPitch;
            s.RollIncreaseSpeed = t.Stats.RollIncreaseSpeed;
            s.RollAutoLevelSpeed = t.Stats.RollAutoLevelSpeed;
            s.YawSpeed = t.Stats.YawSpeed;
            s.BankTurnSpeed = t.Stats.BankTurnSpeed;
        }
        if (cam != null && cam.Stats != null) s.CameraSpring = cam.Stats.CameraSpring;
        return s;
    }

    static void Apply(PlaneFlightModel t, PlaneCameraFollow cam, Snapshot s)
    {
        if (t != null && t.Stats != null)
        {
            t.Stats.NormalThrust = s.NormalThrust;
            t.Stats.MaxThrust = s.MaxThrust;
            t.Stats.ThrustAgilityMultiplier = s.ThrustAgilityMultiplier;
            t.Stats.PitchIncreaseSpeed = s.PitchIncreaseSpeed;
            t.Stats.InvertPitch = s.InvertPitch;
            t.Stats.RollIncreaseSpeed = s.RollIncreaseSpeed;
            t.Stats.RollAutoLevelSpeed = s.RollAutoLevelSpeed;
            t.Stats.YawSpeed = s.YawSpeed;
            t.Stats.BankTurnSpeed = s.BankTurnSpeed;
        }
        if (cam != null && cam.Stats != null) cam.Stats.CameraSpring = s.CameraSpring;
    }

    static string Format(Snapshot s) =>
        $"NormalThrust = {s.NormalThrust:0.##}\n" +
        $"MaxThrust = {s.MaxThrust:0.##}\n" +
        $"ThrustAgilityMultiplier = {s.ThrustAgilityMultiplier:0.##}\n" +
        $"PitchIncreaseSpeed = {s.PitchIncreaseSpeed:0.##}\n" +
        $"InvertPitch = {s.InvertPitch}\n" +
        $"RollIncreaseSpeed = {s.RollIncreaseSpeed:0.##}\n" +
        $"RollAutoLevelSpeed = {s.RollAutoLevelSpeed:0.##}\n" +
        $"YawSpeed = {s.YawSpeed:0.##}\n" +
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
        if (!_visible || Target == null || Target.Stats == null) return;

        _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "Plane Settings (press " + ToggleKey + " to toggle)");
    }

    void DrawWindow(int id)
    {
        var fs = Target.Stats;
        fs.NormalThrust = Slider("Normal Thrust", fs.NormalThrust, _initial.NormalThrust, 100f, 2000f);
        fs.MaxThrust = Slider("Max Thrust (Space)", fs.MaxThrust, _initial.MaxThrust, 100f, 3000f);
        fs.ThrustAgilityMultiplier = Slider("Thrust Agility Mult.", fs.ThrustAgilityMultiplier, _initial.ThrustAgilityMultiplier, 1f, 4f);

        GUILayout.Space(6);

        fs.PitchIncreaseSpeed = Slider("Pitch Speed", fs.PitchIncreaseSpeed, _initial.PitchIncreaseSpeed, 50f, 800f);
        fs.InvertPitch = Toggle("Invert Pitch (W = climb)", fs.InvertPitch, _initial.InvertPitch);

        GUILayout.Space(6);

        fs.RollIncreaseSpeed = Slider("Roll Speed (A/D)", fs.RollIncreaseSpeed, _initial.RollIncreaseSpeed, 50f, 1500f);
        fs.RollAutoLevelSpeed = Slider("Roll Auto-Level Speed", fs.RollAutoLevelSpeed, _initial.RollAutoLevelSpeed, 0f, 360f);
        fs.YawSpeed = Slider("Yaw Speed (Q/E)", fs.YawSpeed, _initial.YawSpeed, 0f, 180f);
        fs.BankTurnSpeed = Slider("Bank Turn Speed", fs.BankTurnSpeed, _initial.BankTurnSpeed, 0f, 180f);

        if (CameraFollow != null && CameraFollow.Stats != null)
        {
            GUILayout.Space(6);
            CameraFollow.Stats.CameraSpring = Slider("Camera Spring", CameraFollow.Stats.CameraSpring, _initial.CameraSpring, 0f, 1f);
        }

        GUILayout.Space(10);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset"))
        {
            Apply(Target, CameraFollow, _initial);
        }
        if (GUILayout.Button("Export"))
        {
            var text = Format(Capture(Target, CameraFollow));
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
