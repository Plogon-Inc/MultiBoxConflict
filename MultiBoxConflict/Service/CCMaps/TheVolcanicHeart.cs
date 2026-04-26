using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;

public class TheVolcanicHeart : CrystallineConflictMap
{
    public const float BombArmLength = 70f;
    public const float BombHalfArmWidth = 5f;
        
    public float[][] Obstacles =
    [
        [-10, -5, -30, -25],
        [5, 10, -30, -25],
        [-10, -5, -15, 5],
        [5, 10, -15, 5],
        [-25, -20, -15, -10],
        [20, 25, -15, -10],
        [-55, -35, -15, -10],
        [35, 55, -15, -10],
        [-55, -50, -15, 5],
        [50, 55, -15, 5],
        [-40, -35, 0, 5],
        [35, 40, 0, 5],
        [-25, -5, 0, 5],
        [5, 25, 0, 5],
    ];

    public TheVolcanicHeart(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(-57, -1.5f, -20);
        SpawnB = new Vector3(57, -1.5f, -20);
        SafespotA = new Vector3(-57, 0, -12);
        SafespotB = new Vector3(57, 0, -12);
        DesiredRangedProximity = 15;
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

        var startPos = Team == 0 ? new Vector3(-20,0,-20) : new Vector3(20,0,-20);

        if (Vector3.DistanceSquared(localPlayer.Position, startPos) <= 4)
        {
            return false;
        }

        Move(startPos, true);
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
        
        var bombs = Svc.Objects.Where(o => o.Name.TextValue == "Bomb Core" 
                                           && o is IBattleChara { IsCasting: true, CastActionId: 28726 }).ToList();
    
        var points = positions.Keys.ToList();

        foreach (var obj in bombs)
        {
            foreach (var pos in points.Where(pos => Utils.IsInBomb(pos, obj.Position)))
            {
                positions[pos] -= 20;
            }
            foreach (var pos in points.Where(pos => Utils.IsInBomb((localPlayer.Position + pos) * 0.5f, obj.Position)))
            {
                positions[pos] -= 15;
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

    public override unsafe bool GoHeal()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        var bombs = Svc.Objects.Where(o => o.Name.TextValue == "Bomb Core" 
                                           && o is IBattleChara { IsCasting: true, CastActionId: 28726 }).ToList();
        
        if (bombs.Any(obj => Utils.IsInBomb(localPlayer.Position, obj.Position)))
        {
            return false;
        }
        return base.GoHeal();
    }
}