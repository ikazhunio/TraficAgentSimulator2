using UnityEngine;
using UnityEngine.Events;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Recompensas por Tarea")]
    [Tooltip("Dólares por cada peatón que cruza exitosamente")]
    public float rewardPerPedestrianCrossing = 10f;

    // Agrega aquí nuevas tareas con su recompensa:
    // public float rewardPerNPCEvaded = 5f;
    // public float rewardPerObjectiveComplete = 50f;

    [Header("Eventos")]
    [Tooltip("Se dispara cada vez que cambia el dinero. Pasa el total actual.")]
    public UnityEvent<float> OnMoneyChanged;

    private float _totalMoney = 0f;
    public float TotalMoney => _totalMoney;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>Agrega dinero al total y notifica a la UI.</summary>
    public void AddMoney(float amount)
    {
        if (amount <= 0f) return;
        _totalMoney += amount;
        OnMoneyChanged?.Invoke(_totalMoney);
    }

    /// <summary>Atajo para recompensar el cruce de N peatones.</summary>
    public void RewardPedestrianCrossing(int pedestrianCount)
    {
        AddMoney(rewardPerPedestrianCrossing * pedestrianCount);
    }
}
