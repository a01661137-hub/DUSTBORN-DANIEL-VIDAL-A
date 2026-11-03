using System.Collections;
using UnityEngine;

/// <summary>
/// Curación estilo "frasco": al pulsar la tecla el jugador bebe durante unos segundos,
/// se mueve más lento y no puede atacar ni esquivar. Al terminar recupera un % de su vida máxima.
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(Health))]
public class PlayerHealing : MonoBehaviour
{
    [Header("Curación")]
    [SerializeField] private KeyCode healKey = KeyCode.R;
    [SerializeField, Range(0.01f, 1f)] private float healPercent = 0.2f; // 20% de la vida máxima
    [SerializeField] private float drinkDuration = 2f;
    [SerializeField, Range(0.1f, 1f)] private float drinkMoveMultiplier = 0.6f; // 1 = sin penalización
    [SerializeField] private int maxCharges = 3;

    [Header("Feedback visual")]
    [SerializeField] private Renderer bodyRenderer;
    [SerializeField] private Color healColor = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private float pulsesPerSecond = 3f;

    private PlayerController player;
    private Health health;
    private Material material;

    public int Charges { get; private set; }
    public int MaxCharges => maxCharges;
    public bool IsDrinking { get; private set; }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        Charges = maxCharges;

        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
        if (bodyRenderer != null) material = bodyRenderer.material; // instancia propia
    }

    private void Update()
    {
        if (IsDrinking || !Input.GetKeyDown(healKey)) return;
        if (!CanDrink()) return;

        StartCoroutine(DrinkRoutine());
    }

    private bool CanDrink()
    {
        if (health.IsDead) return false;
        if (Charges <= 0) return false;
        if (health.Current >= health.Max) return false; // no gastes un frasco con la vida llena
        if (player.IsRolling || player.ActionLocked || player.IsGuarding) return false; // no mientras esquivas, atacas o guardas

        return true;
    }

    private IEnumerator DrinkRoutine()
    {
        IsDrinking = true;
        player.IsDrinking = true;
        player.SpeedMultiplier = drinkMoveMultiplier;
        Charges--;

        Color baseColor = material != null ? material.color : Color.white;

        float t = 0f;
        while (t < drinkDuration)
        {
            t += Time.deltaTime;
            if (health.IsDead) break;

            if (material != null)
            {
                float pulse = Mathf.PingPong(t * pulsesPerSecond * 2f, 1f);
                material.color = Color.Lerp(baseColor, healColor, pulse);
            }

            yield return null;
        }

        // La curación se aplica al terminar de beber; si mueres antes, se pierde.
        if (!health.IsDead) health.Heal(health.Max * healPercent);

        if (material != null) material.color = baseColor;
        player.SpeedMultiplier = 1f;
        player.IsDrinking = false;
        IsDrinking = false;
    }

    // Para usar más adelante en checkpoints / hogueras.
    public void RefillCharges()
    {
        Charges = maxCharges;
    }
}
