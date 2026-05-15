using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public PlaneCameraStats Stats;
    public Camera Camera;
    public Transform CameraTarget;

    float _yawOffset;
    float _pitchOffset;
    float _smoothHeadingDeg;
    bool _snapped;

    public float CameraSpring
    {
        get => Stats != null ? Stats.CameraSpring : 0f;
        set { if (Stats != null) Stats.CameraSpring = value; }
    }

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
        _smoothHeadingDeg = HeadingFromForward(_transform.forward, 0f);
    }

    void LateUpdate()
    {
        if (Camera == null || Stats == null) return;

        ReadMouseLook();

        // Track heading angle-aware so the ~180° flip through vertical (loops/360s)
        // and the ±180 wrap ease in over time instead of snapping the camera around.
        var rawHeading = HeadingFromForward(_transform.forward, _smoothHeadingDeg);
        var headingAlpha = SpringAlpha(Stats.HeadingFollowSpring);
        _smoothHeadingDeg = Mathf.LerpAngle(_smoothHeadingDeg, rawHeading, headingAlpha);

        var orbit = Quaternion.Euler(_pitchOffset, _smoothHeadingDeg + _yawOffset, 0f);
        var targetPos = _transform.position + orbit * Stats.FollowOffset;

        // First frame: jump straight to the target so there's no startup swing.
        var posAlpha = _snapped ? SpringAlpha(Stats.CameraSpring) : 1f;
        var lookAlpha = _snapped ? SpringAlpha(Stats.LookSpring) : 1f;
        _snapped = true;

        var cam = Camera.transform;
        cam.position = Vector3.Lerp(cam.position, targetPos, posAlpha);

        var lookTarget = CameraTarget != null ? CameraTarget : _transform;
        var toTarget = lookTarget.position - cam.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            var lookRot = Quaternion.LookRotation(toTarget, Vector3.up);
            cam.rotation = Quaternion.Slerp(cam.rotation, lookRot, lookAlpha);
        }
    }

    // Frame-rate independent smoothing: 'spring' is the fraction retained per 1/60 s.
    static float SpringAlpha(float spring)
    {
        return 1f - Mathf.Pow(Mathf.Clamp01(spring), Time.deltaTime * 60f);
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
