using System;
using UnityEngine;

[Serializable]
public class AttackData
{
    public float damage = 20f;
    public float staminaCost = 15f;

    [Header("Tiempos (segundos)")]
    public float windup = 0.2f;    // preparación, aún no hace daño
    public float active = 0.15f;   // ventana en la que el golpe sí daña
    public float recovery = 0.3f;  // recuperación, el jugador sigue bloqueado

    [Header("Alcance")]
    public float reach = 1.5f;     // distancia desde el jugador al centro del golpe
    public float radius = 0.9f;    // tamaño de la zona de golpe
}

/// <summary>
/// Datos de un arma. Se crea desde el menú: clic derecho en Project > Create > Hortus > Weapon.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Hortus/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "Cuchilla de afeitar";

    public AttackData lightAttack = new AttackData();

    public AttackData heavyAttack = new AttackData
    {
        damage = 45f,
        staminaCost = 30f,
        windup = 0.45f,
        active = 0.2f,
        recovery = 0.55f,
        reach = 1.7f,
        radius = 1.1f
    };
}
