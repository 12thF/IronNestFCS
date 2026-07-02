using System.Collections;
using System.Reflection;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public class Turret {
    private const float RotationReadyDegrees = 0.35f;
    private const float RotationReadyVelocity = 0.01f;
    private const float MinRotationTimeoutSeconds = 20f;
    private const float MaxRotationTimeoutSeconds = 90f;
    private const float RotationTimeoutPaddingSeconds = 12f;
    private const float AssumedRotationDegreesPerSecond = 3f;

    private TurretController? _turret;
    public bool LastRotationReady { get; private set; }
    public float? CurrentMapAngle => ReadCurrentMapAngle();
    private PropertyInfo? _currentAngleCompassProperty;


    public bool TryBind() {
        var turretObj = GameObject.Find("TurretSystem");
        if (turretObj == null) {
            MelonLogger.Error("[FCS] Aiming: Can't find TurretSystem");
            return false;
        }
        _turret = turretObj.GetComponent<TurretController>();
        CacheAngleProperties();
        return true;
    }
    
    public IEnumerator SetRotation(float angle, Func<bool>? canceled = null) {
        LastRotationReady = false;
        if (_turret == null) {
            MelonLogger.Error("[FCS] Aiming: unbound TurretController");
            yield break;
        }

        var timeoutSeconds = RotationTimeoutFor(angle);
        MelonLogger.Msg($"[FCS] Turret SetRotation start: mapAngle={angle:F2}, current={CurrentMapAngle?.ToString("F2") ?? "unknown"}, desired={-angle:F2}, velocity={_turret.rotationVelocity:F3}, timeout={timeoutSeconds:F1}s");
        _turret.DesiredRotation = -angle;
        yield return new WaitForSeconds(0.2f);
        var waited = 0f;
        while (!IsRotationReady(angle) && waited < timeoutSeconds) {
            if (canceled?.Invoke() == true) {
                yield break;
            }
            if (!Application.isFocused || Time.timeScale <= 0f) {
                yield return null;
                continue;
            }
            _turret.DesiredRotation = -angle;
            yield return new WaitForSeconds(0.1f);
            waited += 0.1f;
        }
        LastRotationReady = IsRotationReady(angle);
        if (LastRotationReady) {
            MelonLogger.Msg($"[FCS] Turret SetRotation ready: mapAngle={angle:F2}, current={CurrentMapAngle?.ToString("F2") ?? "unknown"}, desired={_turret.DesiredRotation:F2}, velocity={_turret.rotationVelocity:F3}");
        }
        else {
            MelonLogger.Warning($"[FCS] Turret SetRotation not ready: mapAngle={angle:F2}, current={CurrentMapAngle?.ToString("F2") ?? "unknown"}, desired={_turret.DesiredRotation:F2}, velocity={_turret.rotationVelocity:F3}");
        }
    }

    public float? DeltaFromCurrent(float targetMapAngle) {
        var current = CurrentMapAngle;
        if (current == null) {
            return null;
        }
        return Mathf.Abs(Mathf.DeltaAngle(current.Value, NormalizeAngle(targetMapAngle)));
    }

    public bool IsReadyFor(float targetMapAngle) {
        return IsRotationReady(targetMapAngle);
    }

    private static float NormalizeAngle(float angle) {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    private bool IsRotationReady(float targetMapAngle) {
        if (_turret == null) return false;
        var current = CurrentMapAngle;
        if (current == null) return Mathf.Abs(_turret.rotationVelocity) <= RotationReadyVelocity;
        var delta = Mathf.Abs(Mathf.DeltaAngle(current.Value, NormalizeAngle(targetMapAngle)));
        return delta <= RotationReadyDegrees && Mathf.Abs(_turret.rotationVelocity) <= RotationReadyVelocity;
    }

    private float RotationTimeoutFor(float targetMapAngle) {
        var current = CurrentMapAngle;
        if (current == null) return MaxRotationTimeoutSeconds;
        var delta = Mathf.Abs(Mathf.DeltaAngle(current.Value, NormalizeAngle(targetMapAngle)));
        var estimated = delta / AssumedRotationDegreesPerSecond + RotationTimeoutPaddingSeconds;
        return Mathf.Clamp(estimated, MinRotationTimeoutSeconds, MaxRotationTimeoutSeconds);
    }

    private void CacheAngleProperties() {
        if (_turret == null) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = _turret.GetType();
        _currentAngleCompassProperty = type.GetProperty("CurrentAngleCompass", flags);
    }

    private float? ReadCurrentMapAngle() {
        if (_turret == null) return null;

        if (TryReadFloatProperty(_currentAngleCompassProperty, out var compassAngle)) {
            return NormalizeAngle(compassAngle);
        }

        return null;
    }

    private bool TryReadFloatProperty(PropertyInfo? property, out float value) {
        value = 0f;
        if (property == null || _turret == null) return false;
        try {
            var raw = property.GetValue(_turret);
            if (raw is float f) {
                value = f;
                return true;
            }
            if (raw is double d) {
                value = (float)d;
                return true;
            }
            if (raw is int i) {
                value = i;
                return true;
            }
        }
        catch {
            return false;
        }
        return false;
    }
}
