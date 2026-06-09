using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianController : MonoBehaviour, IStunnable
{
    [Header("Referencias")]
    public Transform player;
    public PlayerInputBlocker playerInputBlocker;

    [Header("Puntos de Cruce")]
    [Tooltip("Los 2 puntos de cruce del mapa. El peatón irá al más cercano cada cierto tiempo.")]
    public CrossingPoint[] crossingPoints;

    [Header("Patrullaje")]
    [Tooltip("Cada cuántos segundos el peatón decide ir al punto de cruce más cercano")]
    public float patrolToPointInterval = 20f;

    [Header("Interacción con Jugador")]
    [Tooltip("Distancia máxima desde la que el jugador puede hacer clic en este peatón")]
    public float interactRange = 4f;

    [Header("Persecución (modo estrés)")]
    public float stunRange = 1.5f;

    [Header("Visual - Outline")]
    [Tooltip("Renderer del modelo del peatón (para mostrar el outline de interacción)")]
    public Renderer modelRenderer;
    [Tooltip("Color del outline cuando el jugador puede interactuar")]
    public Color outlineColor = Color.yellow;
    [Tooltip("Intensidad del brillo del outline")]
    public float outlineIntensity = 2f;

    // ── Estado ─────────────────────────────────────────────────────────────
    private enum State { Patrolling, MovingToPoint, WaitingAtPoint, Crossing, Chasing, StunPending }
    private State currentState = State.Patrolling;

    private NavMeshAgent agent;
    private float patrolTimer = 0f;
    private int waypointIndex = 0;
    private Transform[] currentPatrolWaypoints;
    private CrossingPoint currentPoint;
    private bool stunRequested = false;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        _mpb = new MaterialPropertyBlock();

        // Determina en qué lado del mapa empieza por el punto de cruce más cercano
        currentPoint = FindNearestCrossingPoint();
        if (currentPoint != null)
            currentPatrolWaypoints = currentPoint.patrolWaypoints;

        SetOutline(false);
        BeginPatrol();
    }

    void Update()
    {
        switch (currentState)
        {
            case State.Patrolling:
                UpdatePatrol();
                break;

            case State.MovingToPoint:
                UpdateMovingToPoint();
                break;

            case State.WaitingAtPoint:
                // Espera — el jugador hace clic para cruzar
                break;

            case State.Crossing:
                UpdateCrossing();
                break;

            case State.Chasing:
                UpdateChasing();
                break;

            case State.StunPending:
                // Quieto esperando turno en la cola de stuns
                break;
        }
    }

    // ── Patrullaje ─────────────────────────────────────────────────────────
    void BeginPatrol()
    {
        currentState = State.Patrolling;
        patrolTimer = 0f;
        GoToNextWaypoint();
    }

    void UpdatePatrol()
    {
        // Avanza al siguiente waypoint al llegar al actual
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            GoToNextWaypoint();

        // Cada cierto tiempo va al punto de cruce más cercano
        patrolTimer += Time.deltaTime;
        if (patrolTimer >= patrolToPointInterval)
        {
            patrolTimer = 0f;
            MoveToNearestCrossingPoint();
        }
    }

    void GoToNextWaypoint()
    {
        if (currentPatrolWaypoints == null || currentPatrolWaypoints.Length == 0) return;
        waypointIndex = (waypointIndex + 1) % currentPatrolWaypoints.Length;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(currentPatrolWaypoints[waypointIndex].position);
    }

    // ── Moverse al punto de cruce ──────────────────────────────────────────
    void MoveToNearestCrossingPoint()
    {
        CrossingPoint nearest = FindNearestCrossingPoint();
        if (nearest == null) return;

        currentPoint = nearest;
        currentState = State.MovingToPoint;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(currentPoint.transform.position);
    }

    void UpdateMovingToPoint()
    {
        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        // Llegó al punto de cruce
        agent.ResetPath();
        currentState = State.WaitingAtPoint;
        currentPoint.RegisterArrival(this);
    }

    // ── Cruzar al otro punto (llamado por CrossingPoint.TriggerCrossing) ───
    public void StartCrossing(CrossingPoint destination)
    {
        currentState = State.Crossing;
        currentPoint = destination;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(destination.transform.position);
    }

    void UpdateCrossing()
    {
        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        // Llegó al otro lado — registra y retoma patrullaje en el nuevo área
        agent.ResetPath();
        currentPoint.RegisterArrival(this);
        currentPatrolWaypoints = currentPoint.patrolWaypoints;
        waypointIndex = 0;
        BeginPatrol();
    }

    // ── Modo estrés: punto lleno → perseguir al jugador ───────────────────
    /// <summary>Llamado por CrossingPoint cuando se supera la capacidad máxima.</summary>
    public void OnPointFull(float waitTime)
    {
        if (currentState != State.WaitingAtPoint) return;
        currentPoint.UnregisterDeparture(this);

        // Espera el tiempo de estrés y luego empieza a perseguir
        Invoke(nameof(BeginChasing), waitTime);
    }

    void BeginChasing()
    {
        if (currentState == State.Chasing || currentState == State.StunPending) return;
        currentState = State.Chasing;
        stunRequested = false;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh && player != null)
            agent.SetDestination(player.position);
    }

    void UpdateChasing()
    {
        if (player == null || !agent.isActiveAndEnabled) return;
        if (agent.isOnNavMesh) agent.SetDestination(player.position);

        if (stunRequested) return;

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= stunRange)
        {
            stunRequested = true;
            currentState = State.StunPending;
            if (agent.isOnNavMesh) agent.ResetPath();

            if (StunManager.Instance != null)
                StunManager.Instance.RequestStun(this);
            else if (playerInputBlocker != null)
                playerInputBlocker.BlockInput();
        }
    }

    public void NotifyStunApplied() { /* Animación de ataque, VFX, etc. */ }

    // ── Outline de interacción ─────────────────────────────────────────────
    public void SetOutline(bool active)
    {
        if (modelRenderer == null) return;
        modelRenderer.GetPropertyBlock(_mpb);

        if (active)
        {
            modelRenderer.material.EnableKeyword("_EMISSION");
            _mpb.SetColor(EmissionColorID, outlineColor * outlineIntensity);
        }
        else
        {
            _mpb.SetColor(EmissionColorID, Color.black);
        }

        modelRenderer.SetPropertyBlock(_mpb);
    }

    // ── Propiedades públicas para PlayerInteraction ───────────────────────
    public bool IsWaitingAtPoint => currentState == State.WaitingAtPoint;
    public CrossingPoint CurrentPoint => currentPoint;

    // ── Utilidades ─────────────────────────────────────────────────────────
    CrossingPoint FindNearestCrossingPoint()
    {
        if (crossingPoints == null || crossingPoints.Length == 0) return null;

        CrossingPoint nearest = null;
        float minDist = float.MaxValue;
        foreach (var cp in crossingPoints)
        {
            if (cp == null) continue;
            float d = Vector3.Distance(transform.position, cp.transform.position);
            if (d < minDist) { minDist = d; nearest = cp; }
        }
        return nearest;
    }

    void OnDisable()
    {
        CancelInvoke();
        SetOutline(false);
        if (StunManager.Instance != null)
            StunManager.Instance.CancelStun(this);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
#endif
}