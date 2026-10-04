#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine.Tilemaps;

public static class LobbyUIAutoBuilder
{
    [MenuItem("Tools/1. Setup Lobby UI in Scene")]
    public static void BuildLobbyUI()
    {
        // 1. Ensure EventSystem exists
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 2. Find or Create Canvas
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        }

        // Configure CanvasScaler
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Resources
        TMP_DefaultControls.Resources tmpResources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
        };

        // Clean up old panels if they exist
        Transform oldBrowser = canvas.transform.Find("LobbyBrowserPanel");
        if (oldBrowser != null) Object.DestroyImmediate(oldBrowser.gameObject);

        Transform oldInside = canvas.transform.Find("InsideRoomPanel");
        if (oldInside != null) Object.DestroyImmediate(oldInside.gameObject);

        // Remove loose Card Lobby in scene root if present
        Transform looseCard = canvas.transform.Find("Card Lobby");
        if (looseCard != null) Object.DestroyImmediate(looseCard.gameObject);

        // ==========================================
        // 3. CREATE LOBBY BROWSER PANEL
        // ==========================================
        GameObject browserPanel = new GameObject("LobbyBrowserPanel");
        browserPanel.transform.SetParent(canvas.transform, false);
        RectTransform bpRect = browserPanel.AddComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0.5f, 0.5f);
        bpRect.anchorMax = new Vector2(0.5f, 0.5f);
        bpRect.pivot = new Vector2(0.5f, 0.5f);
        bpRect.sizeDelta = new Vector2(1050, 680);

        Image bpImage = browserPanel.AddComponent<Image>();
        bpImage.sprite = tmpResources.background;
        bpImage.type = Image.Type.Sliced;
        bpImage.color = new Color(0.12f, 0.14f, 0.20f, 0.95f);

        // Header
        GameObject headerGo = TMP_DefaultControls.CreateText(tmpResources);
        headerGo.name = "HeaderTitle";
        headerGo.transform.SetParent(browserPanel.transform, false);
        RectTransform headerRect = headerGo.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(1, 1);
        headerRect.pivot = new Vector2(0.5f, 1);
        headerRect.anchoredPosition = new Vector2(0, -15);
        headerRect.sizeDelta = new Vector2(0, 50);
        TextMeshProUGUI headerText = headerGo.GetComponent<TextMeshProUGUI>();
        headerText.text = "LOBBY BROWSER";
        headerText.fontSize = 32;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;

        // Refresh Button
        GameObject refreshBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        refreshBtnGo.name = "RefreshButton";
        refreshBtnGo.transform.SetParent(browserPanel.transform, false);
        RectTransform refreshRect = refreshBtnGo.GetComponent<RectTransform>();
        refreshRect.anchorMin = new Vector2(1, 1);
        refreshRect.anchorMax = new Vector2(1, 1);
        refreshRect.pivot = new Vector2(1, 1);
        refreshRect.anchoredPosition = new Vector2(-40, -22);
        refreshRect.sizeDelta = new Vector2(130, 40);
        Button refreshButton = refreshBtnGo.GetComponent<Button>();
        refreshBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Refresh";

        // Scroll View for Lobby List
        GameObject scrollGo = new GameObject("LobbyScrollView");
        scrollGo.transform.SetParent(browserPanel.transform, false);
        RectTransform scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0);
        scrollRectTransform.anchorMax = new Vector2(0, 1);
        scrollRectTransform.pivot = new Vector2(0, 0.5f);
        scrollRectTransform.anchoredPosition = new Vector2(40, -35);
        scrollRectTransform.sizeDelta = new Vector2(560, 560);

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.sprite = tmpResources.inputField;
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = new Color(0.08f, 0.10f, 0.14f, 0.85f);

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Viewport
        GameObject viewportGo = new GameObject("Viewport");
        viewportGo.transform.SetParent(scrollGo.transform, false);
        RectTransform vpRect = viewportGo.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = new Vector2(-10, -10);
        vpRect.anchoredPosition = Vector2.zero;
        viewportGo.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        // Content
        GameObject contentGo = new GameObject("Content");
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.sizeDelta = new Vector2(0, 0);

        VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(8, 8, 8, 8);
        vlg.spacing = 10;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        // ==========================================
        // SIDE CONTROLS (Right Side)
        // ==========================================
        GameObject sidePanel = new GameObject("SideControls");
        sidePanel.transform.SetParent(browserPanel.transform, false);
        RectTransform sideRect = sidePanel.AddComponent<RectTransform>();
        sideRect.anchorMin = new Vector2(1, 0.5f);
        sideRect.anchorMax = new Vector2(1, 0.5f);
        sideRect.pivot = new Vector2(1, 0.5f);
        sideRect.anchoredPosition = new Vector2(-40, -35);
        sideRect.sizeDelta = new Vector2(380, 560);

        // --- Create Room Section ---
        GameObject createTitleGo = TMP_DefaultControls.CreateText(tmpResources);
        createTitleGo.name = "CreateTitle";
        createTitleGo.transform.SetParent(sidePanel.transform, false);
        RectTransform ctRect = createTitleGo.GetComponent<RectTransform>();
        ctRect.anchoredPosition = new Vector2(0, 230);
        ctRect.sizeDelta = new Vector2(360, 30);
        TextMeshProUGUI ctText = createTitleGo.GetComponent<TextMeshProUGUI>();
        ctText.text = "Host a New Lobby";
        ctText.fontSize = 20;
        ctText.fontStyle = FontStyles.Bold;
        ctText.alignment = TextAlignmentOptions.Left;

        // Room Name Input
        GameObject roomInputGo = TMP_DefaultControls.CreateInputField(tmpResources);
        roomInputGo.name = "RoomNameInput";
        roomInputGo.transform.SetParent(sidePanel.transform, false);
        RectTransform riRect = roomInputGo.GetComponent<RectTransform>();
        riRect.anchoredPosition = new Vector2(0, 180);
        riRect.sizeDelta = new Vector2(360, 44);
        TMP_InputField roomNameInput = roomInputGo.GetComponent<TMP_InputField>();
        ((TextMeshProUGUI)roomNameInput.placeholder).text = "Enter Room Name...";

        // Private Toggle
        GameObject toggleGo = new GameObject("PrivateToggle");
        toggleGo.transform.SetParent(sidePanel.transform, false);
        RectTransform toggleRect = toggleGo.AddComponent<RectTransform>();
        toggleRect.anchoredPosition = new Vector2(-110, 130);
        toggleRect.sizeDelta = new Vector2(140, 30);
        Toggle privToggle = toggleGo.AddComponent<Toggle>();

        GameObject bgToggle = new GameObject("Background");
        bgToggle.transform.SetParent(toggleGo.transform, false);
        RectTransform bgtRect = bgToggle.AddComponent<RectTransform>();
        bgtRect.anchorMin = new Vector2(0, 0.5f);
        bgtRect.anchorMax = new Vector2(0, 0.5f);
        bgtRect.pivot = new Vector2(0, 0.5f);
        bgtRect.sizeDelta = new Vector2(24, 24);
        Image bgtImage = bgToggle.AddComponent<Image>();
        bgtImage.sprite = tmpResources.inputField;
        bgtImage.type = Image.Type.Sliced;

        GameObject checkGo = new GameObject("Checkmark");
        checkGo.transform.SetParent(bgToggle.transform, false);
        RectTransform ckRect = checkGo.AddComponent<RectTransform>();
        ckRect.anchorMin = Vector2.zero;
        ckRect.anchorMax = Vector2.one;
        ckRect.sizeDelta = new Vector2(-4, -4);
        Image ckImage = checkGo.AddComponent<Image>();
        ckImage.sprite = tmpResources.checkmark;
        privToggle.graphic = ckImage;
        privToggle.targetGraphic = bgtImage;

        GameObject toggleLabel = TMP_DefaultControls.CreateText(tmpResources);
        toggleLabel.transform.SetParent(toggleGo.transform, false);
        RectTransform tlRect = toggleLabel.GetComponent<RectTransform>();
        tlRect.anchorMin = new Vector2(0, 0.5f);
        tlRect.anchorMax = new Vector2(1, 0.5f);
        tlRect.anchoredPosition = new Vector2(40, 0);
        tlRect.sizeDelta = new Vector2(100, 24);
        TextMeshProUGUI tlText = toggleLabel.GetComponent<TextMeshProUGUI>();
        tlText.text = "Private Room";
        tlText.fontSize = 16;
        tlText.alignment = TextAlignmentOptions.MidlineLeft;

        // Create Room Button
        GameObject createBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        createBtnGo.name = "CreateRoomButton";
        createBtnGo.transform.SetParent(sidePanel.transform, false);
        RectTransform cbRect = createBtnGo.GetComponent<RectTransform>();
        cbRect.anchoredPosition = new Vector2(0, 75);
        cbRect.sizeDelta = new Vector2(360, 48);
        Button createRoomButton = createBtnGo.GetComponent<Button>();
        createBtnGo.GetComponent<Image>().color = new Color(0.18f, 0.55f, 0.32f, 1f);
        createBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Create Room";

        // --- Join with Code Section ---
        GameObject joinTitleGo = TMP_DefaultControls.CreateText(tmpResources);
        joinTitleGo.name = "JoinCodeTitle";
        joinTitleGo.transform.SetParent(sidePanel.transform, false);
        RectTransform jtRect = joinTitleGo.GetComponent<RectTransform>();
        jtRect.anchoredPosition = new Vector2(0, -30);
        jtRect.sizeDelta = new Vector2(360, 30);
        TextMeshProUGUI jtText = joinTitleGo.GetComponent<TextMeshProUGUI>();
        jtText.text = "Join Private Lobby";
        jtText.fontSize = 20;
        jtText.fontStyle = FontStyles.Bold;
        jtText.alignment = TextAlignmentOptions.Left;

        // Join Code Input
        GameObject joinInputGo = TMP_DefaultControls.CreateInputField(tmpResources);
        joinInputGo.name = "JoinCodeInput";
        joinInputGo.transform.SetParent(sidePanel.transform, false);
        RectTransform jiRect = joinInputGo.GetComponent<RectTransform>();
        jiRect.anchoredPosition = new Vector2(0, -80);
        jiRect.sizeDelta = new Vector2(360, 44);
        TMP_InputField joinCodeInput = joinInputGo.GetComponent<TMP_InputField>();
        ((TextMeshProUGUI)joinCodeInput.placeholder).text = "Enter Lobby Code...";

        // Join Button
        GameObject joinBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        joinBtnGo.name = "JoinByCodeButton";
        joinBtnGo.transform.SetParent(sidePanel.transform, false);
        RectTransform jbRect = joinBtnGo.GetComponent<RectTransform>();
        jbRect.anchoredPosition = new Vector2(0, -145);
        jbRect.sizeDelta = new Vector2(360, 48);
        Button joinByCodeButton = joinBtnGo.GetComponent<Button>();
        joinBtnGo.GetComponent<Image>().color = new Color(0.22f, 0.45f, 0.78f, 1f);
        joinBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Join by Code";

        // ==========================================
        // 4. CREATE INSIDE ROOM PANEL
        // ==========================================
        GameObject insidePanel = new GameObject("InsideRoomPanel");
        insidePanel.transform.SetParent(canvas.transform, false);
        RectTransform ipRect = insidePanel.AddComponent<RectTransform>();
        ipRect.anchorMin = new Vector2(0.5f, 0.5f);
        ipRect.anchorMax = new Vector2(0.5f, 0.5f);
        ipRect.pivot = new Vector2(0.5f, 0.5f);
        ipRect.sizeDelta = new Vector2(700, 450);

        Image ipImage = insidePanel.AddComponent<Image>();
        ipImage.sprite = tmpResources.background;
        ipImage.type = Image.Type.Sliced;
        ipImage.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);

        // Room Title
        GameObject roomTitleGo = TMP_DefaultControls.CreateText(tmpResources);
        roomTitleGo.name = "RoomTitleText";
        roomTitleGo.transform.SetParent(insidePanel.transform, false);
        RectTransform rtRect = roomTitleGo.GetComponent<RectTransform>();
        rtRect.anchoredPosition = new Vector2(0, 140);
        rtRect.sizeDelta = new Vector2(600, 50);
        TextMeshProUGUI roomTitleText = roomTitleGo.GetComponent<TextMeshProUGUI>();
        roomTitleText.text = "Room Name";
        roomTitleText.fontSize = 28;
        roomTitleText.fontStyle = FontStyles.Bold;
        roomTitleText.alignment = TextAlignmentOptions.Center;

        // Room Code
        GameObject codeGo = TMP_DefaultControls.CreateText(tmpResources);
        codeGo.name = "LobbyCodeText";
        codeGo.transform.SetParent(insidePanel.transform, false);
        RectTransform codeRect = codeGo.GetComponent<RectTransform>();
        codeRect.anchoredPosition = new Vector2(0, 80);
        codeRect.sizeDelta = new Vector2(600, 40);
        TextMeshProUGUI lobbyCodeText = codeGo.GetComponent<TextMeshProUGUI>();
        lobbyCodeText.text = "Room Code: ------";
        lobbyCodeText.fontSize = 22;
        lobbyCodeText.color = new Color(1f, 0.85f, 0.3f, 1f);
        lobbyCodeText.alignment = TextAlignmentOptions.Center;

        // Start Game Button
        GameObject startBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        startBtnGo.name = "StartGameButton";
        startBtnGo.transform.SetParent(insidePanel.transform, false);
        RectTransform sgRect = startBtnGo.GetComponent<RectTransform>();
        sgRect.anchoredPosition = new Vector2(0, -30);
        sgRect.sizeDelta = new Vector2(240, 54);
        Button startGameButton = startBtnGo.GetComponent<Button>();
        startBtnGo.GetComponent<Image>().color = new Color(0.18f, 0.65f, 0.35f, 1f);
        TextMeshProUGUI startText = startBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        startText.text = "START GAME";
        startText.fontStyle = FontStyles.Bold;

        // Leave Room Button
        GameObject leaveBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        leaveBtnGo.name = "LeaveRoomButton";
        leaveBtnGo.transform.SetParent(insidePanel.transform, false);
        RectTransform lrRect = leaveBtnGo.GetComponent<RectTransform>();
        lrRect.anchoredPosition = new Vector2(0, -110);
        lrRect.sizeDelta = new Vector2(200, 42);
        Button leaveRoomButton = leaveBtnGo.GetComponent<Button>();
        leaveBtnGo.GetComponent<Image>().color = new Color(0.75f, 0.25f, 0.25f, 1f);
        leaveBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Leave Room";

        insidePanel.SetActive(false);

        // ==========================================
        // 5. ATTACH & AUTO-WIRE LOBBYUI COMPONENT
        // ==========================================
        LobbyUI lobbyUI = canvas.GetComponent<LobbyUI>();
        if (lobbyUI == null)
            lobbyUI = canvas.gameObject.AddComponent<LobbyUI>();

        // Load Card Lobby Prefab
        LobbyItemCard cardPrefab = AssetDatabase.LoadAssetAtPath<LobbyItemCard>("Assets/Prefabs/Card Lobby.prefab");
        if (cardPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("Card Lobby t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cardPrefab = AssetDatabase.LoadAssetAtPath<LobbyItemCard>(path);
            }
        }

        lobbyUI.SetEditorReferences(
            browserPanel,
            insidePanel,
            roomNameInput,
            privToggle,
            createRoomButton,
            joinCodeInput,
            joinByCodeButton,
            contentGo.transform,
            cardPrefab,
            refreshButton,
            roomTitleText,
            lobbyCodeText,
            startGameButton,
            leaveRoomButton
        );

        // Disable PlayerSpawner in Lobby scene so players don't spawn over the menu
        FishNet.Component.Spawning.PlayerSpawner ps = Object.FindObjectOfType<FishNet.Component.Spawning.PlayerSpawner>();
        if (ps != null)
        {
            ps.enabled = false;
            EditorUtility.SetDirty(ps);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green>[LobbyUIAutoBuilder]</color> Successfully built and wired all Lobby UI elements!");
    }

    [MenuItem("Tools/2. Setup Gameplay Scene (SampleScene)")]
    public static void SetupGameplayScene()
    {
        // 1. Open SampleScene
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");

        // 2. Remove dummy player in scene if present
        GameObject oldPlayer = GameObject.Find("Player");
        if (oldPlayer != null)
        {
            Object.DestroyImmediate(oldPlayer);
        }

        // 3. Create or find SpawnPoints
        GameObject sp1 = GameObject.Find("SpawnPoint1");
        if (sp1 == null)
        {
            sp1 = new GameObject("SpawnPoint1");
            sp1.transform.position = new Vector3(-3, 0, 0);
        }

        GameObject sp2 = GameObject.Find("SpawnPoint2");
        if (sp2 == null)
        {
            sp2 = new GameObject("SpawnPoint2");
            sp2.transform.position = new Vector3(3, 0, 0);
        }

        // 4. Create GameplayManager and attach GamePlayerSpawner
        GameObject gm = GameObject.Find("GameplayManager");
        if (gm == null)
        {
            gm = new GameObject("GameplayManager");
        }

        GamePlayerSpawner spawner = gm.GetComponent<GamePlayerSpawner>();
        if (spawner == null)
        {
            spawner = gm.AddComponent<GamePlayerSpawner>();
        }

        // Load Player.prefab NetworkObject
        NetworkObject playerNob = AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Player.prefab");
        spawner.SetEditorReferences(playerNob, new Transform[] { sp1.transform, sp2.transform });

        // 5. Ensure Grid, TilemapTanah (Order 0), and TilemapBasah (Order 1) exist
        GameObject gridObj = GameObject.Find("Grid");
        if (gridObj == null)
        {
            gridObj = new GameObject("Grid");
            gridObj.AddComponent<Grid>();
        }

        Material defaultSpriteMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Sprite.mat");

        GameObject tilemapTanahObj = GameObject.Find("TilemapTanah");
        if (tilemapTanahObj == null)
        {
            tilemapTanahObj = new GameObject("TilemapTanah");
            tilemapTanahObj.transform.SetParent(gridObj.transform, false);
            tilemapTanahObj.AddComponent<Tilemap>();
            TilemapRenderer tmr = tilemapTanahObj.AddComponent<TilemapRenderer>();
            if (defaultSpriteMat != null) tmr.material = defaultSpriteMat;
            tmr.sortingLayerName = "Tanah";
            tmr.sortingOrder = 0;
            int tanahLayer = LayerMask.NameToLayer("Tanah");
            if (tanahLayer >= 0) tilemapTanahObj.layer = tanahLayer;
        }

        GameObject tilemapBasahObj = GameObject.Find("TilemapBasah");
        if (tilemapBasahObj == null)
        {
            tilemapBasahObj = new GameObject("TilemapBasah");
            tilemapBasahObj.transform.SetParent(gridObj.transform, false);
            tilemapBasahObj.AddComponent<Tilemap>();
            TilemapRenderer tmr = tilemapBasahObj.AddComponent<TilemapRenderer>();
            if (defaultSpriteMat != null) tmr.material = defaultSpriteMat;
            tmr.sortingLayerName = "TanahBasah";
            tmr.sortingOrder = 1;
            int tanahLayer = LayerMask.NameToLayer("Tanah");
            if (tanahLayer >= 0) tilemapBasahObj.layer = tanahLayer;
        }

        // 6. Ensure GameManager prefab instance exists in scene
        GameObject gameManagerObj = GameObject.Find("GameManager");
        if (gameManagerObj == null)
        {
            GameObject gmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GameManager.prefab");
            if (gmPrefab != null)
            {
                gameManagerObj = (GameObject)PrefabUtility.InstantiatePrefab(gmPrefab);
                gameManagerObj.name = "GameManager";
            }
        }

        if (gameManagerObj != null)
        {
            LahanManagerTilemap lmt = gameManagerObj.GetComponent<LahanManagerTilemap>();
            if (lmt != null)
            {
                if (tilemapTanahObj != null) lmt.tilemapTanah = tilemapTanahObj.GetComponent<Tilemap>();
                if (tilemapBasahObj != null) lmt.tilemapBasah = tilemapBasahObj.GetComponent<Tilemap>();
                if (lmt.tileKering == null) lmt.tileKering = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileKering.asset");
                if (lmt.tileBasah == null) lmt.tileBasah = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileBasah.asset");
                EditorUtility.SetDirty(lmt);
            }
        }

        // 6. Ensure Global Light 2D illuminates all sorting layers (Tanah, TanahBasah, Tanaman, Karakter, etc.)
        GameObject globalLightGo = GameObject.Find("Global Light 2D");
        if (globalLightGo != null)
        {
            var light2D = globalLightGo.GetComponent<UnityEngine.Rendering.Universal.Light2D>();
            if (light2D != null)
            {
                SerializedObject so = new SerializedObject(light2D);
                SerializedProperty prop = so.FindProperty("m_ApplyToSortingLayers");
                if (prop != null)
                {
                    prop.ClearArray();
                    var allLayers = SortingLayer.layers;
                    for (int i = 0; i < allLayers.Length; i++)
                    {
                        prop.InsertArrayElementAtIndex(i);
                        prop.GetArrayElementAtIndex(i).intValue = allLayers[i].id;
                    }
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(light2D);
                }
            }
        }

        // 7. Ensure Main Camera has PlayerCameraFollow component
        GameObject mainCam = GameObject.FindWithTag("MainCamera");
        if (mainCam != null)
        {
            var follow = mainCam.GetComponent<CameraSystem.PlayerCameraFollow>();
            if (follow == null)
            {
                mainCam.AddComponent<CameraSystem.PlayerCameraFollow>();
            }
        }

        // 8. Ensure Silo prefab instance exists in scene
        GameObject siloObj = GameObject.Find("Silo");
        if (siloObj == null)
        {
            GameObject siloPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Silo.prefab");
            if (siloPrefab != null)
            {
                siloObj = (GameObject)PrefabUtility.InstantiatePrefab(siloPrefab);
                siloObj.name = "Silo";
                siloObj.transform.position = new Vector3(-4.5f, 2f, 0f);
                Undo.RegisterCreatedObjectUndo(siloObj, "Create Silo Instance");
            }
        }

        // Save SampleScene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // 5. Ensure Build Settings has both scenes
        EnsureScenesInBuildSettings();

        // 6. Return back to Lobby scene
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");

        Debug.Log("<color=green>[LobbyUIAutoBuilder]</color> SampleScene configured for networked player spawning & Build Settings verified!");
    }

    private static void EnsureScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
