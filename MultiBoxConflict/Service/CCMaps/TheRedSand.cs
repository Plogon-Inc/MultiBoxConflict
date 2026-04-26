using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;

//Geyser - Casting(35755)
//Red Sands Antlion - Casting(35754[)
//Sabotender
//Acutender
public class TheRedSands : CrystallineConflictMap
{
    public float[][] Obstacles =
    [
        [12, 33, 5.5f, 20],
        [-33, -12, -20, -5.5f],
    ];
    public TheRedSands(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(-102, 2, -50);
        SpawnB = new Vector3(102, 2, 50);
        SafespotA = new Vector3(-90, 2, -41);
        SafespotB = new Vector3(90, 2, 41);
        MatchStartDelay = 1000;
        
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
            if ((Team == 0 && localPlayer.Position.Z < -10) || (Team == 1 && localPlayer.Position.Z > 10))
            {
                var jumpPadPos = Team == 0 ? new Vector3(-95, 0.8f, -28) : new Vector3(95, 0.8f, 28); //jump pads
                Move(jumpPadPos, true);
            }
            else
            {
                ExitSpawnFlag = 2;
            }
        }
        if (ExitSpawnFlag == 2)
        {
            var startPos = Team == 0 ? new Vector3(-34, -1, 0) : new Vector3(34, -1, 0);
            if (Vector3.DistanceSquared(localPlayer.Position, startPos) <= 1)
            {
                return false;
            }
            Move(startPos, true);
        }
        return true;
    }

    public override Dictionary<Vector3, float> AdjustPositionsForMap(Dictionary<Vector3, float> positions)
    {
        var badPitObjects = Svc.Objects
            .Where(o => o.Name.TextValue is "Red Sands Antlion" or "Sabotender" 
                        && o is IBattleChara battleObj  
                        && battleObj.IsCasting
                        && battleObj.CastActionId is 35754 or 35752).ToList();
        var acutenderObjects = Svc.Objects
            .Where(o => o.Name.TextValue == "Acutender"
                        && o is IBattleChara battleObj  
                        && battleObj.IsCasting).ToList();
        var geyserObjects = Svc.Objects
            .Where(o => o.Name.TextValue == "Geyser"
                        && o is IBattleChara battleObj  
                        && battleObj.IsCasting).ToList();
        
        var points = positions.Keys.ToList();
        foreach (var obj in badPitObjects)
        {
            foreach (var pos in points)
            {
                if (Vector3.DistanceSquared(pos, obj.Position) <= 529)
                {
                    positions[pos] -= 100;
                }
            }
        }
        foreach (var obj in acutenderObjects)
        {
            foreach (var pos in points)
            {
                if (Vector3.DistanceSquared(pos, obj.Position) <= 529)
                {
                    positions[pos] += 3;
                }
            }
        }
        
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return positions;
        }

        foreach (var obj in geyserObjects)
        {
            foreach (var pos in points)
            {
                if (Vector3.DistanceSquared(pos, obj.Position) <= 1)
                {
                    if (Vector3.DistanceSquared(obj.Position, new Vector3(-52, -1, -8)) <= 1 || Vector3.DistanceSquared(obj.Position, new Vector3(52, -1, 8)) <= 1)
                        positions[pos] -= 8f;
                    else if (Vector3.DistanceSquared(obj.Position, localPlayer.Position) <= 36)
                        positions[pos] += 2f;
                }
            }
        }
        
        foreach(var point in points)
        {
            foreach (var area in Obstacles)
            {
                if (point.X >= area[0] && point.X <= area[1] && point.Z >= area[2] && point.Z <= area[3])
                {
                    positions[point] -= 1000;
                }
            }
        }
        
        return positions;
    }
    
    
    public override bool GoHeal()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        
        var badPitObjects = Svc.Objects
            .Where(o => o.Name.TextValue is "Red Sands Antlion" or "Sabotender" 
                        && o is IBattleChara battleObj  
                        && battleObj.IsCasting).ToList();
        
        foreach (var obj in badPitObjects)
        {
            if (Vector3.DistanceSquared(localPlayer.Position, obj.Position) <= 529)
            {
                Move(GetSafespot(), true);
                return true;
            }
        }

        return base.GoHeal();
    }
}