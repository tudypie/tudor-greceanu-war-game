using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PlaneLockOn : MonoBehaviour
{
    Transform _transform;
    PlaneShooter _shooter;

    public Camera Camera;

    [Header("Reticle Box (px @ 1080p)")]
    public float BoxWidth = 360f;
    public float BoxHeight = 280f;

    [Header("Crosshair")]
    [Tooltip("Distance ahead of the plane used to project the free crosshair position.")]
    public float CrosshairRange = 300f;
    [Tooltip("Higher = the crosshair catches up to its target screen position faster.")]
    public float CrosshairSmoothing = 12f;
    [Tooltip("Crosshair arm length in pixels at 1080p — scales with resolution.")]
    public float CrosshairSize = 18f;
    [Tooltip("Line thickness in pixels at 1080p — scales with resolution.")]
    public float LineThickness = 2f;
    [Tooltip("Reference screen height the px values were authored against.")]
    public float ReferenceHeight = 1080f;

    [Header("Lock-On")]
    public float MaxLockDistance = 800f;
    [Tooltip("Time the target must stay inside the box for a lock to complete.")]
    public float AcquireTime = 0.25f;
    [Tooltip("Time the target can stay outside the box before the lock drops.")]
    public float LoseGrace = 0.35f;

    [Header("Colors")]
    public Color BoxColor = new Color(1f, 1f, 1f, 0.35f);
    public Color CrosshairColor = new Color(1f, 1f, 1f, 0.9f);
    public Color LockColor = new Color(1f, 0.25f, 0.25f, 1f);

    Vector2 _restScreen;
    Vector2 _crosshairScreen;
    bool _restVisible;
    Transform _lockedTarget;
    float _lockProgress;
    float _outsideTimer;

    const float EnemyRefreshInterval = 0.25f;
    float _nextEnemyRefresh;
    static readonly List<PlaneAIController> _enemies = new();

    public bool HasLock => _lockedTarget != null && _lockProgress >= 1f;
    public Transform LockedTarget => HasLock ? _lockedTarget : null;
    public Vector2 CrosshairScreen => _crosshairScreen;
    public bool CrosshairVisible => _restVisible;

    float UiScale => Screen.height / Mathf.Max(1f, ReferenceHeight);

    void Start()
    {
        _transform = transform;
        _shooter = GetComponent<PlaneShooter>();

        if (Camera == null)
        {
            var follow = GetComponent<PlaneCameraFollow>();
            if (follow != null) Camera = follow.Camera;
        }
        if (Camera == null) Camera = UnityEngine.Camera.main;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _restScreen = center;
        _crosshairScreen = center;
    }

    void Update()
    {
        if (Camera == null) return;

        var alpha = 1f - Mathf.Exp(-CrosshairSmoothing * Time.deltaTime);

        var sp = Camera.WorldToScreenPoint(_transform.position + _transform.forward * CrosshairRange);
        _restVisible = sp.z > 0f;

        if (_restVisible)
        {
            var restTarget = new Vector2(sp.x, sp.y);
            _restScreen = Vector2.Lerp(_restScreen, restTarget, alpha);
        }

        if (!_restVisible)
        {
            UpdateLockState(null);
            PushAimToShooter();
            return;
        }

        var box = GetBoxRect();
        var candidate = FindBestCandidate(box);
        UpdateLockState(candidate);

        Vector2 desired;
        if (HasLock)
        {
            var lockSp = Camera.WorldToScreenPoint(_lockedTarget.position);
            desired = lockSp.z > 0f ? ClampToBox(new Vector2(lockSp.x, lockSp.y), box) : _restScreen;
        }
        else
        {
            desired = _restScreen;
        }
        _crosshairScreen = Vector2.Lerp(_crosshairScreen, desired, alpha);

        PushAimToShooter();
    }

    Rect GetBoxRect()
    {
        var s = UiScale;
        var w = BoxWidth * s;
        var h = BoxHeight * s;
        return new Rect(_restScreen.x - w * 0.5f, _restScreen.y - h * 0.5f, w, h);
    }

    Transform FindBestCandidate(Rect box)
    {
        if (Time.unscaledTime >= _nextEnemyRefresh)
        {
            _enemies.Clear();
            var found = Object.FindObjectsByType<PlaneAIController>(FindObjectsSortMode.None);
            _enemies.AddRange(found);
            _nextEnemyRefresh = Time.unscaledTime + EnemyRefreshInterval;
        }

        Transform best = null;
        var bestScore = float.MaxValue;
        var planePos = _transform.position;
        var planeFwd = _transform.forward;

        foreach (var enemy in _enemies)
        {
            if (enemy == null || enemy.transform == _transform) continue;

            var t = enemy.transform;
            var toEnemy = t.position - planePos;
            var dist = toEnemy.magnitude;
            if (dist > MaxLockDistance || dist < 0.001f) continue;
            if (Vector3.Dot(toEnemy, planeFwd) <= 0f) continue;

            var sp = Camera.WorldToScreenPoint(t.position);
            if (sp.z <= 0f) continue;

            var screen = new Vector2(sp.x, sp.y);
            if (!box.Contains(screen)) continue;

            var d = Vector2.Distance(screen, _crosshairScreen);
            if (d < bestScore)
            {
                bestScore = d;
                best = t;
            }
        }

        return best;
    }

    void UpdateLockState(Transform candidate)
    {
        if (_lockedTarget == null && _lockProgress > 0f) _lockProgress = 0f;

        if (candidate != null)
        {
            if (_lockedTarget != candidate)
            {
                _lockedTarget = candidate;
                _lockProgress = 0f;
            }
            _outsideTimer = 0f;
            _lockProgress = Mathf.Clamp01(_lockProgress + Time.deltaTime / Mathf.Max(0.0001f, AcquireTime));
        }
        else
        {
            _outsideTimer += Time.deltaTime;
            if (_outsideTimer >= LoseGrace)
            {
                _lockedTarget = null;
                _lockProgress = 0f;
            }
        }
    }

    static Vector2 ClampToBox(Vector2 p, Rect box)
    {
        return new Vector2(
            Mathf.Clamp(p.x, box.xMin, box.xMax),
            Mathf.Clamp(p.y, box.yMin, box.yMax));
    }

    void PushAimToShooter()
    {
        if (_shooter == null) return;

        if (HasLock)
        {
            var muzzle = _transform.position + _transform.forward * _shooter.MuzzleOffsetZ;
            _shooter.AimDirection = (_lockedTarget.position - muzzle).normalized;
            _shooter.UseAimDirection = true;
        }
        else
        {
            _shooter.UseAimDirection = false;
        }
    }

    // ---- HUD ----

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (Camera == null || !_restVisible) return;

        var s = UiScale;
        var box = GetBoxRect();
        var guiBox = ScreenToGuiRect(box);

        DrawBox(guiBox, BoxColor, Mathf.Max(1f, 2f * s));

        var crossColor = HasLock
            ? LockColor
            : (_lockedTarget != null ? Color.Lerp(CrosshairColor, LockColor, _lockProgress) : CrosshairColor);

        var cross = new Vector2(_crosshairScreen.x, Screen.height - _crosshairScreen.y);
        var size = CrosshairSize * s;
        var thickness = Mathf.Max(1f, LineThickness * s);
        DrawCrosshair(cross, size, thickness, crossColor);

        if (_lockedTarget != null) DrawLockBrackets(cross, size * 1.15f, thickness, crossColor, _lockProgress);
    }

    static Rect ScreenToGuiRect(Rect r)
    {
        return new Rect(r.xMin, Screen.height - r.yMax, r.width, r.height);
    }

    static void DrawBox(Rect r, Color c, float thickness)
    {
        var prev = GUI.color;
        GUI.color = c;
        var tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), tex);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), tex);
        GUI.color = prev;
    }

    static void DrawCrosshair(Vector2 pos, float size, float t, Color c)
    {
        var prev = GUI.color;
        GUI.color = c;
        var tex = Texture2D.whiteTexture;
        GUI.DrawTexture(new Rect(pos.x - size, pos.y - t * 0.5f, size * 2f, t), tex);
        GUI.DrawTexture(new Rect(pos.x - t * 0.5f, pos.y - size, t, size * 2f), tex);
        GUI.color = prev;
    }

    static void DrawLockBrackets(Vector2 pos, float size, float t, Color c, float progress)
    {
        var prev = GUI.color;
        var col = c;
        col.a *= Mathf.Lerp(0.4f, 1f, progress);
        GUI.color = col;
        var tex = Texture2D.whiteTexture;
        var arm = size * 0.45f;

        // top-left
        GUI.DrawTexture(new Rect(pos.x - size, pos.y - size, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x - size, pos.y - size, t, arm), tex);
        // top-right
        GUI.DrawTexture(new Rect(pos.x + size - arm, pos.y - size, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x + size - t, pos.y - size, t, arm), tex);
        // bottom-left
        GUI.DrawTexture(new Rect(pos.x - size, pos.y + size - t, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x - size, pos.y + size - arm, t, arm), tex);
        // bottom-right
        GUI.DrawTexture(new Rect(pos.x + size - arm, pos.y + size - t, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x + size - t, pos.y + size - arm, t, arm), tex);
        GUI.color = prev;
    }
}
