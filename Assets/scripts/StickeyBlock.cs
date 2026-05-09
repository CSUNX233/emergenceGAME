using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class StickyBlock : MonoBehaviour
{
    private Collider2D selfCollider;
    private Rigidbody2D selfRigidbody;

    [Header("State")]
    public bool isAttached = false;
    public RotatingAxle attachedAxle;

    [Header("Local Offset To Axle")]
    public Vector3 localOffset;
    public float localAngleOffset;

    [Header("Auto Attachment")]
    public bool autoAttachOnStart = true;
    public float attachSearchRadius = 1.2f;
    public LayerMask attachmentLayer;

    private void Awake()
    {
        selfCollider = GetComponent<Collider2D>();
        selfRigidbody = GetComponent<Rigidbody2D>();
        if (selfRigidbody == null)
            selfRigidbody = gameObject.AddComponent<Rigidbody2D>();

        ConfigureDetachedPhysics();
    }

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = false;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        rb.freezeRotation = true;
    }

    void Start()
    {
        if (autoAttachOnStart)
            TryAutoAttach();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        RotatingAxle axle = other.GetComponent<RotatingAxle>();
        if (axle != null)
        {
            axle.AttachBlock(this);
            return;
        }

        StickyBlock otherBlock = other.GetComponent<StickyBlock>();
        if (otherBlock != null && !isAttached && otherBlock.attachedAxle != null)
            otherBlock.attachedAxle.AttachBlock(this);
    }

    void OnDestroy()
    {
        if (attachedAxle != null)
            attachedAxle.HandleBlockRemoved(this);
    }

    public void TryAutoAttach()
    {
        if (isAttached)
            return;

        AttachmentCandidate candidate = FindBestAttachmentCandidate(transform.position);
        if (!candidate.IsValid)
            return;

        transform.position = candidate.snappedPosition;

        if (candidate.axle != null)
        {
            candidate.axle.AttachBlock(this);
            return;
        }

        if (candidate.neighborBlock != null && candidate.neighborBlock.attachedAxle != null)
            candidate.neighborBlock.attachedAxle.AttachBlock(this);
    }

    public void AttachToAxle(RotatingAxle axle)
    {
        if (axle == null)
            return;

        attachedAxle = axle;
        isAttached = true;
        ConfigureAttachedPhysics();

        localOffset = Quaternion.Inverse(axle.transform.rotation) * (transform.position - axle.transform.position);
        localAngleOffset = transform.eulerAngles.z - axle.transform.eulerAngles.z;
    }

    public void DetachFromAxle()
    {
        attachedAxle = null;
        isAttached = false;
        ConfigureDetachedPhysics();
    }

    public bool TryGetSnappedAttachPosition(Vector3 desiredPosition, out Vector3 snappedPosition)
    {
        return TryGetSnappedAttachPosition(desiredPosition, out snappedPosition, out _);
    }

    public bool TryGetSnappedAttachPosition(
        Vector3 desiredPosition,
        out Vector3 snappedPosition,
        out Collider2D attachmentTarget)
    {
        AttachmentCandidate candidate = FindBestAttachmentCandidate(desiredPosition);
        if (candidate.IsValid)
        {
            snappedPosition = candidate.snappedPosition;
            attachmentTarget = candidate.targetCollider;
            return true;
        }

        snappedPosition = desiredPosition;
        attachmentTarget = null;
        return false;
    }

    AttachmentCandidate FindBestAttachmentCandidate(Vector3 desiredPosition)
    {
        AttachmentCandidate bestCandidate = default;

        if (selfCollider == null)
            return bestCandidate;

        Collider2D[] hits = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject == gameObject)
                continue;

            RotatingAxle axle = hit.GetComponent<RotatingAxle>();
            StickyBlock otherBlock = hit.GetComponent<StickyBlock>();

            List<Vector3> candidatePositions = BuildCandidatePositions(desiredPosition, hit);
            for (int i = 0; i < candidatePositions.Count; i++)
            {
                Vector3 position = candidatePositions[i];
                float distance = Vector2.Distance(desiredPosition, position);

                AttachmentCandidate candidate = new AttachmentCandidate
                {
                    snappedPosition = position,
                    distance = distance,
                    axle = axle,
                    neighborBlock = otherBlock,
                    targetCollider = hit
                };

                if (!bestCandidate.IsValid || candidate.distance < bestCandidate.distance)
                    bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    public List<Vector3> GetCandidatePositionsForTarget(Vector3 desiredPosition, Collider2D targetCollider)
    {
        return BuildCandidatePositions(desiredPosition, targetCollider);
    }

    List<Vector3> BuildCandidatePositions(Vector3 desiredPosition, Collider2D targetCollider)
    {
        List<Vector3> positions = new List<Vector3>(4);
        if (selfCollider == null || targetCollider == null)
            return positions;

        Vector2 myHalfSize = PlacementSizeUtility.GetHalfSize(gameObject);
        Vector2 targetHalfSize = PlacementSizeUtility.GetHalfSize(targetCollider);
        Vector3 targetCenter = targetCollider.bounds.center;

        positions.Add(new Vector3(
            targetCenter.x + targetHalfSize.x + myHalfSize.x,
            targetCenter.y,
            transform.position.z
        ));

        positions.Add(new Vector3(
            targetCenter.x - targetHalfSize.x - myHalfSize.x,
            targetCenter.y,
            transform.position.z
        ));

        positions.Add(new Vector3(
            targetCenter.x,
            targetCenter.y + targetHalfSize.y + myHalfSize.y,
            transform.position.z
        ));

        positions.Add(new Vector3(
            targetCenter.x,
            targetCenter.y - targetHalfSize.y - myHalfSize.y,
            transform.position.z
        ));

        positions.Sort((a, b) =>
            Vector2.Distance(desiredPosition, a).CompareTo(Vector2.Distance(desiredPosition, b)));

        return positions;
    }

    void ConfigureAttachedPhysics()
    {
        if (selfRigidbody == null)
            return;

        selfRigidbody.bodyType = RigidbodyType2D.Kinematic;
        selfRigidbody.velocity = Vector2.zero;
        selfRigidbody.angularVelocity = 0f;
        selfRigidbody.gravityScale = 0f;
        selfRigidbody.freezeRotation = true;
    }

    void ConfigureDetachedPhysics()
    {
        if (selfRigidbody == null)
            return;

        selfRigidbody.bodyType = RigidbodyType2D.Dynamic;
        selfRigidbody.velocity = Vector2.zero;
        selfRigidbody.angularVelocity = 0f;
        selfRigidbody.gravityScale = 1f;
        selfRigidbody.freezeRotation = true;
    }

    public void FollowAxle()
    {
        if (!isAttached || attachedAxle == null)
            return;

        Vector3 rotatedOffset = attachedAxle.transform.rotation * localOffset;
        transform.position = attachedAxle.transform.position + rotatedOffset;

        Vector3 euler = transform.eulerAngles;
        euler.z = attachedAxle.transform.eulerAngles.z + localAngleOffset;
        transform.eulerAngles = euler;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attachSearchRadius);
    }

    struct AttachmentCandidate
    {
        public Vector3 snappedPosition;
        public float distance;
        public RotatingAxle axle;
        public StickyBlock neighborBlock;
        public Collider2D targetCollider;

        public bool IsValid => axle != null || neighborBlock != null;
    }
}
