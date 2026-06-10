using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Detección")]
    public float maxInteractDistance = 8f;
    [Tooltip("Qué tan centrado debe estar el peatón en la pantalla (0=cualquier lugar, 1=exactamente al centro). Recomendado: 0.97")]
    [Range(0.9f, 1f)]
    public float aimThreshold = 0.97f;

    [Header("Cursor")]
    public RectTransform crosshairRect;
    public Color normalColor = Color.white;
    public Color interactColor = Color.yellow;

    private Camera cam;
    private Image crosshairImage;
    private PedestrianController hoveredPedestrian;

    void Start()
    {
        cam = GetComponent<Camera>();
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
        PedestrianController found = null;
        float bestDot = -1f;
        Vector3 camPos = cam.transform.position;
        Vector3 camFwd = cam.transform.forward;

        // Itera todos los peatones activos — no depende de colliders ni jerarquía
        foreach (var ped in PedestrianController.All)
        {
            if (!ped.IsWaitingAtPoint) continue;

            float dist = Vector3.Distance(camPos, ped.transform.position);
            if (dist > maxInteractDistance)
            {
                continue;
            }
            if (dist > ped.interactRange)
            {
                continue;
            }

            // Dot product: qué tan centrado está el peatón en la cámara
            Vector3 dir = (ped.transform.position - camPos).normalized;
            float dot = Vector3.Dot(camFwd, dir);

            if (dot < aimThreshold)
            {
                Debug.Log($"Skipping {ped.name}: not aimed at (dot {dot:F2} < threshold {aimThreshold})");
                continue;
            }
            if (dot <= bestDot)
            {
                Debug.Log($"Skipping {ped.name}: less centered than current candidate (dot {dot:F2} <= best {bestDot:F2})");
                continue;
            }

            bestDot = dot;
            Debug.Log($"Candidate {ped.name}: distance {dist:F2}, dot {dot:F2} (best so far)");
            found = ped;
            Debug.Log($"Found candidate: {ped.name} at distance {dist:F2} with dot {dot:F2}");
        }

        // Actualiza outline
        if (found != hoveredPedestrian)
        {
            hoveredPedestrian?.SetOutline(false);
            hoveredPedestrian = found;
            hoveredPedestrian?.SetOutline(true);
        }

        // Actualiza color del cursor
        if (crosshairImage != null)
            crosshairImage.color = hoveredPedestrian != null ? interactColor : normalColor;
    }

    void HandleInteractClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (hoveredPedestrian == null) return;
        hoveredPedestrian.CurrentPoint?.TriggerCrossing();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (cam == null) cam = GetComponent<Camera>();
        Vector3 origin = cam.transform.position;
        Vector3 forward = cam.transform.forward * maxInteractDistance;
        Gizmos.color = Color.green;
        Gizmos.DrawRay(origin, forward);
    }
#endif
}