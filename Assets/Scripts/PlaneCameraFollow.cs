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
    float _lastHeadingDeg;

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
        _lastHeadingDeg = HeadingFromForward(_transform.forward, 0f);
    }

    void LateUpdate()
    {
        if (Camera == null || Stats == null) return;

        ReadMouseLook();

        var headingDeg = HeadingFromForward(_transform.forward, _lastHeadingDeg);
        _lastHeadingDeg = headingDeg;

        var orbit = Quaternion.Euler(_pitchOffset, headingDeg + _yawOffset, 0f);
        var targetPos = _transform.position + orbit * Stats.FollowOffset;

        var spring = Mathf.Clamp01(Stats.CameraSpring);
        var alpha = 1f - Mathf.Pow(spring, Time.deltaTime * 60f);

        var cam = Camera.transform;
        cam.position = Vector3.Lerp(cam.position, targetPos, alpha);

        if (CameraTarget != null) cam.LookAt(CameraTarget);
        else cam.LookAt(_transform);
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
