using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using MultiBoxConflict.Service.IPC;

namespace MultiBoxConflict.Service.CCMaps;

//Tactical Crystal
//Bishop Autoturret
public abstract class CrystallineConflictMap
{
    public Configuration Config = null!;
    internal static VnavmeshIPC _vnav = new();
    private Random Random = new();
    public static TaskManager TaskManager = new();
    public List<Vector3> LastTopPositions = new();
    public DateTime? HealEnding = null;
    public DateTime DutyStartTime;

    public Vector3 SpawnA;
    public Vector3 SpawnB;
    public Vector3 SafespotA;
    public Vector3 SafespotB;
    public ushort Team;
    public float DesiredRangedProximity = 21;
    public float LoSZonePenalty = 5f;
    public float[][] BadLoSAreas = [];

    public int ExitSpawnFlag = 0;
    public int MatchStartDelay = 2000;

    public Vector3 GetSpawn()
    {
        return Team == 0 ? SpawnA : SpawnB;
    }

    public virtual Vector3 GetSafespot()
    {
        return Team == 0 ? SafespotA : SafespotB;
    }
    
    public void Move(Vector3 position, bool bigMove = false, bool adjust = true)
    {
        if(adjust)
        {
            var adjusted = _vnav.NearestPointReachable(position, 5, 5);
            if (adjusted.HasValue)
            {
                position = adjusted.Value;
            }
        }
        if (!bigMove || EzThrottler.Throttle($"big_move_{ExitSpawnFlag}", 399))
        {
            _vnav.PathfindAndMoveTo(position, false);
        }
    }

    public List<Vector3> GetPointsList()
    {
        List<Vector3> points = [];
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return points;
        }
        var allies = Utils.GetAlliesAlive().ToList();
        var enemies = Utils.GetEnemiesAlive().ToList();
        var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
        if (tacticalCrystal == null)
        {
            Svc.Log.Error("Could not find Tactical Crystal object");
            return points;
        }
        
        points.Add(localPlayer.Position);
        float[] ranges = [5, 15];
        foreach (var range in ranges)
        {
            float rd = range / 1.414f;
            points.Add(localPlayer.Position with { X = localPlayer.Position.X + range });
            points.Add(localPlayer.Position with { Z = localPlayer.Position.Z + range });
            points.Add(localPlayer.Position with { X = localPlayer.Position.X - range });
            points.Add(localPlayer.Position with { Z = localPlayer.Position.Z - range });
            points.Add(localPlayer.Position with { X = localPlayer.Position.X + rd, Z = localPlayer.Position.Z + rd });
            points.Add(localPlayer.Position with { X = localPlayer.Position.X - rd, Z = localPlayer.Position.Z + rd });
            points.Add(localPlayer.Position with { X = localPlayer.Position.X + rd, Z = localPlayer.Position.Z - rd });
            points.Add(localPlayer.Position with { X = localPlayer.Position.X - rd, Z = localPlayer.Position.Z - rd });
        }

        for (int i = 0; i < allies.Count; i++)
        {
            var allyA = allies[i];
            points.Add(allyA.Position);
            for (int j = i + 1; j < allies.Count; j++)
            {
                var allyB = allies[j];
                var averagePosition = (allyA.Position + allyB.Position) / 2;
                points.Add(averagePosition);
            }
            points.Add((allyA.Position + tacticalCrystal.Position) / 2);
        }

        if (!Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText()))
        {
            foreach (var enemy in enemies)
                points.Add(enemy.Position);
        }

        return points;
    }

    public List<Vector3> SanitizePointsList(List<Vector3> points)
    {        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return [];
        }
        var sanitizedList = points
            .Select(p => _vnav.NearestPointReachable(p, 5, 5))
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .Where(p => Vector3.DistanceSquared(p, localPlayer.Position) <= 900)
            .ToList();
        
        var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
        if (tacticalCrystal == null)
        {
            Svc.Log.Error("Could not find Tactical Crystal object");
            return sanitizedList;
        }
        sanitizedList.Add(tacticalCrystal.Position);
        return sanitizedList;
    }
    
    public Dictionary<Vector3, float> EvaluatePoints(List<Vector3> points)
    {
        Dictionary<Vector3, float> scores = new();
        
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return scores;
        }
        var allies = Utils.GetAlliesAlive().ToList();
        var enemies = Utils.GetEnemiesAlive().ToList();
        var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
        if (tacticalCrystal == null)
        {
            Svc.Log.Error("Could not find Tactical Crystal object");
            return scores;
        }
        string[] aoeNames = ["Bishop Autoturret"];
        var aoeObjects = Svc.Objects.Where(o => aoeNames.Contains(o.Name.TextValue)).ToList();

        var missingHpMult = 0.5f + 1 / (1 + ((float)localPlayer.CurrentHp / localPlayer.MaxHp));
        var missingManaMult = 0.5f + 1 / (1 + ((float)localPlayer.CurrentMp / localPlayer.MaxMp));
        var aggroMultiplier = 1 + Aggro.Value / 2000;
        var squishyMultiplier = 1f;
        if (Utils.JobIsSquishy(localPlayer.ClassJob.Value.NameEnglish.ExtractText())) squishyMultiplier = 1.3f;
        var guardedMultiplier = localPlayer.StatusList.Any(s => s.StatusId == PvPStatus.Guard) ? 2f : 1f;
        var totalEnemyMultiplier = missingHpMult * missingManaMult * aggroMultiplier * squishyMultiplier * guardedMultiplier;
        
        foreach (var point in points)
        {
            if (scores.ContainsKey(point)) continue;
            
            float score = 0;
            
            if (Vector3.DistanceSquared(point, tacticalCrystal.Position) <= 625) score += 5;
            if ((DateTime.Now - DutyStartTime >= TimeSpan.FromSeconds(30) || Config.Wintrade) && Vector3.DistanceSquared(point, tacticalCrystal.Position) <= 16)
            {
                score += 2f;
                if(!allies.Any(a=>Vector3.DistanceSquared(a.Position, tacticalCrystal.Position) <= 17.64)){
                    if (enemies.Count(e => Vector3.DistanceSquared(e.Position, tacticalCrystal.Position) <= 484) <= 1)
                    {
                        score += 8;
                    }

                    if (DateTime.Now - DutyStartTime >= TimeSpan.FromSeconds(295))
                    {
                        score += 15;
                    }
                }
            }
            
            bool canAttack = false;
            foreach (var enemy in enemies)
            {
                if (Vector3.DistanceSquared(point, enemy.Position) <= 36)
                {
                    canAttack = true;
                    if (Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText()))
                        score -= 4 * totalEnemyMultiplier;
                    else
                        score -= 2 * totalEnemyMultiplier;
                }
                else if (Vector3.DistanceSquared(point, enemy.Position) <= DesiredRangedProximity*DesiredRangedProximity)
                {
                    if(Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText()))
                        canAttack = true;

                    if (Utils.JobIsRanged(enemy.ClassJob.Value.NameEnglish.ExtractText()))
                    {
                        score -= 2.5f * totalEnemyMultiplier;
                    }
                    else
                    {
                        score -= 1f * totalEnemyMultiplier;
                    }
                }
            }

            if (canAttack)
            {
                score += 4;
                if (!Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText())) score += 3;
            }

            foreach (var ally in allies)
            {
                if (Vector3.DistanceSquared(point, ally.Position) <= 324)
                {
                    score += 0.6f;
                }
                if (Vector3.DistanceSquared(point, ally.Position) <= 64)
                {
                    score += 0.4f;
                    if (Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText()) ==
                        Utils.JobIsRanged(ally.ClassJob.Value.NameEnglish.ExtractText()))
                    {
                        score += 1f;
                    }
                }
            }

            foreach (var aoeObj in aoeObjects)
            {
                if(aoeObj is IBattleChara battleAoe)
                {
                    var ownerId = battleAoe.OwnerId;
                    var owner = Svc.Objects.SearchById(ownerId);
                    if (owner != null)
                    {
                        if (enemies.Select(o=>o.GameObjectId).Contains(owner.GameObjectId))
                        {
                            score -= 2.5f;
                        }
                        else
                        {
                            score += 0.5f;
                        }
                    }
                }
            }

            if(Utils.JobIsRanged(localPlayer.ClassJob.Value.NameEnglish.ExtractText()))
                foreach (var area in BadLoSAreas)
                {
                    if (point.X >= area[0] && point.X <= area[1] && point.Z >= area[2] && point.Z <= area[3])
                    {
                        score -= LoSZonePenalty;
                    }
                }
            
            score += 0.01f / Math.Max(1f, Vector3.Distance(point, tacticalCrystal.Position));
            
            scores.Add(point, score);
        }

        return scores;
    }

    public virtual Dictionary<Vector3, float> AdjustPositionsForMap(Dictionary<Vector3, float> positions)
    {
        return positions;
    }

    public virtual bool OnMatchStart()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }

        var spawnPos = GetSpawn();
        
        if (Vector3.DistanceSquared(localPlayer.Position, spawnPos) <= 4)
        {
            ExitSpawnFlag = 1;
            return false;
        }
        
        if(ExitSpawnFlag == 0)
        {        
            if (Vector3.DistanceSquared(localPlayer.Position, spawnPos) <= 4)
            {
                ExitSpawnFlag = 1;
                return false;
            }
            Move(spawnPos, true);
        }
        
        return true;
    }

    public virtual void SkirmishMove()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return;
        }

        if (localPlayer.ClassJob.Value.NameEnglish.ExtractText() == "Black Mage"
            && (!localPlayer.StatusList.Select(s => s.StatusId).Intersect([
                PvPStatus.UmbralFireIii, PvPStatus.UmbralIceIii, PvPStatus.Paradox]).Any() || localPlayer.StatusList.Select(s => s.StatusId).Contains(PvPStatus.SoulResonance)) // if is not about to cast a movement gcd
            && PvPStatus.StatusCanAct(localPlayer.StatusList))
            unsafe {
                if (localPlayer.TargetObject != null
                    && ActionManager.GetActionInRangeOrLoS(29649, localPlayer.GameObject(),
                        localPlayer.TargetObject.Struct()) == 0
                    && (ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Action, 29649) == 0 || ActionManager.Instance()->GetRecastTimeElapsed(ActionType.Action, 29649) >= 1.5f)
                    && Aggro.Value < 1000
                    && (float)localPlayer.CurrentMp / localPlayer.MaxMp > 0.2f
                   )
                {
                    Svc.Log.Debug("Stopping to cast");
                    _vnav.Stop();
                    return;
                }
            }
        
        var points = GetPointsList();
        points = SanitizePointsList(points);
        points.AddRange(LastTopPositions);
        var positions = EvaluatePoints(points);
        positions = AdjustPositionsForMap(positions);

        var topPairs = positions
            .OrderByDescending(pair => pair.Value)
            .Take(5)
            .ToList();

        if (topPairs.Count == 0)
        {
            Svc.Log.Debug("No positions evaluated.");
            return;
        }
        
        LastTopPositions = topPairs.Select(pair => pair.Key).ToList();

        Vector3 bestPosition = LastTopPositions[0];

        if (Config.DisplayPositionMarkers) 
        {
            Utils.DisplayPositions(positions, bestPosition);
        }
    
        Move(bestPosition, false, false);
    }

    public virtual unsafe bool GoHeal()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }

        var closestEnemy = Utils.GetEnemiesAlive()
            .OrderBy(o => Vector3.DistanceSquared(o.Position, localPlayer.Position))
            .FirstOrDefault();
        
        bool canHeal = false;

        if (closestEnemy == null)
            canHeal = true;
        else if (Vector3.DistanceSquared(closestEnemy.Position, localPlayer.Position) > 1225)
            canHeal = true;
        else if (Vector3.DistanceSquared(closestEnemy.Position, localPlayer.Position) > 400 && localPlayer.IsCasting &&
                 localPlayer.CastActionId == 29055)
        {
            if (localPlayer.CurrentCastTime >= 3)
            {
                Chat.ExecuteCommand("/rotation NoCasting");
            }
            if (localPlayer.CurrentCastTime >= 4)
            {
                HealEnding = DateTime.Now;
            }
            
            return true;
        }
        else if (Vector3.DistanceSquared(localPlayer.Position, GetSafespot()) <= 4)
        {
            if (closestEnemy.TargetObject == null || closestEnemy.TargetObject.GameObjectId != localPlayer.GameObjectId)
                canHeal = true;
            else if (Vector3.DistanceSquared(closestEnemy.Position, GetSafespot()) > 625)
                canHeal = true;
            else
                return false;
        }
        if (canHeal)
        {
            _vnav.Stop();
            if (!localPlayer.IsCasting)
            {
                if (HealEnding == null || DateTime.Now - HealEnding > TimeSpan.FromSeconds(2))
                {
                    var actMgr = ActionManager.Instance();
                    actMgr->UseAction(ActionType.Action, 29055); //Use Elixir
                }
            }
            else if (localPlayer.CastActionId == 29055 && localPlayer.CurrentCastTime >= 4)
                HealEnding = DateTime.Now;
        }
        else
        {
            Move(GetSafespot(), true);
        }

        return true;
    }

    public virtual bool RetreatMove()
    {
        var safespot = GetSafespot();
        if (Random.NextSingle() <= 0.2)
        {
            safespot.X += Random.NextSingle() * 6 - 3;
            safespot.Z += Random.NextSingle() * 6 - 3;
        }
        Move(safespot, true);
        return true;
    }

    public virtual bool Reengage()
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

        var closestEnemy = Utils.GetEnemiesAlive()
            .OrderBy(o => Vector3.DistanceSquared(o.Position, localPlayer.Position))
            .FirstOrDefault();

        if ((closestEnemy != null && Vector3.DistanceSquared(closestEnemy.Position, localPlayer.Position) <= 841)
            || Vector3.DistanceSquared(tacticalCrystal.Position, localPlayer.Position) <= 841)
        {
            return false;
        }

        Move(tacticalCrystal.Position, true);
        return true;
    }

    public virtual bool Respawn()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        
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
        
        else if (ExitSpawnFlag == 1)
            return Reengage();
        
        return true;
    }

    public virtual int PriorityMapActions()
    {
        return 0;
    }

    public unsafe bool Sprint()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        
        var closestEnemy = Utils.GetEnemiesAlive()
            .OrderBy(o => Vector3.DistanceSquared(o.Position, localPlayer.Position))
            .FirstOrDefault();
        
        var actMgr = ActionManager.Instance();
        if ((Player.Status.All(e => e.StatusId != 1342) && actMgr->GetActionStatus(ActionType.Action, 29057) == 0)&&
            (closestEnemy == null || Vector3.DistanceSquared(localPlayer.Position, closestEnemy.Position) > 625))
        {
                actMgr->UseAction(ActionType.Action, 29057); //Use Sprint
                return true;
        }
        return false;
    }
}