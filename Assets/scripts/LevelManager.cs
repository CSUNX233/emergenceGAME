using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(1000)]
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    public SimpleBlockPlacer blockPlacer;
    public WorldGrid worldGrid;
    public PlayerMovement2D player;

    [Header("Runtime UI")]
    public bool createRuntimeUI = true;

    private LevelCollectionData collection = new LevelCollectionData();
    private PlayerLife playerLife;
    private Text statusText;
    private bool initialized;
    private bool hasSavedCollection;
    private bool isLoadingLevel;
    private string SavePath => Path.Combine(Application.persistentDataPath, "levels.json");

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

        if (createRuntimeUI)
            EnsureRuntimeUI();

        if (hasSavedCollection && ActiveLevel != null)
            LoadActiveLevel();
        else
            ResetPlayerToSpawn(ActiveLevel != null ? ActiveLevel.playerSpawn.ToVector3() : GetDefaultSpawnPoint());

        UpdateStatus("关卡系统就绪");
    }

    void CacheSceneReferences()
    {
        if (blockPlacer == null)
            blockPlacer = FindObjectOfType<SimpleBlockPlacer>();

        if (worldGrid == null)
            worldGrid = FindObjectOfType<WorldGrid>();

        if (player == null)
            player = FindObjectOfType<PlayerMovement2D>();

        if (player != null)
        {
            playerLife = player.GetComponent<PlayerLife>();
            if (playerLife == null)
                playerLife = player.gameObject.AddComponent<PlayerLife>();
        }
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
                playerSpawn = new SerializableVector3(GetDefaultSpawnPoint())
            });
        }

        collection.activeLevelIndex = Mathf.Clamp(collection.activeLevelIndex, 0, collection.levels.Count - 1);
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
        UpdateStatus($"已保存 {level.name}");
    }

    public void LoadActiveLevel()
    {
        CacheSceneReferences();
        EnsureDefaultLevel();

        LevelData level = ActiveLevel;
        if (level == null)
            return;

        isLoadingLevel = true;
        ClearPlacedObjects();

        if (worldGrid != null)
            worldGrid.LoadMaterials(level.materialCells);

        if (blockPlacer != null && level.placedObjects != null)
        {
            for (int i = 0; i < level.placedObjects.Count; i++)
            {
                PlacedObjectData data = level.placedObjects[i];
                blockPlacer.CreatePlacedObjectFromLevel(data.tool, data.position.ToVector3(), data.rotationZ);
            }
        }

        ResetPlayerToSpawn(level.playerSpawn.ToVector3());
        isLoadingLevel = false;
        UpdateStatus($"已加载 {level.name}");
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
        ClearPlacedObjects();

        if (worldGrid != null)
            worldGrid.ClearAllMaterials();

        ResetPlayerToSpawn(level.playerSpawn.ToVector3());
        WriteCollectionToDisk();
        UpdateStatus($"新建 {level.name}");
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
            UpdateStatus("只有一个关卡");
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
        UpdateStatus("已设置出生点");
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
        UpdateStatus("死亡，正在重开");
    }

    public void CompleteLevel(PlayerMovement2D completedPlayer)
    {
        UpdateStatus($"{ActiveLevelName()} 通关");
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

    void EnsureRuntimeUI()
    {
        EnsureEventSystem();

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (canvas.transform.Find("LevelToolbar") != null)
            return;

        GameObject root = new GameObject("LevelToolbar");
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(12f, -56f);
        rootRect.sizeDelta = new Vector2(660f, 34f);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.08f, 0.09f, 0.10f, 0.58f);

        AddButton(root.transform, "保存", new Vector2(8f, -5f), SaveCurrentLevel);
        AddButton(root.transform, "加载", new Vector2(68f, -5f), LoadActiveLevel);
        AddButton(root.transform, "新关卡", new Vector2(128f, -5f), CreateNewLevel);
        AddButton(root.transform, "上一关", new Vector2(208f, -5f), LoadPreviousLevel);
        AddButton(root.transform, "下一关", new Vector2(288f, -5f), LoadNextLevel);
        AddButton(root.transform, "重置", new Vector2(368f, -5f), LoadActiveLevel);
        AddButton(root.transform, "设出生点", new Vector2(428f, -5f), SetSpawnToPlayerPosition, 88f);

        GameObject label = new GameObject("LevelStatus");
        label.transform.SetParent(root.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(524f, -8f);
        labelRect.sizeDelta = new Vector2(128f, 22f);

        statusText = label.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        statusText.fontSize = 13;
        statusText.alignment = TextAnchor.MiddleLeft;
        statusText.color = Color.white;
    }

    Button AddButton(Transform parent, string label, Vector2 position, UnityEngine.Events.UnityAction action, float width = 54f)
    {
        GameObject obj = new GameObject(label);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 24f);

        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.88f, 0.90f, 0.94f, 0.96f);

        Button button = obj.AddComponent<Button>();
        button.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(obj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = label;
        text.fontSize = 13;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.12f, 0.14f, 0.16f, 1f);

        return button;
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = $"{ActiveLevelName()}  {message}";

        Debug.Log($"LevelManager: {message}");
    }

    string ActiveLevelName()
    {
        LevelData level = ActiveLevel;
        return level != null ? level.name : "No Level";
    }
}
