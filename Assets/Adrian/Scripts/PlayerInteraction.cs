using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class PlayerInteraction : MonoBehaviour
{
    [Header("Detección")]
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

        foreach (var ped in PedestrianController.All)
        {
            if (!ped.IsWaitingAtPoint)
            {
                continue;
            }

            Vector3 pedPos = ped.transform.position;

            // Distancia HORIZONTAL (ignora diferencia de altura cámara-peatón)
            float horizDist = Vector2.Distance(
                new Vector2(camPos.x, camPos.z),
                new Vector2(pedPos.x, pedPos.z));

            if (horizDist > ped.interactRange)
            {
                Debug.DrawLine(camPos, pedPos, Color.red);
                continue;
            }

            // Dot product: qué tan centrado está en la cámara
            Vector3 dir = (pedPos - camPos).normalized;
            float dot = Vector3.Dot(camFwd, dir);

            if (dot < aimThreshold)
            {
                Debug.DrawLine(camPos, pedPos, Color.yellow);
                continue;
            }
            if (dot <= bestDot)
            {
                Debug.DrawLine(camPos, pedPos, Color.blue);
                continue;
            }

            Debug.DrawLine(camPos, pedPos, Color.green);
            bestDot = dot;
            found = ped;
        }

        if (found != hoveredPedestrian)
        {
            hoveredPedestrian?.SetOutline(false);
            hoveredPedestrian = found;
            hoveredPedestrian?.SetOutline(true);
        }

        if (crosshairImage != null)
            crosshairImage.color = hoveredPedestrian != null ? interactColor : normalColor;
    }

    void HandleInteractClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (hoveredPedestrian == null) return;
        hoveredPedestrian.CurrentPoint?.TriggerCrossing();
    }
}