using System.Numerics;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;

public class ThePalaistra : CrystallineConflictMap
{
    public ThePalaistra(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(-75, 4, 10);
        SpawnB = new Vector3(75, 4, -10);
        SafespotA = new Vector3(-67, 4, 0);
        SafespotB = new Vector3(67, 4, 0);
        DesiredRangedProximity = 18f;
        BadLoSAreas =
        [
            [40, 55, -25, -15],
            [-55, -40, 15, 25],
            [13, 23, -23, -15],
            [-23, 13, 15, 23],
        ];
        
        if (playerSpawnPos.X < 0)
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

        var startPos = Team == 0 ? new Vector3(-30,2,10) : new Vector3(30,2,-10);

        if (Vector3.DistanceSquared(localPlayer.Position, startPos) <= 4)
        {
            return false;
        }

        Move(startPos, true);
        return true;
    }
}