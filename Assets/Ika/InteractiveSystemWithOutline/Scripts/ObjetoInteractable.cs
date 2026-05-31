using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;


public class ObjetoInteractable : MonoBehaviour , IInteractiveObj
{
    [SerializeField] private string nameOrDescriptionObj;
    [SerializeField] private Outline outline;
    public Coroutine moveRoutine;
    public Transform currentTarget;
    public MeshRenderer renderer;
    public Material maskMaterial;
    public UnityEvent OnInteract;
    public UnityEvent OnExpulse;
    [SerializeField] PlayerStates playerStates;
    public Outline outlineObj
    {
        get { return outline; }
        set { outline = value; }
    }
    private void Start()
    {
        maskMaterial = renderer.material;
    }
    public string nameObj
    {
        get { return nameOrDescriptionObj; }
    }


    public virtual void Interactuar(Transform handPlayer, PlayerInteractor interactor, PlayerStates playerStates)
    {
        if (interactor.handTransform.childCount > 0 || interactor.MaskPosition.childCount > 0) { interactor.OnCantInteract.Invoke(); return; }
        OnInteract.Invoke();

    }

    public virtual void ExpulseObj(Transform handAttach, Transform maskAttach)
    {

        OnExpulse?.Invoke();
        if (handAttach.childCount > 0)
        {
            playerStates.ChangeState(PlayerStates.PlayerState.normal);
            Debug.Log("Lanzar Obj de mano");
            Transform oldItem = handAttach.GetChild(0);
            oldItem.SetParent(null, true);
            oldItem.GetComponent<ObjetoInteractable>()?.maskMaterial.DisableKeyword("_EMISSION");
            Collider oldCol = oldItem.GetComponent<Collider>();
            if (oldCol != null) oldCol.enabled = true;
            Rigidbody oldRb = oldItem.GetComponent<Rigidbody>();
            if (oldRb != null) oldRb.isKinematic = false;
            if (oldRb != null) oldRb.useGravity = true;
            oldItem.transform.localPosition = new Vector3(handAttach.transform.position.x, handAttach.transform.position.y, handAttach.transform.position.z + 1);
        }
        if (maskAttach.childCount > 0)
        {
            playerStates.ChangeState(PlayerStates.PlayerState.normal);
            Debug.Log("Lanzar Obj de cabeza");
            Transform oldItem2 = maskAttach.GetChild(0);
            oldItem2.SetParent(null, true);
            oldItem2.GetComponent<ObjetoInteractable>()?.maskMaterial.DisableKeyword("_EMISSION");
            Collider oldCol = oldItem2.GetComponent<Collider>();
            if (oldCol != null) oldCol.enabled = true;
            Rigidbody oldRb = oldItem2.GetComponent<Rigidbody>();
            if (oldRb != null) oldRb.isKinematic = false;
        }
    }
}
