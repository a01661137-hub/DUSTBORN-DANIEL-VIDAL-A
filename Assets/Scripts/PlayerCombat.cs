using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Combate cuerpo a cuerpo: clic izquierdo = ataque ligero, clic derecho = ataque pesado.
/// Cada ataque tiene preparación (windup), ventana de daño (active) y recuperación (recovery).
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private WeaponData weapon;
    [SerializeField] private LayerMask hittableMask = ~0;
    [SerializeField] private float hitHeightOffset = 0f;

    [Header("Visual opcional (para el cubo)")]
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private float swingAngle = 70f;

    private PlayerController player;
    private Health health;
    private Coroutine attackRoutine;
    private AttackData currentAttack;
    private bool hitboxActive;
    private readonly HashSet<Health> alreadyHit = new HashSet<Health>();

    public bool IsAttacking => attackRoutine != null;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (weapon == null || IsAttacking || player.IsRolling || player.IsDrinking || player.IsGuarding) return;
        if (health != null && health.IsDead) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (Input.GetMouseButtonDown(0)) TryAttack(weapon.lightAttack);
        else if (Input.GetMouseButtonDown(1)) TryAttack(weapon.heavyAttack);
    }

    private void TryAttack(AttackData attack)
    {
        if (!player.TryConsumeStamina(attack.staminaCost)) return;
        attackRoutine = StartCoroutine(AttackRoutine(attack));
    }

    private IEnumerator AttackRoutine(AttackData attack)
    {
        currentAttack = attack;
        player.ActionLocked = true;
        alreadyHit.Clear();

        float total = attack.windup + attack.active + attack.recovery;
        float t = 0f;

        while (t < total)
        {
            t += Time.deltaTime;

            hitboxActive = t >= attack.windup && t < attack.windup + attack.active;
            if (hitboxActive) DoHitCheck(attack);

            UpdateSwing(t / total);
            yield return null;
        }

        hitboxActive = false;
        ResetSwing();
        player.ActionLocked = false;
        attackRoutine = null;
    }

    private Vector3 GetHitCenter(AttackData attack)
    {
        return transform.position + transform.forward * attack.reach + Vector3.up * hitHeightOffset;
    }

    // Cada enemigo recibe daño una sola vez por ataque.
    private void DoHitCheck(AttackData attack)
    {
        Collider[] hits = Physics.OverlapSphere(
            GetHitCenter(attack), attack.radius, hittableMask, QueryTriggerInteraction.Ignore);

        foreach (Collider c in hits)
        {
            Health target = c.GetComponentInParent<Health>();
            if (target == null || target.gameObject == gameObject) continue;
            if (!alreadyHit.Add(target)) continue;

            target.TakeDamage(attack.damage);
        }
    }

    private void UpdateSwing(float normalized)
    {
        if (weaponPivot == null) return;
        float angle = Mathf.Lerp(-swingAngle, swingAngle, Mathf.Clamp01(normalized));
        weaponPivot.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    private void ResetSwing()
    {
        if (weaponPivot != null) weaponPivot.localRotation = Quaternion.identity;
    }

    // Muestra la zona de golpe en rojo mientras el ataque daña (activa Gizmos en la vista Game).
    private void OnDrawGizmos()
    {
        if (!hitboxActive || currentAttack == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawSphere(GetHitCenter(currentAttack), currentAttack.radius);
    }
}
