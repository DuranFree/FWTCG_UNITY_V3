using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using FWTCG.Data;

namespace FWTCG.Editor
{
    /// <summary>
    /// Editor utility: FWTCG/Build Game Scene
    /// Creates GameScene.unity with all UI hierarchy, GameManager, CardPrefab,
    /// RunePrefab and 10 CardData ScriptableObjects in one click.
    /// </summary>
    public static class SceneBuilder
    {
        // Chinese font — loaded once per build, assigned to all Text components
        private static Font _font;

        private static Font LoadFont()
        {
            // Try project-imported fonts first
            string[] candidates = new[]
            {
                "Assets/Fonts/simhei.ttf",
                "Assets/Fonts/simkai.ttf",
                "Assets/Fonts/msyh.ttc",
            };
            foreach (string p in candidates)
            {
                Font f = AssetDatabase.LoadAssetAtPath<Font>(p);
                if (f != null) return f;
            }
            // Fallback: OS font
            Font os = Font.CreateDynamicFontFromOSFont(new[] { "SimHei", "Microsoft YaHei", "Arial" }, 14);
            return os;
        }

        // ── Menu entry ────────────────────────────────────────────────────────
        [MenuItem("FWTCG/Build Game Scene")]
        public static void BuildGameScene()
        {
            _font = LoadFont();

            // 1. Create a new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── Camera ────────────────────────────────────────────────────────
            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var cam = cameraGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = HexColor("#010a13");
            cam.orthographic = true;
            cam.depth = -1;
            cameraGO.AddComponent<AudioListener>();

            // ── EventSystem (required for all UI interaction) ─────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // ── Canvas ────────────────────────────────────────────────────────
            var canvasGO = CreateCanvas();
            var canvas = canvasGO.GetComponent<Canvas>();
            var canvasRT = canvasGO.GetComponent<RectTransform>();

            // ── Background ────────────────────────────────────────────────────
            var background = CreateFullscreenPanel(canvasGO.transform, "Background",
                HexColor("#010a13"));

            // ── TopBar ────────────────────────────────────────────────────────
            var topBar = CreateTopBar(canvasGO.transform);

            // ── MainArea ──────────────────────────────────────────────────────
            var mainArea = CreateMainArea(canvasGO.transform);

            // ── BottomBar ─────────────────────────────────────────────────────
            var bottomBar = CreateBottomBar(canvasGO.transform,
                out var manaDisplay, out var phaseDisplay,
                out var endTurnButton, out var schDisplay);

            // ── MessagePanel ──────────────────────────────────────────────────
            var messagePanel = CreateMessagePanel(canvasGO.transform, out var messageText);

            // ── GameOverPanel ─────────────────────────────────────────────────
            var gameOverPanel = CreateGameOverPanel(canvasGO.transform,
                out var resultText, out var restartButton);

            // Collect sub-references from TopBar
            var playerScoreText  = topBar.transform.Find("PlayerScore").GetComponent<Text>();
            var roundInfoText    = topBar.transform.Find("RoundInfo").GetComponent<Text>();
            var enemyScoreText   = topBar.transform.Find("EnemyScore").GetComponent<Text>();

            // Collect sub-references from MainArea
            var enemyArea       = mainArea.transform.Find("EnemyArea");
            var battlefieldsArea = mainArea.transform.Find("BattlefieldsArea");
            var playerArea      = mainArea.transform.Find("PlayerArea");

            var enemyHand       = enemyArea.Find("EnemyHand");
            var enemyBase       = enemyArea.Find("EnemyBase");
            var enemyRunes      = enemyArea.Find("EnemyRunes");

            var bf1Panel        = battlefieldsArea.Find("BF1Panel");
            var bf2Panel        = battlefieldsArea.Find("BF2Panel");
            var bf1EnemyUnits   = bf1Panel.Find("BF1EnemyUnits");
            var bf1Label        = bf1Panel.Find("BF1Label").GetComponent<Text>();
            var bf1PlayerUnits  = bf1Panel.Find("BF1PlayerUnits");
            var bf2EnemyUnits   = bf2Panel.Find("BF2EnemyUnits");
            var bf2Label        = bf2Panel.Find("BF2Label").GetComponent<Text>();
            var bf2PlayerUnits  = bf2Panel.Find("BF2PlayerUnits");

            var playerRunes     = playerArea.Find("PlayerRunes");
            var playerBase      = playerArea.Find("PlayerBase");
            var playerHand      = playerArea.Find("PlayerHand");

            // ── Card Prefab ───────────────────────────────────────────────────
            EnsureDirectory("Assets/Prefabs");
            var cardPrefab = CreateCardPrefab();

            // ── Rune Prefab ───────────────────────────────────────────────────
            var runePrefab = CreateRunePrefab();

            // ── CardData ScriptableObjects ────────────────────────────────────
            EnsureDirectory("Assets/Resources/Cards");
            CreateAllCardData();

            // ── GameManager GameObject ────────────────────────────────────────
            var gmGO = new GameObject("GameManager");
            var gameMgr    = gmGO.AddComponent<FWTCG.GameManager>();
            var turnMgr    = gmGO.AddComponent<FWTCG.Systems.TurnManager>();
            var combatSys  = gmGO.AddComponent<FWTCG.Systems.CombatSystem>();
            var scoreMgr   = gmGO.AddComponent<FWTCG.Systems.ScoreManager>();
            var simpleAI   = gmGO.AddComponent<FWTCG.AI.SimpleAI>();
            var gameUI     = gmGO.AddComponent<FWTCG.UI.GameUI>();

            // Duel buttons sit inside BF panels
            var bf1DuelButton = bf1Panel.Find("DuelButton").GetComponent<Button>();
            var bf2DuelButton = bf2Panel.Find("DuelButton").GetComponent<Button>();

            // ── Wire UI references via SerializedObject ───────────────────────
            WireGameUI(gameUI, cardPrefab, runePrefab,
                playerScoreText, enemyScoreText, roundInfoText,
                manaDisplay, schDisplay,
                playerHand, enemyHand,
                playerBase, enemyBase,
                bf1PlayerUnits, bf1EnemyUnits,
                bf2PlayerUnits, bf2EnemyUnits,
                bf1Label, bf2Label,
                playerRunes, enemyRunes,
                bf1DuelButton, bf2DuelButton,
                endTurnButton,
                messagePanel.transform,
                messageText,
                gameOverPanel,
                resultText,
                restartButton);

            WireGameManager(gameMgr, turnMgr, combatSys, scoreMgr, simpleAI, gameUI);

            // ── Save scene ────────────────────────────────────────────────────
            EnsureDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameScene.unity");
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] GameScene.unity 创建成功！");
        }

        // ── Canvas ────────────────────────────────────────────────────────────

        private static GameObject CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        // ── Background ────────────────────────────────────────────────────────

        private static GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = color;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        // ── TopBar ────────────────────────────────────────────────────────────

        private static GameObject CreateTopBar(Transform parent)
        {
            var go = new GameObject("TopBar");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor top-left to top-right, 100px height
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -100f);
            rt.offsetMax = new Vector2(0f, 0f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.spacing = 10f;

            CreateTMPText(go.transform, "PlayerScore", "玩家: 0/8", Color.white, 24, TextAnchor.MiddleLeft);
            CreateTMPText(go.transform, "RoundInfo", "回合 1 · 你的回合", Color.white, 24, TextAnchor.MiddleCenter);
            CreateTMPText(go.transform, "EnemyScore", "AI: 0/8", Color.white, 24, TextAnchor.MiddleRight);

            return go;
        }

        // ── MainArea ──────────────────────────────────────────────────────────

        private static GameObject CreateMainArea(Transform parent)
        {
            var go = new GameObject("MainArea");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // Below TopBar (100px from top), above BottomBar (100px from bottom)
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 100f);   // bottom offset = BottomBar height
            rt.offsetMax = new Vector2(-200f, -100f); // top offset = TopBar, right offset = MessagePanel

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 5f;

            // EnemyArea — 30%
            var enemyArea = CreateAreaWithLayoutElement(go.transform, "EnemyArea", 0.3f);
            {
                var vlg2 = enemyArea.AddComponent<VerticalLayoutGroup>();
                vlg2.childControlWidth = true;
                vlg2.childControlHeight = true;
                vlg2.childForceExpandWidth = true;
                vlg2.childForceExpandHeight = true;
                vlg2.spacing = 3f;

                CreateHorizontalZone(enemyArea.transform, "EnemyHand");
                CreateHorizontalZone(enemyArea.transform, "EnemyBase");
                CreateHorizontalZone(enemyArea.transform, "EnemyRunes");
            }

            // BattlefieldsArea — 20%
            var bfArea = CreateAreaWithLayoutElement(go.transform, "BattlefieldsArea", 0.2f);
            {
                var hlg = bfArea.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 5f;

                CreateBattlefieldPanel(bfArea.transform, "BF1Panel", "BF1EnemyUnits", "战场1", "BF1Label", "BF1PlayerUnits");
                CreateBattlefieldPanel(bfArea.transform, "BF2Panel", "BF2EnemyUnits", "战场2", "BF2Label", "BF2PlayerUnits");
            }

            // PlayerArea — 50%
            var playerArea = CreateAreaWithLayoutElement(go.transform, "PlayerArea", 0.5f);
            {
                var vlg3 = playerArea.AddComponent<VerticalLayoutGroup>();
                vlg3.childControlWidth = true;
                vlg3.childControlHeight = true;
                vlg3.childForceExpandWidth = true;
                vlg3.childForceExpandHeight = true;
                vlg3.spacing = 3f;

                CreateHorizontalZone(playerArea.transform, "PlayerRunes");
                CreateHorizontalZone(playerArea.transform, "PlayerBase");
                CreateHorizontalZone(playerArea.transform, "PlayerHand");
            }

            return go;
        }

        private static GameObject CreateAreaWithLayoutElement(Transform parent, string name, float flexibleHeight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.flexibleHeight = flexibleHeight;
            le.flexibleWidth = 1f;

            return go;
        }

        private static void CreateHorizontalZone(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
        }

        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName)
        {
            var panel = new GameObject(panelName);
            panel.transform.SetParent(parent, false);
            panel.AddComponent<RectTransform>();

            var le = panel.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.spacing = 2f;

            // Enemy units zone
            CreateHorizontalZone(panel.transform, enemyZoneName);

            // Label
            var lbl = CreateTMPText(panel.transform, labelName, labelText, Color.white, 18, TextAnchor.MiddleCenter);
            var lblLE = lbl.gameObject.AddComponent<LayoutElement>();
            lblLE.preferredHeight = 25f;
            lblLE.flexibleHeight = 0f;

            // Player units zone
            CreateHorizontalZone(panel.transform, playerZoneName);

            // Duel button — shown only when BF is contested (hidden by default, shown via RefreshBattlefields)
            var duelBtnGO = new GameObject("DuelButton");
            duelBtnGO.transform.SetParent(panel.transform, false);
            var duelImg = duelBtnGO.AddComponent<Image>();
            duelImg.color = new Color(0.8f, 0.6f, 0.1f); // gold
            var duelBtn = duelBtnGO.AddComponent<Button>();
            var duelLE = duelBtnGO.AddComponent<LayoutElement>();
            duelLE.preferredHeight = 30f;
            duelLE.flexibleHeight = 0f;
            CreateTMPText(duelBtnGO.transform, "DuelLabel", "⚔ 开始法术对决", Color.black, 16, TextAnchor.MiddleCenter);
            duelBtnGO.SetActive(false); // hidden until contested

            // Add button to panel for BF click
            panel.AddComponent<Button>();
        }

        // ── BottomBar ─────────────────────────────────────────────────────────

        private static GameObject CreateBottomBar(Transform parent,
            out Text manaDisplay, out Text phaseDisplay,
            out Button endTurnButton, out Text schDisplay)
        {
            var go = new GameObject("BottomBar");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(-200f, 100f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.spacing = 10f;

            manaDisplay  = CreateTMPText(go.transform, "ManaDisplay", "法力: 0", Color.white, 20, TextAnchor.MiddleLeft);
            phaseDisplay = CreateTMPText(go.transform, "PhaseDisplay", "阶段: -", Color.white, 20, TextAnchor.MiddleCenter);
            endTurnButton = CreateButton(go.transform, "EndTurnButton", "结束回合");
            schDisplay   = CreateTMPText(go.transform, "SchDisplay", "符能: -", Color.white, 20, TextAnchor.MiddleRight);

            return go;
        }

        // ── MessagePanel ──────────────────────────────────────────────────────

        private static GameObject CreateMessagePanel(Transform parent, out Text messageText)
        {
            var go = new GameObject("MessagePanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-200f, 100f);
            rt.offsetMax = new Vector2(0f, -100f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 4f;

            messageText = CreateTMPText(go.transform, "MessageText", "", Color.white, 14, TextAnchor.UpperLeft);
            var msgLE = messageText.gameObject.AddComponent<LayoutElement>();
            msgLE.flexibleWidth = 1f;
            msgLE.flexibleHeight = 1f;

            return go;
        }

        // ── GameOverPanel ─────────────────────────────────────────────────────

        private static GameObject CreateGameOverPanel(Transform parent,
            out Text resultText, out Button restartButton)
        {
            var go = CreateFullscreenPanel(parent, "GameOverPanel", new Color(0f, 0f, 0f, 0.8f));

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20f;

            resultText    = CreateTMPText(go.transform, "ResultText", "结果", Color.white, 48, TextAnchor.MiddleCenter);
            restartButton = CreateButton(go.transform, "RestartButton", "再来一局");

            go.SetActive(false);

            return go;
        }

        // ── Card Prefab ───────────────────────────────────────────────────────

        private static GameObject CreateCardPrefab()
        {
            // Build in scene first, then save as prefab
            var root = new GameObject("CardPrefab");

            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0.85f, 0.85f, 0.85f, 1f);

            var rootRT = root.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(80f, 120f);

            // Button on root
            root.AddComponent<Button>();

            // CardView component
            var cardView = root.AddComponent<FWTCG.UI.CardView>();

            // CardName — top
            var cardName = CreateTMPText(root.transform, "CardName", "卡名", Color.black, 12, TextAnchor.MiddleCenter);
            var nameRT = cardName.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0f, 0.8f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            // CostText — top-left
            var costText = CreateTMPText(root.transform, "CostText", "0", Color.black, 14, TextAnchor.MiddleLeft);
            var costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0f, 0.8f);
            costRT.anchorMax = new Vector2(0.35f, 1f);
            costRT.offsetMin = new Vector2(2f, 0f);
            costRT.offsetMax = new Vector2(0f, 0f);

            // AtkText — bottom
            var atkText = CreateTMPText(root.transform, "AtkText", "0", Color.black, 16, TextAnchor.MiddleCenter);
            var atkRT = atkText.GetComponent<RectTransform>();
            atkRT.anchorMin = new Vector2(0f, 0f);
            atkRT.anchorMax = new Vector2(1f, 0.2f);
            atkRT.offsetMin = Vector2.zero;
            atkRT.offsetMax = Vector2.zero;

            // Wire CardView serialized fields
            var so = new SerializedObject(cardView);
            so.FindProperty("_nameText").objectReferenceValue = cardName;
            so.FindProperty("_costText").objectReferenceValue = costText;
            so.FindProperty("_atkText").objectReferenceValue = atkText;
            so.FindProperty("_cardBg").objectReferenceValue = rootImg;
            so.FindProperty("_clickButton").objectReferenceValue = root.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/CardPrefab.prefab");
            Object.DestroyImmediate(root);

            return prefab;
        }

        // ── Rune Prefab ───────────────────────────────────────────────────────

        private static GameObject CreateRunePrefab()
        {
            var root = new GameObject("RunePrefab");

            var rootImg = root.AddComponent<Image>();
            rootImg.color = new Color(0.7f, 0.85f, 0.7f, 1f);

            var rootRT = root.GetComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(40f, 60f);

            root.AddComponent<Button>();

            // RuneTypeText
            var runeTypeText = CreateTMPText(root.transform, "RuneTypeText", "符", Color.black, 12, TextAnchor.MiddleCenter);
            var runeRT = runeTypeText.GetComponent<RectTransform>();
            runeRT.anchorMin = new Vector2(0f, 0.3f);
            runeRT.anchorMax = new Vector2(1f, 1f);
            runeRT.offsetMin = Vector2.zero;
            runeRT.offsetMax = Vector2.zero;

            // TappedIndicator
            var tappedGO = new GameObject("TappedIndicator");
            tappedGO.transform.SetParent(root.transform, false);
            var tappedImg = tappedGO.AddComponent<Image>();
            tappedImg.color = new Color(0.9f, 0.3f, 0.3f, 0.8f);
            var tappedRT = tappedGO.GetComponent<RectTransform>();
            tappedRT.anchorMin = new Vector2(0f, 0f);
            tappedRT.anchorMax = new Vector2(1f, 0.3f);
            tappedRT.offsetMin = Vector2.zero;
            tappedRT.offsetMax = Vector2.zero;
            // Rotate 90 degrees to indicate tapped
            tappedGO.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tappedGO.SetActive(false);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/RunePrefab.prefab");
            Object.DestroyImmediate(root);

            return prefab;
        }

        // ── CardData ScriptableObjects ─────────────────────────────────────────

        private static void CreateAllCardData()
        {
            // Kaisa deck — RuneType.Blazing
            CreateCardData("kaisa_vanguard",        "先锋哨兵", 2, 2, RuneType.Blazing,  0, "先锋哨兵");
            CreateCardData("kaisa_void_sentinel",   "虚空哨兵", 3, 3, RuneType.Blazing,  0, "虚空哨兵");
            CreateCardData("kaisa_blazing_warrior", "炽烈战士", 4, 4, RuneType.Blazing,  0, "炽烈战士");
            CreateCardData("kaisa_radiant_guard",   "灵光守护", 3, 2, RuneType.Blazing,  0, "灵光守护");
            CreateCardData("kaisa_void_scout",      "虚空游击", 5, 5, RuneType.Blazing,  0, "虚空游击");

            // Yi deck — RuneType.Verdant
            CreateCardData("yi_dawn_warrior",       "晨曦武士", 2, 2, RuneType.Verdant,  0, "晨曦武士");
            CreateCardData("yi_verdant_swordsman",  "碧绿剑客", 3, 3, RuneType.Verdant,  0, "碧绿剑客");
            CreateCardData("yi_crushing_vanguard",  "摧破战将", 4, 4, RuneType.Verdant,  0, "摧破战将");
            CreateCardData("yi_order_mage",         "序理法师", 3, 2, RuneType.Verdant,  0, "序理法师");
            CreateCardData("yi_leaf_fighter",       "翠叶斗士", 5, 5, RuneType.Verdant,  0, "翠叶斗士");
        }

        private static CardData CreateCardData(string id, string cardName,
            int cost, int atk, RuneType runeType, int runeCost, string description)
        {
            string path = $"Assets/Resources/Cards/{id}.asset";

            // Reuse if exists
            var existing = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (existing != null)
            {
                existing.EditorSetup(id, cardName, cost, atk, runeType, runeCost, description);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var data = ScriptableObject.CreateInstance<CardData>();
            data.EditorSetup(id, cardName, cost, atk, runeType, runeCost, description);
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // ── Wire GameUI ───────────────────────────────────────────────────────

        private static void WireGameUI(
            FWTCG.UI.GameUI gameUI,
            GameObject cardPrefab,
            GameObject runePrefab,
            Text playerScoreText, Text enemyScoreText, Text roundPhaseText,
            Text playerManaText, Text playerSchText,
            Transform playerHandContainer, Transform enemyHandContainer,
            Transform playerBaseContainer, Transform enemyBaseContainer,
            Transform bf1PlayerContainer, Transform bf1EnemyContainer,
            Transform bf2PlayerContainer, Transform bf2EnemyContainer,
            Text bf1CtrlText, Text bf2CtrlText,
            Transform playerRuneContainer, Transform enemyRuneContainer,
            Button bf1DuelButton, Button bf2DuelButton,
            Button endTurnButton,
            Transform messageContainer,
            Text messageTextPrefab,
            GameObject gameOverPanel,
            Text gameOverText,
            Button restartButton)
        {
            var so = new SerializedObject(gameUI);

            so.FindProperty("_playerScoreText").objectReferenceValue  = playerScoreText;
            so.FindProperty("_enemyScoreText").objectReferenceValue   = enemyScoreText;
            so.FindProperty("_roundPhaseText").objectReferenceValue   = roundPhaseText;

            so.FindProperty("_playerManaText").objectReferenceValue   = playerManaText;
            // _enemyManaText — not wired (no dedicated enemy mana display in this layout)
            so.FindProperty("_playerSchText").objectReferenceValue    = playerSchText;
            // _enemySchText — not wired

            so.FindProperty("_playerHandContainer").objectReferenceValue  = playerHandContainer;
            so.FindProperty("_enemyHandContainer").objectReferenceValue   = enemyHandContainer;
            so.FindProperty("_cardViewPrefab").objectReferenceValue       = cardPrefab;

            so.FindProperty("_playerBaseContainer").objectReferenceValue  = playerBaseContainer;
            so.FindProperty("_enemyBaseContainer").objectReferenceValue   = enemyBaseContainer;

            so.FindProperty("_bf1PlayerContainer").objectReferenceValue   = bf1PlayerContainer;
            so.FindProperty("_bf1EnemyContainer").objectReferenceValue    = bf1EnemyContainer;
            so.FindProperty("_bf2PlayerContainer").objectReferenceValue   = bf2PlayerContainer;
            so.FindProperty("_bf2EnemyContainer").objectReferenceValue    = bf2EnemyContainer;
            so.FindProperty("_bf1CtrlText").objectReferenceValue          = bf1CtrlText;
            so.FindProperty("_bf2CtrlText").objectReferenceValue          = bf2CtrlText;

            // BF buttons — we placed Button on the BF panels; fetch them from their containers
            var bf1Btn = bf1PlayerContainer.parent.GetComponent<Button>();
            var bf2Btn = bf2PlayerContainer.parent.GetComponent<Button>();
            so.FindProperty("_bf1Button").objectReferenceValue = bf1Btn;
            so.FindProperty("_bf2Button").objectReferenceValue = bf2Btn;
            so.FindProperty("_bf1DuelButton").objectReferenceValue = bf1DuelButton;
            so.FindProperty("_bf2DuelButton").objectReferenceValue = bf2DuelButton;

            so.FindProperty("_playerRuneContainer").objectReferenceValue  = playerRuneContainer;
            so.FindProperty("_enemyRuneContainer").objectReferenceValue   = enemyRuneContainer;
            so.FindProperty("_runeButtonPrefab").objectReferenceValue     = runePrefab;

            so.FindProperty("_endTurnButton").objectReferenceValue        = endTurnButton;
            // _endTurnLabel — child of endTurnButton
            var endTurnLabel = endTurnButton.GetComponentInChildren<UnityEngine.UI.Text>();
            so.FindProperty("_endTurnLabel").objectReferenceValue         = endTurnLabel;

            so.FindProperty("_messageContainer").objectReferenceValue     = messageContainer;
            so.FindProperty("_messageTextPrefab").objectReferenceValue    = messageTextPrefab;

            so.FindProperty("_gameOverPanel").objectReferenceValue        = gameOverPanel;
            so.FindProperty("_gameOverText").objectReferenceValue         = gameOverText;
            so.FindProperty("_restartButton").objectReferenceValue        = restartButton;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Wire GameManager ──────────────────────────────────────────────────

        private static void WireGameManager(
            FWTCG.GameManager gameMgr,
            FWTCG.Systems.TurnManager turnMgr,
            FWTCG.Systems.CombatSystem combatSys,
            FWTCG.Systems.ScoreManager scoreMgr,
            FWTCG.AI.SimpleAI simpleAI,
            FWTCG.UI.GameUI gameUI)
        {
            var so = new SerializedObject(gameMgr);
            so.FindProperty("_turnMgr").objectReferenceValue   = turnMgr;
            so.FindProperty("_combatSys").objectReferenceValue = combatSys;
            so.FindProperty("_scoreMgr").objectReferenceValue  = scoreMgr;
            so.FindProperty("_ai").objectReferenceValue        = simpleAI;
            so.FindProperty("_ui").objectReferenceValue        = gameUI;

            // Wire CardData arrays — load from Resources/Cards
            var kaisaCards = new CardData[]
            {
                LoadCard("kaisa_vanguard"),
                LoadCard("kaisa_void_sentinel"),
                LoadCard("kaisa_blazing_warrior"),
                LoadCard("kaisa_radiant_guard"),
                LoadCard("kaisa_void_scout")
            };
            var yiCards = new CardData[]
            {
                LoadCard("yi_dawn_warrior"),
                LoadCard("yi_verdant_swordsman"),
                LoadCard("yi_crushing_vanguard"),
                LoadCard("yi_order_mage"),
                LoadCard("yi_leaf_fighter")
            };

            var kaisaProp = so.FindProperty("_kaisaDeck");
            kaisaProp.arraySize = kaisaCards.Length;
            for (int i = 0; i < kaisaCards.Length; i++)
                kaisaProp.GetArrayElementAtIndex(i).objectReferenceValue = kaisaCards[i];

            var yiProp = so.FindProperty("_yiDeck");
            yiProp.arraySize = yiCards.Length;
            for (int i = 0; i < yiCards.Length; i++)
                yiProp.GetArrayElementAtIndex(i).objectReferenceValue = yiCards[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static Text CreateTMPText(Transform parent, string name, string text,
            Color color, float fontSize, TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var t = go.AddComponent<Text>();
            t.text = text;
            t.color = color;
            t.fontSize = Mathf.RoundToInt(fontSize);
            t.alignment = alignment;
            t.resizeTextForBestFit = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) t.font = _font;

            return t;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            var btn = go.AddComponent<Button>();

            // Label child
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lbl = lblGO.AddComponent<Text>();
            lbl.text = label;
            lbl.color = Color.white;
            lbl.fontSize = 18;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) lbl.font = _font;

            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;

            return btn;
        }

        private static CardData LoadCard(string id)
        {
            return AssetDatabase.LoadAssetAtPath<CardData>($"Assets/Resources/Cards/{id}.asset");
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            c.a = 1f;
            return c;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
