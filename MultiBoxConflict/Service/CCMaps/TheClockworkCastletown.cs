using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;

namespace MultiBoxConflict.Service.CCMaps;
//Clockwork Yojimbo
//Clockwork Onmyoji
//Trick Floor
public class TheClockworkCastletown : CrystallineConflictMap
{
    public TheClockworkCastletown(Configuration config, Vector3 playerSpawnPos)
    {
        Config = config;
        SpawnA = new Vector3(-70, 0, -30);
        SpawnB = new Vector3(70, 0, 30);
        SafespotA = new Vector3(-60, 0, -23);
        SafespotB = new Vector3(60, 0, 23);
        MatchStartDelay = 1200;
        DesiredRangedProximity = 20;
        
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

        var startPos = Team == 0 ? new Vector3(-19,0,-30) : new Vector3(19,0,30);

        if (ExitSpawnFlag == 1)
        {
            if ((Team == 0 && localPlayer.Position.X  >= -10) || ( Team == 1 && localPlayer.Position.X <= 10))
            {
                ExitSpawnFlag = 2;
                return false;
            }
            Move(startPos, true);
        }

        return true;
    }

    public override Dictionary<Vector3, float> AdjustPositionsForMap(Dictionary<Vector3, float> positions)
    {
        var yojimbos = Svc.Objects.Where(o => o.Name.TextValue == "Clockwork Yojimbo").ToList();
        var onmyojis = Svc.Objects.Where(o => o.Name.TextValue == "Clockwork Onmyoji").ToList();
        var trickFloors = Svc.Objects.Where(o => o.Name.TextValue == "Trick Floor").ToList();
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return positions;
        }
    
        var positionKeys = positions.Keys.ToList();
    
        foreach (var obj in yojimbos)
        {
            if (obj is IBattleChara battleChara && battleChara.IsCasting && battleChara.CastActionId == 32365)
            {
                const float maxDistanceSq = 225.0f; 
                
                foreach (var pos in positionKeys)
                {
                    if (Vector3.DistanceSquared(pos, obj.Position) <= maxDistanceSq)
                    {
                        positions[pos] -= 100;
                    }
                }
            }
        }
        
        foreach (var obj in onmyojis)
        {
            if (obj is IBattleChara battleChara && battleChara.IsCasting && battleChara.CastActionId == 32366)
            {
                float distSqPlayerToMob = Vector3.DistanceSquared(localPlayer.Position, obj.Position);
    
                foreach (var pos in positionKeys)
                {
                    if (Vector3.DistanceSquared(pos, obj.Position) <= distSqPlayerToMob && Vector3.DistanceSquared(pos, obj.Position) <= 2500)
                    {
                        positions[pos] -= 15;
                    }
                }
            }
        }

        foreach (var obj in trickFloors)
        {
            foreach (var pos in positionKeys)
            {
                if (Math.Abs(pos.X - obj.Position.X) <= 5 && Math.Abs(pos.Z - obj.Position.Z) <= 5)
                {
                    positions[pos] -= 10;
                }
            }
        }
        
        int[] xSide = [-1, 1];
        int[] zSide = [-1, 1];
        foreach (var pos in positionKeys) //vnavmesh thinks it can walk into these
        {
            foreach (var a in xSide)
            {
                foreach (var b in zSide)
                {
                    if (Math.Abs(pos.X - 17.5f * a) <= 7.5f && Math.Abs(pos.Z - 12.5f * b) <= 7.5f)
                    {
                        positions[pos] -= 1000;
                    }
                    if (Math.Abs(pos.X - 42.5f * a) <= 7.5f && Math.Abs(pos.Z - 12.5f * b) <= 7.5f)
                    {
                        positions[pos] -= 1000;
                    }
                    if (Math.Abs(pos.X - 52.5f * a) <= 2.5f && Math.Abs(pos.Z - 20f * b) <= 5f)
                    {
                        positions[pos] -= 1000;
                    }
                    if (Math.Abs(pos.X - 7.5f * a) <= 2.5f && Math.Abs(pos.Z - 20f * b) <= 5f)
                    {
                        positions[pos] -= 1000;
                    }
                }
            }
        }

        return base.AdjustPositionsForMap(positions);
    }

    public override bool GoHeal()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        
        var trickFloors = Svc.Objects.Where(o => o.Name.TextValue == "Trick Floor").ToList();
        foreach (var obj in trickFloors)
        {
            if (Math.Abs(localPlayer.Position.X - obj.Position.X) <= 5 && Math.Abs(localPlayer.Position.Z - obj.Position.Z) <= 5)
            {
                Move(GetSafespot(), true);
                return true;
            }
        }

        return base.GoHeal();
    }
}