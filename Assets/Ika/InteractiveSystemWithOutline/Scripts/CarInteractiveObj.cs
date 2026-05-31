using UnityEngine;

public class CarInteractiveObj : ObjetoInteractable
{
    [Header("Atributos CarInteractiveOBJ")]
    [SerializeField]private CarController carController;
    public override void Interactuar(Transform handPlayer, PlayerInteractor interactor, PlayerStates playerStates)
    {
        OnInteract.Invoke();
        interactor.DeleDeleDele();
    }
    public override void ExpulseObj(Transform handAttach, Transform maskAttach)
    {
        
    }
}
