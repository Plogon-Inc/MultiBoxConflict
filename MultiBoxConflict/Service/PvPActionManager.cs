using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MultiBoxConflict.Service;

public static class PvPActionManager
{
    public static uint LastGlobalSequence = 0;
    private static TaskManager _taskManager = new TaskManager();

    public static void Reset()
    {
        LastGlobalSequence = 0;
    }
    
    public static GameAction[] Actions =
    [
        new GameAction(
            "PurifyHighMP",
            "Any",
            29056,
            minMp: 0.7f,
            minAggro: 100,
            blacklistedStatuses:[PvPStatus.Covered, PvPStatus.Guard],
            customCondition: () =>
            {
                if (Player.ClassJob.Value.NameEnglish.ExtractText() == "Bard" || Player.ClassJob.Value.NameEnglish.ExtractText() == "White Mage")
                    return Player.Status.Where(s => s.RemainingTime >= 1.2f).Select(e => e.StatusId).Intersect([
                            PvPStatus.Stun,
                            PvPStatus.MiracleOfNature,
                            PvPStatus.DeepFreeze,
                            PvPStatus.Silence,
                        ]
                    ).Any();
                else    
                    return Player.Status.Where(s => s.RemainingTime >= 1.2f).Select(e => e.StatusId).Intersect([
                            PvPStatus.Stun,
                            PvPStatus.MiracleOfNature,
                            PvPStatus.DeepFreeze,
                            PvPStatus.Heavy,
                            PvPStatus.Bind,
                            PvPStatus.Silence,
                        ]
                    ).Any();
            }
        ),
        
        new GameAction(
            "PurifyLowMP",
            "Any",
            29056,
            minMp: 0.4f,
            minAggro: 101,
            blacklistedStatuses:[PvPStatus.Covered, PvPStatus.Guard],
            customCondition: () =>
            {
                return Player.Status.Where(s => s.RemainingTime >= 1.2f).Select(e => e.StatusId).Intersect([
                        PvPStatus.Stun,
                        PvPStatus.MiracleOfNature,
                        PvPStatus.DeepFreeze,
                    ]
                    ).Any();
            }
        ),
        
        new GameAction(
            "PurifyAntlion",
            "Any",
            29056,
            minMp: 0.2f,
            blacklistedStatuses:[PvPStatus.Guard],
            customCondition: () =>
            {
                return Svc.Objects.Any(o => o.Name.TextValue == "Red Sands Antlion" 
                                            && o is IBattleChara battleObj 
                                            && battleObj.IsCasting 
                                            && battleObj.CastActionId == 35754
                                            && battleObj.TotalCastTime - battleObj.CurrentCastTime <= 1.9f
                                            && Vector3.DistanceSquared(Player.Position, o.Position) < 529);
            }
        ),
        
        new GameAction(
            "Guard",
            "Any",
            29054,
            minAggro: 950,
            maxHp: 0.75f,
            blacklistedStatuses: [
                PvPStatus.Guard,
                PvPStatus.Stun,
                PvPStatus.MiracleOfNature,
                PvPStatus.Silence,
                PvPStatus.Hysteria,
                PvPStatus.Seduced,
                PvPStatus.Meteodrive,
                PvPStatus.Unguarded,
                PvPStatus.Covered
            ]
        ),
        
        new GameAction(
            "GuardHighDamage",
            "Any",
            29054,
            blacklistedStatuses: [
                PvPStatus.Guard,
                PvPStatus.Stun,
                PvPStatus.MiracleOfNature,
                PvPStatus.Silence,
                PvPStatus.Hysteria,
                PvPStatus.Seduced,
                PvPStatus.Meteodrive,
                PvPStatus.Unguarded,
                PvPStatus.Covered
            ],
            customCondition: () =>
            {
                bool targetedByEffect = false;
                unsafe
                {
                    if (Player.Object != null)
                    {
                        var incomingEffects = Player.Object.Struct()->GetActionEffectHandler()->IncomingEffects;
                        foreach (var effect in incomingEffects)
                        {
                            if (effect.GlobalSequence > LastGlobalSequence && effect.SpellId == 29415)
                            {
                                targetedByEffect = true;
                                LastGlobalSequence = effect.GlobalSequence;
                                Svc.Log.Debug("Target by Marksman's Spite");
                            }
                        }
                    }
                }

                return targetedByEffect
                       ||
                       Utils.GetEnemiesAlive().Any(e =>
                           e.ClassJob.Value.NameEnglish == "Dragoon"
                           && e.StatusList.Any(s => s.StatusId == PvPStatus.SkyHigh)
                           && Vector3.DistanceSquared(Player.Position, e.Position) <= 49
                       )
                       ||
                       Svc.Objects.Any(o =>
                           o.Name.TextValue == "Bomb Core"
                           && o is IBattleChara battleChara
                           && battleChara.IsCasting
                           && battleChara.CastActionId == 28726 
                           && battleChara.TotalCastTime - battleChara.CurrentCastTime <= 1.2f
                           && Utils.IsInBomb(Player.Position, o.Position)
                       )
                       ||
                       Svc.Objects.Any(o =>
                           o.Name.TextValue == "Clockwork Yojimbo"
                           && o is IBattleChara battleChara
                           && battleChara.IsCasting
                           && battleChara.TotalCastTime - battleChara.CurrentCastTime <= 1.2f
                           && Vector3.DistanceSquared(Player.Position, o.Position) <= 225
                       );
            }
        ),
        
        new GameAction(
            "Sprint",
            "Any",
            29057,
            enemyCheckRange: 27,
            maxEnemies: 0,
            blacklistedStatuses:[
                PvPStatus.Guard,
                PvPStatus.Sprint,
                PvPStatus.Stun,
                PvPStatus.Heavy,
                PvPStatus.Bind,
                PvPStatus.Silence,
                PvPStatus.MiracleOfNature,
                PvPStatus.DeepFreeze,
                PvPStatus.Hysteria,
                PvPStatus.Seduced,
                PvPStatus.Meteodrive,
                PvPStatus.SkyHigh,
            ]
        ),
        
        new GameAction(
            "ElixirCovered",
            "Any",
            29055,
            maxMp: 0.6f,
            customCondition: () =>
            {
                return Player.Status.Any(s => s.StatusId == PvPStatus.Covered && s.RemainingTime >= 4.0f);
            }
        ),
        
        new GameAction(
            "Final Fantasia",
            "Bard",
            29401,
            minHp: 0.6f,
            minMp: 0.5f,
            minAllies: 2,
            allyCheckRange: 30,
            minEnemies: 2,
            enemyCheckRange: 30,
            maxAggro: 800,
            earlyDisableRotation: true
        ),
        
        new GameAction(
            "Soul Resonance",
            "Black Mage",
            29662,
            minHp: 0.6f,
            minMp: 0.5f,
            minAllies: 2,
            allyCheckRange: 25,
            minEnemies: 2,
            enemyCheckRange: 22,
            maxAggro: 800,
            earlyDisableRotation: true
        ),
        
        new GameAction(
            "Aetherial Manipulation",
            "Black Mage",
            29660,
            minHp: 0.75f,
            minMp: 0.4f,
            minAllies: 1,
            allyCheckRange: 25,
            minEnemies: 1,
            maxEnemies: 2,
            enemyCheckRange: 35,
            maxAggro: 101,
            customCondition: () =>
                {
                    var localPlayer = Svc.Objects.LocalPlayer;
                    if (localPlayer == null)
                    {
                        Svc.Log.Error("Could not find local player");
                        return false;
                    }
                    unsafe
                    {
                        if (localPlayer.TargetObject == null
                            || ActionManager.GetActionInRangeOrLoS(29660, localPlayer.GameObject(),
                                localPlayer.TargetObject.Struct()) != 0)
                            return false;
                    }
                    var tacticalCrystal = Svc.Objects.FirstOrDefault(o => o.Name.TextValue == "Tactical Crystal");
                    if (tacticalCrystal == null)
                    {
                        Svc.Log.Error("Could not find Tactical Crystal object");
                        return false;
                    }
                    var allies = Utils.GetAlliesAlive().ToList();
                    if (Svc.Objects.LocalPlayer!.TargetObject != null
                        && Vector3.DistanceSquared(Svc.Objects.LocalPlayer!.TargetObject.Position,
                            Svc.Objects.LocalPlayer.Position) <= 36)
                        return false;
                    return allies.Any(a => Vector3.DistanceSquared(a.Position, tacticalCrystal.Position) <= 49);
                }
            ),
        
        
        new GameAction(
            "Wreath of Ice",
            "Black Mage",
            41478,
            minMp: 0.3f,
            minEnemies: 1,
            enemyCheckRange: 25,
            minAggro: 600
        ),
        
        new GameAction(
            "Afflatus Purgation",
            "White Mage",
            29230,
            minHp: 0.6f,
            minMp: 0.3f,
            minAllies: 2,
            allyCheckRange: 30,
            minEnemies: 2,
            enemyCheckRange: 40,
            maxAggro: 999,
            customCondition: () =>
            {
                var localPlayer = Svc.Objects.LocalPlayer;
                if (localPlayer == null)
                {
                    Svc.Log.Error("Could not find local player");
                    return false;
                }

                unsafe
                {
                    var enemies = Utils.GetEnemiesAlive()
                        .Where(e => ActionManager.GetActionInRangeOrLoS(29230, localPlayer.GameObject(),
                            e.GameObject()) != 0)
                        .Where(e => !e.StatusList.Select(s => s.StatusId).Intersect([PvPStatus.Guard, PvPStatus.SkyHigh, PvPStatus.HallowedGround]).Any()).ToList();

                foreach (var target in enemies)
                {
                    if (Vector3.DistanceSquared(localPlayer.Position, target.Position) <= 4) continue;
                    var affected = Utils.GetPlayersInDirectionalRectangle(localPlayer.Position, target.Position, enemies);
                    float spellScore = 0;
                    foreach (var a in affected)
                    {
                        if (a.CurrentHp <= 18000)
                        {
                            spellScore += 2f;
                            continue;
                        }
                        if (a.StatusList.Select(s => s.StatusId).Contains(PvPStatus.Resilience))
                        {
                            spellScore += 0.5f;
                        }
                        else
                        {
                            spellScore += 1;
                        }

                        if (a.CurrentHp <= 30000) spellScore += 0.25f;
                    }

                    if (spellScore >= 2.25f) return true;
                }
                return false;
                }
            },
            customExecution: () =>
            {
                var localPlayer = Svc.Objects.LocalPlayer;
                if (localPlayer == null)
                {
                    Svc.Log.Error("Could not find local player");
                    return;
                }

                var enemies = Utils.GetEnemiesAlive()
                    .Where(e => Vector3.DistanceSquared(localPlayer.Position, e.Position) <= 1600)
                    .Where(e => !e.StatusList.Select(s => s.StatusId).Intersect([PvPStatus.Guard, PvPStatus.SkyHigh, PvPStatus.HallowedGround]).Any()).ToList();
                
                if (enemies.Count == 0) return;
                
                float bestSpellScore = 0f;
                var bestTarget = enemies[0];
                
                foreach (var target in enemies)
                {
                    if (Vector3.DistanceSquared(localPlayer.Position, target.Position) <= 4) continue;
                    var affected = Utils.GetPlayersInDirectionalRectangle(localPlayer.Position, target.Position, enemies);
                    float spellScore = 0;
                    foreach (var a in affected)
                    {
                        if (a.CurrentHp <= 18000)
                        {
                            spellScore += 2f;
                            continue;
                        }
                        if (a.StatusList.Select(s => s.StatusId).Contains(PvPStatus.Resilience))
                        {
                            spellScore += 0.5f;
                        }
                        else
                        {
                            spellScore += 1;
                        }

                        if (a.CurrentHp <= 30000) spellScore += 0.25f;
                    }

                    if (spellScore > bestSpellScore)
                    {
                        bestSpellScore = spellScore;
                        bestTarget = target;
                    }
                }

                unsafe
                {
                    ActionManager.Instance()->UseAction(ActionType.Action, 29230, bestTarget.GameObjectId);
                }
            }
        ),
        
        new GameAction(
            "Seraph Strike",
            "White Mage",
            29229,
            minEnemies: 1,
            enemyCheckRange: 20,
            maxAggro: 900,
            customCondition: () =>
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
                
                if(Vector3.DistanceSquared(localPlayer.Position, tacticalCrystal.Position) > 900) return false;
                
                unsafe
                {
                    if (localPlayer.TargetObject == null
                        || ActionManager.GetActionInRangeOrLoS(29229, localPlayer.GameObject(),
                            localPlayer.TargetObject.Struct()) != 0)
                        return false;
                }

                var targetId = localPlayer.TargetObjectId;
                var targetEnemy = Utils.GetEnemiesAlive().Where(e => e.GameObjectId == targetId).ToList();
                if (targetEnemy.Count == 0) return false;
                var target = targetEnemy.First();
                if (target.StatusList.Select(s => s.StatusId)
                    .Intersect([PvPStatus.Guard, PvPStatus.Covered, PvPStatus.SkyHigh]).Any()) return false;

                var allies = Utils.GetAlliesAlive();
                return allies.Any(a => Vector3.DistanceSquared(a.Position, tacticalCrystal.Position) <= 49) 
                       || Vector3.DistanceSquared(localPlayer.Position, tacticalCrystal.Position) > 49
                       || Vector3.DistanceSquared(target.Position, tacticalCrystal.Position) <= 49;
            }
        ),
        
        new GameAction(
            "Aquaveil",
            "White Mage",
            29227,
            minEnemies: 1,
            enemyCheckRange: 25,
            maxHp: 0.95f,
            minAggro: 800
        ),
    ];

    public static bool Execute(GameAction action)
    {
        if (_taskManager.IsBusy || !action.CanExecute()) return false;
        
        _taskManager.Enqueue(()=>{Chat.ExecuteCommand("/rotation off");});
        if (action.EarlyDisableRotation)
            _taskManager.EnqueueDelay(700);
        _taskManager.Enqueue(action.ExecuteAction);
        _taskManager.EnqueueDelay(600);
        _taskManager.Enqueue(()=>{Chat.ExecuteCommand("/rotation auto");});
        return true;
    }
    
    public static void ExecuteAll()
    {
        if (!_taskManager.IsBusy)
        {
            foreach (var action in Actions)
            {
                if(Execute(action)) break;
            }
        }
    }

    public static void ForceGuard()
    {
        var action = new GameAction(
            "ForceGuard",
            "Any",
            29054,
            earlyDisableRotation: true
            );
        Execute(action);
    }
}