using UnityEngine;

public class PlayerInputBlocker : MonoBehaviour
{
    [Tooltip("Arrastra aquí el componente que controla el movimiento del Player")]
    public MonoBehaviour playerMovementScript;
    [Tooltip("Arrastra aquí el componente de cámara/look del Player, si existe")]
    public MonoBehaviour playerLookScript;

    private bool isBlocked = false;

    public void BlockInput()
    {
        if (isBlocked) return;
        isBlocked = true;

        if (playerMovementScript != null)
            playerMovementScript.enabled = false;

        if (playerLookScript != null)
            playerLookScript.enabled = false;

        Debug.Log("Controles del Player bloqueados.");
    }

    public void UnblockInput()
    {
        isBlocked = false;

        if (playerMovementScript != null)
            playerMovementScript.enabled = true;

        if (playerLookScript != null)
            playerLookScript.enabled = true;
    }
}