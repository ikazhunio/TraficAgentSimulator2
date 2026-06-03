using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class StunManager : MonoBehaviour
{
    public static StunManager Instance { get; private set; }

    [Header("Referencias")]
    public Transform playerCamera;
    public PlayerInputBlocker playerInputBlocker;

    [Header("Configuración del Stun")]
    [Tooltip("Duración del stun en segundos")]
    public float stunDuration = 3f;
    [Tooltip("Velocidad a la que la cámara gira hacia el NPC")]
    public float cameraRotationSpeed = 3f;

    [Header("Eventos")]
    public UnityEvent OnStunStart;
    public UnityEvent OnStunEnd;

    private Queue<NPCController> stunQueue = new Queue<NPCController>();
    private bool isStunActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Llamado por cada NPC cuando entra al rango de stun.
    /// Si ya hay un stun activo, el NPC espera en la cola.
    /// </summary>
    public void RequestStun(NPCController npc)
    {
        // Evita duplicados en la cola
        if (stunQueue.Contains(npc)) return;

        stunQueue.Enqueue(npc);

        if (!isStunActive)
            StartCoroutine(ProcessQueue());
    }

    /// <summary>
    /// Elimina un NPC de la cola (por si fue destruido o desactivado).
    /// </summary>
    public void CancelStun(NPCController npc)
    {
        // Reconstruye la cola sin ese NPC
        var temp = new List<NPCController>(stunQueue);
        temp.Remove(npc);
        stunQueue = new Queue<NPCController>(temp);
    }

    IEnumerator ProcessQueue()
    {
        while (stunQueue.Count > 0)
        {
            NPCController npc = stunQueue.Dequeue();

            // El NPC puede haber sido destruido mientras esperaba
            if (npc == null) continue;

            isStunActive = true;

            // 1 — Rota la cámara del jugador para que mire al NPC
            yield return StartCoroutine(RotateCameraTowards(npc.transform));

            // 2 — Bloquea los controles del jugador
            if (playerInputBlocker != null)
                playerInputBlocker.BlockInput();

            OnStunStart?.Invoke();
            npc.NotifyStunApplied();

            // 3 — Espera la duración del stun
            yield return new WaitForSeconds(stunDuration);

            // 4 — Desbloquea controles
            if (playerInputBlocker != null)
                playerInputBlocker.UnblockInput();

            OnStunEnd?.Invoke();

            isStunActive = false;
        }
    }

    IEnumerator RotateCameraTowards(Transform target)
    {
        if (playerCamera == null) yield break;

        Quaternion initialRot = playerCamera.rotation;
        Vector3 direction = (target.position - playerCamera.position).normalized;

        if (direction == Vector3.zero) yield break;

        Quaternion targetRot = Quaternion.LookRotation(direction);
        float elapsed = 0f;
        float duration = 1f / cameraRotationSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerCamera.rotation = Quaternion.Slerp(initialRot, targetRot, elapsed / duration);
            yield return null;
        }

        playerCamera.rotation = targetRot;
    }
}