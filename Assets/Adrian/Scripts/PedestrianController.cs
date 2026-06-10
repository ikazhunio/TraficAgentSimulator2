using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PedestrianController : MonoBehaviour, IStunnable
{
    [Header("Referencias")]
    public Transform player;
    public PlayerInputBlocker playerInputBlocker;

    [Header("Puntos de Cruce")]
    public CrossingPoint[] crossingPoints;

    [Header("Patrullaje")]
    public float patrolToPointInterval = 20f;

    [Header("Interacción con Jugador")]
    public float interactRange = 4f;

    [Header("Persecución (modo estrés)")]
    public float stunRange = 1.5f;

    [Header("Ragdoll")]
    public float ragdollUpForce = 6f;
    public float ragdollForwardForce = 4f;
    public float ragdollMinTime = 1.5f;
    public float getUpDuration = 1f;

    [Header("Visual - Outline")]
    public Renderer modelRenderer;
    public Color outlineColor = Color.yellow;
    public float outlineIntensity = 2f;

    // ── Estados ────────────────────────────────────────────────────────────
    private enum State
    {
        Patrolling, MovingToPoint, WaitingAtPoint,
        Crossing, Chasing, StunPending, Ragdoll, GettingUp
    }
    private State currentState = State.Patrolling;
    private State stateBeforeRagdoll;   // para saber a qué volver al levantarse

    // ── Componentes ────────────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Rigidbody mainRigidbody;
    private Collider mainCollider;

    // ── Variables internas ─────────────────────────────────────────────────
    private float patrolTimer = 0f;
    private int waypointIndex = 0;
    private Transform[] currentPatrolWaypoints;
    private CrossingPoint currentPoint;
    private bool stunRequested = false;
    private Vector3 lastMoveDirection;
    private float stateTimer = 0f;
    private Quaternion getUpStartRotation;
    private Quaternion getUpTargetRotation;

    // ── Outline ────────────────────────────────────────────────────────────
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        mainRigidbody = GetComponent<Rigidbody>();
        mainCollider = GetComponent<Collider>();
        _mpb = new MaterialPropertyBlock();

        InitializePhysicsState();

        currentPoint = FindNearestCrossingPoint();
        if (currentPoint != null)
            currentPatrolWaypoints = currentPoint.patrolWaypoints;

        SetOutline(false);
        BeginPatrol();
    }

    void InitializePhysicsState()
    {
        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = true;
            mainRigidbody.useGravity = false;
            mainRigidbody.linearVelocity = Vector3.zero;
            mainRigidbody.angularVelocity = Vector3.zero;
            mainRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
        // Siempre activo como trigger: el raycast lo detecta y OnTriggerEnter usa el estado para filtrar
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = true;
        }
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
                break;
            case State.Crossing:
                UpdateCrossing();
                break;
            case State.Chasing:
                UpdateChasing();
                break;
            case State.StunPending:
                break;
            case State.Ragdoll:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f && IsGrounded())
                    StartGettingUp();
                break;
            case State.GettingUp:
                stateTimer -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(stateTimer / getUpDuration);
                transform.rotation = Quaternion.Slerp(getUpStartRotation, getUpTargetRotation, t);
                if (stateTimer <= 0f)
                    FinishGettingUp();
                break;
        }
    }

    // ── Patrullaje ─────────────────────────────────────────────────────────
    void BeginPatrol()
    {
        currentState = State.Patrolling;
        patrolTimer = 0f;
        if (agent != null && !agent.enabled) agent.enabled = true;
        GoToNextWaypoint();
    }

    void UpdatePatrol()
    {
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            GoToNextWaypoint();

        patrolTimer += Time.deltaTime;
        if (patrolTimer >= patrolToPointInterval)
        {
            patrolTimer = 0f;
            MoveToNearestCrossingPoint();
        }

        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;
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
        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;

        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        agent.ResetPath();
        currentState = State.WaitingAtPoint;
        currentPoint.RegisterArrival(this);
    }

    // ── Cruzar al otro punto ───────────────────────────────────────────────
    public void StartCrossing(CrossingPoint destination)
    {
        currentState = State.Crossing;
        currentPoint = destination;
        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.SetDestination(destination.transform.position);
    }

    void UpdateCrossing()
    {
        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;

        if (agent.pathPending) return;
        if (agent.remainingDistance > agent.stoppingDistance + 0.2f) return;

        agent.ResetPath();
        currentPoint.RegisterArrival(this);
        currentPatrolWaypoints = currentPoint.patrolWaypoints;
        waypointIndex = 0;
        BeginPatrol();
    }

    // ── Modo estrés ────────────────────────────────────────────────────────
    public void OnPointFull(float waitTime)
    {
        if (currentState != State.WaitingAtPoint) return;
        currentPoint.UnregisterDeparture(this);
        Invoke(nameof(BeginChasing), waitTime);
    }

    void BeginChasing()
    {
        if (currentState == State.Chasing || currentState == State.StunPending) return;
        currentState = State.Chasing;
        stunRequested = false;

        // El collider ya está activo — solo asegura que sea trigger
        if (mainCollider != null) mainCollider.isTrigger = true;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh && player != null)
            agent.SetDestination(player.position);
    }

    void UpdateChasing()
    {
        if (player == null || !agent.isActiveAndEnabled) return;
        if (agent.isOnNavMesh) agent.SetDestination(player.position);
        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;

        if (stunRequested) return;
        if (Vector3.Distance(transform.position, player.position) <= stunRange)
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

    public void NotifyStunApplied() { }

    // ── Colisión → Ragdoll (Trigger) ───────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        // Solo activa ragdoll cuando está persiguiendo al jugador
        if (currentState != State.Chasing) return;

        if (other.CompareTag("Player")) return;
        if (other.CompareTag("Ground")) return;

        ActivateRagdoll();
    }

    // ── Ragdoll ────────────────────────────────────────────────────────────
    void ActivateRagdoll()
    {
        stateBeforeRagdoll = currentState;   // recuerda el estado anterior
        currentState = State.Ragdoll;
        stateTimer = ragdollMinTime;

        if (agent != null) { agent.ResetPath(); agent.enabled = false; }

        // Collider físico para caer y tocar el suelo
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;
        }

        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = false;
            mainRigidbody.useGravity = true;
            mainRigidbody.constraints = RigidbodyConstraints.None;

            Vector3 dir = lastMoveDirection.sqrMagnitude > 0.01f
                          ? lastMoveDirection : transform.forward;

            mainRigidbody.AddForce(
                Vector3.up * ragdollUpForce + dir * ragdollForwardForce,
                ForceMode.Impulse);
            mainRigidbody.AddTorque(
                Random.insideUnitSphere * ragdollUpForce * 0.5f,
                ForceMode.Impulse);
        }
    }

    void StartGettingUp()
    {
        currentState = State.GettingUp;
        stateTimer = getUpDuration;
        getUpStartRotation = transform.rotation;
        getUpTargetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = true;
            mainRigidbody.useGravity = false;
            mainRigidbody.linearVelocity = Vector3.zero;
            mainRigidbody.angularVelocity = Vector3.zero;
            mainRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }
        // Vuelve a trigger — el peatón vuelve a ser detectable por el raycast
        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = true;
        }
    }

    void FinishGettingUp()
    {
        stunRequested = false;
        CancelInvoke(nameof(BeginChasing));

        switch (stateBeforeRagdoll)
        {
            case State.Chasing:
                // Reactiva el trigger para seguir detectando colisiones
                if (mainCollider != null)
                {
                    mainCollider.enabled = true;
                    mainCollider.isTrigger = true;
                }
                currentState = State.Chasing;
                if (agent != null) agent.enabled = true;
                break;

            default:
                // En cualquier otro caso vuelve a patrullar (sin collider activo)
                BeginPatrol();
                break;
        }
    }

    bool IsGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        return Physics.Raycast(origin, Vector3.down, 0.4f);
    }

    // ── Outline ────────────────────────────────────────────────────────────
    public void SetOutline(bool active)
    {
        if (modelRenderer == null)
        {
            Debug.LogWarning($"[PedestrianController] {name}: modelRenderer no está asignado en el Inspector.", this);
            return;
        }

        // .material crea automáticamente una instancia — no modifica el material compartido
        Material mat = modelRenderer.material;

        if (active)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", outlineColor * outlineIntensity);
            DynamicGI.UpdateEnvironment();
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
        }
    }

    // ── Propiedades públicas ───────────────────────────────────────────────
    public bool IsWaitingAtPoint => currentState == State.WaitingAtPoint;
    public CrossingPoint CurrentPoint => currentPoint;

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

    // ── Registro global de todos los peatones activos ─────────────────────
    // PlayerInteraction lo usa para detectar sin depender de colliders
    private static readonly HashSet<PedestrianController> _all =
        new HashSet<PedestrianController>();

    public static IReadOnlyCollection<PedestrianController> All => _all;

    void OnEnable() => _all.Add(this);
    void OnDisable()
    {
        _all.Remove(this);
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
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stunRange);
    }
#endif
}