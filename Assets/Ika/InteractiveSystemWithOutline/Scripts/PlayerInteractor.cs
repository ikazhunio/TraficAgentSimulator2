using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR;
using UnityEngine.Events;
public class PlayerInteractor : MonoBehaviour
{
    [Header("UI interactive Obj")]
    public TextMeshProUGUI CurrentobjectName;

    [Header("Configuración del Raycast")]
    public float distancia = 3f; // Largo del raycast
    public float altura = 1.5f;  // Altura desde donde sale el raycast
    public LayerMask capaInteractiva = ~0; // Qué capas puede detectar (por defecto todas)

    private IInteractiveObj objetoActual; // Objeto que se está interactuando
    private bool interactuando = false;

    public Transform handTransform;

    public float moveSpeed = 5f; // Velocidad para moverse a un objeto interactuable

    private Coroutine currentMoveCoroutine;
    [Header("Player State Controller")]
    public PlayerStates playerStates;
    [Header("Player Attach Mask")]
    public Transform MaskPosition;

    [Header("Eventos de interaccion")]
    public UnityEvent CarCanMoveAction;

    [Header("Eventos de Interacción Fallida")]
    public UnityEvent OnCantInteract;
    private void Awake()
    {
        CurrentobjectName.gameObject.SetActive(true);
    }
    private void Start()
    {
        CurrentobjectName.gameObject.SetActive(true);
        CurrentobjectName.text = (" ");
        CurrentobjectName.gameObject.SetActive(false);
    }
    private void Update()


    {
        // Punto de inicio (con altura configurable)
        Vector3 origen = transform.position + Vector3.up * altura;
        Vector3 direccion = transform.forward;
        // Lanza el raycast
        if (Physics.Raycast(origen, direccion, out RaycastHit hit, distancia, capaInteractiva))
        {
            // Revisa si el objeto tiene un script con la interfaz IInteractiveObj

            IInteractiveObj interactivo = hit.collider.GetComponent<IInteractiveObj>();

            if (interactivo != null )
            {
                CurrentobjectName.gameObject.SetActive(true);
                CurrentobjectName.text = objetoActual?.nameObj;
                objetoActual = interactivo;
                objetoActual.outlineObj.OutlineWidth = 5f;
                CurrentobjectName.gameObject.SetActive(true);
                CurrentobjectName.text = objetoActual.nameObj;

                // Si se mantiene presionado click derecho
                if (Input.GetMouseButtonDown(0))
                {
                    Debug.Log("SiInteractuo");
                    
                    objetoActual.Interactuar(handTransform, gameObject.GetComponent<PlayerInteractor>(),playerStates);
                    objetoActual.outlineObj.OutlineWidth = 0f;
                }
            }
            else // Si deja de mirar al objeto interactivo
            {
                CurrentobjectName.gameObject.SetActive(false);
                if(objetoActual != null)
                {
                    objetoActual.outlineObj.OutlineWidth = 0f;
                }
                objetoActual = null;

            }
        }
        else
        {
            if(Input.GetMouseButtonDown(0))
            {
                Debug.Log("Soltar obj");
                if(MaskPosition.childCount > 0)
                {
                    IInteractiveObj interactivo = MaskPosition.GetChild(0).GetComponent<IInteractiveObj>();
                    interactivo?.ExpulseObj(handTransform,MaskPosition );
                }
                if(handTransform.childCount > 0 )
                {
                    
                    IInteractiveObj interactivo = handTransform.GetChild(0).GetComponent<IInteractiveObj>();
                    interactivo?.ExpulseObj(handTransform ,MaskPosition );
 
                }
            }
            CurrentobjectName.gameObject.SetActive(false);
            if(objetoActual != null)
            {
                objetoActual.outlineObj.OutlineWidth = 0f;
            }
            objetoActual = null;
        }
    }

    #region Eventos Propios del Juego Trafic Simulator
    public void DeleDeleDele()
    {
        CarCanMoveAction.Invoke();
    }
    #endregion

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Vector3 origen = transform.position + Vector3.up * altura;
        Vector3 fin = origen + transform.forward * distancia;
        Gizmos.DrawLine(origen, fin);
        Gizmos.DrawSphere(fin, 0.05f);
    }
}