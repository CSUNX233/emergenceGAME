using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class RotatingAxle : MonoBehaviour
{
    [Header("Rotation")]
    public float rotationSpeed = 90f;
    public bool rotateClockwise = false;

    [Header("Attached Blocks")]
    public List<StickyBlock> attachedBlocks = new List<StickyBlock>();

    [Header("Connectivity")]
    public float connectionTolerance = 0.05f;

    private Collider2D axleCollider;

    void Awake()
    {
        axleCollider = GetComponent<Collider2D>();
    }

    void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;
    }

    void Update()
    {
        float dir = rotateClockwise ? -1f : 1f;
        float deltaAngle = rotationSpeed * dir * Time.deltaTime;

        transform.Rotate(0f, 0f, deltaAngle);

        for (int i = attachedBlocks.Count - 1; i >= 0; i--)
        {
            if (attachedBlocks[i] == null)
            {
                attachedBlocks.RemoveAt(i);
                continue;
            }

            attachedBlocks[i].FollowAxle();
        }
    }

    public void AttachBlock(StickyBlock block)
    {
        if (block == null)
            return;

        if (!attachedBlocks.Contains(block))
            attachedBlocks.Add(block);

        block.AttachToAxle(this);
    }

    public void HandleBlockRemoved(StickyBlock block)
    {
        if (block != null)
            attachedBlocks.Remove(block);

        RebuildAttachments();
    }

    public void RebuildAttachments()
    {
        attachedBlocks.RemoveAll(block => block == null);
        if (attachedBlocks.Count == 0)
            return;

        HashSet<StickyBlock> connected = new HashSet<StickyBlock>();
        Queue<StickyBlock> frontier = new Queue<StickyBlock>();

        for (int i = 0; i < attachedBlocks.Count; i++)
        {
            StickyBlock block = attachedBlocks[i];
            if (block != null && IsConnectedToAxle(block))
            {
                connected.Add(block);
                frontier.Enqueue(block);
            }
        }

        while (frontier.Count > 0)
        {
            StickyBlock current = frontier.Dequeue();
            for (int i = 0; i < attachedBlocks.Count; i++)
            {
                StickyBlock candidate = attachedBlocks[i];
                if (candidate == null || connected.Contains(candidate))
                    continue;

                if (AreConnected(current, candidate))
                {
                    connected.Add(candidate);
                    frontier.Enqueue(candidate);
                }
            }
        }

        for (int i = 0; i < attachedBlocks.Count; i++)
        {
            StickyBlock block = attachedBlocks[i];
            if (block == null)
                continue;

            if (connected.Contains(block))
                block.AttachToAxle(this);
            else
                block.DetachFromAxle();
        }

        attachedBlocks.RemoveAll(block => block == null || !connected.Contains(block));
    }

    bool IsConnectedToAxle(StickyBlock block)
    {
        if (block == null || axleCollider == null)
            return false;

        Collider2D blockCollider = block.GetComponent<Collider2D>();
        return AreConnected(axleCollider, blockCollider);
    }

    bool AreConnected(StickyBlock a, StickyBlock b)
    {
        if (a == null || b == null)
            return false;

        return AreConnected(a.GetComponent<Collider2D>(), b.GetComponent<Collider2D>());
    }

    bool AreConnected(Collider2D a, Collider2D b)
    {
        if (a == null || b == null)
            return false;

        ColliderDistance2D distance = a.Distance(b);
        return distance.isOverlapped || distance.distance <= connectionTolerance;
    }

    public bool CanAcceptAttachment(Vector3 worldPos, float radius, LayerMask attachmentLayer)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(worldPos, radius, attachmentLayer);

        foreach (Collider2D hit in hits)
        {
            if (hit == null)
                continue;

            RotatingAxle axle = hit.GetComponent<RotatingAxle>();
            if (axle != null)
                return true;

            StickyBlock block = hit.GetComponent<StickyBlock>();
            if (block != null)
                return true;
        }

        return false;
    }
}
