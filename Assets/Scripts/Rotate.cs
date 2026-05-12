using UnityEngine;

public class Rotate : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 10f;
    
    void Update()
    {
        float rot = transform.rotation.eulerAngles.z;
        rot += rotateSpeed * Time.deltaTime;
        transform.eulerAngles = new Vector3(0, 0, rot);
    }
}
