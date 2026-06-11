using UnityEngine;
using UnityEngine.Events;

public class CarController : MonoBehaviour
{
    [Header("Waypoints")]
    [Tooltip("Arrastra los WaypointController en orden")]
    public WaypointController[] waypoints;
    public UnityEvent[] wayPointsEvents;


    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 5f;
    [Tooltip("Distancia a la que se considera que el carro llegó al waypoint")]
    public float arrivalThreshold = 0.4f;

    [Header("Direccionales")]
    [Tooltip("Ángulo mínimo (en grados) para activar una direccional")]
    public float indicatorAngleThreshold = 30f;
    public bool leftIndicator = false;
    public bool rightIndicator = false;
    public GameObject dirLeft;
    public GameObject dirRigth;

    [Header("Eventos Globales")]
    [Tooltip("Se dispara al llegar al último waypoint")]
    public UnityEvent OnLastWaypointReached;
    [Tooltip("Se dispara cuando el carro se topa con otro carro en cualquier waypoint")]
    public UnityEvent<GameObject> OnCarEncountered;

    // ── Estado interno ─────────────────────────────────────────────────────
    private int currentIndex = 0;
    private bool isMoving = false;
    private bool isStopped = false;
    private bool initialized = false;

    // ── Debug ──────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    [Header("Debug (Solo Editor)")]
    [SerializeField] private bool debugAdvance = false;

    void OnValidate()
    {
        if (Application.isPlaying && debugAdvance)
        {
            debugAdvance = false;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Advance();
            };
        }
    }

    [ContextMenu("Debug: Llamar Advance")]
    void DebugAdvance() => Advance();

    [ContextMenu("Debug: Llamar Resume")]
    void DebugResume() => Resume();
#endif

    private void Start()
    {
        Advance();
    }
    // ─────────────────────────────────────────────────────────────────────
    void Update()
    {
        if (!isMoving || isStopped) return;
        if (waypoints == null || currentIndex >= waypoints.Length) return;

        WaypointController target = waypoints[currentIndex];

        MoveTowards(target.transform.position);

        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance <= arrivalThreshold)
            HandleWaypointArrival(currentIndex);
    }

    // ── Método principal — activa el movimiento del carro ─────────────────
    public void Advance()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("[CarController] No hay waypoints asignados.");
            return;
        }

        if (!initialized)
        {
            currentIndex = 0;
            initialized = true;
        }

        isStopped = false;
        isMoving = true;
        UpdateIndicators();
    }

    // ── Reanuda el movimiento después de una parada por obstáculo ─────────
    public void Resume()
    {
        if (!initialized) return;

        isStopped = false;

        // Avanza al siguiente waypoint si no estamos en el último
        if (currentIndex < waypoints.Length - 1)
        {
            currentIndex++;
            isMoving = true;
            UpdateIndicators();
        }
    }

    // ── Lógica al llegar a un waypoint ────────────────────────────────────
    void HandleWaypointArrival(int index)
    {
        WaypointController wp = waypoints[index];
        wayPointsEvents[index].Invoke();
        // ¿Es el último waypoint?
        if (index == waypoints.Length - 1)
        {
            StopAtWaypoint(wp);
            OnLastWaypointReached?.Invoke();
            OnReachedLastWaypoint();
            return;
        }

        // Si OnlyRoute está activo: pasa sin detenerse ni ejecutar eventos
        if (wp.OnlyRoute)
        {
            GoToNext();
            return;
        }
        // Trigger tiene un objeto al llegar → para y dispara evento
        if (wp.HasObjectInTrigger)
        {
            StopAtWaypoint(wp);
            if (wp.TriggerObjectType == "Car")
            {
                OnCarEncountered?.Invoke(wp.TriggerObject);
                OnCarMeetsAnotherCar(wp.TriggerObject);
            }
            wp.NotifyCarArrived(this);
            return;
        }
        // Trigger vacío
        wp.NotifyCarArrived(this);
        if (wp.StoptOnArrive)
        {
            StopAtWaypoint(wp);  // para y queda fijo
        }
        else
        {
            GoToNext();           // continúa al siguiente
        }
    }
    // ── Para el carro exactamente en la posición del waypoint ─────────────
    void StopAtWaypoint(WaypointController wp)
    {
        // Snap exacto: evita que el carro oscile alrededor del threshold
        transform.position = wp.transform.position;
        isMoving = false;
        isStopped = true;
        UpdateIndicators();
    }

    // ── Avanza al siguiente índice ─────────────────────────────────────────
    void GoToNext()
    {
        currentIndex++;
        UpdateIndicators();

        // Si el nuevo waypoint actual tiene OnlyRoute, no se detiene al llegar
        // (el Update lo manejará automáticamente)
    }

    // ── Movimiento físico ──────────────────────────────────────────────────
    void MoveTowards(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    // ── Direccionales ──────────────────────────────────────────────────────
    void UpdateIndicators()
    {
        leftIndicator = false;
        rightIndicator = false;
        dirRigth.SetActive(false);
        dirLeft.SetActive(false);

        int nextIndex = currentIndex + 1;
        if (nextIndex >= waypoints.Length) return;

        Vector3 toNext = waypoints[nextIndex].transform.position - transform.position;
        float angle = Vector3.SignedAngle(transform.forward, toNext, Vector3.up);

        if (angle > indicatorAngleThreshold)
        { 
            rightIndicator = true;
            leftIndicator = false;
            dirRigth.SetActive(true);
            dirLeft.SetActive(false);
        }
        else if (angle < -indicatorAngleThreshold)
        {
            rightIndicator = false;
            leftIndicator = true;
            dirRigth.SetActive(false);
            dirLeft.SetActive(true);
        }
        else
        {
            rightIndicator = false;
            leftIndicator = false;
            dirRigth.SetActive(false);
            dirLeft.SetActive(false);
        }
            
    }

    // ── Métodos de override ────────────────────────────────────────────────

    /// <summary>
    /// Se llama cuando el carro llega al último waypoint.
    /// Puedes sobreescribir este método en una clase hija.
    /// </summary>
    protected virtual void OnReachedLastWaypoint()
    {
        Debug.Log($"[CarController] {gameObject.name} llegó al último waypoint.");
    }

    /// <summary>
    /// Se llama cuando el carro se topa con otro carro en un waypoint.
    /// Puedes sobreescribir este método en una clase hija.
    /// </summary>
    protected virtual void OnCarMeetsAnotherCar(GameObject otherCar)
    {
        Debug.Log($"[CarController] {gameObject.name} se topó con el carro: {otherCar.name}");
    }

    // ── Gizmos ─────────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.color = waypoints[i].OnlyRoute ? Color.green : Color.yellow;
            Gizmos.DrawLine(waypoints[i].transform.position, waypoints[i + 1].transform.position);
        }

        // Marca el waypoint actual
        if (Application.isPlaying && currentIndex < waypoints.Length && waypoints[currentIndex] != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(waypoints[currentIndex].transform.position, 0.6f);
        }
    }
}