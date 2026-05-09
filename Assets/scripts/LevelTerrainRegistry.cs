using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelTerrainRegistry : MonoBehaviour
{
    public bool disableUnselectedTerrains = true;
    public List<LevelTerrainBinding> bindings = new List<LevelTerrainBinding>();

    public string GetDefaultTerrainIdForLevel(int levelNumber)
    {
        LevelTerrainBinding binding = FindByLevelNumber(levelNumber);
        return binding != null ? binding.TerrainId : "";
    }

    public void ApplyLevelTerrain(int levelNumber, string terrainPrefabId)
    {
        LevelTerrainBinding selected = FindBinding(levelNumber, terrainPrefabId);

        if (disableUnselectedTerrains)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                LevelTerrainBinding binding = bindings[i];
                if (binding == null || binding.terrainRoot == null)
                    continue;

                binding.terrainRoot.SetActive(binding == selected);
            }
        }
        else if (selected != null && selected.terrainRoot != null)
        {
            selected.terrainRoot.SetActive(true);
        }
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
