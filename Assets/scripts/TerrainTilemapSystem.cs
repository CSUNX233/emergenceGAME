using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TerrainTilemapSystem : MonoBehaviour
{
    public string terrainLayerName = "PlaceBlock";
    public bool generateInitialPlatform = true;
    public int width = 22;
    public int baseY = -5;
    public int topY = -2;
    public int fillDepth = 5;
    public TileBase terrainTile;
    public Color terrainColor = new Color(0.42f, 0.46f, 0.38f, 1f);
    public Color terrainTopColor = new Color(0.55f, 0.62f, 0.42f, 1f);
    public bool rebuildCollidersForAllChildTilemaps = true;

    [SerializeField] private bool builtInitialTerrain;
    [SerializeField] private Tilemap terrainTilemap;

    private Tile runtimeTile;
    private Sprite fallbackSprite;
    private Texture2D fallbackTexture;

    public Tilemap TerrainTilemap => terrainTilemap;
    public TileBase TerrainTile => terrainTile != null ? terrainTile : GetTerrainTile();

    void OnEnable()
    {
        EnsureTilemap();

        if (generateInitialPlatform && (!builtInitialTerrain || IsTerrainEmpty()))
            RebuildInitialTerrain();

        RebuildSolidColliders();
    }

    void OnValidate()
    {
        width = Mathf.Max(1, width);
        fillDepth = Mathf.Max(1, fillDepth);
    }

    [ContextMenu("Rebuild Initial Terrain")]
    public void RebuildInitialTerrain()
    {
        EnsureTilemap();
        if (terrainTilemap == null)
            return;

        terrainTilemap.ClearAllTiles();

        int halfWidth = width / 2;
        for (int x = -halfWidth; x < width - halfWidth; x++)
        {
            int edgeDistance = Mathf.Min(x + halfWidth, width - halfWidth - 1 - x);
            int edgeDrop = edgeDistance < 3 ? 3 - edgeDistance : 0;
            int surfaceY = topY + GetSurfaceOffset(x) - edgeDrop;
            int bottomY = Mathf.Min(baseY, surfaceY - fillDepth);

            for (int y = bottomY; y <= surfaceY; y++)
                terrainTilemap.SetTile(new Vector3Int(x, y, 0), TerrainTile);
        }

        terrainTilemap.CompressBounds();
        builtInitialTerrain = true;
        RebuildSolidColliders();
    }

    public void SetTerrainTile(Vector3Int cell, bool filled)
    {
        EnsureTilemap();
        if (terrainTilemap == null)
            return;

        terrainTilemap.SetTile(cell, filled ? TerrainTile : null);
        terrainTilemap.CompressBounds();
        RebuildSolidColliders();
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        EnsureTilemap();
        return terrainTilemap != null ? terrainTilemap.WorldToCell(worldPosition) : Vector3Int.zero;
    }

    void EnsureTilemap()
    {
        Grid grid = GetComponent<Grid>();
        if (grid == null)
            grid = gameObject.AddComponent<Grid>();

        grid.cellSize = Vector3.one;

        if (terrainTilemap == null)
        {
            Transform child = transform.Find("Terrain Tilemap");
            if (child != null)
                terrainTilemap = child.GetComponent<Tilemap>();
        }

        if (terrainTilemap == null)
        {
            GameObject tilemapObject = new GameObject("Terrain Tilemap");
            tilemapObject.transform.SetParent(transform, false);
            terrainTilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
        }

        ConfigureTerrainTilemap(terrainTilemap, 1, true);
    }

    void ConfigureTerrainTilemap(Tilemap tilemap, int sortingOrder, bool normalizeTransform)
    {
        if (tilemap == null)
            return;

        GameObject terrainObject = tilemap.gameObject;
        if (normalizeTransform)
        {
            terrainObject.transform.localPosition = Vector3.zero;
            terrainObject.transform.localRotation = Quaternion.identity;
            terrainObject.transform.localScale = Vector3.one;
        }

        int layer = LayerMask.NameToLayer(terrainLayerName);
        terrainObject.layer = layer >= 0 ? layer : 6;

        TilemapRenderer renderer = terrainObject.GetComponent<TilemapRenderer>();
        if (renderer == null)
            renderer = terrainObject.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;

        Rigidbody2D rigidbody2D = terrainObject.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = terrainObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Static;

        CompositeCollider2D composite = terrainObject.GetComponent<CompositeCollider2D>();
        if (composite == null)
            composite = terrainObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;

        TilemapCollider2D collider = terrainObject.GetComponent<TilemapCollider2D>();
        if (collider == null)
            collider = terrainObject.AddComponent<TilemapCollider2D>();
        collider.usedByComposite = true;

        TerrainTilemapSurface surface = terrainObject.GetComponent<TerrainTilemapSurface>();
        if (surface == null)
            terrainObject.AddComponent<TerrainTilemapSurface>();
    }

    [ContextMenu("Rebuild Solid Colliders")]
    public void RebuildSolidColliders()
    {
        EnsureTilemap();

        Tilemap[] tilemaps = rebuildCollidersForAllChildTilemaps
            ? GetComponentsInChildren<Tilemap>(true)
            : new[] { terrainTilemap };

        for (int i = 0; i < tilemaps.Length; i++)
        {
            Tilemap tilemap = tilemaps[i];
            if (tilemap == null)
                continue;

            ConfigureTerrainTilemap(tilemap, i + 1, tilemap == terrainTilemap);
            RebuildSolidColliders(tilemap);
        }
    }

    void RebuildSolidColliders(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        Transform collisionRoot = tilemap.transform.Find("Generated Solid Colliders");
        if (collisionRoot == null)
        {
            GameObject rootObject = new GameObject("Generated Solid Colliders");
            rootObject.transform.SetParent(tilemap.transform, false);
            collisionRoot = rootObject.transform;
        }

        for (int i = collisionRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = collisionRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        BoundsInt bounds = tilemap.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            int runStart = int.MinValue;

            for (int x = bounds.xMin; x <= bounds.xMax; x++)
            {
                bool hasTile = x < bounds.xMax && tilemap.HasTile(new Vector3Int(x, y, 0));
                if (hasTile && runStart == int.MinValue)
                    runStart = x;

                if ((!hasTile || x == bounds.xMax) && runStart != int.MinValue)
                {
                    int runEnd = x - 1;
                    CreateSolidCollider(tilemap, collisionRoot, runStart, runEnd, y);
                    runStart = int.MinValue;
                }
            }
        }
    }

    void CreateSolidCollider(Tilemap sourceTilemap, Transform collisionRoot, int startX, int endX, int y)
    {
        GameObject colliderObject = new GameObject($"Terrain Solid {startX}_{endX}_{y}");
        colliderObject.layer = sourceTilemap.gameObject.layer;
        colliderObject.transform.SetParent(collisionRoot, false);

        float widthInCells = endX - startX + 1f;
        Vector3 cellCenter = sourceTilemap.GetCellCenterLocal(new Vector3Int(startX, y, 0));
        Vector3 lastCellCenter = sourceTilemap.GetCellCenterLocal(new Vector3Int(endX, y, 0));
        Vector3 center = (cellCenter + lastCellCenter) * 0.5f;

        colliderObject.transform.localPosition = center;
        BoxCollider2D box = colliderObject.AddComponent<BoxCollider2D>();
        box.size = new Vector2(widthInCells, 1f);
    }

    bool IsTerrainEmpty()
    {
        if (terrainTilemap == null)
            return true;

        BoundsInt bounds = terrainTilemap.cellBounds;
        foreach (Vector3Int cell in bounds.allPositionsWithin)
        {
            if (terrainTilemap.HasTile(cell))
                return false;
        }

        return true;
    }

    int GetSurfaceOffset(int x)
    {
        float wave = Mathf.Sin(x * 0.7f) * 0.65f + Mathf.Sin((x + 4) * 0.31f) * 0.55f;
        return Mathf.RoundToInt(wave);
    }

    TileBase GetTerrainTile()
    {
        if (runtimeTile == null)
            runtimeTile = CreateRuntimeTile();
        return runtimeTile;
    }

    Tile CreateRuntimeTile()
    {
        if (fallbackTexture != null && fallbackSprite != null)
        {
            Tile cachedTile = ScriptableObject.CreateInstance<Tile>();
            cachedTile.sprite = fallbackSprite;
            cachedTile.colliderType = Tile.ColliderType.Grid;
            cachedTile.hideFlags = HideFlags.DontSave;
            return cachedTile;
        }

        fallbackTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        fallbackTexture.filterMode = FilterMode.Point;
        fallbackTexture.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < fallbackTexture.height; y++)
        {
            for (int x = 0; x < fallbackTexture.width; x++)
            {
                Color color = y > 11 ? terrainTopColor : terrainColor;
                if (x == 0 || y == 0 || x == fallbackTexture.width - 1)
                    color *= 0.82f;
                fallbackTexture.SetPixel(x, y, color);
            }
        }

        fallbackTexture.Apply();
        fallbackTexture.hideFlags = HideFlags.DontSave;

        fallbackSprite = Sprite.Create(fallbackTexture, new Rect(0, 0, fallbackTexture.width, fallbackTexture.height), new Vector2(0.5f, 0.5f), 16f);
        fallbackSprite.hideFlags = HideFlags.DontSave;

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = fallbackSprite;
        tile.colliderType = Tile.ColliderType.Grid;
        tile.hideFlags = HideFlags.DontSave;
        return tile;
    }
}
