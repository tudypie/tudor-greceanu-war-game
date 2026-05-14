using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Flight Stats", fileName = "PlaneFlightStats")]
public class PlaneFlightStats : ScriptableObject
{
    [Header("Thrust")]
    public float NormalThrust = 600f;
    public float MaxThrust = 1200f;
    public float ThrustAgilityMultiplier = 1.8f;

    [Header("Pitch")]
    public float PitchIncreaseSpeed = 300f;
    public bool InvertPitch = true;

    [Header("Roll")]
    public float RollIncreaseSpeed = 420f;
    public float RollAutoLevelSpeed = 120f;

    [Header("Yaw")]
    public float YawSpeed = 30f;
    public float BankTurnSpeed = 15f;

    [Header("Response")]
    public float RollResponseTime = 0.3f;
    public float PitchResponseTime = 0.3f;
    public float YawResponseTime = 0.3f;
}
