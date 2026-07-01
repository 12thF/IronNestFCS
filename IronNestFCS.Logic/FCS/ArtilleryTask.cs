using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public enum Progress {
    Pending,
    Ranging,
    Calculating,
    PurchasingAmmo,
    SelectingBullet,
    LoadingBullet,
    WaitLoaderRetract,
    SelectingCharge,
    LoadingPowder,
    WaitLoading,
    WaitTrayRetract,
    ClosingBreech,
    LockingBreech,
    SettingElevation,
    SettingBearing,
    PressingConfirm,
    PressingLock,
    PullingLever,
    ConfirmingFire,
    DumpingWrongShell,
    BackToIdle,
    Finished,
    Failed,
}

public class ArtilleryTask {
    public int targetId;
    public float angel;
    public float distance;
    public Vector3 position;
    public BulletType bulletType;
    public Progress progress;
}
