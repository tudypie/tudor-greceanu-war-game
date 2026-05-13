using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public Camera Camera;
    public Transform CameraTarget;
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);

    [Header("Mouse Look")]
    public Vector2 MouseSensitivity = new Vector2(0.2f, 0.15f);
    public bool InvertY = false;
    public float MinPitch = -60f;
    public float MaxPitch = 75f;

    [Header("Smoothing")]
    [Range(0f, 1f)] public float CameraSpring = 0.92f;

    [Header("Cursor")]
    public bool LockCursor = true;

    float _yawOffset;
    float _pitchOffset;
    float _lastHeadingDeg;

    void Start()
    {
        _transform = transform;
        if (Camera != null) Camera.transform.SetParent(null);
        if (LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        _lastHeadingDeg = HeadingFromForward(_transform.forward, 0f);
    }

    void LateUpdate()
    {
        if (Camera == null) return;

        ReadMouseLook();

        var headingDeg = HeadingFromForward(_transform.forward, _lastHeadingDeg);
        _lastHeadingDeg = headingDeg;

        var orbit = Quaternion.Euler(_pitchOffset, headingDeg + _yawOffset, 0f);
        var targetPos = _transform.position + orbit * FollowOffset;

        var spring = Mathf.Clamp01(CameraSpring);
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

        _yawOffset += delta.x * MouseSensitivity.x;
        var pitchDelta = delta.y * MouseSensitivity.y;
        _pitchOffset += InvertY ? pitchDelta : -pitchDelta;
        _pitchOffset = Mathf.Clamp(_pitchOffset, MinPitch, MaxPitch);
    }

    static float HeadingFromForward(Vector3 forward, float fallbackDeg)
    {
        var flat = new Vector2(forward.x, forward.z);
        if (flat.sqrMagnitude < 0.0001f) return fallbackDeg;
        return Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
    }
}
