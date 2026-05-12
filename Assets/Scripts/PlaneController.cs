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
    public float RollIncreaseSpeed = 300f;
    public float BarrelRollSpeed = 720f;
    public float RollAutoLevelSpeed = 120f;

    float _deltaYaw;
    public float BankTurnSpeed = 60f;

    bool _worldPitch;

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

    void Update()
    {
        var move = _moveAction.ReadValue<Vector2>();
        var agility = _jumpAction.IsPressed() ? ThrustAgilityMultiplier : 1f;

        _deltaPitch = (InvertPitch ? -move.y : move.y) * PitchIncreaseSpeed * agility * Time.deltaTime;

        var keyboard = Keyboard.current;
        var barrelRollDir = 0f;
        if (keyboard != null)
        {
            if (keyboard.qKey.isPressed) barrelRollDir += 1f;
            if (keyboard.eKey.isPressed) barrelRollDir -= 1f;
        }

        var bank = _transform.right.y;
        _worldPitch = barrelRollDir != 0f;
        if (barrelRollDir != 0f)
        {
            _deltaRoll = barrelRollDir * BarrelRollSpeed * agility * Time.deltaTime;
            _deltaYaw = 0f;
        }
        else
        {
            if (Mathf.Approximately(move.x, 0f))
            {
                _deltaRoll = -bank * RollAutoLevelSpeed * agility * Time.deltaTime;
            }
            else
            {
                _deltaRoll = -move.x * RollIncreaseSpeed * agility * Time.deltaTime;
            }

            _deltaYaw = -bank * BankTurnSpeed * agility * Time.deltaTime;
        }
    }

    void FixedUpdate()
    {
        var localRotation = _transform.localRotation;
        localRotation *= Quaternion.Euler(0f, 0f, _deltaRoll);
        if (!_worldPitch)
        {
            localRotation *= Quaternion.Euler(_deltaPitch, 0f, 0f);
        }
        _transform.localRotation = localRotation;

        if (_worldPitch)
        {
            Vector3 pitchAxis = Vector3.Cross(Vector3.up, _transform.forward);
            if (pitchAxis.sqrMagnitude > 0.0001f)
            {
                _transform.Rotate(pitchAxis.normalized, _deltaPitch, Space.World);
            }
        }

        _transform.Rotate(Vector3.up, _deltaYaw, Space.World);
        var thrust = _jumpAction.IsPressed() ? MaxThrust : NormalThrust;
        _rigidbody.linearVelocity = _transform.forward * (thrust * Time.fixedDeltaTime);

        Vector3 cameraTargetPosition = _transform.position + _transform.forward * -8f + new Vector3(0f, 3f, 0f);
        var cameraTransform = Camera.transform;

        cameraTransform.position = cameraTransform.position * CameraSpring + cameraTargetPosition * (1 - CameraSpring);
        Camera.transform.LookAt(CameraTarget);
    }
}
