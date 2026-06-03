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
    [Tooltip("Distancia a la que el NPC solicita el stun al jugador")]
    public float stunRange = 1.5f;
    private bool stunRequested = false;

    [Header("Ragdoll")]
    public float ragdollUpForce = 6f;
    public float ragdollForwardForce = 4f;
    public float ragdollMinTime = 1.5f;
    public float getUpDuration = 1f;

    [Header("Salida del Padre")]
    [Tooltip("Objeto vacío hijo del mismo padre que el NPC. El NPC se moverá hacia aquí al salir.")]
    public Transform exitPoint;

    private Vector3 exitWorldPosition; // guarda la posición mundial al momento de salir

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

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        mainCollider = GetComponent<Collider>();
        mainRigidbody = GetComponent<Rigidbody>();
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();
        ToggleRagdoll(false);
        if (agent != null) agent.enabled = false;
    }

    void Update()
    {
        switch (currentState)
        {
            case State.InParent:
                break;

            case State.Chasing:
                ChasePlayer();
                CheckStunRange();   // ← detección por distancia en vez de colisión
                break;

            case State.Ragdoll:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f && IsGrounded())
                    StartGettingUp();
                break;

            case State.GettingUp:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0f)
                    FinishGettingUp();
                break;

            case State.StunPending:
                // El NPC se queda quieto esperando su turno en la cola de stuns
                break;

            case State.ExitingParent:
                MoveToExitPoint();
                break;
        }
    }

    // ── Salir del padre ────────────────────────────────────────────────────
    public void ExitParent()
    {
        if (currentState != State.InParent) return;

        // Guarda la posición mundial del exitPoint ANTES de desheretarse
        // (por si el padre sigue moviéndose después)
        if (exitPoint != null)
            exitWorldPosition = exitPoint.position;
        else
            exitWorldPosition = transform.position; // fallback: se queda donde está

        // Se desheredea del padre
        transform.SetParent(null);

        currentState = State.ExitingParent;

        if (animator != null) animator.SetBool(AnimRunning, true);
    }

    // ── Perseguir ──────────────────────────────────────────────────────────
    void ChasePlayer()
    {
        if (player == null || agent == null || !agent.isActiveAndEnabled) return;
        if (agent.isOnNavMesh) agent.SetDestination(player.position);
        if (agent.velocity.sqrMagnitude > 0.01f)
            lastMoveDirection = agent.velocity.normalized;
    }

    // ── Detección de rango de stun ─────────────────────────────────────────
    void CheckStunRange()
    {
        if (stunRequested || player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance <= stunRange)
        {
            stunRequested = true;
            currentState = State.StunPending;

            // Detiene al NPC mientras espera su turno
            if (agent != null) agent.ResetPath();

            // Solicita el stun al manager (entra a la cola si hay otro activo)
            if (StunManager.Instance != null)
                StunManager.Instance.RequestStun(this);
            else if (playerInputBlocker != null)
                playerInputBlocker.BlockInput(); // fallback si no hay StunManager
        }
    }

    /// <summary>
    /// Llamado por el StunManager cuando le toca el turno a este NPC.
    /// </summary>
    public void NotifyStunApplied()
    {
        // Aquí puedes reproducir animación de ataque, sonido, VFX, etc.
        if (animator != null) animator.SetBool(AnimRunning, false);
    }

    // ── Colisiones para Ragdoll (solo objetos que no sean Player) ──────────
    void OnCollisionEnter(Collision collision)
    {
        if (currentState != State.Chasing) return;
        if (!collision.gameObject.CompareTag("Player"))
            ActivateRagdoll();
    }

    // ── Ragdoll ────────────────────────────────────────────────────────────
    void ActivateRagdoll()
    {
        currentState = State.Ragdoll;
        stateTimer = ragdollMinTime;
        if (agent != null) agent.enabled = false;
        ToggleRagdoll(true);

        Vector3 impulse = Vector3.up * ragdollUpForce + lastMoveDirection * ragdollForwardForce;
        if (ragdollBodies.Length > 0)
            ragdollBodies[0].AddForce(impulse, ForceMode.Impulse);
        else if (mainRigidbody != null)
            mainRigidbody.AddForce(impulse, ForceMode.Impulse);

        if (animator != null)
        {
            animator.enabled = false;
            animator.SetBool(AnimRunning, false);
        }
    }

    void StartGettingUp()
    {
        currentState = State.GettingUp;
        stateTimer = getUpDuration;
        ToggleRagdoll(false);
        if (animator != null)
        {
            animator.enabled = true;
            animator.SetTrigger(AnimGettingUp);
        }
    }

    void FinishGettingUp()
    {
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.Warp(transform.position);
        }
        if (animator != null) animator.SetBool(AnimRunning, true);
        stunRequested = false;  // resetea por si se recuperó del ragdoll
        currentState = State.Chasing;
    }

    void MoveToExitPoint()
    {
        Vector3 direction = (exitWorldPosition - transform.position).normalized;
        transform.position += direction * agent.speed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, agent.angularSpeed * Time.deltaTime);
        }

        float distance = Vector3.Distance(transform.position, exitWorldPosition);
        if (distance <= 0.3f)
        {
            // Llegó al punto de salida → ahora persigue al jugador con NavMesh
            if (agent != null)
            {
                agent.enabled = true;
                if (agent.isOnNavMesh) agent.Warp(transform.position);
            }
            currentState = State.Chasing;
        }
    }

    void ToggleRagdoll(bool active)
    {
        foreach (var rb in ragdollBodies)
        {
            if (rb == mainRigidbody) continue;
            rb.isKinematic = !active;
        }
        foreach (var col in ragdollColliders)
        {
            if (col == mainCollider) continue;
            col.enabled = active;
        }
        if (mainCollider != null) mainCollider.enabled = !active;
        if (mainRigidbody != null) mainRigidbody.isKinematic = active;
    }

    bool IsGrounded()
    {
        Vector3 origin = ragdollBodies.Length > 0
            ? ragdollBodies[0].position : transform.position;
        return Physics.Raycast(origin, Vector3.down, 0.4f);
    }

    void OnDisable()
    {
        // Si el NPC es desactivado mientras está en cola, se elimina
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

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stunRange);
    }
#endif
}