using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum PlayerInputMode
{
    Elements,
    Build
}

public enum ElementPaintTool
{
    Fire,
    Water,
    Wood
}

public enum BuildTool
{
    Axle,
    SlimeBlock,
    WoodPlank,
    WoodBarrel,
    IronBlock,
    StoneBlock,
    Spike,
    Flag,
    BalanceScale
}

public class SimpleBlockPlacer : MonoBehaviour
{
    public Camera mainCamera;
    public GameObject axlePrefab;
    public GameObject slimeBlockPrefab;
    public GameObject woodPlankPrefab;
    public GameObject woodBarrelPrefab;
    public GameObject ironBlockPrefab;
    public GameObject stoneBlockPrefab;
    public GameObject spikePrefab;
    public GameObject flagPrefab;
    public GameObject balanceScalePrefab;

    [Header("Snap Size")]
    public float snapSize = 1f;

    [Header("Placement Layer")]
    public LayerMask placementBlockLayer;

    [Header("Attachment Layer")]
    public LayerMask attachmentLayer;

    [Header("Preview Colors")]
    public Color validColor = new Color(0f, 1f, 0f, 0.35f);
    public Color invalidColor = new Color(1f, 0f, 0f, 0.35f);
    public Color attachHintColor = new Color(0.3f, 1f, 0.3f, 0.85f);

    [Header("Attachment Search Radius")]
    public float attachSearchRadius = 1.2f;

    [Header("Rotation")]
    public float rotationStepDegrees = 90f;

    [Header("UI")]
    public bool showModeUI = true;
    public bool toolbarExpanded = true;

    private GameObject currentPrefabToPlace;
    private GameObject runtimeBalanceScalePrefab;
    private GameObject previewInstance;
    private SpriteRenderer[] previewSpriteRenderers;
    private float currentRotationZ;

    private Canvas toolbarCanvas;
    private RectTransform toolbarRoot;
    private GameObject toolbarBackground;
    private Button toggleToolbarButton;
    private Button buildModeButton;
    private Button elementModeButton;
    private readonly List<Button> modeButtons = new List<Button>();
    private readonly List<Button> elementToolButtons = new List<Button>();
    private readonly List<Button> buildToolButtons = new List<Button>();
    private readonly List<Button> expandedBuildToolButtons = new List<Button>();
    private readonly List<GameObject> collapsibleUiObjects = new List<GameObject>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private Button buildExpandButton;
    private bool buildExpanded;
    private bool restoreLevelPanelAfterBuildExpand;
    private int ignorePlacementThroughFrame = -1;

    private static readonly string[] FlagButtonNames = { "旗帜", "Flag", "flag", "终点旗" };
    private static readonly string[] BalanceScaleButtonNames = { "BtnBalanceScale", "天平", "Balance", "BalanceScale", "balance" };
    private static readonly string[] BuildModeButtonNames = { "BtnBuildMode", "建造模式" };
    private static readonly string[] ElementModeButtonNames = { "BtnElementMode", "元素模式" };
    private static readonly string[] BuildExpandButtonNames = { "BtnBuildExpand", "BtnExpandBuild", "展开", "Expand" };
    private bool hasActiveBuildTool = false;
    private bool hasActiveElementTool = false;

    public PlayerInputMode CurrentMode { get; private set; } = PlayerInputMode.Build;
    public ElementPaintTool CurrentElementTool { get; private set; } = ElementPaintTool.Fire;
    public BuildTool CurrentBuildTool { get; private set; } = BuildTool.SlimeBlock;

    public bool IsPlacementModeActive => CurrentMode == PlayerInputMode.Build && currentPrefabToPlace != null;
    public bool IsElementPaintActive => CurrentMode == PlayerInputMode.Elements && hasActiveElementTool;
    public bool IsPointerOverToolbar => showModeUI && IsPointerOverBlockingUiControl();

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        CurrentMode = PlayerInputMode.Build;
        hasActiveBuildTool = false;
        hasActiveElementTool = false;
        currentPrefabToPlace = null;
        EnsureRuntimeToolbar();
        LevelManager.EnsureInScene(this);
        SetBuildExpanded(false, false);
        RefreshToolbarButtons();
    }

    void Update()
    {
        if (mainCamera == null)
        {
            Debug.LogError("SimpleBlockPlacer: mainCamera is not assigned.");
            return;
        }

        HandleHotkeys();
        HandleRotationInput();
        UpdatePreview();
        HandlePlacement();
    }

    void HandleHotkeys()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            SwitchToElementMode();

        if (Input.GetKeyDown(KeyCode.F2))
            SwitchToBuildMode();

        if (Input.GetKeyDown(KeyCode.Escape))
            SwitchToElementMode();

        if (Input.GetKeyDown(KeyCode.Tab))
            SetToolbarExpanded(!toolbarExpanded);
    }

    void HandleRotationInput()
    {
        if (CurrentMode != PlayerInputMode.Build || previewInstance == null)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f)
            return;

        float delta = scroll > 0f ? rotationStepDegrees : -rotationStepDegrees;
        currentRotationZ = Mathf.Repeat(currentRotationZ + delta, 360f);
        previewInstance.transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);
    }

    void UpdatePreview()
    {
        if (CurrentMode != PlayerInputMode.Build || currentPrefabToPlace == null || previewInstance == null)
            return;

        if (IsPointerOverToolbar)
            return;

        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;

        Vector3 desiredPos = GetSnappedPosition(mousePos);
        bool canPlace = TryGetPlacementPosition(currentPrefabToPlace, desiredPos, currentRotationZ, out Vector3 previewPos, out float previewRotationZ, out bool willAttach);
        previewInstance.transform.position = previewPos;
        previewInstance.transform.rotation = Quaternion.Euler(0f, 0f, previewRotationZ);

        if (previewSpriteRenderers != null)
        {
            Color previewColor = !canPlace
                ? invalidColor
                : willAttach ? attachHintColor : validColor;

            for (int i = 0; i < previewSpriteRenderers.Length; i++)
            {
                if (previewSpriteRenderers[i] == null)
                    continue;

                previewSpriteRenderers[i].color = previewColor;
                previewSpriteRenderers[i].sortingOrder = 100 + i;
            }
        }
    }

    void HandlePlacement()
    {
        if (CurrentMode != PlayerInputMode.Build || currentPrefabToPlace == null || previewInstance == null)
            return;

        if (Time.frameCount <= ignorePlacementThroughFrame)
            return;

        if (IsPointerOverToolbar || !Input.GetMouseButtonDown(0))
            return;

        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector3 desiredPos = GetSnappedPosition(mousePos);

        if (!TryGetPlacementPosition(currentPrefabToPlace, desiredPos, currentRotationZ, out Vector3 placePos, out float placeRotationZ, out _))
            return;

        GameObject placed = Instantiate(currentPrefabToPlace, placePos, Quaternion.Euler(0f, 0f, placeRotationZ));
        placed.SetActive(true);
        ConfigurePlacedObject(placed);
        MarkPlacedObject(placed, CurrentBuildTool);
    }

    void ConfigurePlacedObject(GameObject placed)
    {
        StickyBlock sticky = placed.GetComponent<StickyBlock>();
        if (sticky != null)
        {
            sticky.attachSearchRadius = attachSearchRadius;
            sticky.attachmentLayer = attachmentLayer;
            sticky.TryAutoAttach();
        }

        WoodPlankBlock plank = placed.GetComponent<WoodPlankBlock>();
        if (plank != null)
        {
            plank.attachSearchRadius = attachSearchRadius;
            plank.attachmentLayer = attachmentLayer;
            plank.TryAutoAttach();
        }

        StickyAttachBlock attachedBlock = placed.GetComponent<StickyAttachBlock>();
        if (attachedBlock != null)
        {
            attachedBlock.attachSearchRadius = attachSearchRadius;
            attachedBlock.attachmentLayer = attachmentLayer;
            attachedBlock.TryAutoAttach();
        }

        SpikeTrap spikeTrap = placed.GetComponent<SpikeTrap>();
        if (spikeTrap != null)
        {
            spikeTrap.attachSearchRadius = attachSearchRadius;
            spikeTrap.attachmentLayer = attachmentLayer;
            spikeTrap.TryAutoAttach();
        }

        FlagGoal flagGoal = placed.GetComponent<FlagGoal>();
        if (flagGoal != null)
        {
            flagGoal.attachSearchRadius = attachSearchRadius;
            flagGoal.attachmentLayer = attachmentLayer;
            flagGoal.TryAutoAttach();
        }
    }

    public GameObject CreatePlacedObjectFromLevel(BuildTool tool, Vector3 position, float rotationZ)
    {
        return CreatePlacedObjectFromLevel(tool, position, rotationZ, false);
    }

    public GameObject CreatePlacedObjectFromLevel(BuildTool tool, Vector3 position, float rotationZ, bool preserveSavedPose)
    {
        GameObject prefab = GetPrefabForTool(tool);
        if (prefab == null)
            return null;

        GameObject placed = Instantiate(prefab, position, Quaternion.Euler(0f, 0f, rotationZ));
        placed.SetActive(true);
        if (preserveSavedPose)
            PrepareLoadedObject(placed);
        else
            ConfigurePlacedObject(placed);
        MarkPlacedObject(placed, tool);
        return placed;
    }

    void PrepareLoadedObject(GameObject placed)
    {
        StickyBlock sticky = placed.GetComponent<StickyBlock>();
        if (sticky != null)
        {
            sticky.autoAttachOnStart = false;
            sticky.attachSearchRadius = attachSearchRadius;
            sticky.attachmentLayer = attachmentLayer;
        }

        WoodPlankBlock plank = placed.GetComponent<WoodPlankBlock>();
        if (plank != null)
        {
            plank.attachSearchRadius = attachSearchRadius;
            plank.attachmentLayer = attachmentLayer;
        }

        StickyAttachBlock attachedBlock = placed.GetComponent<StickyAttachBlock>();
        if (attachedBlock != null)
        {
            attachedBlock.attachSearchRadius = attachSearchRadius;
            attachedBlock.attachmentLayer = attachmentLayer;
        }

        SpikeTrap spikeTrap = placed.GetComponent<SpikeTrap>();
        if (spikeTrap != null)
        {
            spikeTrap.attachSearchRadius = attachSearchRadius;
            spikeTrap.attachmentLayer = attachmentLayer;
        }

        FlagGoal flagGoal = placed.GetComponent<FlagGoal>();
        if (flagGoal != null)
        {
            flagGoal.attachSearchRadius = attachSearchRadius;
            flagGoal.attachmentLayer = attachmentLayer;
        }
    }

    public void RestoreLoadedLevelObjects(IList<GameObject> loadedObjects)
    {
        if (loadedObjects == null)
            return;

        RestoreStickyAxleAttachments(loadedObjects);
        RestoreAttachedBlocksToStickyAxles(loadedObjects);
    }

    void RestoreStickyAxleAttachments(IList<GameObject> loadedObjects)
    {
        List<RotatingAxle> axles = new List<RotatingAxle>();
        List<StickyBlock> stickyBlocks = new List<StickyBlock>();

        for (int i = 0; i < loadedObjects.Count; i++)
        {
            GameObject obj = loadedObjects[i];
            if (obj == null)
                continue;

            RotatingAxle axle = obj.GetComponent<RotatingAxle>();
            if (axle != null)
                axles.Add(axle);

            StickyBlock sticky = obj.GetComponent<StickyBlock>();
            if (sticky != null)
            {
                sticky.autoAttachOnStart = false;
                stickyBlocks.Add(sticky);
            }
        }

        for (int i = 0; i < axles.Count; i++)
        {
            RotatingAxle axle = axles[i];
            if (axle != null)
                axle.attachedBlocks.Clear();
        }

        bool changed;
        int guard = 0;
        do
        {
            changed = false;
            guard++;

            for (int i = 0; i < stickyBlocks.Count; i++)
            {
                StickyBlock sticky = stickyBlocks[i];
                if (sticky == null || sticky.isAttached)
                    continue;

                RotatingAxle axle = FindConnectedAxleForSticky(sticky, axles, stickyBlocks);
                if (axle == null)
                    continue;

                axle.AttachBlock(sticky);
                changed = true;
            }
        }
        while (changed && guard < stickyBlocks.Count + 2);
    }

    RotatingAxle FindConnectedAxleForSticky(StickyBlock sticky, List<RotatingAxle> axles, List<StickyBlock> stickyBlocks)
    {
        Collider2D stickyCollider = sticky != null ? sticky.GetComponent<Collider2D>() : null;
        if (stickyCollider == null)
            return null;

        for (int i = 0; i < axles.Count; i++)
        {
            RotatingAxle axle = axles[i];
            if (axle == null)
                continue;

            Collider2D axleCollider = axle.GetComponent<Collider2D>();
            if (AreCollidersConnected(stickyCollider, axleCollider, 0.12f))
                return axle;
        }

        for (int i = 0; i < stickyBlocks.Count; i++)
        {
            StickyBlock other = stickyBlocks[i];
            if (other == null || other == sticky || !other.isAttached || other.attachedAxle == null)
                continue;

            Collider2D otherCollider = other.GetComponent<Collider2D>();
            if (AreCollidersConnected(stickyCollider, otherCollider, 0.12f))
                return other.attachedAxle;
        }

        return null;
    }

    void RestoreAttachedBlocksToStickyAxles(IList<GameObject> loadedObjects)
    {
        for (int i = 0; i < loadedObjects.Count; i++)
        {
            GameObject obj = loadedObjects[i];
            if (obj == null)
                continue;

            WoodPlankBlock plank = obj.GetComponent<WoodPlankBlock>();
            if (plank != null && !plank.isAttached)
            {
                RotatingAxle axle = FindNearbyStickyAxle(plank.GetComponent<Collider2D>());
                if (axle != null)
                    plank.AttachToAxle(axle);
            }

            StickyAttachBlock attachBlock = obj.GetComponent<StickyAttachBlock>();
            if (attachBlock != null && !attachBlock.isAttached)
            {
                RotatingAxle axle = FindNearbyStickyAxle(attachBlock.GetComponent<Collider2D>());
                if (axle != null)
                    attachBlock.AttachToAxle(axle);
            }
        }
    }

    RotatingAxle FindNearbyStickyAxle(Collider2D sourceCollider)
    {
        if (sourceCollider == null)
            return null;

        StickyBlock[] stickyBlocks = FindObjectsOfType<StickyBlock>();
        for (int i = 0; i < stickyBlocks.Length; i++)
        {
            StickyBlock sticky = stickyBlocks[i];
            if (sticky == null || !sticky.isAttached || sticky.attachedAxle == null)
                continue;

            if (AreCollidersConnected(sourceCollider, sticky.GetComponent<Collider2D>(), 0.12f))
                return sticky.attachedAxle;
        }

        return null;
    }

    bool AreCollidersConnected(Collider2D a, Collider2D b, float tolerance)
    {
        if (a == null || b == null)
            return false;

        ColliderDistance2D distance = a.Distance(b);
        return distance.isOverlapped || distance.distance <= tolerance;
    }

    void MarkPlacedObject(GameObject placed, BuildTool tool)
    {
        if (placed == null)
            return;

        LevelPlacedObject marker = placed.GetComponent<LevelPlacedObject>();
        if (marker == null)
            marker = placed.AddComponent<LevelPlacedObject>();

        marker.tool = tool;
    }

    public void SwitchToElementMode(ElementPaintTool tool)
    {
        CurrentElementTool = tool;
        CurrentMode = PlayerInputMode.Elements;
        hasActiveElementTool = true;
        DestroyPreview();
        RefreshToolbarButtons();
    }

    public void SwitchToElementMode()
    {
        CurrentMode = PlayerInputMode.Elements;
        hasActiveElementTool = false;
        DestroyPreview();
        currentPrefabToPlace = null;
        RefreshToolbarButtons();
    }

    public void SwitchToBuildMode(BuildTool tool)
    {
        CurrentBuildTool = tool;
        CurrentMode = PlayerInputMode.Build;
        hasActiveBuildTool = true;
        CreatePreviewForTool(tool);
        RefreshToolbarButtons();
    }

    public void SwitchToBuildMode()
    {
        CurrentMode = PlayerInputMode.Build;
        hasActiveBuildTool = false;
        DestroyPreview();
        currentPrefabToPlace = null;
        RefreshToolbarButtons();
    }

    public void ToggleElementTool(ElementPaintTool tool)
    {
        if (CurrentMode == PlayerInputMode.Elements && hasActiveElementTool && CurrentElementTool == tool)
        {
            hasActiveElementTool = false;
            DestroyPreview();
            RefreshToolbarButtons();
            return;
        }

        SwitchToElementMode(tool);
    }

    public void ToggleBuildTool(BuildTool tool)
    {
        if (CurrentMode == PlayerInputMode.Build && hasActiveBuildTool && CurrentBuildTool == tool)
        {
            ClearActiveSelection();
            return;
        }

        SwitchToBuildMode(tool);
        SetBuildExpanded(false);
    }

    void ClearActiveSelection(bool refresh = true)
    {
        hasActiveBuildTool = false;
        hasActiveElementTool = false;
        DestroyPreview();
        currentPrefabToPlace = null;

        if (refresh)
            RefreshToolbarButtons();
    }

    public MaterialType GetSelectedMaterialType()
    {
        switch (CurrentElementTool)
        {
            case ElementPaintTool.Water:
                return MaterialType.Water;
            case ElementPaintTool.Wood:
                return MaterialType.Wood;
            default:
                return MaterialType.Fire;
        }
    }

    void CreatePreviewForTool(BuildTool tool)
    {
        GameObject prefab = GetPrefabForTool(tool);
        if (prefab == null)
        {
            DestroyPreview();
            return;
        }

        DestroyPreview();
        currentPrefabToPlace = prefab;
        currentRotationZ = 0f;

        previewInstance = Instantiate(prefab);
        previewInstance.SetActive(true);
        previewInstance.name = prefab.name + "_Preview";
        previewInstance.transform.rotation = Quaternion.Euler(0f, 0f, currentRotationZ);
        DisablePreviewPhysicsAndScripts(previewInstance);
        previewSpriteRenderers = previewInstance.GetComponentsInChildren<SpriteRenderer>();
    }

    void DestroyPreview()
    {
        if (previewInstance != null)
            Destroy(previewInstance);

        previewInstance = null;
        previewSpriteRenderers = null;

        if (CurrentMode != PlayerInputMode.Build)
            currentPrefabToPlace = null;
    }

    GameObject GetPrefabForTool(BuildTool tool)
    {
        switch (tool)
        {
            case BuildTool.Axle: return axlePrefab;
            case BuildTool.SlimeBlock: return slimeBlockPrefab;
            case BuildTool.WoodPlank: return woodPlankPrefab;
            case BuildTool.WoodBarrel: return woodBarrelPrefab;
            case BuildTool.IronBlock: return ironBlockPrefab;
            case BuildTool.StoneBlock: return stoneBlockPrefab;
            case BuildTool.Spike: return spikePrefab;
            case BuildTool.Flag: return flagPrefab;
            case BuildTool.BalanceScale: return balanceScalePrefab != null ? balanceScalePrefab : GetRuntimeBalanceScalePrefab();
            default: return null;
        }
    }

    GameObject GetRuntimeBalanceScalePrefab()
    {
        if (runtimeBalanceScalePrefab == null)
            runtimeBalanceScalePrefab = BalanceScale.CreateRuntimePrefab();

        return runtimeBalanceScalePrefab;
    }

    Vector3 GetSnappedPosition(Vector3 worldPos)
    {
        worldPos.x = Mathf.Round(worldPos.x / snapSize) * snapSize;
        worldPos.y = Mathf.Round(worldPos.y / snapSize) * snapSize;
        worldPos.z = 0f;
        return worldPos;
    }

    bool TryGetPlacementPosition(GameObject prefab, Vector3 desiredPosition, float rotationZ, out Vector3 finalPosition, out float finalRotationZ, out bool willAttach)
    {
        finalPosition = desiredPosition;
        finalRotationZ = rotationZ;
        willAttach = false;

        GameObject attachmentSource = previewInstance != null ? previewInstance : prefab;

        if (TryHandleStickyPlacement(attachmentSource, prefab, desiredPosition, rotationZ, out finalPosition, out Collider2D stickyTarget, out bool stickyAttach))
        {
            willAttach = stickyAttach;
            return stickyAttach ? CanPlaceAt(prefab, finalPosition, rotationZ, stickyTarget) : CanPlaceAt(prefab, desiredPosition, rotationZ, null);
        }

        SpikeTrap spikeTrap = attachmentSource.GetComponent<SpikeTrap>();
        if (spikeTrap != null)
        {
            spikeTrap.attachSearchRadius = attachSearchRadius;
            spikeTrap.attachmentLayer = attachmentLayer;

            if (!TryGetValidAttachPlacement(spikeTrap, prefab, desiredPosition, out finalPosition, out finalRotationZ, out Collider2D spikeTarget))
                return false;

            willAttach = true;
            return CanPlaceAt(prefab, finalPosition, finalRotationZ, spikeTarget);
        }

        FlagGoal flagGoal = attachmentSource.GetComponent<FlagGoal>();
        if (flagGoal != null)
        {
            flagGoal.attachSearchRadius = attachSearchRadius;
            flagGoal.attachmentLayer = attachmentLayer;

            if (!TryGetValidAttachPlacement(flagGoal, prefab, desiredPosition, out finalPosition, out finalRotationZ, out Collider2D flagTarget))
                return false;

            willAttach = true;
            return CanPlaceAt(prefab, finalPosition, finalRotationZ, flagTarget);
        }

        return CanPlaceAt(prefab, desiredPosition, rotationZ, null);
    }

    bool TryHandleStickyPlacement(GameObject attachmentSource, GameObject prefab, Vector3 desiredPosition, float rotationZ, out Vector3 finalPosition, out Collider2D target, out bool willAttach)
    {
        finalPosition = desiredPosition;
        target = null;
        willAttach = false;

        StickyBlock sticky = attachmentSource.GetComponent<StickyBlock>();
        if (sticky != null)
        {
            sticky.attachSearchRadius = attachSearchRadius;
            sticky.attachmentLayer = attachmentLayer;

            if (!TryGetValidAttachPlacement(sticky, prefab, desiredPosition, rotationZ, out finalPosition, out target))
                return false;

            willAttach = true;
            return true;
        }

        WoodPlankBlock plank = attachmentSource.GetComponent<WoodPlankBlock>();
        if (plank != null)
        {
            plank.attachSearchRadius = attachSearchRadius;
            plank.attachmentLayer = attachmentLayer;

            if (!TryGetValidAttachPlacement(plank, prefab, desiredPosition, rotationZ, out finalPosition, out target))
                return false;

            willAttach = true;
            return true;
        }

        StickyAttachBlock attachBlock = attachmentSource.GetComponent<StickyAttachBlock>();
        if (attachBlock != null)
        {
            attachBlock.attachSearchRadius = attachSearchRadius;
            attachBlock.attachmentLayer = attachmentLayer;

            if (!TryGetValidAttachPlacement(attachBlock, prefab, desiredPosition, rotationZ, out finalPosition, out target))
                return false;

            willAttach = true;
            return true;
        }

        return false;
    }

    bool TryGetValidAttachPlacement(StickyBlock sticky, GameObject prefab, Vector3 desiredPosition, float rotationZ, out Vector3 finalPosition, out Collider2D allowedAttachmentTarget)
    {
        finalPosition = desiredPosition;
        allowedAttachmentTarget = null;
        Collider2D[] targets = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            Collider2D target = targets[i];
            if (target == null)
                continue;
            if (target.GetComponent<RotatingAxle>() == null && target.GetComponent<StickyBlock>() == null)
                continue;

            List<Vector3> candidatePositions = sticky.GetCandidatePositionsForTarget(desiredPosition, target);
            for (int j = 0; j < candidatePositions.Count; j++)
            {
                Vector3 candidate = candidatePositions[j];
                if (!CanPlaceAt(prefab, candidate, rotationZ, target))
                    continue;

                float distance = Vector2.Distance(desiredPosition, candidate);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    finalPosition = candidate;
                    allowedAttachmentTarget = target;
                }
            }
        }

        return found;
    }

    bool TryGetValidAttachPlacement(WoodPlankBlock plank, GameObject prefab, Vector3 desiredPosition, float rotationZ, out Vector3 finalPosition, out Collider2D allowedAttachmentTarget)
    {
        finalPosition = desiredPosition;
        allowedAttachmentTarget = null;
        Collider2D[] targets = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            Collider2D target = targets[i];
            if (target == null || target.GetComponent<StickyBlock>() == null)
                continue;

            List<Vector3> candidatePositions = plank.GetCandidatePositionsForTarget(desiredPosition, target);
            for (int j = 0; j < candidatePositions.Count; j++)
            {
                Vector3 candidate = candidatePositions[j];
                if (!CanPlaceAt(prefab, candidate, rotationZ, target))
                    continue;

                float distance = Vector2.Distance(desiredPosition, candidate);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    finalPosition = candidate;
                    allowedAttachmentTarget = target;
                }
            }
        }

        return found;
    }

    bool TryGetValidAttachPlacement(StickyAttachBlock attachBlock, GameObject prefab, Vector3 desiredPosition, float rotationZ, out Vector3 finalPosition, out Collider2D allowedAttachmentTarget)
    {
        finalPosition = desiredPosition;
        allowedAttachmentTarget = null;
        Collider2D[] targets = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            Collider2D target = targets[i];
            if (target == null || target.GetComponent<StickyBlock>() == null)
                continue;

            List<Vector3> candidatePositions = attachBlock.GetCandidatePositionsForTarget(desiredPosition, target);
            for (int j = 0; j < candidatePositions.Count; j++)
            {
                Vector3 candidate = candidatePositions[j];
                if (!CanPlaceAt(prefab, candidate, rotationZ, target))
                    continue;

                float distance = Vector2.Distance(desiredPosition, candidate);
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    finalPosition = candidate;
                    allowedAttachmentTarget = target;
                }
            }
        }

        return found;
    }

    bool TryGetValidAttachPlacement(SpikeTrap spikeTrap, GameObject prefab, Vector3 desiredPosition, out Vector3 finalPosition, out float finalRotationZ, out Collider2D allowedAttachmentTarget)
    {
        finalPosition = desiredPosition;
        finalRotationZ = 0f;
        allowedAttachmentTarget = null;
        Collider2D[] targets = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            Collider2D target = targets[i];
            if (!spikeTrap.CanAttachToCollider(target))
                continue;

            if (!spikeTrap.TryGetNearestFacePose(desiredPosition, target, out SpikeTrap.AttachmentPose candidatePose))
                continue;

            if (!spikeTrap.IsFaceAvailable(target, candidatePose))
                continue;

            Vector3 candidate = candidatePose.position;
            float candidateRotationZ = candidatePose.rotationZ;
            if (!CanPlaceAt(prefab, candidate, candidateRotationZ, target))
                continue;

            float distance = Vector2.Distance(desiredPosition, candidate);
            if (!found || distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                finalPosition = candidate;
                finalRotationZ = candidateRotationZ;
                allowedAttachmentTarget = target;
            }
        }

        return found;
    }

    bool TryGetValidAttachPlacement(FlagGoal flagGoal, GameObject prefab, Vector3 desiredPosition, out Vector3 finalPosition, out float finalRotationZ, out Collider2D allowedAttachmentTarget)
    {
        finalPosition = desiredPosition;
        finalRotationZ = 0f;
        allowedAttachmentTarget = null;
        Collider2D[] targets = Physics2D.OverlapCircleAll(desiredPosition, attachSearchRadius, attachmentLayer);
        bool found = false;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < targets.Length; i++)
        {
            Collider2D target = targets[i];
            if (!flagGoal.CanAttachToCollider(target))
                continue;

            if (!flagGoal.TryGetNearestFacePose(desiredPosition, target, out SpikeTrap.AttachmentPose candidatePose))
                continue;

            if (!flagGoal.IsFaceAvailable(target, candidatePose))
                continue;

            Vector3 candidate = candidatePose.position;
            float candidateRotationZ = candidatePose.rotationZ;
            if (!CanPlaceAt(prefab, candidate, candidateRotationZ, target))
                continue;

            float distance = Vector2.Distance(desiredPosition, candidate);
            if (!found || distance < bestDistance)
            {
                found = true;
                bestDistance = distance;
                finalPosition = candidate;
                finalRotationZ = candidateRotationZ;
                allowedAttachmentTarget = target;
            }
        }

        return found;
    }

    bool CanPlaceAt(GameObject prefab, Vector3 position, float rotationZ, Collider2D allowedAttachmentTarget)
    {
        Vector2 checkSize = GetPrefabCheckSize(prefab);
        TerrainTilemapSurface allowedTerrain = allowedAttachmentTarget != null
            ? allowedAttachmentTarget.GetComponentInParent<TerrainTilemapSurface>()
            : null;

        Collider2D[] hits = Physics2D.OverlapBoxAll(position, checkSize, rotationZ, placementBlockLayer);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;
            if (allowedAttachmentTarget != null && hit == allowedAttachmentTarget)
                continue;
            if (allowedTerrain != null && hit.GetComponentInParent<TerrainTilemapSurface>() == allowedTerrain)
                continue;
            return false;
        }
        return true;
    }

    Vector2 GetPrefabCheckSize(GameObject prefab)
    {
        return PlacementSizeUtility.GetPlacementCheckSize(prefab);
    }

    void DisablePreviewPhysicsAndScripts(GameObject preview)
    {
        foreach (Collider2D col in preview.GetComponentsInChildren<Collider2D>())
            col.enabled = false;
        foreach (Rigidbody2D rb in preview.GetComponentsInChildren<Rigidbody2D>())
            rb.simulated = false;

        DisableIfExists<RotatingAxle>(preview);
        DisableIfExists<StickyBlock>(preview);
        DisableIfExists<WoodPlankBlock>(preview);
        DisableIfExists<StickyAttachBlock>(preview);
        DisableIfExists<SpikeTrap>(preview);
        DisableIfExists<FlagGoal>(preview);
        DisableIfExists<GridObjectBinder>(preview);
        DisableIfExists<BurnableObject>(preview);
        DisableIfExists<FloatOnWater>(preview);
        DisableIfExists<WaterBarrel>(preview);
        DisableIfExists<HeatConductor>(preview);
        DisableIfExists<PhysicalWeight>(preview);
        DisableIfExists<BalanceScale>(preview);
    }

    void DisableIfExists<T>(GameObject preview) where T : Behaviour
    {
        T behaviour = preview.GetComponent<T>();
        if (behaviour != null)
            behaviour.enabled = false;
    }

    void EnsureRuntimeToolbar()
    {
        EnsureEventSystem();
        toolbarCanvas = FindObjectOfType<Canvas>();
        toolbarRoot = FindRectTransformByName("toolbar");
        toolbarBackground = FindGameObjectByName("background");

        buildModeButton = FindButtonByName(BuildModeButtonNames);
        elementModeButton = FindButtonByName(ElementModeButtonNames);
        toggleToolbarButton = FindButtonByName("隐藏");
        buildExpandButton = FindButtonByName(BuildExpandButtonNames);

        modeButtons.Clear();
        if (buildModeButton != null) modeButtons.Add(buildModeButton);
        if (elementModeButton != null) modeButtons.Add(elementModeButton);

        elementToolButtons.Clear();
        AddButtonIfFound(elementToolButtons, "BtnFire", "火");
        AddButtonIfFound(elementToolButtons, "BtnWater", "水");
        AddButtonIfFound(elementToolButtons, "BtnWood", "木");

        buildToolButtons.Clear();
        AddButtonIfFound(buildToolButtons, "BtnAxle", "转轴");
        AddButtonIfFound(buildToolButtons, "BtnSlimeBlock", "粘液块");
        AddButtonIfFound(buildToolButtons, "BtnWoodPlank", "木板");
        AddButtonIfFound(buildToolButtons, "BtnWoodBarrel", "木桶");
        AddButtonIfFound(buildToolButtons, "BtnIronBlock", "铁块");
        AddButtonIfFound(buildToolButtons, "BtnStoneBlock", "石头");
        AddButtonIfFound(buildToolButtons, "BtnSpike", "尖刺");
        AddButtonIfFound(buildToolButtons, "BtnFlag", "旗帜", "Flag", "flag", "终点旗");

        expandedBuildToolButtons.Clear();
        AddButtonIfFound(expandedBuildToolButtons, BalanceScaleButtonNames);

        collapsibleUiObjects.Clear();
        AddUiObjectIfFound(toolbarBackground);
        AddUiObjectIfFound(buildModeButton != null ? buildModeButton.gameObject : null);
        AddUiObjectIfFound(elementModeButton != null ? elementModeButton.gameObject : null);
        for (int i = 0; i < elementToolButtons.Count; i++)
            AddUiObjectIfFound(elementToolButtons[i].gameObject);
        for (int i = 0; i < buildToolButtons.Count; i++)
            AddUiObjectIfFound(buildToolButtons[i].gameObject);
        for (int i = 0; i < expandedBuildToolButtons.Count; i++)
            AddUiObjectIfFound(expandedBuildToolButtons[i].gameObject);
        AddUiObjectIfFound(buildExpandButton != null ? buildExpandButton.gameObject : null);

        BindButton(buildModeButton, () => SwitchToBuildMode());
        BindButton(elementModeButton, () => SwitchToElementMode());
        BindButton(toggleToolbarButton, () =>
        {
            ClearActiveSelection(false);
            SetToolbarExpanded(!toolbarExpanded);
        });
        BindButton(buildExpandButton, () => SetBuildExpanded(!buildExpanded));

        BindButton(FindButtonByName("BtnFire", "火"), () => ToggleElementTool(ElementPaintTool.Fire));
        BindButton(FindButtonByName("BtnWater", "水"), () => ToggleElementTool(ElementPaintTool.Water));
        BindButton(FindButtonByName("BtnWood", "木"), () => ToggleElementTool(ElementPaintTool.Wood));

        BindButton(FindButtonByName("BtnAxle", "转轴"), () => ToggleBuildTool(BuildTool.Axle));
        BindButton(FindButtonByName("BtnSlimeBlock", "粘液块"), () => ToggleBuildTool(BuildTool.SlimeBlock));
        BindButton(FindButtonByName("BtnWoodPlank", "木板"), () => ToggleBuildTool(BuildTool.WoodPlank));
        BindButton(FindButtonByName("BtnWoodBarrel", "木桶"), () => ToggleBuildTool(BuildTool.WoodBarrel));
        BindButton(FindButtonByName("BtnIronBlock", "铁块"), () => ToggleBuildTool(BuildTool.IronBlock));
        BindButton(FindButtonByName("BtnStoneBlock", "石头"), () => ToggleBuildTool(BuildTool.StoneBlock));
        BindButton(FindButtonByName("BtnSpike", "尖刺"), () => ToggleBuildTool(BuildTool.Spike));
        BindButton(FindButtonByName("BtnFlag", "旗帜", "Flag", "flag", "终点旗"), () => ToggleBuildTool(BuildTool.Flag));
        BindButton(FindButtonByName(BalanceScaleButtonNames), () => ToggleBuildTool(BuildTool.BalanceScale));

        SetToolbarExpanded(toolbarExpanded);
        SetBuildExpanded(false, false);
        RefreshToolbarButtons();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    void SetToolbarExpanded(bool expanded)
    {
        toolbarExpanded = expanded;

        for (int i = 0; i < collapsibleUiObjects.Count; i++)
        {
            if (collapsibleUiObjects[i] != null)
                collapsibleUiObjects[i].SetActive(expanded);
        }

        if (toggleToolbarButton != null)
            SetButtonText(toggleToolbarButton, expanded ? "隐藏" : "展开");

        RefreshToolbarButtons();
    }

    void SetBuildExpanded(bool expanded, bool restoreLevelPanel = true)
    {
        if (buildExpanded == expanded)
        {
            RefreshToolbarButtons();
            return;
        }

        buildExpanded = expanded;

        LevelManager levelManager = LevelManager.Instance;
        if (expanded)
        {
            restoreLevelPanelAfterBuildExpand = levelManager != null && levelManager.IsLevelPanelVisible;
            if (levelManager != null)
                levelManager.SetLevelPanelVisible(false);
        }
        else if (restoreLevelPanel && restoreLevelPanelAfterBuildExpand)
        {
            if (levelManager != null)
                levelManager.SetLevelPanelVisible(true);

            restoreLevelPanelAfterBuildExpand = false;
        }
        else if (!expanded)
        {
            restoreLevelPanelAfterBuildExpand = false;
        }

        RefreshToolbarButtons();
    }

    void RefreshToolbarButtons()
    {
        UpdateModeButtonVisuals();

        bool showElements = CurrentMode == PlayerInputMode.Elements;
        for (int i = 0; i < elementToolButtons.Count; i++)
        {
            if (elementToolButtons[i] != null)
                elementToolButtons[i].gameObject.SetActive(toolbarExpanded && showElements);
        }

        for (int i = 0; i < buildToolButtons.Count; i++)
        {
            if (buildToolButtons[i] != null)
                buildToolButtons[i].gameObject.SetActive(toolbarExpanded && !showElements);
        }

        bool showExpandedBuildTools = toolbarExpanded && !showElements && buildExpanded;
        for (int i = 0; i < expandedBuildToolButtons.Count; i++)
        {
            if (expandedBuildToolButtons[i] != null)
                expandedBuildToolButtons[i].gameObject.SetActive(showExpandedBuildTools);
        }

        if (buildExpandButton != null)
        {
            buildExpandButton.gameObject.SetActive(toolbarExpanded && !showElements);
            SetButtonText(buildExpandButton, buildExpanded ? "收回" : "扩展");
        }
    }

    bool IsPointerOverBlockingUiControl()
    {
        if (EventSystem.current == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, uiRaycastResults);
        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObject = uiRaycastResults[i].gameObject;
            if (hitObject == null)
                continue;

            Selectable selectable = hitObject.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
                return true;
        }

        return false;
    }

    void UpdateModeButtonVisuals()
    {
        // Button visuals are controlled in the Unity scene.
    }

    RectTransform FindRectTransformByName(string objectName)
    {
        GameObject obj = FindGameObjectByName(objectName);
        return obj != null ? obj.GetComponent<RectTransform>() : null;
    }

    Button FindButtonByName(string objectName)
    {
        GameObject obj = FindGameObjectByName(objectName);
        return obj != null ? obj.GetComponent<Button>() : null;
    }

    Button FindButtonByName(params string[] objectNames)
    {
        for (int i = 0; i < objectNames.Length; i++)
        {
            Button button = FindButtonByName(objectNames[i]);
            if (button != null)
                return button;
        }

        return null;
    }

    GameObject FindGameObjectByName(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null || t.hideFlags != HideFlags.None)
                continue;
            if (!t.gameObject.scene.IsValid())
                continue;
            if (t.name.Trim() == objectName)
                return t.gameObject;
        }

        return null;
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            ignorePlacementThroughFrame = Time.frameCount + 1;
            action.Invoke();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        });
    }

    void AddButtonIfFound(List<Button> list, string objectName)
    {
        Button button = FindButtonByName(objectName);
        if (button != null)
            list.Add(button);
    }

    void AddButtonIfFound(List<Button> list, params string[] objectNames)
    {
        Button button = FindButtonByName(objectNames);
        if (button != null)
            list.Add(button);
    }

    void AddUiObjectIfFound(GameObject obj)
    {
        if (obj != null && !collapsibleUiObjects.Contains(obj))
            collapsibleUiObjects.Add(obj);
    }

    void ApplyButtonVisual(Button button, bool selected)
    {
        // Button visuals are authored in the scene and are not overridden at runtime.
    }

    void SetButtonText(Button button, string text)
    {
        if (button == null)
            return;


        Text label = button.GetComponentInChildren<Text>(true);
        if (label != null)
            label.text = text;
    }
}
