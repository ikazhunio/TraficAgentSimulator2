using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    public PlayerInputBlocker playerInputBlocker;

    [Header("Configuración de Movimiento")]
    public float catchDistance = 0.8f;

    [Header("Stun por Proximidad")]
    public float stunRange = 1.5f;
    private bool stunRequested = false;

    [Header("Ragdoll")]
    public float ragdollUpForce = 6f;
    public float ragdollForwardForce = 4f;
    public float ragdollMinTime = 1.5f;
    public float getUpDuration = 1f;

    [Header("Salida del Padre")]
    public Transform exitPoint;
    private Vector3 exitWorldPosition;

    private enum State { InParent, ExitingParent, Chasing, Ragdoll, GettingUp, StunPending }
    private State currentState = State.InParent;

    private NavMeshAgent agent;
    private Animator animator;
    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private Collider mainCollider;
    private Rigidbody mainRigidbody;
    private float stateTimer = 0f;
    private Vector3 lastMoveDirection;

    private static readonly int AnimGettingUp = Animator.StringToHash("GettingUp");
    private static readonly int AnimRunning = Animator.StringToHash("Running");

    private Quaternion getUpStartRotation;
    private Quaternion getUpTargetRotation;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        mainCollider = GetComponent<Collider>();
        mainRigidbody = GetComponent<Rigidbody>();
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Estado inicial: sin física en huesos, sin collider, sin agente
        InitializePhysicsState();
        if (agent != null) agent.enabled = false;
    }

    void InitializePhysicsState()
    {
        // ── Root Rigidbody: kinematic, sin gravedad ──────────────────────
        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = true;
            mainRigidbody.useGravity = false;
            mainRigidbody.linearVelocity = Vector3.zero;
            mainRigidbody.angularVelocity = Vector3.zero;
            mainRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // ── Collider del root: OFF hasta llegar al exitPoint ─────────────
        if (mainCollider != null) mainCollider.enabled = false;

        // ── Huesos: SIEMPRE kinematic — el Animator los controla ─────────
        // Los huesos nunca deben usar física; solo el root usa física en ragdoll
        foreach (var rb in ragdollBodies)
        {
            if (rb == mainRigidbody) continue;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // ── Colliders de huesos: SIEMPRE desactivados ────────────────────
        // Solo el CapsuleCollider del root interactúa con el mundo
        foreach (var col in ragdollColliders)
        {
            if (col == mainCollider) continue;
            col.enabled = false;
        }

        if (animator != null) animator.enabled = true;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.InParent:
                break;

            case State.ExitingParent:
                MoveToExitPoint();
                break;

            case State.Chasing:
                ChasePlayer();
                CheckStunRange();
                break;

            case State.Ragdoll:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f && IsGrounded())
                    StartGettingUp();
                break;

            case State.GettingUp:
                stateTimer -= Time.deltaTime;

                // Levantarse suave: interpola desde la rotación de caída hacia vertical
                float t = 1f - Mathf.Clamp01(stateTimer / getUpDuration);
                transform.rotation = Quaternion.Slerp(getUpStartRotation, getUpTargetRotation, t);

                if (stateTimer <= 0f)
                    FinishGettingUp();
                break;

            case State.StunPending:
                break;
        }
    }

    // ── Salir del padre ────────────────────────────────────────────────────
    public void ExitParent()
    {
        if (currentState != State.InParent) return;

        exitWorldPosition = exitPoint != null ? exitPoint.position : transform.position;

        transform.SetParent(null);

        // FIX 3: el collider NO se activa aquí — se activa al llegar al exitPoint
        currentState = State.ExitingParent;

        if (animator != null) animator.SetBool(AnimRunning, true);
    }

    void MoveToExitPoint()
    {
        float speed = agent != null ? agent.speed : 3.5f;
        Vector3 toExit = exitWorldPosition - transform.position;
        Vector3 direction = toExit.normalized;

        transform.position += direction * speed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);
        }

        if (toExit.magnitude <= 0.3f)
        {
            // Trigger para detectar colisiones mientras persigue (kinematic no genera OnCollisionEnter)
            if (mainCollider != null)
            {
                mainCollider.enabled = true;
                mainCollider.isTrigger = true;   // ← trigger mientras persigue
            }

            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(transform.position);
            }
            currentState = State.Chasing;
        }
    }

    // ── Perseguir ──────────────────────────────────────────────────────────
    void ChasePlayer()
    {
        if (player == null || agent == null || !agent.isActiveAndEnabled) return;
        if (agent.isOnNavMesh) agent.SetDestination(player.position);
        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;
    }

    // ── Stun por proximidad ────────────────────────────────────────────────
    void CheckStunRange()
    {
        if (stunRequested || player == null) return;

        if (Vector3.Distance(transform.position, player.position) <= stunRange)
        {
            stunRequested = true;
            currentState = State.StunPending;
            if (agent != null) agent.ResetPath();

            if (StunManager.Instance != null)
                StunManager.Instance.RequestStun(this);
            else if (playerInputBlocker != null)
                playerInputBlocker.BlockInput();
        }
    }

    public void NotifyStunApplied()
    {
        if (animator != null) animator.SetBool(AnimRunning, false);
    }
    void OnTriggerEnter(Collider other)
    {
        if (transform.parent != null) return;
        if (currentState != State.Chasing) return;
        if (other.CompareTag("Player")) return;
        if (other.CompareTag("Ground")) return;

        ActivateRagdoll();
    }

    // ── Ragdoll ON ─────────────────────────────────────────────────────────
    void ActivateRagdoll()
    {
        currentState = State.Ragdoll;
        stateTimer = ragdollMinTime;

        if (agent != null) { agent.ResetPath(); agent.enabled = false; }
        if (animator != null) animator.enabled = false;

        if (mainCollider != null)
        {
            mainCollider.enabled = true;
            mainCollider.isTrigger = false;  // ← collider físico para caer y tocar el suelo
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

    // ── Ragdoll OFF → Levantarse ───────────────────────────────────────────
    void StartGettingUp()
    {
        currentState = State.GettingUp;
        stateTimer = getUpDuration;

        // Guarda la rotación actual (tumbado) y la rotación destino (de pie)
        getUpStartRotation = transform.rotation;
        getUpTargetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        // Detiene la física
        if (mainRigidbody != null)
        {
            mainRigidbody.isKinematic = true;
            mainRigidbody.useGravity = false;
            mainRigidbody.linearVelocity = Vector3.zero;
            mainRigidbody.angularVelocity = Vector3.zero;
            mainRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        // Reactiva el Animator — ya NO hace snap de rotación, el Update la interpola
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger(AnimGettingUp);
        }
    }

    void FinishGettingUp()
    {
        if (mainCollider != null)
            mainCollider.isTrigger = true;  // ← vuelve a trigger para seguir detectando

        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(transform.position);
        }
        if (animator != null) animator.SetBool(AnimRunning, true);
        stunRequested = false;
        currentState = State.Chasing;
    }

    // ── Grounded check (usa el root, no los huesos) ────────────────────────
    bool IsGrounded()
    {
        // Pequeño offset hacia arriba para no empezar dentro del suelo
        Vector3 origin = transform.position + Vector3.up * 0.15f;
        return Physics.Raycast(origin, Vector3.down, 0.4f);
    }

    void OnDisable()
    {
        if (StunManager.Instance != null)
            StunManager.Instance.CancelStun(this);
    }

    // ── Debug ──────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [Header("Debug (Solo Editor)")]
    [SerializeField] private bool debugExitParent = false;

    void OnValidate()
    {
        if (Application.isPlaying && debugExitParent)
        {
            debugExitParent = false;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) ExitParent();
            };
        }
    }

    [ContextMenu("Debug: Llamar ExitParent")]
    void DebugExitParent() => ExitParent();

    [ContextMenu("Debug: Forzar Ragdoll ON")]
    void DebugForceRagdollOn() => ActivateRagdoll();

    [ContextMenu("Debug: Forzar Ragdoll OFF")]
    void DebugForceRagdollOff() => StartGettingUp();

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stunRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, 0.1f);
    }
#endif
}