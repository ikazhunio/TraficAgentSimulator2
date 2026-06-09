using System.Collections.Generic;
using UnityEngine;

public class CrossingPoint : MonoBehaviour
{
    [Header("Conexión")]
    [Tooltip("El otro punto de cruce hacia donde irán los peatones")]
    public CrossingPoint otherPoint;

    [Header("Área de Patrullaje")]
    [Tooltip("Waypoints donde patrullan los peatones mientras están en este lado del mapa")]
    public Transform[] patrolWaypoints;

    [Header("Capacidad")]
    [Tooltip("Máximo de peatones que pueden esperar aquí antes de entrar en modo estrés")]
    public int maxCapacity = 3;

    [Tooltip("Segundos que espera un peatón en un punto lleno antes de perseguir al jugador")]
    public float stressWaitTime = 8f;

    private readonly List<PedestrianController> _waiting = new();
    public IReadOnlyList<PedestrianController> WaitingPedestrians => _waiting;
    public bool IsFull => _waiting.Count >= maxCapacity;

    public void RegisterArrival(PedestrianController ped)
    {
        if (_waiting.Contains(ped)) return;
        _waiting.Add(ped);

        // Si se llenó el punto, notifica a todos los que esperan
        if (IsFull)
        {
            foreach (var p in new List<PedestrianController>(_waiting))
                p.OnPointFull(stressWaitTime);
        }
    }

    public void UnregisterDeparture(PedestrianController ped)
    {
        _waiting.Remove(ped);
    }

    /// <summary>
    /// Llamado por PlayerInteraction al hacer clic en un peatón de este punto.
    /// Todos los peatones esperando cruzan al otro punto.
    /// </summary>
    public void TriggerCrossing()
    {
        if (otherPoint == null) return;

        var toCross = new List<PedestrianController>(_waiting);
        _waiting.Clear();

        foreach (var ped in toCross)
            ped.StartCrossing(otherPoint);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1f);
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Gizmos.DrawSphere(transform.position, 1f);

        if (patrolWaypoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] == null) continue;
                Gizmos.DrawWireSphere(patrolWaypoints[i].position, 0.3f);
                if (i > 0 && patrolWaypoints[i - 1] != null)
                    Gizmos.DrawLine(patrolWaypoints[i - 1].position, patrolWaypoints[i].position);
            }
        }
    }
#endif
}