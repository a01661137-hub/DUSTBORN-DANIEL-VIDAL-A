using UnityEngine;

/// <summary>
/// Pasa la velocidad real del personaje al Animator, para que la animación de caminar
/// se active cuando avanza y se detenga cuando se queda quieto.
/// Ponlo en ROACH (el objeto raíz que tiene PlayerController).
/// </summary>
public class PlayerAnimator : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Animator animator; // el Animator del modelo (si lo dejas vacío, lo busca en los hijos)

    [Header("Parámetro del Animator")]
    [SerializeField] private string speedParameter = "Speed"; // nombre del parámetro Float en el Animator
    [SerializeField] private float dampTime = 0.1f;           // suaviza el cambio entre quieto y caminando

    [Header("Si NO tienes animación de quieto (solo caminar)")]
    [SerializeField] private bool freezeWhenStopped = false;  // congela la pose cuando no te mueves

    [Header("Velocidad de la animación")]
    [SerializeField] private bool scalePlaybackWithSpeed = false; // solo úsalo si NO tienes animación de correr: acelera la de caminar
    [SerializeField] private float referenceSpeed = 3f;          // velocidad a la que la animación se ve natural (tu Walk Speed)
    [SerializeField] private float maxPlaybackSpeed = 1.8f;

    private int speedHash;
    private bool hasSpeedParameter;
    private Vector3 lastPosition;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("PlayerAnimator: no encontré ningún Animator en ROACH ni en sus hijos. " +
                             "Agrega el componente Animator al modelo o arrástralo al campo Animator.", this);
            enabled = false;
            return;
        }

        // El movimiento lo hace PlayerController, no la animación.
        animator.applyRootMotion = false;

        speedHash = Animator.StringToHash(speedParameter);
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == speedHash && p.type == AnimatorControllerParameterType.Float)
            {
                hasSpeedParameter = true;
                break;
            }
        }

        if (!hasSpeedParameter)
        {
            Debug.LogWarning("PlayerAnimator: el Animator no tiene un parámetro Float llamado '" + speedParameter +
                             "'. Créalo en la ventana Animator > Parameters.", this);
        }

        lastPosition = transform.position;
    }

    // Se calcula con el cambio de posición, porque CharacterController.velocity
    // no sirve aquí (PlayerController mueve horizontal y gravedad en llamadas separadas).
    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;

        float speed = delta.magnitude / dt;
        if (speed < 0.05f) speed = 0f; // evita movimientos fantasma por gravedad o choques

        if (hasSpeedParameter)
            animator.SetFloat(speedHash, speed, dampTime, dt);

        bool moving = speed > 0.1f;

        if (freezeWhenStopped)
        {
            animator.speed = moving ? PlaybackSpeed(speed) : 0f;
        }
        else
        {
            animator.speed = moving ? PlaybackSpeed(speed) : 1f;
        }
    }

    private float PlaybackSpeed(float speed)
    {
        if (!scalePlaybackWithSpeed) return 1f;
        return Mathf.Clamp(speed / Mathf.Max(referenceSpeed, 0.01f), 0.6f, maxPlaybackSpeed);
    }
}