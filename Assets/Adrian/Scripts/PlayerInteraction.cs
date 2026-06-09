using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adjunta a la cámara en primera persona del jugador.
/// Muestra un cursor en pantalla, detecta peatones en rango
/// y permite hacer clic para cruzarlos.
/// </summary>
[RequireComponent(typeof(Camera))]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Detección")]
    [Tooltip("Distancia máxima del raycast para detectar peatones")]
    public float maxRayDistance = 8f;
    [Tooltip("Layers que contienen los peatones (asigna el layer del peatón)")]
    public LayerMask pedestrianLayer;

    [Header("Cursor")]
    [Tooltip("RectTransform del Image UI que actúa como cursor (colócalo en el centro del Canvas)")]
    public RectTransform crosshairRect;
    [Tooltip("Color del cursor cuando no hay peatón en rango")]
    public Color normalColor = Color.white;
    [Tooltip("Color del cursor cuando hay un peatón interactuable")]
    public Color interactColor = Color.yellow;

    private Camera cam;
    private Image crosshairImage;
    private PedestrianController hoveredPedestrian;

    void Start()
    {
        cam = GetComponent<Camera>();

        // Oculta el cursor del sistema y lo bloquea al centro (FPS estándar)
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (crosshairRect != null)
            crosshairImage = crosshairRect.GetComponent<Image>();
    }

    void Update()
    {
        ScanForPedestrian();
        HandleInteractClick();
    }

    void ScanForPedestrian()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        PedestrianController found = null;

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, pedestrianLayer))
        {
            // Busca el componente tanto en el objeto golpeado como en su padre
            var ped = hit.collider.GetComponentInParent<PedestrianController>();
            if (ped != null && ped.IsWaitingAtPoint)
            {
                float dist = Vector3.Distance(transform.position, ped.transform.position);
                if (dist <= ped.interactRange)
                    found = ped;
            }
        }

        // Actualiza outline si cambió el peatón detectado
        if (found != hoveredPedestrian)
        {
            hoveredPedestrian?.SetOutline(false);
            hoveredPedestrian = found;
            hoveredPedestrian?.SetOutline(true);
        }

        // Cambia el color del cursor
        if (crosshairImage != null)
            crosshairImage.color = hoveredPedestrian != null ? interactColor : normalColor;
    }

    void HandleInteractClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (hoveredPedestrian == null) return;

        // Dispara el cruce de TODOS los peatones en ese punto
        hoveredPedestrian.CurrentPoint?.TriggerCrossing();
    }
}
