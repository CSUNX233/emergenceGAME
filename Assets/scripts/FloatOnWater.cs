using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class FloatOnWater : MonoBehaviour
{
    public WorldGrid worldGrid;

    [Header("Fluid")]
    public float waterDensity = 2.15f;
    public float airDrag = 0.08f;
    public float waterDrag = 3.2f;
    public float waterAngularDrag = 2.8f;
    public float verticalDamping = 1.5f;
    public float lateralDamping = 0.55f;
    public float uprightTorque = 0.12f;
    public float sideCurrentForce = 1.25f;

    [Header("Body")]
    public float objectWeight = 1f;
    public int sampleColumns = 9;
    public int supportSearchRadiusCells = 2;
    public int supportSearchDepthCells = 24;
    public float sampleOffset = 0.02f;
    public float loadProbeHeight = 0.08f;
    public float loadProbeInset = 0.06f;
    public LayerMask loadLayers = ~0;

    private Collider2D cachedCollider;
    private Rigidbody2D cachedRigidbody;
    private BoxCollider2D cachedBoxCollider;
    private PhysicalWeight cachedWeight;
    private float defaultGravityScale = 1f;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        cachedRigidbody = GetComponent<Rigidbody2D>();
        cachedBoxCollider = GetComponent<BoxCollider2D>();
        cachedWeight = GetComponent<PhysicalWeight>();

        if (cachedRigidbody == null)
            cachedRigidbody = gameObject.AddComponent<Rigidbody2D>();

        defaultGravityScale = cachedRigidbody.gravityScale;
        SyncRigidBodyMass(0f);
    }

    void FixedUpdate()
    {
        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();

        if (worldGrid == null || cachedCollider == null || cachedRigidbody == null)
            return;

        float supportedWeight = CalculateSupportedWeight();
        SyncRigidBodyMass(supportedWeight);
        cachedRigidbody.gravityScale = defaultGravityScale;

        FloatState state = SampleFloatState();
        if (!state.isTouchingWater)
        {
            cachedRigidbody.drag = airDrag;
            cachedRigidbody.angularDrag = 0.05f;
            return;
        }

        cachedRigidbody.drag = waterDrag;
        cachedRigidbody.angularDrag = waterAngularDrag;

        ApplyBuoyancy(state);

        if (Mathf.Abs(state.sideFlow) > 0.001f)
        {
            cachedRigidbody.AddForce(
                Vector2.right * (state.sideFlow * sideCurrentForce * state.averageSubmergedFraction),
                ForceMode2D.Force
            );
        }

        if (state.averageSubmergedFraction > 0.001f)
        {
            cachedRigidbody.AddTorque(
                -cachedRigidbody.rotation * uprightTorque * state.averageSubmergedFraction,
                ForceMode2D.Force
            );
        }
    }

    void SyncRigidBodyMass(float supportedWeight)
    {
        if (cachedWeight != null)
        {
            cachedWeight.baseWeight = Mathf.Max(0.05f, objectWeight);
            cachedWeight.SetSupportedWeight(supportedWeight);
            cachedWeight.SyncMass();
            return;
        }

        float desiredMass = Mathf.Max(0.05f, objectWeight + supportedWeight);
        if (!Mathf.Approximately(cachedRigidbody.mass, desiredMass))
            cachedRigidbody.mass = desiredMass;
    }

    void ApplyBuoyancy(FloatState state)
    {
        float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y * cachedRigidbody.gravityScale);

        for (int i = 0; i < state.columns.Length; i++)
        {
            ColumnState column = state.columns[i];
            if (column.submergedHeight <= 0.0001f || column.width <= 0.0001f)
                continue;

            float displacedArea = column.submergedHeight * column.width;
            float buoyantForce = waterDensity * gravityMagnitude * displacedArea;

            Vector2 pointVelocity = cachedRigidbody.GetPointVelocity(column.forcePoint);
            float dampingForce = -pointVelocity.y * verticalDamping * column.submergedFraction;
            float lateralForce = -pointVelocity.x * lateralDamping * column.submergedFraction;

            cachedRigidbody.AddForceAtPosition(
                Vector2.up * (buoyantForce + dampingForce),
                column.forcePoint,
                ForceMode2D.Force
            );

            cachedRigidbody.AddForceAtPosition(
                Vector2.right * lateralForce,
                column.forcePoint,
                ForceMode2D.Force
            );
        }
    }

    FloatState SampleFloatState()
    {
        int columnCount = Mathf.Max(2, sampleColumns);
        FloatState state = new FloatState
        {
            columns = new ColumnState[columnCount]
        };

        float submergedSum = 0f;
        bool anyWater = false;
        float leftSubmerge = 0f;
        float rightSubmerge = 0f;
        int leftCount = 0;
        int rightCount = 0;

        for (int i = 0; i < columnCount; i++)
        {
            float t = columnCount == 1 ? 0.5f : (float)i / (columnCount - 1);
            ColumnGeometry geometry = GetColumnGeometry(t, columnCount);
            ColumnState column = new ColumnState
            {
                width = geometry.width,
                bottomPoint = geometry.bottomPoint
            };

            if (TryGetWaterSurfaceY(geometry.bottomPoint, out float surfaceY))
            {
                float submergedHeight = Mathf.Clamp(surfaceY - geometry.bottomPoint.y, 0f, geometry.height);
                if (submergedHeight > 0.0001f)
                {
                    anyWater = true;
                    column.submergedHeight = submergedHeight;
                    column.submergedFraction = Mathf.Clamp01(submergedHeight / Mathf.Max(0.05f, geometry.height));
                    column.forcePoint = geometry.bottomPoint + geometry.up * (submergedHeight * 0.5f);
                    submergedSum += column.submergedFraction;

                    if (i < columnCount / 2)
                    {
                        leftSubmerge += column.submergedFraction;
                        leftCount++;
                    }
                    else if (i > columnCount / 2)
                    {
                        rightSubmerge += column.submergedFraction;
                        rightCount++;
                    }
                }
            }

            if (column.forcePoint == Vector2.zero)
                column.forcePoint = geometry.bottomPoint;

            state.columns[i] = column;
        }

        state.isTouchingWater = anyWater;
        state.averageSubmergedFraction = submergedSum / columnCount;
        float leftAverage = leftCount == 0 ? state.averageSubmergedFraction : leftSubmerge / leftCount;
        float rightAverage = rightCount == 0 ? state.averageSubmergedFraction : rightSubmerge / rightCount;
        state.sideFlow = Mathf.Clamp(leftAverage - rightAverage, -1f, 1f);
        return state;
    }

    float CalculateSupportedWeight()
    {
        Bounds bounds = cachedCollider.bounds;
        Vector2 probeSize = new Vector2(
            Mathf.Max(0.05f, bounds.size.x - loadProbeInset * 2f),
            Mathf.Max(0.04f, loadProbeHeight)
        );
        Vector2 probeCenter = new Vector2(bounds.center.x, bounds.max.y + probeSize.y * 0.5f);

        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, transform.eulerAngles.z, loadLayers);
        HashSet<Collider2D> countedColliders = new HashSet<Collider2D>();
        float totalWeight = 0f;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == cachedCollider || countedColliders.Contains(hit))
                continue;

            countedColliders.Add(hit);

            Vector2 toHit = hit.bounds.center - bounds.center;
            if (Vector2.Dot(toHit, transform.up) <= 0f)
                continue;

            ColliderDistance2D distance = cachedCollider.Distance(hit);
            if (!distance.isOverlapped && distance.distance > 0.08f)
                continue;

            PhysicalWeight otherWeight = hit.GetComponent<PhysicalWeight>();
            if (otherWeight != null)
            {
                totalWeight += otherWeight.CurrentWeight;
                continue;
            }

            Rigidbody2D otherBody = hit.attachedRigidbody;
            if (otherBody != null && otherBody != cachedRigidbody)
            {
                totalWeight += Mathf.Max(0.05f, otherBody.mass);
            }
        }

        return totalWeight;
    }

    bool TryGetWaterSurfaceY(Vector2 bottomPoint, out float surfaceY)
    {
        Vector2Int baseCell = worldGrid.WorldToGrid(bottomPoint);
        float bestSurfaceY = float.MinValue;
        bool found = false;
        int horizontalRadius = Mathf.Max(0, supportSearchRadiusCells);
        int verticalDepth = Mathf.Max(2, supportSearchDepthCells);

        for (int offsetX = -horizontalRadius; offsetX <= horizontalRadius; offsetX++)
        {
            int cellX = baseCell.x + offsetX;
            int topWaterY = int.MinValue;

            for (int step = 0; step <= verticalDepth; step++)
            {
                int cellY = baseCell.y - step;
                if (!worldGrid.IsWaterCell(cellX, cellY))
                    continue;

                int scanY = cellY;
                while (worldGrid.IsWaterCell(cellX, scanY + 1))
                    scanY++;

                if (scanY > topWaterY)
                    topWaterY = scanY;
            }

            if (topWaterY == int.MinValue)
                continue;

            float candidateSurfaceY = worldGrid.GridToWorld(cellX, topWaterY).y + worldGrid.CellHeight * 0.5f;
            if (!found || candidateSurfaceY > bestSurfaceY)
            {
                bestSurfaceY = candidateSurfaceY;
                found = true;
            }
        }

        surfaceY = bestSurfaceY;
        return found;
    }

    ColumnGeometry GetColumnGeometry(float horizontal01, int columnCount)
    {
        if (cachedBoxCollider != null)
        {
            Vector2 size = cachedBoxCollider.size;
            Vector2 offset = cachedBoxCollider.offset;

            float localLeft = offset.x - size.x * 0.5f;
            float localRight = offset.x + size.x * 0.5f;
            float localBottom = offset.y - size.y * 0.5f;
            float localTop = offset.y + size.y * 0.5f;

            float inset = Mathf.Min(0.02f, size.x * 0.08f);
            float localX = Mathf.Lerp(localLeft + inset, localRight - inset, horizontal01);
            float columnWidthLocal = size.x / columnCount;

            Vector2 bottomPoint = transform.TransformPoint(new Vector3(localX, localBottom - sampleOffset, 0f));
            Vector2 topPoint = transform.TransformPoint(new Vector3(localX, localTop, 0f));
            Vector2 leftPoint = transform.TransformPoint(new Vector3(localX - columnWidthLocal * 0.5f, offset.y, 0f));
            Vector2 rightPoint = transform.TransformPoint(new Vector3(localX + columnWidthLocal * 0.5f, offset.y, 0f));

            return new ColumnGeometry
            {
                bottomPoint = bottomPoint,
                height = Vector2.Distance(bottomPoint, topPoint),
                width = Mathf.Max(0.01f, Vector2.Distance(leftPoint, rightPoint)),
                up = (topPoint - bottomPoint).normalized
            };
        }

        Bounds bounds = cachedCollider.bounds;
        float columnWidth = bounds.size.x / columnCount;
        Vector2 bottom = new Vector2(
            Mathf.Lerp(bounds.min.x, bounds.max.x, horizontal01),
            bounds.min.y - sampleOffset
        );

        return new ColumnGeometry
        {
            bottomPoint = bottom,
            height = bounds.size.y,
            width = columnWidth,
            up = Vector2.up
        };
    }

    struct ColumnGeometry
    {
        public Vector2 bottomPoint;
        public float height;
        public float width;
        public Vector2 up;
    }

    struct ColumnState
    {
        public Vector2 bottomPoint;
        public Vector2 forcePoint;
        public float width;
        public float submergedHeight;
        public float submergedFraction;
    }

    struct FloatState
    {
        public bool isTouchingWater;
        public float averageSubmergedFraction;
        public float sideFlow;
        public ColumnState[] columns;
    }
}
