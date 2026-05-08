using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class FlagGoal : MonoBehaviour
{
    [Header("Attach")]
    public float attachSearchRadius = 1.2f;
    public LayerMask attachmentLayer;
    public bool destroyWithHost = true;

    public int scoreAmount = 1;
    public bool singleUse = true;
    public bool disableAfterScore = true;

    private Collider2D cachedCollider;
    private SpriteRenderer cachedSpriteRenderer;
    private bool hasScored;
    private Transform attachedHost;
    private Vector3 localOffset;
    private float localAngleOffset;
    private bool hasAttachedHost;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        cachedSpriteRenderer = GetComponent<SpriteRenderer>();

        if (cachedCollider != null)
            cachedCollider.isTrigger = true;
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

    public bool CanAttachToCollider(Collider2D target)
    {
        if (target == null || target.gameObject == gameObject)
            return false;

        if (target.GetComponent<FlagGoal>() != null)
            return false;

        if (target.GetComponent<SpikeTrap>() != null)
            return false;

        if (target.GetComponent<RotatingAxle>() != null)
            return false;

        if (target.GetComponent<WaterBarrel>() != null)
            return false;

        return true;
    }

    public bool TryGetNearestFacePose(Vector3 desiredPosition, Collider2D targetCollider, out SpikeTrap.AttachmentPose pose)
    {
        pose = default;
        if (cachedCollider == null || targetCollider == null)
            return false;

        Vector2 targetHalfSize = targetCollider.bounds.extents;
        Vector3 targetCenter = targetCollider.bounds.center;
        Vector2 offset = desiredPosition - targetCenter;
        float normalizedX = targetHalfSize.x > 0.001f ? offset.x / targetHalfSize.x : 0f;
        float normalizedY = targetHalfSize.y > 0.001f ? offset.y / targetHalfSize.y : 0f;

        TerrainTilemapSurface terrainSurface = targetCollider.GetComponentInParent<TerrainTilemapSurface>();
        if (terrainSurface != null)
            return terrainSurface.TryGetNearestFacePose(desiredPosition, GetColliderLocalSize(), GetColliderLocalOffset(), out pose);

        if (Mathf.Abs(normalizedX) >= Mathf.Abs(normalizedY))
        {
            bool attachRight = offset.x >= 0f;
            float rotationZ = attachRight ? -90f : 90f;
            Vector2 myHalfSize = GetRotatedColliderHalfExtents(rotationZ);
            Vector2 rotatedOffset = GetRotatedColliderOffset(rotationZ);
            float colliderCenterX = targetCenter.x + (attachRight ? targetHalfSize.x + myHalfSize.x : -targetHalfSize.x - myHalfSize.x);
            pose = new SpikeTrap.AttachmentPose
            {
                position = new Vector3(
                    colliderCenterX - rotatedOffset.x,
                    targetCenter.y - rotatedOffset.y,
                    0f),
                rotationZ = rotationZ
            };
            return true;
        }

        bool attachTop = offset.y >= 0f;
        float verticalRotationZ = attachTop ? 0f : 180f;
        Vector2 verticalHalfSize = GetRotatedColliderHalfExtents(verticalRotationZ);
        Vector2 verticalOffset = GetRotatedColliderOffset(verticalRotationZ);
        float colliderCenterY = targetCenter.y + (attachTop ? targetHalfSize.y + verticalHalfSize.y : -targetHalfSize.y - verticalHalfSize.y);
        pose = new SpikeTrap.AttachmentPose
        {
            position = new Vector3(
                targetCenter.x - verticalOffset.x,
                colliderCenterY - verticalOffset.y,
                0f),
            rotationZ = verticalRotationZ
        };
        return true;
    }

    public bool IsFaceAvailable(Collider2D targetCollider, SpikeTrap.AttachmentPose pose)
    {
        if (cachedCollider == null || targetCollider == null)
            return false;

        Vector2 checkSize = GetRotatedColliderSize(pose.rotationZ) * 0.98f;
        Vector2 boxCenter = (Vector2)pose.position + GetRotatedColliderOffset(pose.rotationZ);
        Collider2D[] hits = Physics2D.OverlapBoxAll(boxCenter, checkSize, pose.rotationZ);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == cachedCollider || hit == targetCollider)
                continue;

            if (IsBlockingMechanism(hit))
                return false;
        }

        return true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryAwardScore(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryAwardScore(other);
    }

    void TryAwardScore(Collider2D other)
    {
        if (hasScored && singleUse)
            return;

        if (other == null)
            return;

        PlayerMovement2D player = other.GetComponent<PlayerMovement2D>();
        if (player == null && other.attachedRigidbody != null)
            player = other.attachedRigidbody.GetComponent<PlayerMovement2D>();

        if (player == null)
            return;

        PlayerScore playerScore = player.GetComponent<PlayerScore>();
        if (playerScore == null)
            playerScore = player.gameObject.AddComponent<PlayerScore>();

        playerScore.AddScore(scoreAmount);
        hasScored = true;

        if (disableAfterScore)
        {
            if (cachedCollider != null)
                cachedCollider.enabled = false;

            if (cachedSpriteRenderer != null)
            {
                Color color = cachedSpriteRenderer.color;
                color.a = 0.55f;
                cachedSpriteRenderer.color = color;
            }
        }
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

            if (!TryGetNearestFacePose(desiredPosition, hit, out SpikeTrap.AttachmentPose pose))
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

    bool IsBlockingMechanism(Collider2D hit)
    {
        return hit.GetComponent<FlagGoal>() != null || hit.GetComponent<SpikeTrap>() != null;
    }

    Vector2 GetRotatedColliderHalfExtents(float rotationZ)
    {
        return GetRotatedColliderSize(rotationZ) * 0.5f;
    }

    Vector2 GetRotatedColliderSize(float rotationZ)
    {
        Vector2 size = GetColliderLocalSize();
        int quarterTurns = Mathf.RoundToInt(Mathf.Repeat(rotationZ, 360f) / 90f) % 4;
        if (quarterTurns % 2 != 0)
            size = new Vector2(size.y, size.x);
        return size;
    }

    Vector2 GetRotatedColliderOffset(float rotationZ)
    {
        Vector2 localOffset = GetColliderLocalOffset();
        float radians = rotationZ * Mathf.Deg2Rad;
        float cos = Mathf.Cos(radians);
        float sin = Mathf.Sin(radians);
        return new Vector2(
            localOffset.x * cos - localOffset.y * sin,
            localOffset.x * sin + localOffset.y * cos);
    }

    Vector2 GetColliderLocalSize()
    {
        Vector3 scale = transform.lossyScale;

        if (cachedCollider is BoxCollider2D box)
            return new Vector2(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y));

        if (cachedCollider is CapsuleCollider2D capsule)
            return new Vector2(Mathf.Abs(capsule.size.x * scale.x), Mathf.Abs(capsule.size.y * scale.y));

        return cachedCollider.bounds.size;
    }

    Vector2 GetColliderLocalOffset()
    {
        Vector3 scale = transform.lossyScale;

        if (cachedCollider is BoxCollider2D box)
            return new Vector2(box.offset.x * scale.x, box.offset.y * scale.y);

        if (cachedCollider is CapsuleCollider2D capsule)
            return new Vector2(capsule.offset.x * scale.x, capsule.offset.y * scale.y);

        return Vector2.zero;
    }
}
