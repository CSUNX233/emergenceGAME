using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class SpikeTrap : MonoBehaviour
{
    const float FaceBlockPadding = 0.9f;

    public float attachSearchRadius = 1.2f;
    public LayerMask attachmentLayer;

    [Header("Damage")]
    public float damage = 1f;
    public float damageCooldown = 0.35f;
    public bool destroyWithHost = true;

    private Collider2D selfCollider;
    private Transform attachedHost;
    private Vector3 localOffset;
    private float localAngleOffset;
    private bool hasAttachedHost;
    private readonly Dictionary<Collider2D, float> nextDamageTimes = new Dictionary<Collider2D, float>();

    void Awake()
    {
        selfCollider = GetComponent<Collider2D>();
    }

    void Update()
    {
        FollowHost();
    }

    public void TryAutoAttach()
    {
        AttachmentCandidate candidate = FindBestAttachmentCandidate(transform.position);
        if (!candidate.IsValid)
            return;

        transform.position = candidate.snappedPosition;
        Vector3 euler = transform.eulerAngles;
        euler.z = candidate.snappedRotationZ;
        transform.eulerAngles = euler;
        AttachToHost(candidate.targetCollider.transform);
    }

    public bool TryGetNearestFacePose(Vector3 desiredPosition, Collider2D targetCollider, out AttachmentPose pose)
    {
        pose = default;
        if (selfCollider == null || targetCollider == null)
            return false;

        TerrainTilemapSurface terrainSurface = targetCollider.GetComponentInParent<TerrainTilemapSurface>();
        if (terrainSurface != null)
            return terrainSurface.TryGetNearestFacePose(desiredPosition, GetColliderLocalSize(), GetColliderLocalOffset(), out pose);

        Vector2 myHalfSize = PlacementSizeUtility.GetHalfSize(gameObject);
        Vector2 targetHalfSize = PlacementSizeUtility.GetHalfSize(targetCollider);
        Vector3 targetCenter = targetCollider.bounds.center;
        Vector2 offset = desiredPosition - targetCenter;
        float normalizedX = targetHalfSize.x > 0.001f ? offset.x / targetHalfSize.x : 0f;
        float normalizedY = targetHalfSize.y > 0.001f ? offset.y / targetHalfSize.y : 0f;

        if (Mathf.Abs(normalizedX) >= Mathf.Abs(normalizedY))
        {
            bool attachRight = offset.x >= 0f;
            pose = new AttachmentPose
            {
                position = new Vector3(
                    targetCenter.x + (attachRight ? targetHalfSize.x + myHalfSize.x : -targetHalfSize.x - myHalfSize.x),
                    targetCenter.y,
                    0f),
                rotationZ = attachRight ? -90f : 90f
            };
            return true;
        }

        bool attachTop = offset.y >= 0f;
        pose = new AttachmentPose
        {
            position = new Vector3(
                targetCenter.x,
                targetCenter.y + (attachTop ? targetHalfSize.y + myHalfSize.y : -targetHalfSize.y - myHalfSize.y),
                0f),
            rotationZ = attachTop ? 0f : 180f
        };
        return true;
    }

    public bool IsFaceAvailable(Collider2D targetCollider, AttachmentPose pose)
    {
        if (selfCollider == null || targetCollider == null)
            return false;

        Vector2 checkSize = PlacementSizeUtility.GetPlacementCheckSize(gameObject) * FaceBlockPadding;
        Collider2D[] hits = Physics2D.OverlapBoxAll(pose.position, checkSize, pose.rotationZ);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == selfCollider || hit == targetCollider)
                continue;

            if (hit.GetComponent<SpikeTrap>() != null)
                return false;
        }

        return true;
    }

    public bool CanAttachToCollider(Collider2D target)
    {
        if (target == null || target.gameObject == gameObject)
            return false;

        if (target.GetComponent<SpikeTrap>() != null)
            return false;

        if (target.GetComponent<RotatingAxle>() != null)
            return false;

        if (target.GetComponent<WaterBarrel>() != null)
            return false;

        return true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    void TryDamage(Collider2D other)
    {
        if (other == null || other == selfCollider)
            return;

        float now = Time.time;
        if (nextDamageTimes.TryGetValue(other, out float nextTime) && now < nextTime)
            return;

        nextDamageTimes[other] = now + damageCooldown;

        MonoBehaviour[] behaviours = other.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDamageable damageable)
            {
                damageable.TakeDamage(damage);
                return;
            }
        }

        other.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }

    void AttachToHost(Transform host)
    {
        attachedHost = host;
        hasAttachedHost = host != null;
        localOffset = host.InverseTransformPoint(transform.position);
        localAngleOffset = transform.eulerAngles.z - host.eulerAngles.z;
    }

    void FollowHost()
    {
        if (attachedHost == null)
        {
            if (destroyWithHost && hasAttachedHost)
                Destroy(gameObject);
            return;
        }

        transform.position = attachedHost.TransformPoint(localOffset);
        Vector3 euler = transform.eulerAngles;
        euler.z = attachedHost.eulerAngles.z + localAngleOffset;
        transform.eulerAngles = euler;
    }

    AttachmentCandidate FindBestAttachmentCandidate(Vector3 desiredPosition)
    {
        AttachmentCandidate bestCandidate = default;
        Collider2D[] hits = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!CanAttachToCollider(hit))
                continue;

            if (!TryGetNearestFacePose(desiredPosition, hit, out AttachmentPose pose))
                continue;

            if (!IsFaceAvailable(hit, pose))
                continue;

            float distance = Vector2.Distance(desiredPosition, pose.position);
            if (!bestCandidate.IsValid || distance < bestCandidate.distance)
            {
                bestCandidate = new AttachmentCandidate
                {
                    snappedPosition = pose.position,
                    snappedRotationZ = pose.rotationZ,
                    distance = distance,
                    targetCollider = hit
                };
            }
        }

        return bestCandidate;
    }

    struct AttachmentCandidate
    {
        public Vector3 snappedPosition;
        public float snappedRotationZ;
        public float distance;
        public Collider2D targetCollider;
        public bool IsValid => targetCollider != null;
    }

    public struct AttachmentPose
    {
        public Vector3 position;
        public float rotationZ;
    }

    Vector2 GetColliderLocalSize()
    {
        Vector3 scale = transform.lossyScale;

        if (selfCollider is BoxCollider2D box)
            return new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y));

        if (selfCollider is CapsuleCollider2D capsule)
            return new Vector2(Mathf.Abs(capsule.size.x * scale.x), Mathf.Abs(capsule.size.y * scale.y));

        return selfCollider.bounds.size;
    }

    Vector2 GetColliderLocalOffset()
    {
        Vector3 scale = transform.lossyScale;

        if (selfCollider is BoxCollider2D box)
            return new Vector2(box.offset.x * scale.x, box.offset.y * scale.y);

        if (selfCollider is CapsuleCollider2D capsule)
            return new Vector2(capsule.offset.x * scale.x, capsule.offset.y * scale.y);

        return Vector2.zero;
    }
}
