using UnityEngine;

public class PlaneCameraFollow : MonoBehaviour
{
    Transform _transform;

    public Camera Camera;
    public Transform CameraTarget;
    [Range(0, 1)] public float CameraSpring = 0.96f;
    public Vector3 FollowOffset = new Vector3(0f, 3f, -8f);

    void Start()
    {
        _transform = transform;
        if (Camera != null) Camera.transform.SetParent(null);
    }

    void LateUpdate()
    {
        if (Camera == null) return;

        var targetPos =
            _transform.position
            + _transform.forward * FollowOffset.z
            + Vector3.up * FollowOffset.y
            + _transform.right * FollowOffset.x;

        var cam = Camera.transform;
        cam.position = cam.position * CameraSpring + targetPos * (1 - CameraSpring);

        if (CameraTarget != null) cam.LookAt(CameraTarget);
        else cam.LookAt(_transform);
    }
}
