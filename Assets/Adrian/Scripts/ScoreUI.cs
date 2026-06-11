using UnityEngine;
using TMPro;   // Si usas TextMeshPro — si usas legacy UI cambia por UnityEngine.UI.Text

public class ScoreUI : MonoBehaviour
{
    [Header("Referencia")]
    [Tooltip("Texto UI donde se muestra el dinero")]
    public TMP_Text moneyText;

    [Tooltip("Formato del número. Ej: 'F0' = sin decimales, 'F2' = dos decimales")]
    public string numberFormat = "F0";

    void Start()
    {
        // Suscribe al evento del ScoreManager
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnMoneyChanged.AddListener(UpdateDisplay);

        UpdateDisplay(ScoreManager.Instance != null ? ScoreManager.Instance.TotalMoney : 0f);
    }

    void OnDestroy()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnMoneyChanged.RemoveListener(UpdateDisplay);
    }

    void UpdateDisplay(float amount)
    {
        if (moneyText != null)
            moneyText.text = $"${amount.ToString(numberFormat)}";
    }
}