using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PlaneFlightModel))]
public class PlanePlayerInput : MonoBehaviour
{
    PlaneFlightModel _model;
    PlaneShooter _shooter;

    Controls _controls;
    InputAction _moveAction;
    InputAction _jumpAction;
    InputAction _attackAction;

    void Awake()
    {
        _controls = new Controls();
        _moveAction = _controls.Player.Move;
        _jumpAction = _controls.Player.Jump;
        _attackAction = _controls.Player.Attack;
    }

    void OnEnable() { _controls.Player.Enable(); }
    void OnDisable() { _controls.Player.Disable(); }
    void OnDestroy() { _controls?.Dispose(); }

    void Start()
    {
        _model = GetComponent<PlaneFlightModel>();
        _shooter = GetComponent<PlaneShooter>();
    }

    void FixedUpdate()
    {
        var move = _moveAction.ReadValue<Vector2>();
        var keyboard = Keyboard.current;
        var yaw = 0f;
        if (keyboard != null)
        {
            if (keyboard.eKey.isPressed) yaw += 1f;
            if (keyboard.qKey.isPressed) yaw -= 1f;
        }

        _model.PitchInput = move.y;
        _model.RollInput = move.x;
        _model.YawInput = yaw;
        // Hold space to boost from NormalThrust toward MaxThrust.
        _model.Boost = _jumpAction.IsPressed();
    }

    void Update()
    {
        if (_shooter != null) _shooter.Trigger = _attackAction.IsPressed();
    }
}
