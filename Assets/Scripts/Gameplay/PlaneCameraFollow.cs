using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public PlaneCameraStats Stats;
    public Camera Camera;
    public Transform CameraTarget;

    public Key FirstPersonToggleKey = Key.C;

    PlaneLockOn _lockOn;
    bool _firstPerson;
    float _yaw;
    float _pitch;
    float _panYaw;
    float _panPitch;
    bool _freeLooking;

    // True while the player holds RMB to look around; PlaneLockOn freezes
    // the reticle/aim during this.
    public bool IsFreeLooking => _freeLooking;

    void Start()
    {
        _transform = transform;
        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneCameraFollow)} on {name} has no Stats assigned.", this);
            return;
        }
        _lockOn = GetComponent<PlaneLockOn>();
        if (Camera != null) Camera.transform.SetParent(null);
        if (Stats.LockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        _firstPerson = Stats.StartInFirstPerson;
        // Park behind the plane's initial heading.
        _yaw = HeadingFromForward(_transform.forward, 0f);
        _pitch = 0f;
    }

    void LateUpdate()
    {
        if (Camera == null || Stats == null) return;

        var kb = Keyboard.current;
        if (kb != null && kb[FirstPersonToggleKey].wasPressedThisFrame)
            _firstPerson = !_firstPerson;

        if (_firstPerson)
        {
            // Rigidly attached to the cockpit; view turns with the plane.
            var fpCam = Camera.transform;
            fpCam.position = _transform.TransformPoint(Stats.FirstPersonOffset);
            fpCam.rotation = _transform.rotation;
            return;
        }

        // Chase cam auto-trails behind the heading, matches a fraction of
        // the climb, and leans toward the free-aim reticle.
        var fwd = _transform.forward;
        var desiredYaw = HeadingFromForward(fwd, _yaw);
        var desiredPitch = Mathf.Clamp(
            -Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg * Stats.FollowPitchFactor,
            Stats.MinPitch, Stats.MaxPitch);

        var dt = Time.deltaTime;
        var yawA = 1f - Mathf.Exp(-Stats.FollowYawSmoothing * dt);
        var pitchA = 1f - Mathf.Exp(-Stats.FollowPitchSmoothing * dt);
        _yaw += Mathf.DeltaAngle(_yaw, desiredYaw) * yawA;
        _pitch = Mathf.Lerp(_pitch, desiredPitch, pitchA);

        // Hold RMB to pan a free-look offset on top of the auto-trail;
        // releasing eases it back to zero.
        var mouse = Mouse.current;
        _freeLooking = mouse != null && mouse.rightButton.isPressed;
        if (_freeLooking)
        {
            var d = mouse.delta.ReadValue();
            _panYaw += d.x * Stats.FreeLookSensitivity.x;
            var pitchDelta = d.y * Stats.FreeLookSensitivity.y;
            _panPitch += Stats.InvertFreeLookY ? pitchDelta : -pitchDelta;
            _panPitch = Mathf.Clamp(_panPitch, Stats.MinPitch, Stats.MaxPitch);
        }
        else
        {
            var rk = 1f - Mathf.Exp(-Stats.FreeLookReturnSmoothing * dt);
            _panYaw = Mathf.Lerp(_panYaw, 0f, rk);
            _panPitch = Mathf.Lerp(_panPitch, 0f, rk);
        }

        // Aim lean is suppressed while free-looking since the reticle is frozen.
        var lean = (!_freeLooking && _lockOn != null) ? _lockOn.AimOffsetNormalized : Vector2.zero;
        var leanYaw = lean.x * Stats.AimLeanYaw;
        var leanPitch = -lean.y * Stats.AimLeanPitch;

        var orbit = Quaternion.Euler(
            _pitch + _panPitch + leanPitch,
            _yaw + _panYaw + leanYaw, 0f);

        var cam = Camera.transform;
        cam.position = _transform.position + orbit * Stats.FollowOffset;

        var lookTarget = CameraTarget != null ? CameraTarget : _transform;
        var toTarget = lookTarget.position - cam.position;
        if (toTarget.sqrMagnitude > 0.0001f)
        {
            cam.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
        }
    }

    static float HeadingFromForward(Vector3 forward, float fallbackDeg)
    {
        var flat = new Vector2(forward.x, forward.z);
        if (flat.sqrMagnitude < 0.0001f) return fallbackDeg;
        return Mathf.Atan2(flat.x, flat.y) * Mathf.Rad2Deg;
    }
}
