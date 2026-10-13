using UnityEngine;

/// <summary>
/// Put this on the moving part of the door (the "door" object).
/// The door needs a Collider so the player's ray can hit it.
/// </summary>
public class Door : MonoBehaviour
{
    [Header("Hinge")]
    [Tooltip("Empty object placed at the hinge edge of the door. If empty, the door's own pivot is used.")]
    [SerializeField] Transform hinge;

    [Header("Motion")]
    [SerializeField] float openAngle = 90f;
    [SerializeField] float speed = 180f;          // degrees per second
    [Tooltip("Tick this if the door swings the wrong way.")]
    [SerializeField] bool invertDirection = false;
    [SerializeField] bool startOpen = false;

    [Header("Sounds (optional)")]
    [SerializeField] AudioClip openSound;
    [SerializeField] AudioClip closeSound;

    public bool IsOpen { get; private set; }

    float currentAngle;
    float targetAngle;
    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (startOpen)
        {
            IsOpen = true;
            float a = SignedOpenAngle();
            Rotate(a);
            currentAngle = targetAngle = a;
        }
    }

    void Update()
    {
        if (Mathf.Approximately(currentAngle, targetAngle)) return;

        float next = Mathf.MoveTowards(currentAngle, targetAngle, speed * Time.deltaTime);
        Rotate(next - currentAngle);
        currentAngle = next;
    }

    public void Toggle()
    {
        IsOpen = !IsOpen;
        targetAngle = IsOpen ? SignedOpenAngle() : 0f;

        var clip = IsOpen ? openSound : closeSound;
        if (clip != null)
        {
            if (audioSource != null) audioSource.PlayOneShot(clip);
            else AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }

    float SignedOpenAngle() => invertDirection ? -openAngle : openAngle;

    void Rotate(float delta)
    {
        Vector3 pivot = hinge != null ? hinge.position : transform.position;
        transform.RotateAround(pivot, Vector3.up, delta);
    }
}