using UnityEngine;

public class Blinker : MonoBehaviour
{
    [Tooltip("Veces por segundo que titila")]
    public float blinkRate = 2f;
    private float timer = 0f;
    private bool visible = true;
    public Light point;
    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= 1/ blinkRate)
        {
            timer = 0f;
            visible = !visible;
            point.enabled=visible;
        }
    }
    // Reinicia el estado al activarse para que siempre empiece encendido
    void OnEnable()
    {
        timer = 0f;
        visible = true;
        gameObject.SetActive(true);
    }
}