using System.Linq;
using System.Numerics;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;

public class TheBaysideBattleground : CrystallineConflictMap
{
    public TheBaysideBattleground(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(12, -2, 100);
        SpawnB = new Vector3(188, -2, 100);
        SafespotA = new Vector3(42, -2, 98);
        SafespotB = new Vector3(158, -2, 102);
        BadLoSAreas = [
            [99,111, 63, 90],
            [89,101, 110, 138],
        ];
        
        if (playerSpawnPos.X < 100)
            Team = 0;
        else 
            Team = 1;
    }

    public override Vector3 GetSafespot()
    {
        var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
        if (tacticalCrystal == null)
        {
            Svc.Log.Error("Could not find Tactical Crystal object");
            return base.GetSafespot();
        }

        if (Team == 0 && tacticalCrystal.Position.X >= 108 && tacticalCrystal.Position.X <= 160 && tacticalCrystal.Position.Z >= 125)
        {
            return new Vector3(80, -4, 50);
        }
        if (Team == 1 && tacticalCrystal.Position.X <= 92 && tacticalCrystal.Position.X >= 40 && tacticalCrystal.Position.Z <= 75)
        {
            return new Vector3(120, -4, 150);
        }
        return base.GetSafespot();
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

        if(ExitSpawnFlag == 1)
        {
            if ((Team == 0 && localPlayer.Position.X > 80) || (Team == 1 && localPlayer.Position.X < 120))
            {
                ExitSpawnFlag = 2;
                return false;
            }
            var startPos = Team == 0 ? new Vector3(57.5f, -2, 100) : new Vector3(142.5f, -2, 100); //jump pads
            Move(startPos, true);
        }
        return true;
    }

    public override bool Respawn()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        
        var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
        if (tacticalCrystal == null)
        {
            Svc.Log.Error("Could not find Tactical Crystal object");
            return false;
        }
        
        bool takeLaunchPad = (Team == 0 && tacticalCrystal.Position.X >= 108 && tacticalCrystal.Position.Z >= 130)
                             || (Team == 1 && tacticalCrystal.Position.X <= 92 && tacticalCrystal.Position.Z <= 70);
        
        if (ExitSpawnFlag == 0)
        {
            if (Vector3.DistanceSquared(localPlayer.Position, GetSpawn()) <= 9)
            {
                ExitSpawnFlag = 1;
            }
            else
            {
                Move(GetSpawn(), false);
            }
        }
        
        if (ExitSpawnFlag == 1)
        {
            if(takeLaunchPad && ((Team == 0 && localPlayer.Position.X < 80) || (Team == 1 && localPlayer.Position.X > 120)))
            {
                Move(Team == 0 ? new Vector3(57.5f, -2, 100) : new Vector3(142.5f, -2, 100), true);
            }
            else
            {
                ExitSpawnFlag = 2;
            }
        }
        
        if (ExitSpawnFlag == 2)
            return Reengage();
        
        return true;
    }
}