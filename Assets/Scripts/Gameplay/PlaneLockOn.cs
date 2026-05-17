using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
public class PlaneLockOn : MonoBehaviour
{
    Transform _transform;
    PlaneShooter _shooter;
    PlaneHealth _ownHealth;

    public PlaneLockOnStats Stats;
    public Camera Camera;

    // Mouse-driven free reticle, screen pixels (origin bottom-left).
    Vector2 _reticleScreen;
    // Drawn crosshair: follows the reticle, or snaps to a locked target.
    Vector2 _crosshairScreen;
    Vector2 _aimOffsetNormalized;
    bool _active;
    Transform _lockedTarget;
    float _lockProgress;
    float _outsideTimer;

    const float EnemyRefreshInterval = 0.25f;
    float _nextEnemyRefresh;
    static readonly List<PlaneHealth> _hostiles = new();

    // Raised on the false->true lock edge; switching targets re-fires it.
    public event Action LockAcquired;
    bool _wasLocked;

    public bool HasLock => _lockedTarget != null && _lockProgress >= 1f;
    public Transform LockedTarget => HasLock ? _lockedTarget : null;
    public Vector2 CrosshairScreen => _crosshairScreen;
    public bool CrosshairVisible => _active;
    // Reticle offset from screen center, each axis [-1, 1] at travel edge.
    public Vector2 AimOffsetNormalized => _aimOffsetNormalized;

    float UiScale => Stats != null
        ? Screen.height / Mathf.Max(1f, Stats.ReferenceHeight)
        : 1f;

    void Start()
    {
        _transform = transform;
        _shooter = GetComponent<PlaneShooter>();
        _ownHealth = GetComponent<PlaneHealth>();

        if (Stats == null)
        {
            Debug.LogError($"{nameof(PlaneLockOn)} on {name} has no Stats assigned.", this);
        }

        if (Camera == null)
        {
            var follow = GetComponent<PlaneCameraFollow>();
            if (follow != null) Camera = follow.Camera;
        }
        if (Camera == null) Camera = UnityEngine.Camera.main;

        var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        _reticleScreen = center;
        _crosshairScreen = center;
    }

    void Update()
    {
        if (Camera == null || Stats == null)
        {
            _active = false;
            if (_shooter != null) _shooter.UseAimDirection = false;
            return;
        }

        _active = true;
        var dt = Time.deltaTime;

        MoveReticle(dt);

        var box = GetBoxRect();
        var candidate = FindBestCandidate(box);
        UpdateLockState(candidate);

        Vector2 desired;
        if (HasLock)
        {
            var lockSp = Camera.WorldToScreenPoint(_lockedTarget.position);
            desired = lockSp.z > 0f ? ClampToBox(new Vector2(lockSp.x, lockSp.y), box) : _reticleScreen;
        }
        else
        {
            desired = _reticleScreen;
        }

        _crosshairScreen = desired;

        PushAimToShooter();
    }

    void MoveReticle(float dt)
    {
        var mouse = Mouse.current;
        // RMB free-look drives the camera, so freeze the reticle.
        var free = mouse != null && mouse.rightButton.isPressed;

        if (mouse != null && !free)
        {
            var d = mouse.delta.ReadValue();
            var dx = d.x * Stats.ReticleSensitivity.x;
            var dy = d.y * Stats.ReticleSensitivity.y;
            if (Stats.InvertReticleY) dy = -dy;
            _reticleScreen.x += dx;
            _reticleScreen.y += dy;
        }

        var cx = Screen.width * 0.5f;
        var cy = Screen.height * 0.5f;
        var halfX = Mathf.Max(1f, cx * Stats.ReticleRangeX);
        var halfY = Mathf.Max(1f, cy * Stats.ReticleRangeY);

        // Inset travel by half the box so its edges stop at the brackets.
        var s = UiScale;
        var allowX = Mathf.Max(0f, halfX - Stats.BoxWidth * s * 0.5f);
        var allowY = Mathf.Max(0f, halfY - Stats.BoxHeight * s * 0.5f);

        if (!free && Stats.ReticleRecenterPerSecond > 0f)
        {
            var k = 1f - Mathf.Exp(-Stats.ReticleRecenterPerSecond * dt);
            _reticleScreen.x = Mathf.Lerp(_reticleScreen.x, cx, k);
            _reticleScreen.y = Mathf.Lerp(_reticleScreen.y, cy, k);
        }

        _reticleScreen.x = Mathf.Clamp(_reticleScreen.x, cx - allowX, cx + allowX);
        _reticleScreen.y = Mathf.Clamp(_reticleScreen.y, cy - allowY, cy + allowY);

        _aimOffsetNormalized = new Vector2(
            allowX > 0f ? Mathf.Clamp((_reticleScreen.x - cx) / allowX, -1f, 1f) : 0f,
            allowY > 0f ? Mathf.Clamp((_reticleScreen.y - cy) / allowY, -1f, 1f) : 0f);
    }

    Rect GetBoxRect()
    {
        var s = UiScale;
        var w = Stats.BoxWidth * s;
        var h = Stats.BoxHeight * s;
        return new Rect(_reticleScreen.x - w * 0.5f, _reticleScreen.y - h * 0.5f, w, h);
    }

    // Mirrors the clamp math in MoveReticle.
    Rect GetReticleBoundsRect()
    {
        var cx = Screen.width * 0.5f;
        var cy = Screen.height * 0.5f;
        var halfX = Mathf.Max(1f, cx * Stats.ReticleRangeX);
        var halfY = Mathf.Max(1f, cy * Stats.ReticleRangeY);
        return new Rect(cx - halfX, cy - halfY, halfX * 2f, halfY * 2f);
    }

    Transform FindBestCandidate(Rect box)
    {
        if (Time.unscaledTime >= _nextEnemyRefresh)
        {
            _hostiles.Clear();
            var found = FindObjectsByType<PlaneHealth>(FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                var ph = found[i];
                if (ph == null || ph == _ownHealth) continue;
                if (_ownHealth != null && !_ownHealth.IsHostileTo(ph)) continue;
                _hostiles.Add(ph);
            }
            _nextEnemyRefresh = Time.unscaledTime + EnemyRefreshInterval;
        }

        Transform best = null;
        var bestScore = float.MaxValue;
        var planePos = _transform.position;
        var camFwd = Camera.transform.forward;

        foreach (var hostile in _hostiles)
        {
            if (hostile == null || hostile.IsDead) continue;

            var t = hostile.transform;
            if (t == _transform) continue;
            var toEnemy = t.position - planePos;
            var dist = toEnemy.magnitude;
            if (dist > Stats.MaxLockDistance || dist < 0.001f) continue;
            if (Vector3.Dot(toEnemy, camFwd) <= 0f) continue;

            var sp = Camera.WorldToScreenPoint(t.position);
            if (sp.z <= 0f) continue;

            var screen = new Vector2(sp.x, sp.y);
            if (!box.Contains(screen)) continue;

            var d = Vector2.Distance(screen, _reticleScreen);
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
            _lockProgress = Mathf.Clamp01(_lockProgress + Time.deltaTime / Mathf.Max(0.0001f, Stats.AcquireTime));
        }
        else
        {
            _outsideTimer += Time.deltaTime;
            if (_outsideTimer >= Stats.LoseGrace)
            {
                _lockedTarget = null;
                _lockProgress = 0f;
            }
        }

        var locked = _lockedTarget != null && _lockProgress >= 1f;
        if (locked && !_wasLocked) LockAcquired?.Invoke();
        _wasLocked = locked;
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

        var muzzle = _transform.position + _transform.forward * _shooter.MuzzleOffsetZ;

        if (HasLock)
        {
            _shooter.AimDirection = (_lockedTarget.position - muzzle).normalized;
            _shooter.UseAimDirection = true;
            return;
        }

        // RMB free-look with no lock: keep the last aim while the camera pans.
        if (Mouse.current != null && Mouse.current.rightButton.isPressed
            && _shooter.AimDirection.sqrMagnitude > 1e-6f)
        {
            _shooter.UseAimDirection = true;
            return;
        }

        // Free aim: fire from the muzzle toward the world point under the reticle.
        var ray = Camera.ScreenPointToRay(new Vector3(_reticleScreen.x, _reticleScreen.y, 0f));

        var range = _shooter.Stats != null ? _shooter.Stats.Range : Stats.AimConvergeDistance;
        var mask = _shooter.Stats != null ? _shooter.Stats.HitMask : (LayerMask)~0;

        Vector3 aimPoint;
        if (Physics.Raycast(ray, out var hit, range, mask, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlaneHealth>() != _ownHealth)
        {
            aimPoint = hit.point;
        }
        else
        {
            aimPoint = ray.origin + ray.direction * Mathf.Max(1f, Stats.AimConvergeDistance);
        }

        _shooter.AimDirection = (aimPoint - muzzle).normalized;
        _shooter.UseAimDirection = true;
    }

    void OnGUI()
    {
        if (!HudToggle.Visible) return;
        if (Event.current.type != EventType.Repaint) return;
        if (Camera == null || !_active || Stats == null) return;

        var s = UiScale;

        if (Stats.ReticleBoundsColor.a > 0f)
        {
            var bounds = ScreenToGuiRect(GetReticleBoundsRect());
            var arm = Mathf.Min(bounds.width, bounds.height) * 0.12f;
            DrawCornerBrackets(bounds, Stats.ReticleBoundsColor, Mathf.Max(1f, s), arm);
        }

        var box = GetBoxRect();
        var guiBox = ScreenToGuiRect(box);

        DrawBox(guiBox, Stats.BoxColor, Mathf.Max(1f, 2f * s));

        var crossColor = HasLock
            ? Stats.LockColor
            : (_lockedTarget != null ? Color.Lerp(Stats.CrosshairColor, Stats.LockColor, _lockProgress) : Stats.CrosshairColor);

        var cross = new Vector2(_crosshairScreen.x, Screen.height - _crosshairScreen.y);
        var size = Stats.CrosshairSize * s;
        var thickness = Mathf.Max(1f, Stats.LineThickness * s);
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

    static void DrawCornerBrackets(Rect r, Color c, float t, float arm)
    {
        var prev = GUI.color;
        GUI.color = c;
        var tex = Texture2D.whiteTexture;

        GUI.DrawTexture(new Rect(r.xMin, r.yMin, arm, t), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, arm), tex);

        GUI.DrawTexture(new Rect(r.xMax - arm, r.yMin, arm, t), tex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, arm), tex);

        GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, arm, t), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - arm, t, arm), tex);

        GUI.DrawTexture(new Rect(r.xMax - arm, r.yMax - t, arm, t), tex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - arm, t, arm), tex);

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

        GUI.DrawTexture(new Rect(pos.x - size, pos.y - size, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x - size, pos.y - size, t, arm), tex);
        GUI.DrawTexture(new Rect(pos.x + size - arm, pos.y - size, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x + size - t, pos.y - size, t, arm), tex);
        GUI.DrawTexture(new Rect(pos.x - size, pos.y + size - t, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x - size, pos.y + size - arm, t, arm), tex);
        GUI.DrawTexture(new Rect(pos.x + size - arm, pos.y + size - t, arm, t), tex);
        GUI.DrawTexture(new Rect(pos.x + size - t, pos.y + size - arm, t, arm), tex);
        GUI.color = prev;
    }
}
