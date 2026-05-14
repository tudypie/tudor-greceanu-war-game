using UnityEngine;

[CreateAssetMenu(menuName = "War Game/Plane Weapon Stats", fileName = "PlaneWeaponStats")]
public class PlaneWeaponStats : ScriptableObject
{
    [Header("Firing")]
    public float Range = 400f;
    public float FireInterval = 0.08f;
    public float MuzzleOffsetZ = 4f;
    public float Damage = 8f;
    public LayerMask HitMask = ~0;

    [Header("Heat")]
    public float HeatPerShot = 6f;
    public float MaxHeat = 100f;
    public float CoolPerSecond = 35f;
    public float OverheatedCoolPerSecond = 55f;
    [Range(0f, 1f)] public float ResumeHeatFraction = 0.4f;

    [Header("Tracer")]
    public float TracerDuration = 0.04f;
    public Color TracerColor = new Color(1f, 0.85f, 0.3f, 1f);
    public float TracerWidth = 0.12f;
}
