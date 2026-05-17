using UnityEngine;

// Procedural detonation visual, sized by radius. Unscaled time so it animates during bullet-time.
public class Fireball : MonoBehaviour
{
    public static void Spawn(Vector3 center, float radius, float life)
    {
        var rig = new GameObject("Fireball");
        rig.transform.position = center;

        var mat = new Material(Shader.Find("Sprites/Default"));

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.transform.SetParent(rig.transform, false);
        ball.GetComponent<MeshRenderer>().material = mat;

        var shock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(shock.GetComponent<Collider>());
        shock.transform.SetParent(rig.transform, false);
        shock.GetComponent<MeshRenderer>().material = new Material(mat);

        var light = rig.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.4f);
        light.range = radius * 1.5f;
        light.intensity = 12f;

        rig.AddComponent<Fireball>().Init(ball.transform, shock.transform,
            light, mat, radius, life);
    }

    Transform _ball, _shock;
    Light _light;
    Material _mat;
    float _radius, _life, _age;

    void Init(Transform ball, Transform shock, Light light,
        Material mat, float radius, float life)
    {
        _ball = ball; _shock = shock; _light = light;
        _mat = mat; _radius = radius; _life = Mathf.Max(0.1f, life);
    }

    void Update()
    {
        _age += Time.unscaledDeltaTime;
        var k = _age / _life;
        if (k >= 1f) { Destroy(gameObject); return; }

        var bloom = Mathf.Sqrt(Mathf.Clamp01(_age / (_life * 0.25f)));
        var ballSize = _radius * 0.9f * bloom;
        _ball.localScale = Vector3.one * ballSize;
        _ball.localPosition = Vector3.up * ballSize * 0.35f;

        var shockSize = Mathf.Lerp(0f, _radius * 2f, k);
        _shock.localScale = Vector3.one * shockSize;

        var fade = 1f - k;
        _mat.color = new Color(1f, Mathf.Lerp(0.2f, 0.85f, fade), 0.15f, fade);
        _light.intensity = 12f * fade * fade;
        _light.range = _radius * (1.5f + k);
    }
}
