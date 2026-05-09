using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class WoodPlankBlock : MonoBehaviour
{
    private Collider2D selfCollider;
    private Rigidbody2D selfRigidbody;

    [Header("Attachment")]
    public float attachSearchRadius = 1.2f;
    public LayerMask attachmentLayer;

    [Header("Axle Follow")]
    public bool isAttached = false;
    public RotatingAxle attachedAxle;
    public Vector3 localOffset;
    public float localAngleOffset;

    void Awake()
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

    void Update()
    {
        ValidateAttachment();
        FollowAxle();
    }

    public void TryAutoAttach()
    {
        AttachmentCandidate candidate = FindBestAttachmentCandidate(transform.position);
        if (!candidate.IsValid)
            return;

        transform.position = candidate.snappedPosition;

        StickyBlock sticky = candidate.targetCollider != null
            ? candidate.targetCollider.GetComponent<StickyBlock>()
            : null;

        if (sticky != null && sticky.attachedAxle != null)
            AttachToAxle(sticky.attachedAxle);
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

    public List<Vector3> GetCandidatePositionsForTarget(Vector3 desiredPosition, Collider2D targetCollider)
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

    public void AttachToAxle(RotatingAxle axle)
    {
        if (axle == null)
            return;

        attachedAxle = axle;
        isAttached = true;
        selfRigidbody.bodyType = RigidbodyType2D.Kinematic;
        selfRigidbody.velocity = Vector2.zero;
        selfRigidbody.angularVelocity = 0f;
        selfRigidbody.gravityScale = 0f;
        selfRigidbody.freezeRotation = true;

        localOffset = Quaternion.Inverse(axle.transform.rotation) * (transform.position - axle.transform.position);
        localAngleOffset = transform.eulerAngles.z - axle.transform.eulerAngles.z;
    }

    void DetachFromAxle()
    {
        attachedAxle = null;
        isAttached = false;
        ConfigureDetachedPhysics();
    }

    void ConfigureDetachedPhysics()
    {
        if (selfRigidbody == null)
            return;

        selfRigidbody.bodyType = RigidbodyType2D.Dynamic;
        selfRigidbody.velocity = Vector2.zero;
        selfRigidbody.angularVelocity = 0f;
        selfRigidbody.gravityScale = 1f;
        selfRigidbody.freezeRotation = false;
    }

    void FollowAxle()
    {
        if (!isAttached || attachedAxle == null)
            return;

        Vector3 rotatedOffset = attachedAxle.transform.rotation * localOffset;
        transform.position = attachedAxle.transform.position + rotatedOffset;

        Vector3 euler = transform.eulerAngles;
        euler.z = attachedAxle.transform.eulerAngles.z + localAngleOffset;
        transform.eulerAngles = euler;
    }

    void ValidateAttachment()
    {
        if (!isAttached)
            return;

        if (attachedAxle == null)
        {
            DetachFromAxle();
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attachSearchRadius, attachmentLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            StickyBlock sticky = hit.GetComponent<StickyBlock>();
            if (sticky == null || sticky.attachedAxle != attachedAxle)
                continue;

            ColliderDistance2D distance = selfCollider.Distance(hit);
            if (distance.isOverlapped || distance.distance <= 0.08f)
                return;
        }

        DetachFromAxle();
    }

    AttachmentCandidate FindBestAttachmentCandidate(Vector3 desiredPosition)
    {
        AttachmentCandidate bestCandidate = default;

        Collider2D[] hits = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.gameObject == gameObject)
                continue;

            StickyBlock sticky = hit.GetComponent<StickyBlock>();
            if (sticky == null)
                continue;

            List<Vector3> positions = GetCandidatePositionsForTarget(desiredPosition, hit);
            for (int j = 0; j < positions.Count; j++)
            {
                Vector3 position = positions[j];
                float distance = Vector2.Distance(desiredPosition, position);
                if (!bestCandidate.IsValid || distance < bestCandidate.distance)
                {
                    bestCandidate = new AttachmentCandidate
                    {
                        snappedPosition = position,
                        distance = distance,
                        targetCollider = hit
                    };
                }
            }
        }

        return bestCandidate;
    }

    struct AttachmentCandidate
    {
        public Vector3 snappedPosition;
        public float distance;
        public Collider2D targetCollider;

        public bool IsValid => targetCollider != null;
    }
}
