using UnityEngine;

public enum MaterialType
{
    Air,
    Fire,
    Water,
    Wood,
    Smoke,
    Steam
}

public enum CellOccupantType
{
    None,
    SolidObject
}

[RequireComponent(typeof(SpriteRenderer))]
public class WorldGrid : MonoBehaviour
{
    [Header("Camera")]
    public Camera mainCamera;

    [Header("Grid Resolution")]
    public int width = 400;
    public int height = 200;

    [Header("World Size")]
    public float worldWidth = 40f;
    public float worldHeight = 20f;

    [Header("Camera Framing")]
    public bool controlCameraOnStart = true;
    public float cameraOrthographicSize = 5f;

    [Header("Input")]
    public bool enableMousePaint = true;

    private Texture2D tex;
    private SpriteRenderer sr;
    private SimpleBlockPlacer blockPlacer;

    private MaterialType[,] grid;
    private CellOccupantType[,] occupants;

    public float CellWidth => worldWidth / width;
    public float CellHeight => worldHeight / height;
    public float CellsPerUnitX => width / worldWidth;
    public float CellsPerUnitY => height / worldHeight;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        grid = new MaterialType[width, height];
        occupants = new CellOccupantType[width, height];

        tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        sr = GetComponent<SpriteRenderer>();

        Sprite sprite = Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f
        );

        sr.sprite = sprite;

        float spriteWorldWidth = width / 100f;
        float spriteWorldHeight = height / 100f;

        transform.position = Vector3.zero;
        transform.localScale = new Vector3(
            worldWidth / spriteWorldWidth,
            worldHeight / spriteWorldHeight,
            1f
        );

        if (mainCamera != null)
        {
            mainCamera.orthographic = true;
            if (controlCameraOnStart)
            {
                mainCamera.orthographicSize = cameraOrthographicSize;
                mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            }
        }
        else
        {
            Debug.LogError("WorldGrid: mainCamera is not assigned.");
        }

        ClearAllMaterials();
        Draw();
    }

    void Update()
    {
        if (enableMousePaint)
            HandleInput();

        UpdateWorld();
        Draw();
    }

    void HandleInput()
    {
        if (mainCamera == null)
        {
            Debug.LogError("WorldGrid: mainCamera is not assigned.");
            return;
        }

        if (blockPlacer == null)
            blockPlacer = FindObjectOfType<SimpleBlockPlacer>();

        if (blockPlacer != null && blockPlacer.CurrentMode != PlayerInputMode.Elements)
            return;

        if (blockPlacer != null && !blockPlacer.IsElementPaintActive)
            return;

        if (blockPlacer != null && blockPlacer.IsPointerOverToolbar)
            return;

        Vector3 pos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;

        Vector2Int gp = WorldToGrid(pos);
        int x = gp.x;
        int y = gp.y;

        if (!InBounds(x, y))
            return;

        if (Input.GetMouseButton(0))
        {
            MaterialType paintType = blockPlacer != null
                ? blockPlacer.GetSelectedMaterialType()
                : MaterialType.Fire;

            TrySetMaterial(x, y, paintType);
        }
    }

    void UpdateWorld()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                switch (grid[x, y])
                {
                    case MaterialType.Fire:
                        UpdateFire(x, y);
                        break;

                    case MaterialType.Smoke:
                        UpdateSmoke(x, y);
                        break;

                    case MaterialType.Steam:
                        UpdateSteam(x, y);
                        break;
                }
            }
        }

        for (int y = 0; y < height; y++)
        {
            if ((y & 1) == 0)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grid[x, y] == MaterialType.Water)
                        UpdateWater(x, y);
                }
            }
            else
            {
                for (int x = width - 1; x >= 0; x--)
                {
                    if (grid[x, y] == MaterialType.Water)
                        UpdateWater(x, y);
                }
            }
        }
    }

    void UpdateFire(int x, int y)
    {
        bool spread = false;

        spread |= TryIgnite(x + 1, y);
        spread |= TryIgnite(x - 1, y);
        spread |= TryIgnite(x, y + 1);
        spread |= TryIgnite(x, y - 1);

        if (Random.value < 0.18f)
        {
            if (!TrySpawnMaterial(x + 1, y, MaterialType.Smoke))
            {
                if (!TrySpawnMaterial(x - 1, y, MaterialType.Smoke))
                    TrySpawnMaterial(x, y + 1, MaterialType.Smoke);
            }
        }

        if (!spread)
        {
            if (Random.value < 0.10f)
                grid[x, y] = MaterialType.Air;
        }
        else
        {
            if (Random.value < 0.02f)
                grid[x, y] = MaterialType.Air;
        }
    }

    bool TryIgnite(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (occupants[x, y] != CellOccupantType.None) return false;

        if (grid[x, y] == MaterialType.Wood)
        {
            grid[x, y] = MaterialType.Fire;
            return true;
        }

        return false;
    }

    public bool TryIgniteOrSetFire(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (occupants[x, y] != CellOccupantType.None) return false;

        if (grid[x, y] == MaterialType.Wood)
        {
            grid[x, y] = MaterialType.Fire;
            return true;
        }

        if (grid[x, y] == MaterialType.Air || grid[x, y] == MaterialType.Smoke || grid[x, y] == MaterialType.Steam)
        {
            grid[x, y] = MaterialType.Fire;
            return true;
        }

        return false;
    }

    void UpdateWater(int x, int y)
    {
        if (NeighborIsFire(x, y))
        {
            grid[x, y] = MaterialType.Steam;

            ExtinguishFire(x + 1, y);
            ExtinguishFire(x - 1, y);
            ExtinguishFire(x, y + 1);
            ExtinguishFire(x, y - 1);
            return;
        }

        if (TryMove(x, y, x, y - 1)) return;

        bool supported = !CanWaterOccupy(x, y - 1);

        if (Random.value < 0.5f)
        {
            if (TryMove(x, y, x - 1, y - 1)) return;
            if (TryMove(x, y, x + 1, y - 1)) return;
        }
        else
        {
            if (TryMove(x, y, x + 1, y - 1)) return;
            if (TryMove(x, y, x - 1, y - 1)) return;
        }

        TryFlowSideways(x, y, supported);
    }

    void ExtinguishFire(int x, int y)
    {
        if (!InBounds(x, y)) return;

        if (grid[x, y] == MaterialType.Fire)
            grid[x, y] = MaterialType.Steam;
    }

    void UpdateSmoke(int x, int y)
    {
        if (TryMove(x, y, x, y + 1)) return;

        bool moved = false;

        if (Random.value < 0.5f)
        {
            moved |= TryMove(x, y, x + 1, y);
            moved |= TryMove(x, y, x - 1, y);
        }
        else
        {
            moved |= TryMove(x, y, x - 1, y);
            moved |= TryMove(x, y, x + 1, y);
        }

        if (!moved)
        {
            if (Random.value < 0.5f)
            {
                moved |= TryMove(x, y, x + 1, y + 1);
                moved |= TryMove(x, y, x - 1, y + 1);
            }
            else
            {
                moved |= TryMove(x, y, x - 1, y + 1);
                moved |= TryMove(x, y, x + 1, y + 1);
            }
        }

        if (Random.value < 0.01f)
            grid[x, y] = MaterialType.Air;
    }

    void UpdateSteam(int x, int y)
    {
        if (TryMove(x, y, x, y + 1)) return;

        bool moved = false;

        if (Random.value < 0.5f)
        {
            moved |= TryMove(x, y, x + 1, y + 1);
            moved |= TryMove(x, y, x - 1, y + 1);
        }
        else
        {
            moved |= TryMove(x, y, x - 1, y + 1);
            moved |= TryMove(x, y, x + 1, y + 1);
        }

        if (!moved)
        {
            if (Random.value < 0.5f)
            {
                moved |= TryMove(x, y, x + 1, y);
                moved |= TryMove(x, y, x - 1, y);
            }
            else
            {
                moved |= TryMove(x, y, x - 1, y);
                moved |= TryMove(x, y, x + 1, y);
            }
        }

        if (Random.value < 0.025f)
            grid[x, y] = MaterialType.Air;
    }

    bool NeighborIsFire(int x, int y)
    {
        return IsFire(x + 1, y) || IsFire(x - 1, y) || IsFire(x, y + 1) || IsFire(x, y - 1);
    }

    bool IsFire(int x, int y)
    {
        return InBounds(x, y) && grid[x, y] == MaterialType.Fire;
    }

    public bool HasFireNearby(int x, int y)
    {
        return IsFire(x, y) || IsFire(x + 1, y) || IsFire(x - 1, y) || IsFire(x, y + 1) || IsFire(x, y - 1);
    }

    bool TryMove(int x1, int y1, int x2, int y2)
    {
        if (!InBounds(x2, y2)) return false;
        if (occupants[x2, y2] != CellOccupantType.None) return false;

        if (grid[x2, y2] == MaterialType.Air)
        {
            MaterialType temp = grid[x1, y1];
            grid[x1, y1] = grid[x2, y2];
            grid[x2, y2] = temp;
            return true;
        }

        return false;
    }

    bool CanWaterOccupy(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (occupants[x, y] != CellOccupantType.None) return false;

        MaterialType type = grid[x, y];
        return type == MaterialType.Air || type == MaterialType.Smoke || type == MaterialType.Steam;
    }

    void TryFlowSideways(int x, int y, bool supported)
    {
        int[] directions = Random.value < 0.5f ? new[] { -1, 1 } : new[] { 1, -1 };
        int maxDistance = supported ? 3 : 2;

        for (int i = 0; i < directions.Length; i++)
        {
            int dir = directions[i];
            for (int step = 1; step <= maxDistance; step++)
            {
                int targetX = x + dir * step;
                if (!InBounds(targetX, y))
                    break;

                if (occupants[targetX, y] != CellOccupantType.None)
                    break;

                if (grid[targetX, y] != MaterialType.Air)
                    break;

                if (!supported && InBounds(targetX, y - 1) && CanWaterOccupy(targetX, y - 1))
                    continue;

                grid[targetX, y] = grid[x, y];
                grid[x, y] = MaterialType.Air;
                return;
            }
        }
    }

    bool TrySpawnMaterial(int x, int y, MaterialType type)
    {
        if (!InBounds(x, y)) return false;
        if (occupants[x, y] != CellOccupantType.None) return false;
        if (grid[x, y] != MaterialType.Air) return false;

        grid[x, y] = type;
        return true;
    }

    public bool TrySetMaterial(int x, int y, MaterialType type)
    {
        if (!InBounds(x, y)) return false;
        if (occupants[x, y] != CellOccupantType.None) return false;

        grid[x, y] = type;
        return true;
    }

    public void ClearAllMaterials()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                grid[x, y] = MaterialType.Air;
                occupants[x, y] = CellOccupantType.None;
            }
        }
    }

    public bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    public Vector2Int WorldToGrid(Vector3 worldPos)
    {
        float left = -worldWidth / 2f;
        float bottom = -worldHeight / 2f;

        float normalizedX = (worldPos.x - left) / worldWidth;
        float normalizedY = (worldPos.y - bottom) / worldHeight;

        int x = Mathf.FloorToInt(normalizedX * width);
        int y = Mathf.FloorToInt(normalizedY * height);

        return new Vector2Int(x, y);
    }

    public Vector3 GridToWorld(int x, int y)
    {
        float left = -worldWidth / 2f;
        float bottom = -worldHeight / 2f;

        float wx = left + (x + 0.5f) * CellWidth;
        float wy = bottom + (y + 0.5f) * CellHeight;

        return new Vector3(wx, wy, 0f);
    }

    public void ClearOccupants()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                occupants[x, y] = CellOccupantType.None;
        }
    }

    public void SetOccupantCell(int x, int y, CellOccupantType type)
    {
        if (!InBounds(x, y)) return;
        occupants[x, y] = type;
    }

    public void FillOccupantRect(Vector2Int centerCell, Vector2Int sizeInCells, CellOccupantType type, bool displaceFluids = true)
    {
        int startX = centerCell.x - sizeInCells.x / 2;
        int startY = centerCell.y - sizeInCells.y / 2;

        for (int x = 0; x < sizeInCells.x; x++)
        {
            for (int y = 0; y < sizeInCells.y; y++)
            {
                int gx = startX + x;
                int gy = startY + y;

                if (InBounds(gx, gy))
                {
                    occupants[gx, gy] = type;
                    if (type != CellOccupantType.None && displaceFluids)
                        DisplaceFluidAt(gx, gy);
                }
            }
        }
    }

    void DisplaceFluidAt(int x, int y)
    {
        if (!InBounds(x, y))
            return;

        MaterialType material = grid[x, y];
        if (material == MaterialType.Air || material == MaterialType.Wood || material == MaterialType.Fire)
            return;

        if (TryRelocateMaterial(x, y, material))
            grid[x, y] = MaterialType.Air;
    }

    bool TryRelocateMaterial(int x, int y, MaterialType material)
    {
        Vector2Int[] preferredOffsets = new Vector2Int[]
        {
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(1, 1),
        };

        for (int i = 0; i < preferredOffsets.Length; i++)
        {
            int targetX = x + preferredOffsets[i].x;
            int targetY = y + preferredOffsets[i].y;
            if (!CanReceiveDisplacedFluid(targetX, targetY))
                continue;

            if (!HasOpenDisplacementPath(x, y, targetX, targetY))
                continue;

            grid[targetX, targetY] = material;
            return true;
        }

        return false;
    }

    bool CanReceiveDisplacedFluid(int x, int y)
    {
        if (!InBounds(x, y))
            return false;

        if (occupants[x, y] != CellOccupantType.None)
            return false;

        return grid[x, y] == MaterialType.Air;
    }

    bool HasOpenDisplacementPath(int fromX, int fromY, int toX, int toY)
    {
        int stepX = Mathf.Clamp(toX - fromX, -1, 1);
        int stepY = Mathf.Clamp(toY - fromY, -1, 1);

        if (stepX != 0)
        {
            int midX = fromX + stepX;
            if (!IsFluidPassable(midX, fromY))
                return false;
        }

        if (stepY != 0)
        {
            int midY = fromY + stepY;
            if (!IsFluidPassable(toX, midY))
                return false;
        }

        return true;
    }

    bool IsFluidPassable(int x, int y)
    {
        if (!InBounds(x, y))
            return false;

        if (occupants[x, y] != CellOccupantType.None)
            return false;

        return grid[x, y] == MaterialType.Air
            || grid[x, y] == MaterialType.Water
            || grid[x, y] == MaterialType.Smoke
            || grid[x, y] == MaterialType.Steam;
    }

    void Draw()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, GetColor(x, y, grid[x, y]));
        }

        tex.Apply();
    }

    Color GetColor(int x, int y, MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Fire:
                return Color.Lerp(new Color(1f, 0.25f, 0f), Color.yellow, Random.value);

            case MaterialType.Water:
                return GetWaterColor(x, y);

            case MaterialType.Wood:
                return new Color(0.45f, 0.25f, 0.05f);

            case MaterialType.Smoke:
                return Color.Lerp(new Color(0.22f, 0.22f, 0.22f), new Color(0.45f, 0.45f, 0.45f), Random.value);

            case MaterialType.Steam:
                return Color.Lerp(new Color(0.75f, 0.75f, 0.75f), Color.white, Random.value);

            default:
                return Color.white;
        }
    }

    Color GetWaterColor(int x, int y)
    {
        bool topOpen = !IsMaterial(x, y + 1, MaterialType.Water);
        bool leftOpen = !IsMaterial(x - 1, y, MaterialType.Water);
        bool rightOpen = !IsMaterial(x + 1, y, MaterialType.Water);
        bool bottomWater = IsMaterial(x, y - 1, MaterialType.Water);

        float depth01 = Mathf.Clamp01((float)y / Mathf.Max(1, height - 1));
        Color deep = new Color(0.10f, 0.34f, 0.82f);
        Color shallow = new Color(0.22f, 0.58f, 1.00f);
        Color water = Color.Lerp(deep, shallow, 0.25f + depth01 * 0.45f);

        // 顶部亮一些，做出更舒服的液面观感。
        if (topOpen)
            water = Color.Lerp(water, new Color(0.72f, 0.90f, 1.00f), 0.42f);

        // 边缘略亮，让水团轮廓更柔和。
        if (leftOpen || rightOpen)
            water = Color.Lerp(water, new Color(0.45f, 0.74f, 1.00f), 0.18f);

        // 内部略深，减少大块纯蓝发平。
        if (bottomWater && !topOpen && !leftOpen && !rightOpen)
            water = Color.Lerp(water, new Color(0.07f, 0.26f, 0.68f), 0.16f);

        // 给水面一点轻微波动感，但保持稳定不闪烁。
        float shimmer = Mathf.PerlinNoise(x * 0.16f, y * 0.22f);
        if (topOpen)
            water = Color.Lerp(water, new Color(0.86f, 0.96f, 1.00f), 0.08f + shimmer * 0.10f);
        else
            water = Color.Lerp(water, new Color(0.05f, 0.22f, 0.60f), shimmer * 0.05f);

        return water;
    }

    bool IsMaterial(int x, int y, MaterialType type)
    {
        return InBounds(x, y) && grid[x, y] == type;
    }

    public bool IsWaterCell(int x, int y)
    {
        return InBounds(x, y) && grid[x, y] == MaterialType.Water;
    }
}
