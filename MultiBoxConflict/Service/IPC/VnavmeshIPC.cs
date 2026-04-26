using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using ECommons;
using ECommons.EzIpcManager;
using ECommons.Logging;

namespace MultiBoxConflict.Service.IPC;

#pragma warning disable CS8632
#pragma warning disable CS8618
public class VnavmeshIPC
{
    [EzIPC("Nav.IsReady", wrapper: SafeWrapper.None)] private readonly Func<bool> _isReadyNoWrapper;
    public bool? IsReady()
    {
        try
        {
            return _isReadyNoWrapper();
        }
        catch(Exception e)
        {
            DuoLog.Error($"Vnavmesh not found, navigation failed");
            e.LogInternal();
            return null;
        }
    }
    [EzIPC("Nav.%m")] public readonly Func<float> BuildProgress;
    [EzIPC("Nav.%m")] public readonly Func<bool> Reload;
    [EzIPC("Nav.%m")] public readonly Func<bool> Rebuild;
    /// <summary>
    /// Vector3 from, Vector3 to, bool fly
    /// </summary>
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind;

    [EzIPC("SimpleMove.%m")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo;
    [EzIPC("SimpleMove.%m")] public readonly Func<bool> PathfindInProgress;

    [EzIPC("Path.%m")] public readonly Action Stop;
    [EzIPC("Path.%m")] public readonly Func<bool> IsRunning;

    /// <summary>
    /// Vector3 p, float halfExtentXZ, float halfExtentY
    /// </summary>
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPointReachable;
    /// <summary>
    /// Vector3 p, bool allowUnlandable, float halfExtentXZ
    /// </summary>
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, bool, float, Vector3?> PointOnFloor;

    public VnavmeshIPC()
    {
        EzIPC.Init(this, "vnavmesh", SafeWrapper.AnyException);
    }
}
