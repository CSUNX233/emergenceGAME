using UnityEngine;

[DisallowMultipleComponent]
public class PhysicalWeight : MonoBehaviour
{
    public float baseWeight = 1f;
    public float extraWeight = 0f;
    public bool syncRigidbodyMass = true;

    private Rigidbody2D cachedRigidbody;
    private float supportedWeight = 0f;

    public float CurrentWeight => Mathf.Max(0.05f, baseWeight + extraWeight + supportedWeight);

    void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        SyncMass();
    }

    void OnValidate()
    {
        cachedRigidbody = GetComponent<Rigidbody2D>();
        SyncMass();
    }

    public void SetSupportedWeight(float weight)
    {
        supportedWeight = Mathf.Max(0f, weight);
        SyncMass();
    }

    public void SyncMass()
    {
        if (!syncRigidbodyMass)
            return;

        if (cachedRigidbody == null)
            cachedRigidbody = GetComponent<Rigidbody2D>();

        if (cachedRigidbody == null)
            return;

        float desiredMass = CurrentWeight;
        if (!Mathf.Approximately(cachedRigidbody.mass, desiredMass))
            cachedRigidbody.mass = desiredMass;
    }
}
