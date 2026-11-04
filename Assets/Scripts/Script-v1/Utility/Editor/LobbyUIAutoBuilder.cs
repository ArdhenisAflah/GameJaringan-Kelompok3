#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine.Tilemaps;
using SiloSystem;
using SiloSystem.UI;

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

        Transform oldLeaderboard = canvas.transform.Find("LeaderboardPanel");
        if (oldLeaderboard != null) Object.DestroyImmediate(oldLeaderboard.gameObject);

        Transform oldInside = canvas.transform.Find("InsideRoomPanel");
        if (oldInside != null) Object.DestroyImmediate(oldInside.gameObject);

        // Remove loose Card Lobby in scene root if present
        Transform looseCard = canvas.transform.Find("Card Lobby");
        if (looseCard != null) Object.DestroyImmediate(looseCard.gameObject);

        // Remove old legacy Scroll View that caused the vertical dark strip overlay
        Transform legacyScrollView = canvas.transform.Find("Scroll View");
        if (legacyScrollView != null) Object.DestroyImmediate(legacyScrollView.gameObject);

        // ==========================================
        // 3. CREATE LOBBY BROWSER PANEL (SEBELAH KIRI)
        // ==========================================
        GameObject browserPanel = new GameObject("LobbyBrowserPanel");
        browserPanel.transform.SetParent(canvas.transform, false);
        RectTransform bpRect = browserPanel.AddComponent<RectTransform>();
        bpRect.anchorMin = new Vector2(0.5f, 0.5f);
        bpRect.anchorMax = new Vector2(0.5f, 0.5f);
        bpRect.pivot = new Vector2(0.5f, 0.5f);
        bpRect.anchoredPosition = new Vector2(-280, 0); // Posisi rapi di kiri
        bpRect.sizeDelta = new Vector2(1040, 680);

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
        // 3.1 CREATE LEADERBOARD PANEL (BERDAMPINGAN SEBELAH KANAN)
        // ==========================================
        GameObject leaderboardPanel = new GameObject("LeaderboardPanel");
        leaderboardPanel.transform.SetParent(canvas.transform, false);
        RectTransform lbRect = leaderboardPanel.AddComponent<RectTransform>();
        lbRect.anchorMin = new Vector2(0.5f, 0.5f);
        lbRect.anchorMax = new Vector2(0.5f, 0.5f);
        lbRect.pivot = new Vector2(0.5f, 0.5f);
        lbRect.anchoredPosition = new Vector2(530, 0); // Berdampingan di sebelah kanan Lobby (ada gap 50px bersih)
        lbRect.sizeDelta = new Vector2(500, 680);

        Image lbImage = leaderboardPanel.AddComponent<Image>();
        lbImage.sprite = tmpResources.background;
        lbImage.type = Image.Type.Sliced;
        lbImage.color = new Color(0.10f, 0.12f, 0.18f, 0.95f);

        // Header Title
        GameObject lbHeaderGo = TMP_DefaultControls.CreateText(tmpResources);
        lbHeaderGo.name = "HeaderTitle";
        lbHeaderGo.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform lbhRect = lbHeaderGo.GetComponent<RectTransform>();
        lbhRect.anchorMin = new Vector2(0, 1);
        lbhRect.anchorMax = new Vector2(1, 1);
        lbhRect.pivot = new Vector2(0.5f, 1);
        lbhRect.anchoredPosition = new Vector2(0, -15);
        lbhRect.sizeDelta = new Vector2(0, 44);
        TextMeshProUGUI lbHeaderText = lbHeaderGo.GetComponent<TextMeshProUGUI>();
        lbHeaderText.text = "EDEN LEADERBOARD";
        lbHeaderText.fontSize = 26;
        lbHeaderText.fontStyle = FontStyles.Bold;
        lbHeaderText.alignment = TextAlignmentOptions.Center;
        lbHeaderText.color = new Color(1f, 0.85f, 0.2f);

        // Tab Leaderboard Button
        GameObject tabLbBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        tabLbBtnGo.name = "TabLeaderboardBtn";
        tabLbBtnGo.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform tlbRect = tabLbBtnGo.GetComponent<RectTransform>();
        tlbRect.anchorMin = new Vector2(0, 1);
        tlbRect.anchorMax = new Vector2(0, 1);
        tlbRect.pivot = new Vector2(0, 1);
        tlbRect.anchoredPosition = new Vector2(25, -68);
        tlbRect.sizeDelta = new Vector2(140, 36);
        Button tabLbBtn = tabLbBtnGo.GetComponent<Button>();
        tabLbBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Top 10";

        // Tab My Stats Button
        GameObject tabStatsBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        tabStatsBtnGo.name = "TabMyStatsBtn";
        tabStatsBtnGo.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform tstRect = tabStatsBtnGo.GetComponent<RectTransform>();
        tstRect.anchorMin = new Vector2(0, 1);
        tstRect.anchorMax = new Vector2(0, 1);
        tstRect.pivot = new Vector2(0, 1);
        tstRect.anchoredPosition = new Vector2(175, -68);
        tstRect.sizeDelta = new Vector2(140, 36);
        Button tabStatsBtn = tabStatsBtnGo.GetComponent<Button>();
        tabStatsBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Profil Saya";

        // Refresh Button
        GameObject lbRefreshBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        lbRefreshBtnGo.name = "RefreshBtn";
        lbRefreshBtnGo.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform lbrfRect = lbRefreshBtnGo.GetComponent<RectTransform>();
        lbrfRect.anchorMin = new Vector2(1, 1);
        lbrfRect.anchorMax = new Vector2(1, 1);
        lbrfRect.pivot = new Vector2(1, 1);
        lbrfRect.anchoredPosition = new Vector2(-25, -68);
        lbrfRect.sizeDelta = new Vector2(100, 36);
        Button lbRefreshBtn = lbRefreshBtnGo.GetComponent<Button>();
        lbRefreshBtnGo.GetComponentInChildren<TextMeshProUGUI>().text = "Refresh";

        // Container Tab 1: Leaderboard
        GameObject lbContainer = new GameObject("LeaderboardContainer");
        lbContainer.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform lbcRect = lbContainer.AddComponent<RectTransform>();
        lbcRect.anchorMin = new Vector2(0, 0);
        lbcRect.anchorMax = new Vector2(1, 1);
        lbcRect.offsetMin = new Vector2(25, 20);
        lbcRect.offsetMax = new Vector2(-25, -115);

        // Scroll View
        GameObject lbScrollGo = new GameObject("ScrollView");
        lbScrollGo.transform.SetParent(lbContainer.transform, false);
        RectTransform lbsRect = lbScrollGo.AddComponent<RectTransform>();
        lbsRect.anchorMin = Vector2.zero;
        lbsRect.anchorMax = Vector2.one;
        lbsRect.sizeDelta = Vector2.zero;
        Image lbsBg = lbScrollGo.AddComponent<Image>();
        lbsBg.sprite = tmpResources.inputField;
        lbsBg.type = Image.Type.Sliced;
        lbsBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        ScrollRect lbsScroll = lbScrollGo.AddComponent<ScrollRect>();
        lbsScroll.horizontal = false;
        lbsScroll.vertical = true;

        GameObject lbVp = new GameObject("Viewport");
        lbVp.transform.SetParent(lbScrollGo.transform, false);
        RectTransform lbvpRect = lbVp.AddComponent<RectTransform>();
        lbvpRect.anchorMin = Vector2.zero;
        lbvpRect.anchorMax = Vector2.one;
        lbvpRect.sizeDelta = Vector2.zero;
        lbVp.AddComponent<RectMask2D>();
        lbsScroll.viewport = lbvpRect;

        GameObject lbContent = new GameObject("Content");
        lbContent.transform.SetParent(lbVp.transform, false);
        RectTransform lbcntRect = lbContent.AddComponent<RectTransform>();
        lbcntRect.anchorMin = new Vector2(0, 1);
        lbcntRect.anchorMax = new Vector2(1, 1);
        lbcntRect.pivot = new Vector2(0.5f, 1);
        lbcntRect.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup lblayout = lbContent.AddComponent<VerticalLayoutGroup>();
        lblayout.childControlWidth = true;
        lblayout.childControlHeight = false;
        lblayout.childForceExpandWidth = true;
        lblayout.childForceExpandHeight = false;
        lblayout.spacing = 6;
        lblayout.padding = new RectOffset(10, 10, 10, 10);
        ContentSizeFitter lbcsf = lbContent.AddComponent<ContentSizeFitter>();
        lbcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        lbsScroll.content = lbcntRect;

        // Status Text
        GameObject statusTextGo = TMP_DefaultControls.CreateText(tmpResources);
        statusTextGo.name = "StatusText";
        statusTextGo.transform.SetParent(lbContainer.transform, false);
        RectTransform stRect = statusTextGo.GetComponent<RectTransform>();
        stRect.anchorMin = Vector2.zero;
        stRect.anchorMax = Vector2.one;
        stRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI lbStatusTmp = statusTextGo.GetComponent<TextMeshProUGUI>();
        lbStatusTmp.text = "Memuat data...";
        lbStatusTmp.alignment = TextAlignmentOptions.Center;
        lbStatusTmp.fontSize = 18;

        // Container Tab 2: My Stats
        GameObject statsContainer = new GameObject("MyStatsContainer");
        statsContainer.transform.SetParent(leaderboardPanel.transform, false);
        RectTransform stcRect = statsContainer.AddComponent<RectTransform>();
        stcRect.anchorMin = new Vector2(0, 0);
        stcRect.anchorMax = new Vector2(1, 1);
        stcRect.offsetMin = new Vector2(25, 20);
        stcRect.offsetMax = new Vector2(-25, -115);
        Image stcBg = statsContainer.AddComponent<Image>();
        stcBg.sprite = tmpResources.inputField;
        stcBg.type = Image.Type.Sliced;
        stcBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);

        // Player Name Text
        GameObject pNameGo = TMP_DefaultControls.CreateText(tmpResources);
        pNameGo.name = "PlayerNameText";
        pNameGo.transform.SetParent(statsContainer.transform, false);
        RectTransform pnRect = pNameGo.GetComponent<RectTransform>();
        pnRect.anchoredPosition = new Vector2(0, 220);
        pnRect.sizeDelta = new Vector2(400, 36);
        TextMeshProUGUI pNameTmp = pNameGo.GetComponent<TextMeshProUGUI>();
        pNameTmp.fontSize = 22;
        pNameTmp.fontStyle = FontStyles.Bold;
        pNameTmp.alignment = TextAlignmentOptions.Center;
        pNameTmp.color = Color.white;

        // Rank Tier Text
        GameObject rTierGo = TMP_DefaultControls.CreateText(tmpResources);
        rTierGo.name = "RankTierText";
        rTierGo.transform.SetParent(statsContainer.transform, false);
        RectTransform rtRect2 = rTierGo.GetComponent<RectTransform>();
        rtRect2.anchoredPosition = new Vector2(0, 185);
        rtRect2.sizeDelta = new Vector2(400, 30);
        TextMeshProUGUI rTierTmp = rTierGo.GetComponent<TextMeshProUGUI>();
        rTierTmp.fontSize = 18;
        rTierTmp.alignment = TextAlignmentOptions.Center;
        rTierTmp.color = new Color(0.3f, 0.85f, 1f);

        // Highest Score Text
        GameObject hScoreGo = TMP_DefaultControls.CreateText(tmpResources);
        hScoreGo.name = "HighestScoreText";
        hScoreGo.transform.SetParent(statsContainer.transform, false);
        RectTransform hsRect = hScoreGo.GetComponent<RectTransform>();
        hsRect.anchoredPosition = new Vector2(0, 135);
        hsRect.sizeDelta = new Vector2(400, 30);
        TextMeshProUGUI hScoreTmp = hScoreGo.GetComponent<TextMeshProUGUI>();
        hScoreTmp.fontSize = 18;
        hScoreTmp.alignment = TextAlignmentOptions.Center;
        hScoreTmp.color = new Color(1f, 0.85f, 0.2f);

        // Cumulative Score Text
        GameObject cScoreGo = TMP_DefaultControls.CreateText(tmpResources);
        cScoreGo.name = "CumulativeScoreText";
        cScoreGo.transform.SetParent(statsContainer.transform, false);
        RectTransform csRect = cScoreGo.GetComponent<RectTransform>();
        csRect.anchoredPosition = new Vector2(0, 100);
        csRect.sizeDelta = new Vector2(400, 30);
        TextMeshProUGUI cScoreTmp = cScoreGo.GetComponent<TextMeshProUGUI>();
        cScoreTmp.fontSize = 16;
        cScoreTmp.alignment = TextAlignmentOptions.Center;
        cScoreTmp.color = Color.white;

        // History ScrollView
        GameObject histScrollGo = new GameObject("HistoryScrollView");
        histScrollGo.transform.SetParent(statsContainer.transform, false);
        RectTransform hscRect = histScrollGo.AddComponent<RectTransform>();
        hscRect.anchorMin = new Vector2(0, 0);
        hscRect.anchorMax = new Vector2(1, 0.6f);
        hscRect.offsetMin = new Vector2(10, 10);
        hscRect.offsetMax = new Vector2(-10, -10);
        ScrollRect histScroll = histScrollGo.AddComponent<ScrollRect>();
        histScroll.horizontal = false;
        histScroll.vertical = true;

        GameObject histVp = new GameObject("Viewport");
        histVp.transform.SetParent(histScrollGo.transform, false);
        RectTransform histVpRect = histVp.AddComponent<RectTransform>();
        histVpRect.anchorMin = Vector2.zero;
        histVpRect.anchorMax = Vector2.one;
        histVpRect.sizeDelta = Vector2.zero;
        histVp.AddComponent<RectMask2D>();
        histScroll.viewport = histVpRect;

        GameObject histContent = new GameObject("Content");
        histContent.transform.SetParent(histVp.transform, false);
        RectTransform histCntRect = histContent.AddComponent<RectTransform>();
        histCntRect.anchorMin = new Vector2(0, 1);
        histCntRect.anchorMax = new Vector2(1, 1);
        histCntRect.pivot = new Vector2(0.5f, 1);
        histCntRect.sizeDelta = new Vector2(0, 0);
        VerticalLayoutGroup hLayout = histContent.AddComponent<VerticalLayoutGroup>();
        hLayout.childControlWidth = true;
        hLayout.childControlHeight = false;
        hLayout.childForceExpandWidth = true;
        hLayout.childForceExpandHeight = false;
        hLayout.spacing = 4;
        ContentSizeFitter hCsf = histContent.AddComponent<ContentSizeFitter>();
        hCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        histScroll.content = histCntRect;

        statsContainer.SetActive(false);

        // Attach & Wire LeaderboardUIController component
        var lbController = leaderboardPanel.AddComponent<EdenFarm.Score.UI.LeaderboardUIController>();
        SerializedObject lbSo = new SerializedObject(lbController);
        lbSo.FindProperty("panelRoot").objectReferenceValue = leaderboardPanel;
        lbSo.FindProperty("tabLeaderboardButton").objectReferenceValue = tabLbBtn;
        lbSo.FindProperty("tabMyStatsButton").objectReferenceValue = tabStatsBtn;
        lbSo.FindProperty("refreshButton").objectReferenceValue = lbRefreshBtn;
        lbSo.FindProperty("leaderboardContainer").objectReferenceValue = lbContainer;
        lbSo.FindProperty("myStatsContainer").objectReferenceValue = statsContainer;
        lbSo.FindProperty("leaderboardContentList").objectReferenceValue = lbContent.transform;
        lbSo.FindProperty("leaderboardStatusText").objectReferenceValue = lbStatusTmp;
        lbSo.FindProperty("statPlayerNameText").objectReferenceValue = pNameTmp;
        lbSo.FindProperty("statHighestScoreText").objectReferenceValue = hScoreTmp;
        lbSo.FindProperty("statCumulativeScoreText").objectReferenceValue = cScoreTmp;
        lbSo.FindProperty("statRankTierText").objectReferenceValue = rTierTmp;
        lbSo.FindProperty("recentMatchesContentList").objectReferenceValue = histContent.transform;
        lbSo.ApplyModifiedProperties();

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

    [MenuItem("Tools/2. Setup Gameplay Scene (Gameplay)")]
    public static void SetupGameplayScene()
    {
        // 1. Open Gameplay scene
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Gameplay.unity");

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

        GameObject tilemapGrassObj = GameObject.Find("TilemapGrass");
        if (tilemapGrassObj == null)
        {
            tilemapGrassObj = new GameObject("TilemapGrass");
            tilemapGrassObj.transform.SetParent(gridObj.transform, false);
            tilemapGrassObj.AddComponent<Tilemap>();
            TilemapRenderer tmr = tilemapGrassObj.AddComponent<TilemapRenderer>();
            if (defaultSpriteMat != null) tmr.material = defaultSpriteMat;
            tmr.sortingLayerName = "Rumput";
            tmr.sortingOrder = 0;
        }

        GameObject tilemapTreeObj = GameObject.Find("TilemapTree");
        if (tilemapTreeObj == null)
        {
            tilemapTreeObj = new GameObject("TilemapTree");
            tilemapTreeObj.transform.SetParent(gridObj.transform, false);
            tilemapTreeObj.AddComponent<Tilemap>();
            TilemapRenderer tmr = tilemapTreeObj.AddComponent<TilemapRenderer>();
            if (defaultSpriteMat != null) tmr.material = defaultSpriteMat;
            tmr.sortingLayerName = "Pohon";
            tmr.sortingOrder = 0;
            tilemapTreeObj.AddComponent<TilemapCollider2D>();
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
                if (tilemapGrassObj != null) lmt.tilemapGrass = tilemapGrassObj.GetComponent<Tilemap>();
                if (tilemapTanahObj != null) lmt.tilemapTanah = tilemapTanahObj.GetComponent<Tilemap>();
                if (tilemapBasahObj != null) lmt.tilemapBasah = tilemapBasahObj.GetComponent<Tilemap>();
                if (tilemapTreeObj != null) lmt.tilemapTree = tilemapTreeObj.GetComponent<Tilemap>();
                if (lmt.tileRumput == null) lmt.tileRumput = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileRumput.asset");
                if (lmt.tileKering == null) lmt.tileKering = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileKering.asset");
                if (lmt.tileBasah == null) lmt.tileBasah = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileBasah.asset");
                if (lmt.tileTree == null) lmt.tileTree = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileTree.asset");
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

        // 9. Ensure Canvas and SiloUI exist in scene
        SetupSiloUIInGameplayScene();

        // Save Gameplay scene
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // 5. Ensure Build Settings has both scenes
        EnsureScenesInBuildSettings();

        // 6. Return back to Lobby scene
        EditorSceneManager.OpenScene("Assets/Scenes/Lobby.unity");

        Debug.Log("<color=green>[LobbyUIAutoBuilder]</color> Gameplay scene configured for networked player spawning & Build Settings verified!");
    }

    private static void SetupSiloUIInGameplayScene()
    {
        // 1. Ensure EventSystem exists
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(es, "Create EventSystem");
        }

        // 2. Ensure Canvas exists
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

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        // 3. Ensure SiloUIPanel exists
        Transform existingPanel = canvas.transform.Find("SiloUIPanel");
        if (existingPanel != null)
        {
            Object.DestroyImmediate(existingPanel.gameObject);
        }

        TMP_DefaultControls.Resources tmpResources = new TMP_DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
        };

        // Main Panel Box
        GameObject panelObj = new GameObject("SiloUIPanel");
        panelObj.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(860, 560);

        Image panelImg = panelObj.AddComponent<Image>();
        panelImg.sprite = tmpResources.background;
        panelImg.type = Image.Type.Sliced;
        panelImg.color = new Color(0.10f, 0.12f, 0.18f, 0.96f);

        // Header Title
        GameObject titleGo = TMP_DefaultControls.CreateText(tmpResources);
        titleGo.name = "TitleText";
        titleGo.transform.SetParent(panelObj.transform, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -20);
        titleRect.sizeDelta = new Vector2(0, 40);
        TextMeshProUGUI titleText = titleGo.GetComponent<TextMeshProUGUI>();
        titleText.text = "LUMBUNG PANEN (SILO)";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Capacity Text
        GameObject capGo = TMP_DefaultControls.CreateText(tmpResources);
        capGo.name = "CapacityText";
        capGo.transform.SetParent(panelObj.transform, false);
        RectTransform capRect = capGo.GetComponent<RectTransform>();
        capRect.anchorMin = new Vector2(0, 1);
        capRect.anchorMax = new Vector2(1, 1);
        capRect.pivot = new Vector2(0.5f, 1);
        capRect.anchoredPosition = new Vector2(0, -65);
        capRect.sizeDelta = new Vector2(0, 30);
        TextMeshProUGUI capText = capGo.GetComponent<TextMeshProUGUI>();
        capText.text = "Kapasitas: 15 / 999 unit";
        capText.fontSize = 16;
        capText.alignment = TextAlignmentOptions.Center;
        capText.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Close Button [X]
        GameObject closeBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        closeBtnGo.name = "CloseButton";
        closeBtnGo.transform.SetParent(panelObj.transform, false);
        RectTransform closeRect = closeBtnGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-15, -15);
        closeRect.sizeDelta = new Vector2(40, 40);
        Image closeImg = closeBtnGo.GetComponent<Image>();
        closeImg.color = new Color(0.85f, 0.25f, 0.25f, 1f);
        TextMeshProUGUI closeTxt = closeBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (closeTxt != null) { closeTxt.text = "X"; closeTxt.fontSize = 20; closeTxt.fontStyle = FontStyles.Bold; }

        // Content Area (Scroll View)
        GameObject scrollGo = new GameObject("ItemScrollView");
        scrollGo.transform.SetParent(panelObj.transform, false);
        RectTransform scrollRectTransform = scrollGo.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0, 0);
        scrollRectTransform.anchorMax = new Vector2(1, 1);
        scrollRectTransform.pivot = new Vector2(0.5f, 0.5f);
        scrollRectTransform.offsetMin = new Vector2(25, 90);
        scrollRectTransform.offsetMax = new Vector2(-25, -105);

        Image scrollBg = scrollGo.AddComponent<Image>();
        scrollBg.sprite = tmpResources.inputField;
        scrollBg.type = Image.Type.Sliced;
        scrollBg.color = new Color(0.06f, 0.08f, 0.12f, 0.85f);

        ScrollRect scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = true;
        scrollRect.vertical = false;

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
        GameObject contentGo = new GameObject("ItemContainer");
        contentGo.transform.SetParent(viewportGo.transform, false);
        RectTransform contentRect = contentGo.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 0);
        contentRect.anchorMax = new Vector2(0, 1);
        contentRect.pivot = new Vector2(0, 0.5f);
        contentRect.sizeDelta = new Vector2(0, 0);

        HorizontalLayoutGroup hlg = contentGo.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 20;
        hlg.padding = new RectOffset(15, 15, 15, 15);
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRect;

        // Bottom Status Bar: Player Hand Status
        GameObject handGo = TMP_DefaultControls.CreateText(tmpResources);
        handGo.name = "PlayerHandStatusText";
        handGo.transform.SetParent(panelObj.transform, false);
        RectTransform handRect = handGo.GetComponent<RectTransform>();
        handRect.anchorMin = new Vector2(0, 0);
        handRect.anchorMax = new Vector2(0.6f, 0);
        handRect.pivot = new Vector2(0, 0);
        handRect.anchoredPosition = new Vector2(30, 25);
        handRect.sizeDelta = new Vector2(0, 45);
        TextMeshProUGUI handText = handGo.GetComponent<TextMeshProUGUI>();
        handText.text = "Tangan Pemain: <i>Kosong</i>";
        handText.fontSize = 18;
        handText.alignment = TextAlignmentOptions.MidlineLeft;
        handText.color = Color.white;

        // Bottom Button: Setor Item di Tangan
        GameObject depositBtnGo = TMP_DefaultControls.CreateButton(tmpResources);
        depositBtnGo.name = "DepositHeldItemButton";
        depositBtnGo.transform.SetParent(panelObj.transform, false);
        RectTransform depRect = depositBtnGo.GetComponent<RectTransform>();
        depRect.anchorMin = new Vector2(1, 0);
        depRect.anchorMax = new Vector2(1, 0);
        depRect.pivot = new Vector2(1, 0);
        depRect.anchoredPosition = new Vector2(-30, 25);
        depRect.sizeDelta = new Vector2(240, 45);
        Image depImg = depositBtnGo.GetComponent<Image>();
        depImg.color = new Color(0.2f, 0.65f, 0.35f, 1f);
        TextMeshProUGUI depTxt = depositBtnGo.GetComponentInChildren<TextMeshProUGUI>();
        if (depTxt != null) { depTxt.text = "Setor Item di Tangan"; depTxt.fontSize = 16; depTxt.fontStyle = FontStyles.Bold; }

        // Feedback Notification Text
        GameObject feedGo = TMP_DefaultControls.CreateText(tmpResources);
        feedGo.name = "FeedbackText";
        feedGo.transform.SetParent(panelObj.transform, false);
        RectTransform feedRect = feedGo.GetComponent<RectTransform>();
        feedRect.anchorMin = new Vector2(0, 0);
        feedRect.anchorMax = new Vector2(1, 0);
        feedRect.pivot = new Vector2(0.5f, 0);
        feedRect.anchoredPosition = new Vector2(0, 72);
        feedRect.sizeDelta = new Vector2(0, 25);
        TextMeshProUGUI feedText = feedGo.GetComponent<TextMeshProUGUI>();
        feedText.text = "";
        feedText.fontSize = 15;
        feedText.alignment = TextAlignmentOptions.Center;
        feedGo.SetActive(false);

        // Attach SiloUI component
        SiloUI siloUI = panelObj.AddComponent<SiloUI>();
        siloUI.AutoDiscoverPanelReferences();

        // Load Item Database
        string[] guids = AssetDatabase.FindAssets("t:HarvestItemData", new[] { "Assets/Data/HarvestItems" });
        SerializedObject so = new SerializedObject(siloUI);
        SerializedProperty dbProp = so.FindProperty("itemDatabase");
        if (dbProp != null)
        {
            dbProp.ClearArray();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                HarvestItemData itemData = AssetDatabase.LoadAssetAtPath<HarvestItemData>(path);
                if (itemData != null)
                {
                    dbProp.InsertArrayElementAtIndex(i);
                    dbProp.GetArrayElementAtIndex(i).objectReferenceValue = itemData;
                }
            }
            so.ApplyModifiedProperties();
        }

        // Hide panel by default
        panelObj.SetActive(false);
    }

    private static void EnsureScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene("Assets/Scenes/Lobby.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Gameplay.unity", true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
