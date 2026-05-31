using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;
    /*[Tooltip("Componente que bloquea los controles del Player al ser tocado")]
    public PlayerInputBlocker playerInputBlocker;*/

    [Header("Configuración")]
    [Tooltip("Distancia a la que el NPC sale del objeto padre")]
    public float exitDistance = 1f;
    [Tooltip("Fuerza hacia arriba cuando choca con un obstáculo")]
    public float ragdollUpForce = 6f;
    [Tooltip("Fuerza hacia adelante cuando choca con un obstáculo")]
    public float ragdollForwardForce = 4f;
    [Tooltip("Tiempo mínimo en modo ragdoll antes de recuperarse")]
    public float ragdollRecoveryTime = 2f;

    // Estados del NPC
    private enum State { InParent, Chasing, Ragdoll }
    private State currentState = State.InParent;

    private NavMeshAgent agent;
    private Rigidbody[] ragdollBodies;   // todos los Rigidbody de los huesos
    private Collider[] ragdollColliders; // todos los Colliders de los huesos
    private Collider mainCollider;
    private Rigidbody mainRigidbody;
    private float ragdollTimer = 0f;
    private Vector3 directionBeforeCollision;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        mainCollider = GetComponentInChildren<Collider>();
        mainRigidbody = GetComponent<Rigidbody>();

        // Recopila todos los rigidbodies/colliders de los huesos del ragdoll
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Inicia con ragdoll desactivado
        SetRagdoll(false);
    }

    void Update()
    {
        switch (currentState)
        {
            case State.InParent:
                // El NPC está dentro del objeto padre, espera a salir
                break;

            case State.Chasing:
                ChasePlayer();
                break;

            case State.Ragdoll:
                ragdollTimer -= Time.deltaTime;
                if (ragdollTimer <= 0f && IsGrounded())
                    RecoverFromRagdoll();
                break;
        }
    }

    // ── Llamar este método para que el NPC salga del objeto padre ──────────
    public void ExitParent()
    {
        if (currentState != State.InParent) return;

        // Se desvincula del padre y pasa a existir en la escena de forma independiente
        transform.SetParent(null);
        currentState = State.Chasing;
    }

    // ── Persecución ────────────────────────────────────────────────────────
    void ChasePlayer()
    {
        if (player == null || agent == null) return;
        if (agent.isOnNavMesh)
            agent.SetDestination(player.position);

        // Guarda la dirección de movimiento por si choca
        if (agent.velocity.sqrMagnitude > 0.1f)
            directionBeforeCollision = agent.velocity.normalized;
    }

    // ── Detección de colisiones ────────────────────────────────────────────
    void OnCollisionEnter(Collision collision)
    {
        if (currentState != State.Chasing) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            // Toca al Player → bloquea sus controles
            /*if (playerInputBlocker != null)
                playerInputBlocker.BlockInput();*/
        }
        else
        {
            // Choca con cualquier otra cosa → ragdoll
            EnterRagdoll();
        }
    }

    // ── Ragdoll ────────────────────────────────────────────────────────────
    void EnterRagdoll()
    {
        currentState = State.Ragdoll;
        ragdollTimer = ragdollRecoveryTime;

        if (agent != null) agent.enabled = false;
        SetRagdoll(true);

        // Aplica impulso: hacia arriba + hacia adelante (para pasar el obstáculo)
        Vector3 force = Vector3.up * ragdollUpForce + directionBeforeCollision * ragdollForwardForce;
        if (mainRigidbody != null)
            mainRigidbody.AddForce(force, ForceMode.Impulse);

        // Si usa ragdoll de huesos, aplica la fuerza a la cadera (primer rigidbody)
        if (ragdollBodies.Length > 0)
            ragdollBodies[0].AddForce(force, ForceMode.Impulse);
    }

    void RecoverFromRagdoll()
    {
        SetRagdoll(false);

        // Reactiva el NavMeshAgent y vuelve a perseguir
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(transform.position); // lo coloca en el NavMesh más cercano
        }

        currentState = State.Chasing;
    }

    void SetRagdoll(bool active)
    {
        // Activa o desactiva física de ragdoll en todos los huesos
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

        if (mainRigidbody != null)
            mainRigidbody.isKinematic = active; // el cuerpo principal cede el control al ragdoll
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, 0.3f);
    }

    // ── DEBUG — solo visible y funcional en el Editor ──────────────────────
#if UNITY_EDITOR
    [Header("Debug (Solo Editor)")]
    [Tooltip("Activa esto en Play Mode para llamar a ExitParent() desde el Inspector")]
    [SerializeField] private bool debugExitParent = false;

    void OnValidate()
    {
        if (Application.isPlaying && debugExitParent)
        {
            debugExitParent = false;

            // Difiere la llamada para que no se ejecute dentro de OnValidate
            // evita el error de SendMessage durante validación
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ExitParent();
                    Debug.Log("[NPCController] ExitParent() llamado desde el Inspector.");
                }
            };
        }
    }

    [ContextMenu("Debug: Llamar ExitParent")]
    void DebugExitParent()
    {
        ExitParent();
        Debug.Log("[NPCController] ExitParent() llamado desde el menú contextual.");
    }
#endif
}
