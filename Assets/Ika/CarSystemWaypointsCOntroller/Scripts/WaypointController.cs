using UnityEngine;
using UnityEngine.Events;

public class WaypointController : MonoBehaviour
{
    [Header("Configuración")]
    public CarController carController;
    [Tooltip("Si está activo, el carro pasa por este waypoint sin detenerse")]
    public bool OnlyRoute = false;
    public bool StoptOnArrive = false;



    [Header("Eventos (asignables desde el Inspector)")]
    [Tooltip("Se dispara cuando el carro llega y el trigger está vacío")]
    public UnityEvent<CarController> OnCarArrivedClear;
    [Tooltip("Se dispara cuando el carro llega y hay otro carro en el trigger")]
    public UnityEvent<CarController, GameObject> OnCarArrivedWithCar;
    [Tooltip("Se dispara cuando el carro llega y hay un obstáculo en el trigger")]
    public UnityEvent<CarController, GameObject> OnCarArrivedWithObstacle;

    // Estado interno del trigger
    private GameObject objectInTrigger = null;
    private string triggerType = ""; // "Car" u "Obstacle"

    public bool HasObjectInTrigger => objectInTrigger != null;
    public string TriggerObjectType => triggerType;
    public GameObject TriggerObject => objectInTrigger;

    void OnTriggerEnter(Collider other)
    {
        // Solo registra el primer objeto (Car tiene prioridad)
        if (objectInTrigger != null) return;

        if (other.CompareTag("Car"))
        {
            objectInTrigger = other.gameObject;
            triggerType = "Car";

        }
        else if (other.CompareTag("Obstacle"))
        {
            objectInTrigger = other.gameObject;
            triggerType = "Obstacle";
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject == objectInTrigger)
        {
            objectInTrigger = null;
            triggerType = "";
        }
    }

    /// <summary>
    /// Llamado por el CarController al llegar a este waypoint.
    /// Dispara el evento correspondiente según lo que haya en el trigger.
    /// </summary>
    public void NotifyCarArrived(CarController car)
    {
        if (OnlyRoute) return;

        if (!HasObjectInTrigger)
            OnCarArrivedClear?.Invoke(car);
        else if (triggerType == "Car")
        {
            OnCarArrivedWithCar?.Invoke(car, objectInTrigger);

        }

        else if (triggerType == "Obstacle")
            OnCarArrivedWithObstacle?.Invoke(car, objectInTrigger);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = OnlyRoute ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.DrawIcon(transform.position + Vector3.up * 0.8f, "sv_label_0", true);
    }
}