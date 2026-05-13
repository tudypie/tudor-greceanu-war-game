using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public Camera Camera;
    public Transform CameraTarget;
    [Range(0, 1)] public float CameraSpring = 0.96f;
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);

    [Header("Mouse Look")]
    public Vector2 MouseSensitivity = new Vector2(0.2f, 0.15f);
    public float MinPitch = -40f;
    public float MaxPitch = 70f;
    public bool InvertY = false;

    [Header("Auto Recenter")]
    [Tooltip("Seconds without mouse input before the camera drifts back behind the plane.")]
    public float RecenterDelay = 1.2f;
    [Tooltip("Degrees per second the orbit drifts toward zero once recentering starts.")]
    public float RecenterSpeed = 45f;

    [Header("Cursor")]
    public bool LockCursor = true;

    float _yawOffset;
    float _pitchOffset;
    float _idleTime;

    void Start()
    {
        _transform = transform;
        if (Camera != null) Camera.transform.SetParent(null);
        if (LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void LateUpdate()
    {
        if (Camera == null) return;

        ReadMouseLook();

        var planeForward = _transform.forward;
        var headingDeg = Mathf.Atan2(planeForward.x, planeForward.z) * Mathf.Rad2Deg;
        var orbit = Quaternion.Euler(_pitchOffset, headingDeg + _yawOffset, 0f);

        var offset = orbit * FollowOffset;
        var targetPos = _transform.position + offset;

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
        var delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;

        if (delta.sqrMagnitude > 0.0001f)
        {
            _yawOffset += delta.x * MouseSensitivity.x;
            var pitchDelta = delta.y * MouseSensitivity.y;
            _pitchOffset += InvertY ? pitchDelta : -pitchDelta;
            _pitchOffset = Mathf.Clamp(_pitchOffset, MinPitch, MaxPitch);
            _idleTime = 0f;
        }
        else
        {
            _idleTime += Time.deltaTime;
            if (_idleTime >= RecenterDelay)
            {
                var step = RecenterSpeed * Time.deltaTime;
                _yawOffset = Mathf.MoveTowardsAngle(_yawOffset, 0f, step);
                _pitchOffset = Mathf.MoveTowards(_pitchOffset, 0f, step);
            }
        }
    }
}
