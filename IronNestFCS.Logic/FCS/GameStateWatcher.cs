using System.Collections;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

/// <summary>
/// 封装 IL2CPP 游戏类状态查询，基于 IDA 逆向确认的属性名。
/// ArtilleryReloadController / GunController / RamFakeParentConstraintController 等。
/// </summary>
public static class GameStateWatcher
{
    /// <summary>获取 ArtilleryReloadController 当前状态索引</summary>
    public static int GetReloadStateIndex(GameObject gunRoot)
    {
        var rc = gunRoot.GetComponentInChildren<ArtilleryReloadController>();
        return rc != null ? rc.CurrentStateIndex : -1;
    }

    /// <summary>获取 ArtilleryReloadController 当前状态名</summary>
    public static string GetReloadStateName(GameObject gunRoot)
    {
        var rc = gunRoot.GetComponentInChildren<ArtilleryReloadController>();
        return rc != null ? rc.CurrentState.ToString() : "null";
    }

    /// <summary>等待装填状态机推进到目标状态</summary>
    public static IEnumerator WaitForReloadState(GameObject gunRoot, int targetState, float timeout = 15f)
    {
        float waited = 0f;
        while (GetReloadStateIndex(gunRoot) < targetState && waited < timeout)
        {
            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
        }
        if (waited >= timeout)
            MelonLogger.Warning($"[GameState] WaitForReloadState timeout at state={GetReloadStateIndex(gunRoot)}, target={targetState}");
    }

    // ──── GunController 状态 ────

    public static bool IsReloading(GunController gc) => gc.IsReloading;
    public static bool CanFire(GunController gc) => gc.CanFire;
    public static float ElevationErrorDeg(GunController gc) => gc.ElevationErrorDeg;
    public static float CurrentElevationSpeed(GunController gc) => gc.CurrentElevationSpeed;
    public static string? ChamberedShell(GunController gc) =>
        gc.ChamberedShellBlueprint?.shellDefinition?.ShellId;
    public static bool IsBreechLocked(GunController gc) =>
        gc.ExternalReloadLoweringLocked;

    // ──── GunController 等待 ────

    /// <summary>等待炮管装填完成（IsReloading=false）</summary>
    public static IEnumerator WaitForReloadComplete(GunController gc, float timeout = 15f)
    {
        float waited = 0f;
        while (gc.IsReloading && waited < timeout)
        {
            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
        }
    }

    /// <summary>等待仰角误差小于阈值</summary>
    public static IEnumerator WaitForElevationSettled(GunController gc, float tolerance = 0.1f, float timeout = 30f)
    {
        float waited = 0f;
        while (Mathf.Abs(gc.ElevationErrorDeg) > tolerance && waited < timeout)
        {
            yield return new WaitForSeconds(0.3f);
            waited += 0.3f;
        }
    }
}
