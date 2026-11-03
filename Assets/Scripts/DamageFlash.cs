using System.Collections;
using UnityEngine;

/// <summary>
/// Hace parpadear el color del objeto al recibir daño. Útil mientras no hay animaciones.
/// </summary>
[RequireComponent(typeof(Health))]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float duration = 0.1f;

    private Health health;
    private Material material;
    private Color originalColor;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

        material = targetRenderer.material; // instancia propia, no afecta a otros objetos
        originalColor = material.color;
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    private void OnDamaged(float amount)
    {
        StopAllCoroutines();
        StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        material.color = flashColor;
        yield return new WaitForSeconds(duration);
        material.color = originalColor;
    }
}
