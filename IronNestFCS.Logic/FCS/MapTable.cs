using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace IronNestFCS.Logic.FCS;

public class MapTable {
    
    public Transform? turret;
    public Dictionary<int, Transform> artilleries;
    public Transform fireMissionRoot;
    public FireMission? FireMission;
    
    public bool TryBind() {
        artilleries = new Dictionary<int, Transform>();
        turret = GameObject.Find("Player Turret Piece").transform;
        var map = GameObject.Find("Draggable Surface").transform;
        for (var i = 0; i < map.childCount; ++i) {
            var t = map.GetChild(i);
            if (t.name != "MapToken_Artillery") continue;
            var tmp = t.GetComponentInChildren<Il2CppTMPro.TextMeshPro>();
            if (!int.TryParse(tmp.text, out var id)) continue;
            artilleries.Add(id, t);
        }
        MelonLogger.Msg($"[FCS] 找到 Player Turret Piece: {turret}, Artilleries: {artilleries.Count}");
        fireMissionRoot = GameObject.Find("Fire Mission Root").transform;
        FireMission = fireMissionRoot.GetComponent<FireMission>();
        return true;
    }

    public ArtilleryTask GetMarkTarget(int index) {
        if (index > artilleries.Count) {
            MelonLogger.Error($"[FCS] GetMarkTarget: index {index} 超出范围");
            return null;
        }
        var target = artilleries[index].position - turret.position;
        var dist = target.magnitude * 4.715f;
        var angle = Vector3.SignedAngle(target, Vector3.right, Vector3.down);
        if (angle < 0) angle += 360;
        var task = new ArtilleryTask {
            angel = angle,
            distance = dist
        };
        return task;
    }

    public List<EntityLocation> GetAllFireMissionEntities() {
        List<EntityLocation> res = new();
        for (var i = 0; i < fireMissionRoot.childCount; ++i) {
            var m = fireMissionRoot.GetChild(i).GetComponent<EntityLocation>();
            res.Add(m);
        }
        return res;
    }
    
}