using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

interface IInteractiveObj
{
    void Interactuar(Transform handAttach, PlayerInteractor playerInteractor , PlayerStates playerStates); // Acción principal del objeto
    string nameObj { get;}
    Outline outlineObj { get;set; }
    void ExpulseObj(Transform handAttach, Transform maskAttach);
}
