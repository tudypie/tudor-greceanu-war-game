using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneController : MonoBehaviour
{
    Transform _transform;
    Rigidbody _rigidbody;

    Controls _controls;
    InputAction _moveAction;
    InputAction _jumpAction;

    public Camera Camera;
    public Transform CameraTarget;
    [Range(0, 1)] public float CameraSpring = 0.96f;

    public float NormalThrust = 600f;
    public float MaxThrust = 1200f;
    public float ThrustAgilityMultiplier = 1.8f;

    float _deltaPitch;
    public float PitchIncreaseSpeed = 300f;
    public bool InvertPitch = true;

    float _deltaRoll;
    public float RollIncreaseSpeed = 420f;
    public float RollAutoLevelSpeed = 120f;

    float _deltaYaw;
    public float YawSpeed = 30f;
    public float BankTurnSpeed = 15f;

    void Awake()
    {
        _controls = new Controls();
        _moveAction = _controls.Player.Move;
        _jumpAction = _controls.Player.Jump;
    }

    void OnEnable()
    {
        _controls.Player.Enable();
    }

    void OnDisable()
    {
        _controls.Player.Disable();
    }

    void OnDestroy()
    {
        _controls?.Dispose();
    }

    void Start()
    {
        _transform = transform;
        _rigidbody = GetComponent<Rigidbody>();

        Camera.transform.SetParent(null);
    }

    void FixedUpdate()
    {
        var move = _moveAction.ReadValue<Vector2>();
        var agility = _jumpAction.IsPressed() ? ThrustAgilityMultiplier : 1f;
        var dt = Time.fixedDeltaTime;

        _deltaPitch = (InvertPitch ? -move.y : move.y) * PitchIncreaseSpeed * agility * dt;

        var keyboard = Keyboard.current;
        var yawInput = 0f;
        if (keyboard != null)
        {
            if (keyboard.eKey.isPressed) yawInput += 1f;
            if (keyboard.qKey.isPressed) yawInput -= 1f;
        }

        var bank = _transform.right.y;

        if (Mathf.Approximately(move.x, 0f))
        {
            _deltaRoll = -bank * RollAutoLevelSpeed * agility * dt;
        }
        else
        {
            _deltaRoll = -move.x * RollIncreaseSpeed * agility * dt;
        }

        _deltaYaw = (yawInput * YawSpeed - bank * BankTurnSpeed) * agility * dt;

        var localRotation = _transform.localRotation;
        localRotation *= Quaternion.Euler(0f, 0f, _deltaRoll);
        localRotation *= Quaternion.Euler(_deltaPitch, 0f, 0f);
        _transform.localRotation = localRotation;

        _transform.Rotate(Vector3.up, _deltaYaw, Space.World);
        var thrust = _jumpAction.IsPressed() ? MaxThrust : NormalThrust;
        _rigidbody.linearVelocity = _transform.forward * (thrust * Time.fixedDeltaTime);

        Vector3 cameraTargetPosition = _transform.position + _transform.forward * -8f + new Vector3(0f, 3f, 0f);
        var cameraTransform = Camera.transform;

        cameraTransform.position = cameraTransform.position * CameraSpring + cameraTargetPosition * (1 - CameraSpring);
        Camera.transform.LookAt(CameraTarget);
    }
}
