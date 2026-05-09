using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public class LevelTerrainRegistry : MonoBehaviour
{
    public string terrainLayerName = "PlaceBlock";
    public bool disableUnselectedTerrains = true;
    public bool hideUnboundChildTerrains = true;
    public Transform terrainContainer;
    public List<LevelTerrainBinding> bindings = new List<LevelTerrainBinding>();

    private GameObject runtimeTerrainInstance;

    public string GetDefaultTerrainIdForLevel(int levelNumber)
    {
        LevelTerrainBinding binding = FindByLevelNumber(levelNumber);
        return binding != null ? binding.TerrainId : "";
    }

    public void ApplyLevelTerrain(int levelNumber, string terrainPrefabId)
    {
        LevelTerrainBinding selected = FindBinding(levelNumber, terrainPrefabId);
        Transform container = GetTerrainContainer();

        if (disableUnselectedTerrains)
        {
            if (hideUnboundChildTerrains)
                HideContainerTerrains(container);

            for (int i = 0; i < bindings.Count; i++)
            {
                LevelTerrainBinding binding = bindings[i];
                if (binding == null || binding.terrainRoot == null)
                    continue;

                if (IsSceneObject(binding.terrainRoot))
                    binding.terrainRoot.SetActive(binding == selected);
            }
        }

        ClearRuntimeTerrainInstance();

        if (selected == null || selected.terrainRoot == null)
            return;

        if (IsSceneObject(selected.terrainRoot))
        {
            selected.terrainRoot.SetActive(true);
            ConfigureTerrainObject(selected.terrainRoot);
        }
        else
        {
            runtimeTerrainInstance = Instantiate(selected.terrainRoot, container);
            runtimeTerrainInstance.name = selected.TerrainId;
            runtimeTerrainInstance.transform.localPosition = Vector3.zero;
            runtimeTerrainInstance.transform.localRotation = Quaternion.identity;
            runtimeTerrainInstance.transform.localScale = Vector3.one;
            runtimeTerrainInstance.SetActive(true);
            ConfigureTerrainObject(runtimeTerrainInstance);
        }
    }

    Transform GetTerrainContainer()
    {
        if (terrainContainer != null)
            return terrainContainer;

        return transform;
    }

    void HideContainerTerrains(Transform container)
    {
        if (container == null)
            return;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null || child.gameObject == runtimeTerrainInstance)
                continue;

            if (IsBoundSceneTerrain(child.gameObject))
                continue;

            if (child.GetComponentInChildren<UnityEngine.Tilemaps.Tilemap>(true) != null)
                child.gameObject.SetActive(false);
        }
    }

    bool IsBoundSceneTerrain(GameObject candidate)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            LevelTerrainBinding binding = bindings[i];
            if (binding != null && binding.terrainRoot == candidate && IsSceneObject(candidate))
                return true;
        }

        return false;
    }

    void ClearRuntimeTerrainInstance()
    {
        if (runtimeTerrainInstance == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeTerrainInstance);
        else
            DestroyImmediate(runtimeTerrainInstance);

        runtimeTerrainInstance = null;
    }

    bool IsSceneObject(GameObject obj)
    {
        return obj != null && obj.scene.IsValid();
    }

    void ConfigureTerrainObject(GameObject root)
    {
        if (root == null)
            return;

        Tilemap[] tilemaps = root.GetComponentsInChildren<Tilemap>(true);
        for (int i = 0; i < tilemaps.Length; i++)
            ConfigureTilemapCollision(tilemaps[i]);
    }

    void ConfigureTilemapCollision(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        GameObject tilemapObject = tilemap.gameObject;
        int layer = LayerMask.NameToLayer(terrainLayerName);
        tilemapObject.layer = layer >= 0 ? layer : tilemapObject.layer;

        Rigidbody2D rigidbody2D = tilemapObject.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = tilemapObject.AddComponent<Rigidbody2D>();

        rigidbody2D.bodyType = RigidbodyType2D.Static;
        rigidbody2D.simulated = true;

        CompositeCollider2D composite = tilemapObject.GetComponent<CompositeCollider2D>();
        if (composite == null)
            composite = tilemapObject.AddComponent<CompositeCollider2D>();

        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.enabled = false;

        TilemapCollider2D tilemapCollider = tilemapObject.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null)
            tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();

        tilemapCollider.usedByComposite = false;
        tilemapCollider.enabled = false;

        TerrainTilemapSurface surface = tilemapObject.GetComponent<TerrainTilemapSurface>();
        if (surface == null)
            tilemapObject.AddComponent<TerrainTilemapSurface>();

        RebuildSolidColliders(tilemap);
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
        else
        {
            for (int i = collisionRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = collisionRoot.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
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

    LevelTerrainBinding FindBinding(int levelNumber, string terrainPrefabId)
    {
        if (!string.IsNullOrEmpty(terrainPrefabId))
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                LevelTerrainBinding binding = bindings[i];
                if (binding != null && binding.TerrainId == terrainPrefabId)
                    return binding;
            }
        }

        return FindByLevelNumber(levelNumber);
    }

    LevelTerrainBinding FindByLevelNumber(int levelNumber)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            LevelTerrainBinding binding = bindings[i];
            if (binding != null && binding.levelNumber == levelNumber)
                return binding;
        }

        return null;
    }

    [ContextMenu("Auto Bind Child Terrains To Level 2 And 3")]
    void AutoBindChildTerrainsToLevel2And3()
    {
        bindings.Clear();

        int levelNumber = 2;
        foreach (Transform child in transform)
        {
            if (child == null)
                continue;

            bindings.Add(new LevelTerrainBinding
            {
                levelNumber = levelNumber,
                terrainPrefabId = child.name,
                terrainRoot = child.gameObject
            });
            levelNumber++;
        }
    }
}

[Serializable]
public class LevelTerrainBinding
{
    public int levelNumber = 1;
    public string terrainPrefabId;
    public GameObject terrainRoot;

    public string TerrainId
    {
        get
        {
            if (!string.IsNullOrEmpty(terrainPrefabId))
                return terrainPrefabId;

            return terrainRoot != null ? terrainRoot.name : "";
        }
    }
}
