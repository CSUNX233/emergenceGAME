using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public SimpleBlockPlacer blockPlacer;
    public WorldGrid worldGrid;
    public PlayerMovement2D player;
    public LevelTerrainRegistry terrainRegistry;

    [Header("Scene UI")]
    public bool bindSceneUI = true;
    public bool ignoreLevelUiClicksWhilePlacing = true;

    [Header("Scene Terrain Variants")]
    public bool bindSceneTerrainVariants = true;
    public string level2TerrainName = "Terrain Tilemap";
    public string level3TerrainName = "Tilemap2";

    [Header("Level Flow")]
    public bool autoAdvanceOnClear = true;
    public float clearAdvanceDelay = 1f;

    private LevelCollectionData collection = new LevelCollectionData();
    private PlayerLife playerLife;
    private GameObject levelToggleButtonObject;
    private GameObject levelPanel;
    private GameObject resultPanel;
    private GameObject levelNameTextObject;
    private UiText levelNameText;
    private UiText levelStatusText;
    private UiText resultTitleText;
    private bool initialized;
    private bool hasSavedCollection;
    private bool isLoadingLevel;
    private bool isCompletingLevel;
    private Coroutine clearAdvanceRoutine;

    private string SavePath => Path.Combine(Application.persistentDataPath, "levels.json");

    public bool IsLevelPanelVisible => levelPanel != null && levelPanel.activeSelf;

    public LevelData ActiveLevel
    {
        get
        {
            if (collection == null || collection.levels == null || collection.levels.Count == 0)
                return null;

            collection.activeLevelIndex = Mathf.Clamp(collection.activeLevelIndex, 0, collection.levels.Count - 1);
            return collection.levels[collection.activeLevelIndex];
        }
    }

    public static LevelManager EnsureInScene(SimpleBlockPlacer placer)
    {
        LevelManager manager = Instance != null ? Instance : FindObjectOfType<LevelManager>();
        if (manager == null)
        {
            GameObject obj = new GameObject("LevelManager");
            manager = obj.AddComponent<LevelManager>();
        }

        if (placer != null)
            manager.blockPlacer = placer;

        manager.Initialize();
        return manager;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
            return;

        initialized = true;
        StartCoroutine(InitializeRoutine());
    }

    IEnumerator InitializeRoutine()
    {
        yield return null;

        CacheSceneReferences();
        LoadCollectionFromDisk();
        EnsureDefaultLevel();

        if (bindSceneUI)
            BindSceneUI();

        SetLevelPanelVisible(false);
        HideResult();
        ApplyActiveLevelTerrain();

        if (hasSavedCollection && ActiveLevel != null)
            LoadActiveLevel();
        else
            ResetPlayerToSpawn(ActiveLevel != null ? ActiveLevel.playerSpawn.ToVector3() : GetDefaultSpawnPoint());

        UpdateStatus("Ready");
    }

    void CacheSceneReferences()
    {
        if (blockPlacer == null)
            blockPlacer = FindObjectOfType<SimpleBlockPlacer>();

        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();

        if (player == null)
            player = FindObjectOfType<PlayerMovement2D>();

        if (terrainRegistry == null)
            terrainRegistry = FindObjectOfType<LevelTerrainRegistry>();

        if (player != null)
        {
            playerLife = player.GetComponent<PlayerLife>();
            if (playerLife == null)
                playerLife = player.gameObject.AddComponent<PlayerLife>();
        }
    }

    void BindSceneUI()
    {
        levelPanel = FindSceneObject("levelPanel", "LevelPanel");
        levelToggleButtonObject = FindSceneObject("level", "BtnLevel");
        resultPanel = FindSceneObject("ResultPanel");

        KeepLevelToggleOutsidePanel();
        ConfigureLevelPanelRaycasts();

        levelNameTextObject = FindSceneObject("TextLevelName");
        if (levelNameTextObject != null)
            levelNameTextObject.SetActive(false);

        levelNameText = UiText.FromObject(levelNameTextObject);
        levelStatusText = UiText.Find("TextLevelStatus");
        resultTitleText = UiText.Find("TextResultTitle");

        SetButtonLabel("level", "Level");
        SetButtonLabel("BtnLevel", "Level");
        SetButtonLabel("BtnSaveLevel", "save");
        SetButtonLabel("BtnLoadLevel", "load");
        SetButtonLabel("BtnNewLevel", "new");
        SetButtonLabel("BtnPrevLevel", "previous");
        SetButtonLabel("BtnNextLevel", "next");
        SetButtonLabel("BtnResetLevel", "Reset");
        SetButtonLabel("BtnSetSpawn", "spawn");

        BindButton("level", ToggleLevelPanel);
        BindButton("BtnLevel", ToggleLevelPanel);
        BindButton("BtnSaveLevel", SaveCurrentLevel);
        BindButton("BtnLoadLevel", LoadActiveLevel);
        BindButton("BtnNewLevel", CreateNewLevel);
        BindButton("BtnPrevLevel", LoadPreviousLevel);
        BindButton("BtnNextLevel", LoadNextLevel);
        BindButton("BtnResetLevel", LoadActiveLevel);
        BindButton("BtnSetSpawn", SetSpawnToPlayerPosition);
    }

    void KeepLevelToggleOutsidePanel()
    {
        if (levelToggleButtonObject == null || levelPanel == null)
            return;

        if (!levelToggleButtonObject.transform.IsChildOf(levelPanel.transform))
            return;

        Transform newParent = levelPanel.transform.parent;
        Canvas canvas = FindObjectOfType<Canvas>();
        if (newParent == null && canvas != null)
            newParent = canvas.transform;

        if (newParent == null)
            return;

        levelToggleButtonObject.transform.SetParent(newParent, true);
        levelToggleButtonObject.transform.SetAsLastSibling();
        levelToggleButtonObject.SetActive(true);
    }

    void ConfigureLevelPanelRaycasts()
    {
        if (levelPanel == null)
            return;

        Graphic[] graphics = levelPanel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
                continue;

            Selectable selectable = graphic.GetComponentInParent<Selectable>(true);
            bool belongsToButton = selectable != null && selectable.transform.IsChildOf(levelPanel.transform);
            graphic.raycastTarget = belongsToButton;
        }
    }

    void BindButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        Button button = FindButton(objectName);
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            if (ShouldIgnoreLevelUiClick())
                blockPlacer.ClearActiveSelection();

            action.Invoke();
            if (UnityEngine.EventSystems.EventSystem.current != null)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        });
    }

    public bool ShouldIgnoreLevelUiObject(GameObject hitObject)
    {
        if (!ShouldIgnoreLevelUiClick() || hitObject == null)
            return false;

        Transform hitTransform = hitObject.transform;
        if (levelToggleButtonObject != null && hitTransform.IsChildOf(levelToggleButtonObject.transform))
            return true;

        return levelPanel != null && hitTransform.IsChildOf(levelPanel.transform);
    }

    bool ShouldIgnoreLevelUiClick()
    {
        return ignoreLevelUiClicksWhilePlacing
            && blockPlacer != null
            && blockPlacer.IsPlacementModeActive;
    }

    void SetButtonLabel(string objectName, string label)
    {
        GameObject obj = FindSceneObject(objectName);
        if (obj == null)
            return;

        UiText text = UiText.FindInChildren(obj);
        text.SetText(label);
    }

    Button FindButton(string objectName)
    {
        GameObject obj = FindSceneObject(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    GameObject FindSceneObject(params string[] names)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t == null || t.hideFlags != HideFlags.None)
                continue;
            if (!t.gameObject.scene.IsValid())
                continue;

            string sceneName = t.name.Trim();
            for (int j = 0; j < names.Length; j++)
            {
                if (sceneName == names[j])
                    return t.gameObject;
            }
        }

        return null;
    }

    void LoadCollectionFromDisk()
    {
        hasSavedCollection = File.Exists(SavePath);
        if (!hasSavedCollection)
        {
            collection = new LevelCollectionData();
            return;
        }

        string json = File.ReadAllText(SavePath);
        collection = JsonUtility.FromJson<LevelCollectionData>(json);
        if (collection == null)
            collection = new LevelCollectionData();
    }

    void EnsureDefaultLevel()
    {
        if (collection.levels == null)
            collection.levels = new List<LevelData>();

        if (collection.levels.Count == 0)
        {
            collection.levels.Add(new LevelData
            {
                name = "Level 1",
                terrainPrefabId = "",
                playerSpawn = new SerializableVector3(GetDefaultSpawnPoint())
            });
        }

        while (collection.levels.Count < 3)
        {
            int levelNumber = collection.levels.Count + 1;
            collection.levels.Add(new LevelData
            {
                name = $"Level {levelNumber}",
                terrainPrefabId = GetDefaultTerrainIdForLevelNumber(levelNumber),
                playerSpawn = new SerializableVector3(GetDefaultSpawnPoint())
            });
        }

        AssignDefaultTerrainIds();

        collection.activeLevelIndex = Mathf.Clamp(collection.activeLevelIndex, 0, collection.levels.Count - 1);
    }

    void AssignDefaultTerrainIds()
    {
        if (collection == null || collection.levels == null)
            return;

        for (int i = 0; i < collection.levels.Count; i++)
        {
            LevelData level = collection.levels[i];
            if (level == null || !string.IsNullOrEmpty(level.terrainPrefabId))
                continue;

            level.terrainPrefabId = GetDefaultTerrainIdForLevelNumber(i + 1);
        }
    }

    string GetDefaultTerrainIdForLevelNumber(int levelNumber)
    {
        if (terrainRegistry != null)
        {
            string registryTerrainId = terrainRegistry.GetDefaultTerrainIdForLevel(levelNumber);
            return registryTerrainId;
        }

        if (levelNumber == 2)
            return level2TerrainName;

        if (levelNumber == 3)
            return level3TerrainName;

        return "";
    }

    public void ToggleLevelPanel()
    {
        if (levelPanel == null)
            return;

        levelPanel.SetActive(!levelPanel.activeSelf);
        if (levelToggleButtonObject != null)
            levelToggleButtonObject.SetActive(true);
    }

    public void SetLevelPanelVisible(bool visible)
    {
        if (levelPanel != null)
            levelPanel.SetActive(visible);

        if (levelToggleButtonObject != null)
            levelToggleButtonObject.SetActive(true);
    }

    public void SaveCurrentLevel()
    {
        CacheSceneReferences();
        EnsureDefaultLevel();

        LevelData level = ActiveLevel;
        if (level == null)
            return;

        level.playerSpawn = new SerializableVector3(playerLife != null ? playerLife.SpawnPoint : GetDefaultSpawnPoint());
        level.placedObjects = CollectPlacedObjects();
        level.materialCells = worldGrid != null ? worldGrid.ExportNonAirMaterials() : new List<MaterialCellData>();

        WriteCollectionToDisk();
        UpdateStatus("Saved");
    }

    public void LoadActiveLevel()
    {
        CancelClearAdvance();
        CacheSceneReferences();
        EnsureDefaultLevel();

        LevelData level = ActiveLevel;
        if (level == null)
            return;

        isLoadingLevel = true;
        HideResult();
        ApplyActiveLevelTerrain();
        ClearPlacedObjects();

        if (worldGrid != null)
            worldGrid.LoadMaterials(level.materialCells);

        if (blockPlacer != null && level.placedObjects != null)
        {
            List<GameObject> loadedObjects = new List<GameObject>();
            for (int i = 0; i < level.placedObjects.Count; i++)
            {
                PlacedObjectData data = level.placedObjects[i];
                GameObject loadedObject = blockPlacer.CreatePlacedObjectFromLevel(data.tool, data.position.ToVector3(), data.rotationZ, true);
                if (loadedObject != null)
                    loadedObjects.Add(loadedObject);
            }

            blockPlacer.RestoreLoadedLevelObjects(loadedObjects);
        }

        ResetPlayerToSpawn(level.playerSpawn.ToVector3());
        isCompletingLevel = false;
        isLoadingLevel = false;
        UpdateStatus("Loaded");
    }

    public void CreateNewLevel()
    {
        SaveCurrentLevel();
        EnsureDefaultLevel();

        LevelData level = new LevelData
        {
            name = $"Level {collection.levels.Count + 1}",
            playerSpawn = new SerializableVector3(GetDefaultSpawnPoint())
        };

        collection.levels.Add(level);
        collection.activeLevelIndex = collection.levels.Count - 1;
        HideResult();
        ApplyActiveLevelTerrain();
        ClearPlacedObjects();

        if (worldGrid != null)
            worldGrid.ClearAllMaterials();

        ResetPlayerToSpawn(level.playerSpawn.ToVector3());
        WriteCollectionToDisk();
        UpdateStatus("New level");
    }

    public void LoadPreviousLevel()
    {
        ChangeLevel(-1);
    }

    public void LoadNextLevel()
    {
        ChangeLevel(1);
    }

    void ChangeLevel(int direction)
    {
        EnsureDefaultLevel();
        if (collection.levels.Count <= 1)
        {
            UpdateStatus("Only one level");
            return;
        }

        SaveCurrentLevel();
        collection.activeLevelIndex = (collection.activeLevelIndex + direction + collection.levels.Count) % collection.levels.Count;
        WriteCollectionToDisk();
        LoadActiveLevel();
    }

    public void SetSpawnToPlayerPosition()
    {
        CacheSceneReferences();
        if (player == null)
            return;

        EnsureDefaultLevel();
        Vector3 spawn = player.transform.position;
        ActiveLevel.playerSpawn = new SerializableVector3(spawn);

        if (playerLife != null)
            playerLife.SetSpawnPoint(spawn);

        WriteCollectionToDisk();
        UpdateStatus("Spawn set");
    }

    void ApplyActiveLevelTerrain()
    {
        LevelData level = ActiveLevel;
        int levelNumber = collection != null ? collection.activeLevelIndex + 1 : 0;
        ApplySceneTerrainVariant(levelNumber, level != null ? level.terrainPrefabId : "");
    }

    void ApplySceneTerrainVariant(int levelNumber, string terrainPrefabId)
    {
        if (!bindSceneTerrainVariants)
            return;

        if (terrainRegistry == null)
            terrainRegistry = FindObjectOfType<LevelTerrainRegistry>();

        if (terrainRegistry != null)
        {
            terrainRegistry.ApplyLevelTerrain(levelNumber, terrainPrefabId);
            return;
        }

        GameObject level2Terrain = FindSceneObject(level2TerrainName);
        GameObject level3Terrain = FindSceneObject(level3TerrainName);

        SetTerrainVariantActive(level2Terrain, terrainPrefabId == level2TerrainName);
        SetTerrainVariantActive(level3Terrain, terrainPrefabId == level3TerrainName);
    }

    void SetTerrainVariantActive(GameObject terrainVariant, bool active)
    {
        if (terrainVariant == null)
            return;

        if (terrainVariant.activeSelf != active)
            terrainVariant.SetActive(active);
    }

    public void ReloadCurrentLevelAfterDeath()
    {
        if (isLoadingLevel)
            return;

        if (hasSavedCollection || ActiveLevelHasContent())
            LoadActiveLevel();
        else
            ResetPlayerToSpawn(ActiveLevel != null ? ActiveLevel.playerSpawn.ToVector3() : GetDefaultSpawnPoint());
    }

    public void HandlePlayerDied(PlayerLife life)
    {
        CancelClearAdvance();
        ShowResult("DEATH");
        UpdateStatus("Death");
    }

    public void CompleteLevel(PlayerMovement2D completedPlayer)
    {
        if (isCompletingLevel)
            return;

        isCompletingLevel = true;
        ShowResult("CLEAR");
        UpdateStatus("Clear");

        if (autoAdvanceOnClear)
            StartClearAdvance();
    }

    void StartClearAdvance()
    {
        if (clearAdvanceRoutine != null)
            StopCoroutine(clearAdvanceRoutine);

        clearAdvanceRoutine = StartCoroutine(ClearAdvanceRoutine());
    }

    IEnumerator ClearAdvanceRoutine()
    {
        float waitTime = Mathf.Max(0f, clearAdvanceDelay);
        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);
        else
            yield return null;

        clearAdvanceRoutine = null;
        isCompletingLevel = false;
        LoadNextLevel();
    }

    void CancelClearAdvance()
    {
        if (clearAdvanceRoutine != null)
        {
            StopCoroutine(clearAdvanceRoutine);
            clearAdvanceRoutine = null;
        }

        isCompletingLevel = false;
    }

    List<PlacedObjectData> CollectPlacedObjects()
    {
        List<PlacedObjectData> result = new List<PlacedObjectData>();
        LevelPlacedObject[] objects = FindObjectsOfType<LevelPlacedObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null || !objects[i].gameObject.scene.IsValid())
                continue;

            result.Add(objects[i].ToData());
        }

        return result;
    }

    void ClearPlacedObjects()
    {
        LevelPlacedObject[] objects = FindObjectsOfType<LevelPlacedObject>();
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            if (objects[i] == null)
                continue;

            objects[i].gameObject.SetActive(false);
            Destroy(objects[i].gameObject);
        }
    }

    bool ActiveLevelHasContent()
    {
        LevelData level = ActiveLevel;
        if (level == null)
            return false;

        bool hasObjects = level.placedObjects != null && level.placedObjects.Count > 0;
        bool hasMaterials = level.materialCells != null && level.materialCells.Count > 0;
        return hasObjects || hasMaterials;
    }

    void ResetPlayerToSpawn(Vector3 spawn)
    {
        HideResult();

        if (playerLife != null)
            playerLife.ResetForLevel(spawn);
        else if (player != null)
            player.transform.position = spawn;
    }

    Vector3 GetDefaultSpawnPoint()
    {
        if (playerLife != null)
            return playerLife.SpawnPoint;

        if (player != null)
            return player.transform.position;

        return Vector3.zero;
    }

    void WriteCollectionToDisk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        string json = JsonUtility.ToJson(collection, true);
        File.WriteAllText(SavePath, json);
        hasSavedCollection = true;
    }

    void ShowResult(string title)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        resultTitleText.SetText(title);
    }

    void HideResult()
    {
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    void UpdateStatus(string message)
    {
        levelNameText.SetText("");
        levelStatusText.SetText(GetLevelCounterText());
        Debug.Log($"LevelManager: {ActiveLevelName()} {message}");
    }

    string ActiveLevelName()
    {
        LevelData level = ActiveLevel;
        return level != null ? level.name : "No Level";
    }

    string GetLevelCounterText()
    {
        int total = collection != null && collection.levels != null ? collection.levels.Count : 0;
        if (total <= 0)
            return "Level 0 / 0";

        int current = Mathf.Clamp(collection.activeLevelIndex + 1, 1, total);
        return $"Level {current} / {total}";
    }

    struct UiText
    {
        private readonly Text legacyText;
        private readonly TMP_Text tmpText;

        public UiText(Text legacyText, TMP_Text tmpText)
        {
            this.legacyText = legacyText;
            this.tmpText = tmpText;
        }

        public static UiText Find(string objectName)
        {
            GameObject obj = FindSceneObject(objectName);
            return FromObject(obj);
        }

        public static UiText FromObject(GameObject obj)
        {
            if (obj == null)
                return default;

            return new UiText(obj.GetComponent<Text>(), obj.GetComponent<TMP_Text>());
        }

        public static UiText FindInChildren(GameObject obj)
        {
            if (obj == null)
                return default;

            return new UiText(
                obj.GetComponentInChildren<Text>(true),
                obj.GetComponentInChildren<TMP_Text>(true)
            );
        }

        public void SetText(string value)
        {
            if (legacyText != null)
                legacyText.text = value;

            if (tmpText != null)
                tmpText.text = value;
        }

        static GameObject FindSceneObject(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform t = transforms[i];
                if (t == null || t.hideFlags != HideFlags.None)
                    continue;
                if (!t.gameObject.scene.IsValid())
                    continue;
                if (t.name.Trim() == objectName)
                    return t.gameObject;
            }

            return null;
        }
    }
}
