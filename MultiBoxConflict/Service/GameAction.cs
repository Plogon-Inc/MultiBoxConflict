using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ECommons.Automation.NeoTaskManager;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace MultiBoxConflict.Service;

public class GameAction(
    string name,
    string job,
    uint actionId,
    ActionType actionType = ActionType.Action,
    float minHp = 0.0f,
    float minMp = 0.0f,
    float maxHp = 1f,
    float maxMp = 1f,
    float minAggro = 0.0f,
    float maxAggro = 9999.0f,
    float allyCheckRange = 20f,
    uint minAllies = 0,
    float enemyCheckRange = 20f,
    uint minEnemies = 0,
    uint maxEnemies = 5,
    uint[]? blacklistedStatuses = null,
    bool earlyDisableRotation = false,
    Func<bool>? customCondition = null,
    Action? customExecution = null)
{
    
    public string Name = name;
    public string Job = job;
    public uint ActionId = actionId;
    public ActionType ActionType = actionType;

    public float MinHp = minHp;
    public float MinMp = minMp;
    public float MaxHp = maxHp;
    public float MaxMp = maxMp;
    public float MinAggro = minAggro;
    public float MaxAggro = maxAggro;
    public float AllyCheckRange = allyCheckRange;
    public uint MinAllies = minAllies;
    public float EnemyCheckRange = enemyCheckRange;
    public uint MinEnemies = minEnemies;
    public uint MaxEnemies = maxEnemies;
    public uint[] BlacklistedStatuses = blacklistedStatuses ?? [
        PvPStatus.Guard,
        PvPStatus.Stun,
        PvPStatus.MiracleOfNature,
        PvPStatus.Silence,
        PvPStatus.Hysteria,
        PvPStatus.Seduced,
        PvPStatus.Meteodrive,
    ];
    public bool EarlyDisableRotation = earlyDisableRotation;
    

    public Func<bool>? CustomCondition = customCondition;

    
    public Action ExecuteAction = customExecution ?? (() =>
    {
        Svc.Log.Info("Executing action: " + name);
        unsafe {
            ActionManager.Instance()->UseAction(actionType, actionId);
        }
    });
    
    public unsafe bool CanExecute()
    {
        var localPlayer = Svc.Objects.LocalPlayer;
        if (localPlayer == null)
        {
            Svc.Log.Error("Could not find local player");
            return false;
        }
        var allies = Utils.GetAlliesAlive().Where(o => Vector3.DistanceSquared(localPlayer.Position, o.Position) <= AllyCheckRange*AllyCheckRange).ToList();
        var enemies = Utils.GetEnemiesAlive().Where(o => Vector3.DistanceSquared(localPlayer.Position, o.Position) <= EnemyCheckRange*EnemyCheckRange).ToList();
        return
            (ActionManager.Instance()->GetActionStatus(ActionType, ActionId) == 0) &&
            (Job == "Any" || Job == localPlayer.ClassJob.Value.NameEnglish.ExtractText()) &&
            ((float)localPlayer.CurrentHp / localPlayer.MaxHp >= MinHp) &&
            ((float)localPlayer.CurrentMp / localPlayer.MaxMp >= MinMp) &&
            ((float)localPlayer.CurrentHp / localPlayer.MaxHp <= MaxHp) &&
            ((float)localPlayer.CurrentMp / localPlayer.MaxMp <= MaxMp) &&
            (Aggro.Value >= MinAggro && Aggro.Value <= MaxAggro) &&
            (allies.Count >= MinAllies) &&
            (enemies.Count >= MinEnemies) &&
            (enemies.Count <= MaxEnemies) &&
            (!localPlayer.StatusList.Select(s=>s.StatusId).Intersect(BlacklistedStatuses).Any()) &&
            (CustomCondition == null || CustomCondition());
    }
}