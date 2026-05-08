using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WaterBarrel : MonoBehaviour
{
    public WorldGrid worldGrid;
    public FloatOnWater floatOnWater;
    public PhysicalWeight physicalWeight;
    public BurnableObject burnableObject;
    public SpriteRenderer spriteRenderer;

    [Header("Water Storage")]
    public float waterAmount = 0f;
    public float maxWaterAmount = 2f;
    public float fillRate = 0.8f;
    public float releaseRate = 1.25f;
    public float waterWeightMultiplier = 0.8f;
    public float fullSpriteThreshold = 0.5f;

    [Header("Sprites")]
    public Sprite emptySprite;
    public Sprite fullSprite;

    [Header("Fill Detection")]
    public float topFillDepth = 0.12f;
    public float topFillWidthInset = 0.14f;

    [Header("Controls")]
    public int releaseMouseButton = 1;
    public LayerMask clickableLayers = ~0;

    private Collider2D cachedCollider;
    private Camera cachedCamera;
    private bool releaseRequested;

    public bool HasWater => waterAmount > 0.01f;

    void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();
        if (floatOnWater == null)
            floatOnWater = GetComponent<FloatOnWater>();
        if (physicalWeight == null)
            physicalWeight = GetComponent<PhysicalWeight>();
        if (burnableObject == null)
            burnableObject = GetComponent<BurnableObject>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();
        if (cachedCamera == null)
            cachedCamera = Camera.main;

        HandleInput();
        UpdateStoredWater();
        SyncState();
    }

    void HandleInput()
    {
        if (!Input.GetMouseButtonDown(releaseMouseButton) || cachedCamera == null || cachedCollider == null)
            return;

        Vector3 mouseWorld = cachedCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;
        if (cachedCollider.OverlapPoint(mouseWorld))
            releaseRequested = !releaseRequested;
    }

    void UpdateStoredWater()
    {
        if (worldGrid == null || cachedCollider == null)
            return;

        if (IsReceivingWaterFromAbove())
            waterAmount = Mathf.Min(maxWaterAmount, waterAmount + fillRate * Time.deltaTime);

        if (releaseRequested && waterAmount > 0.01f)
            ReleaseWater();

        if (waterAmount <= 0.01f)
        {
            waterAmount = 0f;
            releaseRequested = false;
        }
    }

    bool IsReceivingWaterFromAbove()
    {
        Bounds bounds = cachedCollider.bounds;
        Vector3 fillMin = new Vector3(
            bounds.min.x + topFillWidthInset,
            bounds.max.y - topFillDepth,
            0f
        );
        Vector3 fillMax = new Vector3(
            bounds.max.x - topFillWidthInset,
            bounds.max.y + topFillDepth,
            0f
        );

        Vector2Int min = worldGrid.WorldToGrid(fillMin);
        Vector2Int max = worldGrid.WorldToGrid(fillMax);

        for (int x = min.x; x <= max.x; x++)
        {
            for (int y = min.y; y <= max.y; y++)
            {
                if (worldGrid.IsWaterCell(x, y))
                    return true;
            }
        }

        return false;
    }

    void ReleaseWater()
    {
        float amountToRelease = Mathf.Min(waterAmount, releaseRate * Time.deltaTime);
        if (amountToRelease <= 0f)
            return;

        Vector3 releasePoint = cachedCollider.bounds.center + Vector3.down * (cachedCollider.bounds.extents.y + 0.05f);
        Vector2Int gridPos = worldGrid.WorldToGrid(releasePoint);
        if (worldGrid.TrySetMaterial(gridPos.x, gridPos.y, MaterialType.Water))
            waterAmount -= amountToRelease;
    }

    void SyncState()
    {
        if (physicalWeight != null)
            physicalWeight.extraWeight = waterAmount * waterWeightMultiplier;

        if (burnableObject != null)
            burnableObject.canBurn = !HasWater;

        if (spriteRenderer != null)
        {
            bool shouldShowFull = maxWaterAmount > 0.001f && (waterAmount / maxWaterAmount) >= fullSpriteThreshold;
            if (shouldShowFull && fullSprite != null)
                spriteRenderer.sprite = fullSprite;
            else if (emptySprite != null)
                spriteRenderer.sprite = emptySprite;
        }
    }
}
