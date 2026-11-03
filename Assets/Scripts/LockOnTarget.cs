using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ponlo en cualquier enemigo o boss al que se le pueda fijar la cámara.
/// Se registra solo al activarse, así que no necesitas capas ni tags.
/// </summary>
public class LockOnTarget : MonoBehaviour
{
    // Punto al que apunta la cámara (por ejemplo el pecho o la cabeza del boss).
    // Si lo dejas vacío usa un punto 1 metro arriba del objeto.
    [SerializeField] private Transform aimPoint;

    public static readonly List<LockOnTarget> All = new List<LockOnTarget>();

    public Vector3 AimPosition =>
        aimPoint != null ? aimPoint.position : transform.position + Vector3.up;

    private void OnEnable() => All.Add(this);

    private void OnDisable() => All.Remove(this);
}
