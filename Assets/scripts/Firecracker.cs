using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Firecracker : MonoBehaviour
{
    public float fuseTime = 1.5f;
    public float flashInterval = 0.12f;
    public float attachSearchRadius = 1.2f;
    public LayerMask attachmentLayer;
    public bool autoAttachOnStart = false;
    public int attachedSortingOrder = 40;
    public Color flashTint = new Color(1f, 0.35f, 0.35f, 1f);

    private Collider2D cachedCollider;
    private SpriteRenderer cachedSpriteRenderer;
    private GameObject targetObject;
    private Transform attachedHost;
    private Vector3 localOffset;
    private float localAngleOffset;
    private bool fuseStarted;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>();

        if (cachedCollider != null)
            cachedCollider.isTrigger = true;
    }

    void Start()
    {
        if (autoAttachOnStart)
            TryAutoAttach();
    }

    void Update()
    {
        FollowHost();
    }

    public bool CanAttachToCollider(Collider2D target)
    {
        return TryGetAttachRoot(target, out _);
    }

    public bool TryGetAttachRoot(Collider2D target, out GameObject attachRoot)
    {
        attachRoot = null;

        if (target == null || target.gameObject == gameObject)
            return false;

        if (target.GetComponentInParent<Firecracker>() != null)
            return false;

        LevelPlacedObject placedObject = target.GetComponentInParent<LevelPlacedObject>();
        if (placedObject != null && placedObject.gameObject != gameObject)
        {
            attachRoot = placedObject.gameObject;
            return true;
        }

        return false;
    }

    public bool TryGetCenterPose(Collider2D target, out Vector3 position, out float rotationZ)
    {
        position = transform.position;
        rotationZ = 0f;

        if (!CanAttachToCollider(target))
            return false;

        Bounds targetBounds = target.bounds;
        position = new Vector3(targetBounds.center.x, targetBounds.center.y, 0f);
        return true;
    }

    public void AttachToTarget(Collider2D target)
    {
        if (!TryGetAttachRoot(target, out GameObject attachRoot))
            return;

        targetObject = attachRoot;
        attachedHost = targetObject.transform;
        localOffset = attachedHost.InverseTransformPoint(transform.position);
        localAngleOffset = transform.eulerAngles.z - attachedHost.eulerAngles.z;
        EnsureVisible();
        StartFuse();
    }

    public bool TryAutoAttach()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attachSearchRadius, attachmentLayer);
        Collider2D best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!CanAttachToCollider(hit))
                continue;

            float distance = Vector2.Distance(transform.position, hit.bounds.center);
            if (best == null || distance < bestDistance)
            {
                best = hit;
                bestDistance = distance;
            }
        }

        if (best == null)
            return false;

        AttachToTarget(best);
        return true;
    }

    void StartFuse()
    {
        if (fuseStarted)
            return;

        fuseStarted = true;
        EnsureVisible();
        StartCoroutine(FuseRoutine());
    }

    IEnumerator FuseRoutine()
    {
        float elapsed = 0f;
        bool bright = false;
        Color originalColor = cachedSpriteRenderer != null ? cachedSpriteRenderer.color : Color.white;

        while (elapsed < fuseTime)
        {
            bright = !bright;
            if (cachedSpriteRenderer != null)
                cachedSpriteRenderer.color = bright ? flashTint : originalColor;

            float step = Mathf.Min(flashInterval, fuseTime - elapsed);
            elapsed += step;
            yield return new WaitForSeconds(step);
        }

        Explode();
    }

    void Explode()
    {
        if (targetObject != null)
            Destroy(targetObject);

        Destroy(gameObject);
    }

    void EnsureVisible()
    {
        if (cachedSpriteRenderer == null)
            return;

        cachedSpriteRenderer.enabled = true;
        cachedSpriteRenderer.sortingOrder = Mathf.Max(cachedSpriteRenderer.sortingOrder, attachedSortingOrder);
    }

    void FollowHost()
    {
        if (attachedHost == null)
            return;

        transform.position = attachedHost.TransformPoint(localOffset);
        Vector3 euler = transform.eulerAngles;
        euler.z = attachedHost.eulerAngles.z + localAngleOffset;
        transform.eulerAngles = euler;
    }

    Vector2 GetColliderSize()
    {
        if (cachedCollider == null)
            return Vector2.one * 0.4f;

        BoxCollider2D box = cachedCollider as BoxCollider2D;
        if (box != null)
            return box.size;

        CapsuleCollider2D capsule = cachedCollider as CapsuleCollider2D;
        if (capsule != null)
            return capsule.size;

        CircleCollider2D circle = cachedCollider as CircleCollider2D;
        if (circle != null)
            return Vector2.one * circle.radius * 2f;

        return cachedCollider.bounds.size;
    }
}
