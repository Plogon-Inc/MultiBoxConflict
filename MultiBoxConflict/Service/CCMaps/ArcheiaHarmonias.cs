using System.Numerics;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;

public class ArcheiaHarmonias : CrystallineConflictMap
{
    public ArcheiaHarmonias(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(30, 1, 117.5f);
        SpawnB = new Vector3(170, 1, 82.5f);
        SafespotA = new Vector3(40, 1, 117.5f);
        SafespotB = new Vector3(160, 1, 82.5f);
        DesiredRangedProximity = 18f;
        BadLoSAreas =
        [
        ];
        
        if (playerSpawnPos.X < 100)
            Team = 0;
        else 
            Team = 1;
    }
    
    public override bool OnMatchStart()
    {
        if (ExitSpawnFlag == 0 && base.OnMatchStart())
            return true;
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }

        var startPos = Team == 0 ? new Vector3(89,0,127) : new Vector3(111,0,73);

        if (Vector3.DistanceSquared(localPlayer.Position, startPos) <= 4)
        {
            return false;
        }

        Move(startPos, true);
        return true;
    }
}