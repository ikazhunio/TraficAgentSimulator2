
using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class PlayerStates : MonoBehaviour
{

    [SerializeField] public Animator playerAnimator;

    public enum PlayerState
    {
        normal,
        huma,
        catrina,
        getObj

    }
    private PlayerState currentState = PlayerState.normal;
    private void Start()
    {
        ChangeState(PlayerState.normal);
    }
    private void Update()
    {
        switch (currentState)
        {
            case PlayerState.normal:
                OnUpdateNormal();
                break;
        }
    }

    public void ChangeState(PlayerState newState)
    {
        if(newState == currentState) { return; };

        // activar  metodo de salida del estado
        switch (currentState)
        {

            case PlayerState.normal:
                OnExitNormal();
                break;

        }
        currentState = newState;
        
        // activar metodo de entrada al nuevo estado
        switch(currentState)
        {
            case PlayerState.normal:
                OnEnterNormal();
                break;

        }
    }
    #region ON ENTER STATES METODS 
    private void OnEnterNormal()
    {
        playerAnimator.SetBool("normal",true);


    }


    #endregion
    #region ON EXIT STATES METODS 

    private void OnExitNormal()
    {
        playerAnimator.SetBool("normal", false);

    }

    #endregion
    #region ON UPDATE STATES METODS 
 
    private void OnUpdateNormal()
    {

    }

    #endregion
    #region Metodos Casulaes 
    public void GetObjectEvents()
    {
        playerAnimator.SetTrigger("getobj");
    }
    #endregion
}
