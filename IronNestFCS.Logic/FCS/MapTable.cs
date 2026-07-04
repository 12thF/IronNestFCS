using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public class MapTable {
    private const float MapMinX = 0f;
    private const float MapMaxX = 19.99f;
    private const float MapMinY = 0f;
    private const float MapMaxY = 9.99f;
    private const float DumpOutsideDistance = 2f;
    
    public Transform? turret;
    public Dictionary<int, Transform> artilleries;
    public Transform? fireMissionRoot;
    public FireMission? FireMission;
    private Transform? mapSurface;
    private readonly Dictionary<int, EntityLocation?> markerLocations = new();
    
    public bool TryBind() {
        artilleries = new Dictionary<int, Transform>();
        var turretObject = GameObject.Find("Player Turret Piece");
        if (turretObject == null) {
            MelonLogger.Warning("[FCS] 未找到 Player Turret Piece，当前场景尚未就绪");
            return false;
        }

        var mapObject = GameObject.Find("Draggable Surface");
        if (mapObject == null) {
            MelonLogger.Warning("[FCS] 未找到 Draggable Surface，当前场景尚未就绪");
            return false;
        }

        turret = turretObject.transform;
        mapSurface = mapObject.transform;
        var map = mapObject.transform;
        for (var i = 0; i < map.childCount; ++i) {
            var t = map.GetChild(i);
            if (t.name != "MapToken_Artillery") continue;
            var tmp = t.GetComponentInChildren<Il2CppTMPro.TextMeshPro>();
            if (tmp == null) continue;
            if (!int.TryParse(tmp.text, out var id)) continue;
            artilleries.Add(id, t);
        }
        MelonLogger.Msg($"[FCS] 找到 Player Turret Piece: {turret}, Artilleries: {artilleries.Count}");
        var fireMissionObject = GameObject.Find("Fire Mission Root");
        if (fireMissionObject == null) {
            MelonLogger.Warning("[FCS] 未找到 Fire Mission Root，当前场景尚未就绪");
            return false;
        }

        fireMissionRoot = fireMissionObject.transform;
        FireMission = fireMissionRoot.GetComponent<FireMission>();
        return FireMission != null;
    }

    public void SetMarkerWorldPos(int index, Vector3 worldPos, EntityLocation? location = null)
    {
        if (!artilleries.TryGetValue(index, out var marker)) return;
        if (mapSurface == null) return;
        var local = mapSurface.InverseTransformPoint(worldPos);
        local.z = marker.localPosition.z;
        marker.localPosition = local;
        markerLocations[index] = location;
    }

    public bool TryUpdateTaskFromWorldPos(ArtilleryTask task, Vector3 worldPos, EntityLocation? location = null)
    {
        if (turret == null || mapSurface == null) return false;

        var localPos = mapSurface.InverseTransformPoint(worldPos);
        var target = localPos - turret.localPosition;
        var dist = target.magnitude * 3.8164f;
        var angle = Vector3.SignedAngle(target, Vector3.up, Vector3.forward);
        if (angle < 0) angle += 360;

        task.angel = angle;
        task.distance = dist;
        task.position = localPos * 3.8164f + new Vector3(10.016f, 5.235f, 0f);
        if (location != null) task.location = location;
        return true;
    }

    public void ResetMarker(int index)
    {
        if (!artilleries.TryGetValue(index, out var marker)) return;
        if (turret == null) return;
        marker.localPosition = turret.localPosition;
        markerLocations.Remove(index);
    }

    public void ClearMarkerLocations()
    {
        markerLocations.Clear();
    }

    public void SetMarkerByKmPos(int index, Vector2 kmPos)
    {
        if (!artilleries.TryGetValue(index, out var marker)) return;
        var local = new Vector3(kmPos.x / 3.8164f, kmPos.y / 3.8164f, marker.localPosition.z);
        marker.localPosition = local;
        markerLocations.Remove(index);
    }

    public void SetMarkerLocalPos(int index, Vector2 localPos)
    {
        if (!artilleries.TryGetValue(index, out var marker)) return;
        marker.localPosition = new Vector3(localPos.x, localPos.y, marker.localPosition.z);
        markerLocations.Remove(index);
    }

    public ArtilleryTask? GetMarkTarget(int index) {
        if (turret == null) {
            MelonLogger.Error("[FCS] GetMarkTarget: turret unbound");
            return null;
        }

        if (!artilleries.TryGetValue(index, out var marker)) {
            MelonLogger.Error($"[FCS] GetMarkTarget: index {index} not found, artillery count: {artilleries.Count}");
            return null;
        }

        var target = marker.localPosition - turret.localPosition;
        var dist = target.magnitude * 3.8164f;
        var angle = Vector3.SignedAngle(target, Vector3.up, Vector3.forward);
        if (angle < 0) angle += 360;
        var task = new ArtilleryTask {
            angel = angle,
            distance = dist,
            position = marker.localPosition * 3.8164f + new Vector3(10.016f, 5.235f, 0f),
            location = markerLocations.TryGetValue(index, out var location) ? location : null
        };
        return task;
    }

    public bool IsMarkerInsideTacticalMap(int index) {
        if (!artilleries.TryGetValue(index, out var marker)) return false;
        var kmPos = marker.localPosition * 3.8164f + new Vector3(10.016f, 5.235f, 0f);
        return kmPos.x >= MapMinX && kmPos.x <= MapMaxX
               && kmPos.y >= MapMinY && kmPos.y <= MapMaxY;
    }

    public ArtilleryTask? GetNearestEdgeDumpTarget(int targetId, BulletType bulletType) {
        if (turret == null) {
            MelonLogger.Error("[FCS] GetNearestEdgeDumpTarget: turret unbound");
            return null;
        }

        var pos = turret.localPosition * 3.8164f + new Vector3(10.016f, 5.235f, 0f);
        var x = Mathf.Clamp(pos.x, MapMinX, MapMaxX);
        var y = Mathf.Clamp(pos.y, MapMinY, MapMaxY);
        var left = x;
        var right = MapMaxX - x;
        var bottom = y;
        var top = MapMaxY - y;
        var dump = new Vector2(x, 0f);
        var min = bottom;

        if (top < min) {
            min = top;
            dump = new Vector2(x, MapMaxY + DumpOutsideDistance);
        }
        if (left < min) {
            min = left;
            dump = new Vector2(MapMinX - DumpOutsideDistance, y);
        }
        if (right < min) {
            dump = new Vector2(MapMaxX + DumpOutsideDistance, y);
        }
        if (bottom <= min) {
            dump = new Vector2(x, MapMinY - DumpOutsideDistance);
        }

        var localTarget = new Vector3((dump.x - 10.016f) / 3.8164f, (dump.y - 5.235f) / 3.8164f, turret.localPosition.z);
        var target = localTarget - turret.localPosition;
        var dist = target.magnitude * 3.8164f;
        var angle = Vector3.SignedAngle(target, Vector3.up, Vector3.forward);
        if (angle < 0) angle += 360;
        return new ArtilleryTask {
            targetId = targetId,
            angel = angle,
            distance = dist,
            position = new Vector3(dump.x, dump.y, 0f),
            bulletType = bulletType
        };
    }

    public List<EntityLocation> GetAllFireMissionEntities() {
        List<EntityLocation> res = new();
        if (fireMissionRoot == null) {
            return res;
        }

        for (var i = 0; i < fireMissionRoot.childCount; ++i) {
            var m = fireMissionRoot.GetChild(i).GetComponent<EntityLocation>();
            if (m != null) res.Add(m);
        }
        return res;
    }
    
}
