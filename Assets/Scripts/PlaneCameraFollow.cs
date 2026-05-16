using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public PlaneCameraStats Stats;
    public Camera Camera;
    public Transform CameraTarget;

    public Key FirstPersonToggleKey = Key.C;

    bool _firstPerson;
    float _yawOffset;
    float _pitchOffset;

    void Start()
    {
        _transform = transform;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneCameraFollow)} on {name} has no Stats assigned.", this);
            return;
        }
        if (Camera != null) Camera.transform.SetParent(null);
        if (Stats.LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        _firstPerson = Stats.StartInFirstPerson;
        // Start parked behind the plane's initial heading; from here on the
        // orbit is world-fixed and only the player moves it.
        _yawOffset = HeadingFromForward(_transform.forward, 0f);
    }

    void LateUpdate()
    {
        if (Camera == null || Stats == null) return;

        var kb = Keyboard.current;
        if (kb != null && kb[FirstPersonToggleKey].wasPressedThisFrame)
            _firstPerson = !_firstPerson;

        if (_firstPerson)
        {
            // Rigidly attached to the cockpit; the view turns with the plane,
            // unlike the world-fixed third-person orbit below.
            var fpCam = Camera.transform;
            fpCam.position = _transform.TransformPoint(Stats.FirstPersonOffset);
            fpCam.rotation = _transform.rotation;
            return;
        }

        ReadMouseLook();

        // The orbit is fixed in world space and driven purely by the player.
        // The camera does NOT rotate with the plane's heading/pitch/roll; it
        // only tracks the plane's position so the plane stays framed.
        var orbit = Quaternion.Euler(_pitchOffset, _yawOffset, 0f);

        var cam = Camera.transform;
        cam.position = _transform.position + orbit * Stats.FollowOffset;

        var lookTarget = CameraTarget != null ? CameraTarget : _transform;
        var toTarget = lookTarget.position - cam.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            cam.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
        }
    }

    void ReadMouseLook()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        var delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude <= 0.0001f) return;

        _yawOffset += delta.x * Stats.MouseSensitivity.x;
        var pitchDelta = delta.y * Stats.MouseSensitivity.y;
        _pitchOffset += Stats.InvertY ? pitchDelta : -pitchDelta;
        _pitchOffset = Mathf.Clamp(_pitchOffset, Stats.MinPitch, Stats.MaxPitch);
    }

    static float HeadingFromForward(Vector3 forward, float fallbackDeg)
    {
        var flat = new Vector2(forward.x, forward.z);
        if (flat.sqrMagnitude < 0.0001f) return fallbackDeg;
        return Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
    }
}
