using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace MultiBoxConflict.Service.CCMaps;


//Turbulence
public class CloudNine : CrystallineConflictMap
{
    public CloudNine(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(-90, 7, 80);
        SpawnB = new Vector3(90, 7, -80);
        SafespotA = new Vector3(-80, 7, 45);
        SafespotB = new Vector3(80, 7, -45);
        MatchStartDelay = 2500;
        BadLoSAreas =
        [
            [5, 30, -30, -10],
            [-30, -5, 10, 30],
            [63, 87, -50, -30],
            [-87, -63, 30, 50],
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

        if(ExitSpawnFlag == 1)
        {
            if ((Team == 0 && localPlayer.Position.X > -70) || (Team == 1 && localPlayer.Position.X < 70))
            {
                ExitSpawnFlag = 2;
            }
            else
            {
                var jumpPadPos = Team == 0 ? new Vector3(-76, 8.3f, 72.5f) : new Vector3(76, 8.3f, -72.5f); //jump pads
                Move(jumpPadPos, true);
            }
        }
        
        if(ExitSpawnFlag == 2)
        {
            var startPos = Team == 0 ? new Vector3(-28, 0, 0) : new Vector3(28, 0, 0);
            if (Vector3.DistanceSquared(localPlayer.Position, startPos) <= 4)
            {
                return false;
            }
            Move(startPos, true);
        }
        return true;
    }

    public override Dictionary<Vector3, float> AdjustPositionsForMap(Dictionary<Vector3, float> positions)
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return positions;
        }
        
        var turbulenceObjects = Svc.Objects.Where(o => o.Name.TextValue == "Turbulence").ToList();
        var points = positions.Keys.ToList();
        foreach (var obj in turbulenceObjects)
        {
            foreach (var pos in points)
            {
                if (Utils.LineIntersectsCircle(localPlayer.Position, pos, obj.Position, 7))
                {
                    positions[pos] -= 20;
                }
            }
        }
        return positions;
    }

    public override unsafe int PriorityMapActions()
    {
        if(GenericHelpers.TryGetAddonByName("PvPMKSBattleLog", out AtkUnitBase* battleLogAddon))
        {
            var node3 = battleLogAddon->GetNodeById(3);
            var textNode = node3->GetAsAtkTextNode();
            var text = textNode->NodeText.ToString();
            if (text.StartsWith("Turbulence incoming in"))
            {
                int seconds = int.Parse(Regex.Match(text, @"\d+").Value);
                if (seconds <= 3 && seconds > 0)
                {
                    PvPActionManager.ForceGuard();
                }
                return 0;
            }
        }

        return 0;
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
        
        bool takeLaunchPad = (Team == 0 && tacticalCrystal.Position.X >= 5) || (Team == 1 && tacticalCrystal.Position.X <= -5);
        
        if (ExitSpawnFlag == 0)
        {
            if (Vector3.DistanceSquared(localPlayer.Position, GetSpawn()) <= 4)
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
            if(takeLaunchPad && (localPlayer.Position.X < -70 || localPlayer.Position.X > 70))
            {
                Move(Team == 0 ? new Vector3(-76, 8.3f, 72.5f) : new Vector3(76, 8.3f, -72.5f), true);
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