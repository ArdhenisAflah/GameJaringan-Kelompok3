#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using EdenFarm.Auth.UI;

namespace EdenFarm.EditorScripts
{
    /// <summary>
    /// Editor Tool untuk meng-generate struktur GameObjects Canvas dan UI Panel Auth
    /// secara permanen ke dalam Hierarchy scene 'Auth.unity'.
    /// </summary>
    public static class AuthUIHierarchyBuilder
    {
        [MenuItem("Tools/Eden Farm/Build Auth UI Hierarchy")]
        public static void BuildAuthUIHierarchy()
        {
            // 1. Pastikan scene Auth aktif atau buka dialog konfirmasi
            var activeScene = EditorSceneManager.GetActiveScene();
            if (!activeScene.name.Equals("Auth", System.StringComparison.OrdinalIgnoreCase))
            {
                bool openAuth = EditorUtility.DisplayDialog(
                    "Buka Scene Auth?",
                    $"Scene aktif saat ini adalah '{activeScene.name}'. Apakah Anda ingin membuka scene 'Auth' terlebih dahulu?",
                    "Buka Auth Scene",
                    "Tetap Build di Sini"
                );

                if (openAuth)
                {
                    string scenePath = "Assets/Scenes/Auth.unity";
                    EditorSceneManager.OpenScene(scenePath);
                }
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build Auth UI Hierarchy");

            // 2. Pastikan EventSystem ada di Hierarchy
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                eventSystem = esGo.AddComponent<EventSystem>();
                esGo.AddComponent<StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            // 3. Buat Canvas Root (atau gunakan yang sudah ada)
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            GameObject canvasGo;
            if (canvas == null)
            {
                canvasGo = new GameObject("AuthCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;

                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasGo, "Create AuthCanvas");
            }
            else
            {
                canvasGo = canvas.gameObject;
            }

            // Pasang AuthUIController pada Canvas root
            AuthUIController controller = canvasGo.GetComponent<AuthUIController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<AuthUIController>(canvasGo);
            }

            // Ambil resources standar TMP
            TMP_DefaultControls.Resources tmpResources = new TMP_DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd")
            };

            // Hapus AuthCard lama jika ada agar tidak duplikat
            Transform oldCard = canvas.transform.Find("AuthCard");
            if (oldCard != null)
            {
                Undo.DestroyObjectImmediate(oldCard.gameObject);
            }

            // 4. Buat AuthCard Container
            GameObject cardObj = new GameObject("AuthCard");
            cardObj.transform.SetParent(canvas.transform, false);
            RectTransform cardRect = cardObj.AddComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(720, 720);

            Image cardImg = cardObj.AddComponent<Image>();
            cardImg.sprite = tmpResources.background;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(0.11f, 0.14f, 0.20f, 0.96f);
            Undo.RegisterCreatedObjectUndo(cardObj, "Create AuthCard");

            // Header Title
            GameObject titleGo = TMP_DefaultControls.CreateText(tmpResources);
            titleGo.name = "AuthTitleText";
            titleGo.transform.SetParent(cardObj.transform, false);
            RectTransform titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.anchoredPosition = new Vector2(0, -30);
            titleRect.sizeDelta = new Vector2(0, 50);
            TextMeshProUGUI titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "EDEN FARM AUTHENTICATION";
            titleTmp.fontSize = 28;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = Color.white;

            // Status Message
            GameObject statusGo = TMP_DefaultControls.CreateText(tmpResources);
            statusGo.name = "StatusFeedbackText";
            statusGo.transform.SetParent(cardObj.transform, false);
            RectTransform statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(1, 1);
            statusRect.pivot = new Vector2(0.5f, 1);
            statusRect.anchoredPosition = new Vector2(0, -85);
            statusRect.sizeDelta = new Vector2(640, 40);
            TextMeshProUGUI statusTmp = statusGo.GetComponent<TextMeshProUGUI>();
            statusTmp.text = "Silakan login atau daftar akun baru.";
            statusTmp.fontSize = 16;
            statusTmp.alignment = TextAlignmentOptions.Center;
            statusTmp.color = new Color(0.9f, 0.9f, 0.9f, 1f);

            // ==========================================
            // 5. Panel Login (Child Panel di Hierarchy)
            // ==========================================
            GameObject loginPanel = new GameObject("LoginPanel");
            loginPanel.transform.SetParent(cardObj.transform, false);
            RectTransform lpRect = loginPanel.AddComponent<RectTransform>();
            lpRect.anchorMin = Vector2.zero;
            lpRect.anchorMax = Vector2.one;
            lpRect.offsetMin = new Vector2(40, 40);
            lpRect.offsetMax = new Vector2(-40, -140);

            TMP_InputField loginUserInput = CreateInputField(loginPanel.transform, "LoginUsernameInput", "Masukkan Username...", new Vector2(0, 70), false, tmpResources);
            TMP_InputField loginPassInput = CreateInputField(loginPanel.transform, "LoginPasswordInput", "Masukkan Password...", new Vector2(0, 0), true, tmpResources);

            Button loginSubmitBtn = CreateButton(loginPanel.transform, "LoginSubmitBtn", "MASUK (LOGIN)", new Vector2(0, -80), new Color(0.18f, 0.65f, 0.38f, 1f), tmpResources);
            Button switchRegBtn = CreateButton(loginPanel.transform, "SwitchToRegBtn", "Belum punya akun? Daftar Baru", new Vector2(0, -145), new Color(0.25f, 0.35f, 0.55f, 1f), tmpResources);

            // ==========================================
            // 6. Panel Register (Child Panel di Hierarchy)
            // ==========================================
            GameObject regPanel = new GameObject("RegisterPanel");
            regPanel.transform.SetParent(cardObj.transform, false);
            RectTransform rpRect = regPanel.AddComponent<RectTransform>();
            rpRect.anchorMin = Vector2.zero;
            rpRect.anchorMax = Vector2.one;
            rpRect.offsetMin = new Vector2(40, 40);
            rpRect.offsetMax = new Vector2(-40, -140);

            TMP_InputField regUserInput = CreateInputField(regPanel.transform, "RegUsernameInput", "Username (min. 3 karakter)*", new Vector2(0, 120), false, tmpResources);
            TMP_InputField regPassInput = CreateInputField(regPanel.transform, "RegPasswordInput", "Password (min. 6 karakter)*", new Vector2(0, 55), true, tmpResources);
            TMP_InputField regNickInput = CreateInputField(regPanel.transform, "RegNicknameInput", "Nama Panggilan / Nickname (opsional)", new Vector2(0, -10), false, tmpResources);
            TMP_InputField regBirthInput = CreateInputField(regPanel.transform, "RegBirthDateInput", "Tanggal Lahir YYYY-MM-DD (opsional)", new Vector2(0, -75), false, tmpResources);

            Button regSubmitBtn = CreateButton(regPanel.transform, "RegisterSubmitBtn", "BUAT AKUN BARU", new Vector2(0, -150), new Color(0.20f, 0.60f, 0.90f, 1f), tmpResources);
            Button switchLogBtn = CreateButton(regPanel.transform, "SwitchToLoginBtn", "Sudah punya akun? Masuk di sini", new Vector2(0, -215), new Color(0.35f, 0.40f, 0.50f, 1f), tmpResources);

            // Register panel non-aktif secara default (hanya panel login aktif)
            regPanel.SetActive(false);

            // ==========================================
            // 7. Sambungkan Serialized Properties di Controller
            // ==========================================
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("loginPanelRoot").objectReferenceValue = loginPanel;
            so.FindProperty("registerPanelRoot").objectReferenceValue = regPanel;

            so.FindProperty("loginUsernameInput").objectReferenceValue = loginUserInput;
            so.FindProperty("loginPasswordInput").objectReferenceValue = loginPassInput;
            so.FindProperty("loginSubmitButton").objectReferenceValue = loginSubmitBtn;
            so.FindProperty("switchToRegisterButton").objectReferenceValue = switchRegBtn;

            so.FindProperty("registerUsernameInput").objectReferenceValue = regUserInput;
            so.FindProperty("registerPasswordInput").objectReferenceValue = regPassInput;
            so.FindProperty("registerNicknameInput").objectReferenceValue = regNickInput;
            so.FindProperty("registerBirthDateInput").objectReferenceValue = regBirthInput;
            so.FindProperty("registerSubmitButton").objectReferenceValue = regSubmitBtn;
            so.FindProperty("switchToLoginButton").objectReferenceValue = switchLogBtn;

            so.FindProperty("authTitleText").objectReferenceValue = titleTmp;
            so.FindProperty("statusFeedbackText").objectReferenceValue = statusTmp;
            so.ApplyModifiedProperties();

            // Tandai scene dirty dan simpan
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Undo.CollapseUndoGroup(undoGroup);

            Debug.Log("<color=green><b>[AuthUIHierarchyBuilder]</b> Berhasil membuat hierarki Auth Canvas dan Panel UI di Hierarchy!</color>");
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholderText, Vector2 pos, bool isPassword, TMP_DefaultControls.Resources res)
        {
            GameObject inputGo = TMP_DefaultControls.CreateInputField(res);
            inputGo.name = name;
            inputGo.transform.SetParent(parent, false);

            RectTransform rect = inputGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(520, 50);

            Image bg = inputGo.GetComponent<Image>();
            bg.color = new Color(0.18f, 0.22f, 0.30f, 1f);

            TMP_InputField inputField = inputGo.GetComponent<TMP_InputField>();
            var ph = inputField.placeholder as TextMeshProUGUI;
            if (ph != null)
            {
                ph.text = placeholderText;
                ph.fontSize = 18;
                ph.fontStyle = FontStyles.Italic;
                ph.color = new Color(0.6f, 0.65f, 0.75f, 0.7f);
            }

            var textComponent = inputField.textComponent as TextMeshProUGUI;
            if (textComponent != null)
            {
                textComponent.fontSize = 18;
                textComponent.color = Color.white;
            }

            if (isPassword)
            {
                inputField.contentType = TMP_InputField.ContentType.Password;
                inputField.inputType = TMP_InputField.InputType.Password;
            }

            return inputField;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Color btnColor, TMP_DefaultControls.Resources res)
        {
            GameObject btnGo = TMP_DefaultControls.CreateButton(res);
            btnGo.name = name;
            btnGo.transform.SetParent(parent, false);

            RectTransform rect = btnGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(500, 48);

            Image img = btnGo.GetComponent<Image>();
            img.color = btnColor;

            Button btn = btnGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = btnColor * 1.15f;
            cb.pressedColor = btnColor * 0.85f;
            btn.colors = cb;

            TextMeshProUGUI tmp = btnGo.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.text = label;
                tmp.fontSize = 18;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
            }

            return btn;
        }
    }
}
#endif
