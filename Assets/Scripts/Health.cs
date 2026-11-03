using System;
using UnityEngine;

/// <summary>
/// Vida para cualquier cosa que pueda recibir daño: jugador, enemigos, bosses.
/// En el jugador desactiva "Destroy On Death".
/// </summary>
public class Health : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0.5f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead { get; private set; }

    // Lo activa el PlayerController durante los i-frames de la esquiva.
    public bool Invulnerable { get; set; }

    // Multiplica el daño recibido. El boss aturdido por un parry lo sube (contraataque).
    public float DamageTakenMultiplier { get; set; } = 1f;

    public event Action<float> Damaged; // cantidad de daño recibido
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    /// <returns>true si el daño se aplicó.</returns>
    public bool TakeDamage(float amount)
    {
        if (IsDead || Invulnerable || amount <= 0f) return false;

        amount *= DamageTakenMultiplier;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(amount);

        if (Current <= 0f) Die();
        return true;
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        Current = Mathf.Min(maxHealth, Current + amount);
    }

    private void Die()
    {
        IsDead = true;

        // Que ya no se pueda fijar la cámara a algo muerto.
        LockOnTarget lockOn = GetComponent<LockOnTarget>();
        if (lockOn != null) lockOn.enabled = false;

        Died?.Invoke();

        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }
}
