using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using FWTCG.Data;
using FWTCG.UI;

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

            // ── URP Post Processing (DEV-8) ──────────────────────────────────
            SetupPostProcessing(cameraGO);

            // ── EventSystem (required for all UI interaction) ─────────────────
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // ── Canvas ────────────────────────────────────────────────────────
            var canvasGO = CreateCanvas();
            var canvas = canvasGO.GetComponent<Canvas>();
            var canvasRT = canvasGO.GetComponent<RectTransform>();

            // ── Background (DEV-8: HexGrid shader) ──────────────────────────
            var background = CreateFullscreenPanel(canvasGO.transform, "Background",
                HexColor("#010a13"));
            {
                EnsureDirectory("Assets/Materials");
                var hexMat = LoadOrCreateMaterial("Assets/Materials/BackgroundMat.mat", "UI/HexGrid");
                if (hexMat != null)
                {
                    hexMat.SetColor("_BgColor", HexColor("#010a13"));
                    hexMat.SetColor("_GridColor", new Color(0.04f, 0.78f, 0.73f, 0.08f));
                    hexMat.SetFloat("_GridScale", 40f);
                    hexMat.SetFloat("_GridThickness", 0.04f);
                    hexMat.SetFloat("_NoiseIntensity", 0.02f);
                    hexMat.SetFloat("_VignetteIntensity", 0.3f);
                    hexMat.SetColor("_VignetteColor", new Color(0.04f, 0.78f, 0.73f, 0.05f));
                    background.GetComponent<Image>().material = hexMat;
                }
            }

            // ── TopBar / EnemyInfoStrip ──────────────────────────────────────
            var topBar = CreateTopBar(canvasGO.transform,
                out var enemyRuneInfoText, out var enemyDeckInfoText);

            // ── Enemy hand (below info strip, full width, ~50px) ─────────
            var enemyHandZone = new GameObject("EnemyHandZone");
            enemyHandZone.transform.SetParent(canvasGO.transform, false);
            {
                var ehRT = enemyHandZone.AddComponent<RectTransform>();
                ehRT.anchorMin = new Vector2(0f, 1f);
                ehRT.anchorMax = new Vector2(1f, 1f);
                ehRT.pivot = new Vector2(0.5f, 1f);
                ehRT.offsetMin = new Vector2(0f, -86f);  // 36 (top bar) + 50
                ehRT.offsetMax = new Vector2(-200f, -36f);

                var ehHLG = enemyHandZone.AddComponent<HorizontalLayoutGroup>();
                ehHLG.childControlWidth = false;
                ehHLG.childControlHeight = true;
                ehHLG.childForceExpandWidth = false;
                ehHLG.childForceExpandHeight = true;
                ehHLG.childAlignment = TextAnchor.MiddleCenter;
                ehHLG.spacing = 4f;
            }

            // ── BoardWrapper (main game board) ──────────────────────────────
            var boardWrapper = new GameObject("BoardWrapperOuter");
            boardWrapper.transform.SetParent(canvasGO.transform, false);
            {
                var bwRT = boardWrapper.AddComponent<RectTransform>();
                bwRT.anchorMin = new Vector2(0f, 0f);
                bwRT.anchorMax = new Vector2(1f, 1f);
                bwRT.offsetMin = new Vector2(0f, 200f);   // above bottom (player hand 120 + bottom bar 80)
                bwRT.offsetMax = new Vector2(-200f, -86f); // below enemy hand
            }
            var mainArea = CreateBoardWrapper(boardWrapper.transform,
                out var playerScoreCircleImages, out var enemyScoreCircleImages,
                out var playerDeckCount, out var enemyDeckCount,
                out var playerRunePileCount, out var enemyRunePileCount,
                out var playerDiscardCount, out var enemyDiscardCount,
                out var playerExileCount, out var enemyExileCount,
                out var bf1CtrlBadge, out var bf2CtrlBadge,
                out var bf1CtrlBadgeText, out var bf2CtrlBadgeText,
                out var playerHeroContainer, out var enemyHeroContainer,
                out var boardPlayerLegendText, out var boardLegendSkillBtn,
                out var boardEnemyLegendText);

            // ── Player hand (below board, full width, ~120px) ────────────
            var playerHandZone = new GameObject("PlayerHandZone");
            playerHandZone.transform.SetParent(canvasGO.transform, false);
            {
                var phRT = playerHandZone.AddComponent<RectTransform>();
                phRT.anchorMin = new Vector2(0f, 0f);
                phRT.anchorMax = new Vector2(1f, 0f);
                phRT.pivot = new Vector2(0.5f, 0f);
                phRT.offsetMin = new Vector2(0f, 80f);    // above bottom bar
                phRT.offsetMax = new Vector2(-200f, 200f); // 80 + 120

                var phHLG = playerHandZone.AddComponent<HorizontalLayoutGroup>();
                phHLG.childControlWidth = false;
                phHLG.childControlHeight = true;
                phHLG.childForceExpandWidth = false;
                phHLG.childForceExpandHeight = true;
                phHLG.childAlignment = TextAnchor.MiddleCenter;
                phHLG.spacing = 4f;
            }

            // ── BottomBar (PlayerInfoStrip + ActionPanel) ─────────────────
            var bottomBar = CreateBottomBar(canvasGO.transform,
                out var manaDisplay, out var phaseDisplay,
                out var endTurnButton, out var schDisplay, out var reactBtn,
                out var playerRuneInfoText, out var playerDeckInfoText,
                out var tapAllRunesBtn, out var cancelRunesBtn,
                out var confirmRunesBtn, out var skipReactionBtn);

            // ── MessagePanel ──────────────────────────────────────────────────
            var messagePanel = CreateMessagePanel(canvasGO.transform, out var messageText);

            // ── GameOverPanel ─────────────────────────────────────────────────
            var gameOverPanel = CreateGameOverPanel(canvasGO.transform,
                out var resultText, out var restartButton);

            // ── BannerPanel ───────────────────────────────────────────────────
            var bannerPanel = CreateBannerPanel(canvasGO.transform, out var bannerText);

            // ── ToastPanel ────────────────────────────────────────────────────
            var toastPanel = CreateToastPanel(canvasGO.transform, out var toastText);

            // ── DEV-10: LogToggleButton (anchored to right side, above message panel) ──
            var logToggleGO = new GameObject("LogToggleBtn");
            logToggleGO.transform.SetParent(canvasGO.transform, false);
            {
                var ltRT = logToggleGO.AddComponent<RectTransform>();
                // Position at the left edge of the message panel (right 200px strip)
                ltRT.anchorMin = new Vector2(1f, 0.4f);
                ltRT.anchorMax = new Vector2(1f, 0.6f);
                ltRT.pivot = new Vector2(1f, 0.5f);
                ltRT.anchoredPosition = new Vector2(-196f, 0f); // just left of the 200px log panel
                ltRT.sizeDelta = new Vector2(28f, 80f);
                var ltImg = logToggleGO.AddComponent<Image>();
                ltImg.color = new Color(0.2f, 0.15f, 0.35f, 0.95f);
                logToggleGO.AddComponent<Button>();

                // Add outline for visibility
                var ltOutline = logToggleGO.AddComponent<Outline>();
                ltOutline.effectColor = new Color(GameColors.GoldDark.r, GameColors.GoldDark.g, GameColors.GoldDark.b, 0.6f);
                ltOutline.effectDistance = new Vector2(1f, -1f);

                var ltTextGO = new GameObject("LogToggleText");
                ltTextGO.transform.SetParent(logToggleGO.transform, false);
                var ltTextRT = ltTextGO.AddComponent<RectTransform>();
                ltTextRT.anchorMin = Vector2.zero;
                ltTextRT.anchorMax = Vector2.one;
                ltTextRT.offsetMin = Vector2.zero;
                ltTextRT.offsetMax = Vector2.zero;
                var logToggleText = ltTextGO.AddComponent<Text>();
                logToggleText.text = "<";
                logToggleText.color = GameColors.GoldLight;
                logToggleText.fontSize = 18;
                logToggleText.fontStyle = FontStyle.Bold;
                logToggleText.alignment = TextAnchor.MiddleCenter;
                if (_font != null) logToggleText.font = _font;
            }
            var logToggleBtn = logToggleGO.GetComponent<Button>();
            var logToggleTxt = logToggleGO.GetComponentInChildren<Text>();

            // ── DEV-10: ViewerPanel (discard/exile viewer) ───────────────────
            var viewerPanel = CreateViewerPanel(canvasGO.transform,
                out var viewerTitle, out var viewerCardContainer, out var viewerCloseBtn);

            // ── DEV-10: TimerDisplay ─────────────────────────────────────────
            var timerDisplay = CreateTimerDisplay(canvasGO.transform,
                out var timerFill, out var timerText);

            // Collect sub-references from TopBar
            var playerScoreText  = topBar.transform.Find("PlayerScore").GetComponent<Text>();
            var roundInfoText    = topBar.transform.Find("RoundInfo").GetComponent<Text>();
            var enemyScoreText   = topBar.transform.Find("EnemyScore").GetComponent<Text>();

            // Collect sub-references from BoardWrapper
            var enemyRunes      = mainArea.transform.Find("EnemyRunes");
            var enemyBase       = mainArea.transform.Find("EnemyBase");
            var playerRunes     = mainArea.transform.Find("PlayerRunes");
            var playerBase      = mainArea.transform.Find("PlayerBase");

            var battlefieldsArea = mainArea.transform.Find("BattlefieldsArea");
            var bf1Panel        = battlefieldsArea.Find("BF1Panel");
            var bf2Panel        = battlefieldsArea.Find("BF2Panel");
            var bf1EnemyUnits   = bf1Panel.Find("BF1EnemyUnits");
            var bf1Label        = bf1Panel.Find("LabelRow/" + "BF1Label") ?? bf1Panel.Find("BF1Label");
            var bf1LabelText    = bf1Label != null ? bf1Label.GetComponent<Text>() : null;
            var bf1PlayerUnits  = bf1Panel.Find("BF1PlayerUnits");
            var bf2EnemyUnits   = bf2Panel.Find("BF2EnemyUnits");
            var bf2Label        = bf2Panel.Find("LabelRow/" + "BF2Label") ?? bf2Panel.Find("BF2Label");
            var bf2LabelText    = bf2Label != null ? bf2Label.GetComponent<Text>() : null;
            var bf2PlayerUnits  = bf2Panel.Find("BF2PlayerUnits");

            // Hand zones (outside board)
            var enemyHand       = enemyHandZone.transform;
            var playerHand      = playerHandZone.transform;

            // ── DEV-10: Zone labels + borders ──────────────────────────────────
            AddZoneLabel(playerBase, "BASE");
            AddZoneLabel(enemyBase, "BASE");
            AddZoneLabel(playerRunes, "RUNES");
            AddZoneLabel(enemyRunes, "RUNES");
            AddZoneLabel(playerHeroContainer, "HERO");
            AddZoneLabel(enemyHeroContainer, "HERO");

            // Legend zones
            var pLegendZone = mainArea.transform.Find("PlayerLegendZone");
            var eLegendZone = mainArea.transform.Find("EnemyLegendZone");
            AddZoneLabel(pLegendZone, "LEGEND");
            AddZoneLabel(eLegendZone, "LEGEND");

            // Discard/Exile zone labels
            var pDiscardExile = mainArea.transform.Find("PlayerDiscardExile");
            var eDiscardExile = mainArea.transform.Find("EnemyDiscardExile");
            AddZoneLabel(pDiscardExile, "TRASH/EXILE");
            AddZoneLabel(eDiscardExile, "TRASH/EXILE");

            // Borders on all zones
            Color borderColor = GameColors.GoldDark;
            AddZoneBorder(playerBase, borderColor);
            AddZoneBorder(enemyBase, borderColor);
            AddZoneBorder(playerRunes, borderColor);
            AddZoneBorder(enemyRunes, borderColor);
            if (playerHeroContainer != null) AddZoneBorder(playerHeroContainer.parent, borderColor);
            if (enemyHeroContainer != null) AddZoneBorder(enemyHeroContainer.parent, borderColor);
            if (pLegendZone != null) AddZoneBorder(pLegendZone, borderColor);
            if (eLegendZone != null) AddZoneBorder(eLegendZone, borderColor);

            // ── Card Prefab ───────────────────────────────────────────────────
            EnsureDirectory("Assets/Prefabs");
            var cardPrefab = CreateCardPrefab();

            // ── Rune Prefab ───────────────────────────────────────────────────
            var runePrefab = CreateRunePrefab();

            // ── Startup flow panels (coin flip + mulligan) ────────────────────
            var coinFlipPanel = CreateCoinFlipPanel(canvasGO.transform,
                out var coinFlipText, out var coinFlipOkButton);
            var mulliganPanel = CreateMulliganPanel(canvasGO.transform,
                out var mulliganTitleText, out var mulliganCardContainer,
                out var mulliganConfirmButton, out var mulliganConfirmLabel);

            // ── Debug Panel ───────────────────────────────────────────────────
            var debugPanel = CreateDebugPanel(canvasGO.transform,
                out var debugSpellBtn, out var debugEquipBtn,
                out var debugUnitBtn, out var debugReactiveBtn, out var debugManaBtn);

            // ── Reactive Window Panel ─────────────────────────────────────────
            var reactivePanel = CreateReactiveWindowPanel(canvasGO.transform,
                out var reactiveContextText, out var reactiveCardContainer);

            // ── Legend Panels (DEV-5 → DEV-9: now inside BoardWrapper) ─────────
            // Legends are already created inside CreateBoardWrapper.
            // Use the out-vars from the board wrapper call.
            var playerLegendText = boardPlayerLegendText;
            var legendSkillBtn   = boardLegendSkillBtn;
            var enemyLegendText  = boardEnemyLegendText;

            // ── Card Detail Popup (DEV-8) ────────────────────────────────────
            var cardDetailPopup = CreateCardDetailPopup(canvasGO.transform,
                out var cdpArtImage, out var cdpNameText, out var cdpCostText,
                out var cdpAtkText, out var cdpKeywordsText, out var cdpEffectText,
                out var cdpStateText, out var cdpCloseButton);

            // ── CardArt: ensure all PNGs are imported as Sprite ──────────────
            EnsureCardArtImportedAsSprite();

            // ── CardData ScriptableObjects ────────────────────────────────────
            EnsureDirectory("Assets/Resources/Cards");
            CreateAllCardData();

            // ── GameManager GameObject ────────────────────────────────────────
            var gmGO = new GameObject("GameManager");
            var gameMgr      = gmGO.AddComponent<FWTCG.GameManager>();
            var turnMgr      = gmGO.AddComponent<FWTCG.Systems.TurnManager>();
            var combatSys    = gmGO.AddComponent<FWTCG.Systems.CombatSystem>();
            var scoreMgr     = gmGO.AddComponent<FWTCG.Systems.ScoreManager>();
            var simpleAI     = gmGO.AddComponent<FWTCG.AI.SimpleAI>();
            var gameUI       = gmGO.AddComponent<FWTCG.UI.GameUI>();
            var entryEffects   = gmGO.AddComponent<FWTCG.Systems.EntryEffectSystem>();
            var deathwish      = gmGO.AddComponent<FWTCG.Systems.DeathwishSystem>();
            var spellSys       = gmGO.AddComponent<FWTCG.Systems.SpellSystem>();
            var reactiveSys    = gmGO.AddComponent<FWTCG.Systems.ReactiveSystem>();
            var startupFlowUI  = gmGO.AddComponent<FWTCG.UI.StartupFlowUI>();
            var reactiveWindowUI = gmGO.AddComponent<FWTCG.UI.ReactiveWindowUI>();
            var legendSys    = gmGO.AddComponent<FWTCG.Systems.LegendSystem>();
            var bfSys        = gmGO.AddComponent<FWTCG.Systems.BattlefieldSystem>();
            var toastUI      = gmGO.AddComponent<FWTCG.UI.ToastUI>();
            var cardDetailPopupComp = gmGO.AddComponent<FWTCG.UI.CardDetailPopup>();

            // ── Wire UI references via SerializedObject ───────────────────────
            WireGameUI(gameUI, cardPrefab, runePrefab,
                playerScoreText, enemyScoreText, roundInfoText,
                manaDisplay, schDisplay,
                playerHand, enemyHand,
                playerBase, enemyBase,
                bf1PlayerUnits, bf1EnemyUnits,
                bf2PlayerUnits, bf2EnemyUnits,
                bf1LabelText, bf2LabelText,
                playerRunes, enemyRunes,
                endTurnButton,
                messagePanel.transform,
                messageText,
                gameOverPanel,
                resultText,
                restartButton,
                bannerPanel, bannerText,
                playerLegendText, enemyLegendText, legendSkillBtn,
                playerScoreCircleImages, enemyScoreCircleImages,
                playerDeckCount, enemyDeckCount,
                playerRunePileCount, enemyRunePileCount,
                playerDiscardCount, enemyDiscardCount,
                playerExileCount, enemyExileCount,
                bf1CtrlBadge, bf2CtrlBadge,
                bf1CtrlBadgeText, bf2CtrlBadgeText,
                playerHeroContainer, enemyHeroContainer,
                tapAllRunesBtn, cancelRunesBtn, confirmRunesBtn, skipReactionBtn,
                playerRuneInfoText, enemyRuneInfoText,
                playerDeckInfoText, enemyDeckInfoText,
                // DEV-10 additions
                pLegendZone != null ? pLegendZone : null,
                eLegendZone != null ? eLegendZone : null,
                messagePanel, logToggleBtn, logToggleTxt,
                boardWrapper.GetComponent<RectTransform>(),
                viewerPanel, viewerTitle, viewerCardContainer, viewerCloseBtn,
                timerDisplay, timerFill, timerText,
                playerHandZone.GetComponent<RectTransform>(),
                enemyHandZone.GetComponent<RectTransform>());

            WireGameManager(gameMgr, turnMgr, combatSys, scoreMgr, simpleAI, gameUI,
                            entryEffects, deathwish, spellSys, reactiveSys,
                            startupFlowUI, reactiveWindowUI,
                            coinFlipPanel, coinFlipText, coinFlipOkButton,
                            mulliganPanel, mulliganTitleText, mulliganCardContainer,
                            mulliganConfirmButton, mulliganConfirmLabel, cardPrefab,
                            reactivePanel, reactiveContextText, reactiveCardContainer,
                            reactBtn, legendSys, legendSkillBtn, bfSys,
                            debugSpellBtn, debugEquipBtn, debugUnitBtn, debugReactiveBtn, debugManaBtn,
                            tapAllRunesBtn, skipReactionBtn);

            // ── Wire ToastUI ──────────────────────────────────────────────────
            var toastSO = new SerializedObject(toastUI);
            toastSO.FindProperty("_toastPanel").objectReferenceValue = toastPanel;
            toastSO.FindProperty("_toastText").objectReferenceValue  = toastText;
            toastSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Wire CardDetailPopup (DEV-8) ─────────────────────────────────
            var cdpSO = new SerializedObject(cardDetailPopupComp);
            cdpSO.FindProperty("_panel").objectReferenceValue        = cardDetailPopup;
            cdpSO.FindProperty("_artImage").objectReferenceValue     = cdpArtImage;
            cdpSO.FindProperty("_nameText").objectReferenceValue     = cdpNameText;
            cdpSO.FindProperty("_costText").objectReferenceValue     = cdpCostText;
            cdpSO.FindProperty("_atkText").objectReferenceValue      = cdpAtkText;
            cdpSO.FindProperty("_keywordsText").objectReferenceValue = cdpKeywordsText;
            cdpSO.FindProperty("_effectText").objectReferenceValue   = cdpEffectText;
            cdpSO.FindProperty("_stateText").objectReferenceValue    = cdpStateText;
            cdpSO.FindProperty("_closeButton").objectReferenceValue  = cdpCloseButton;
            cdpSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire into GameUI
            {
                var guiSO = new SerializedObject(gameUI);
                guiSO.FindProperty("_cardDetailPopup").objectReferenceValue = cardDetailPopupComp;
                guiSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // Wire into GameManager
            {
                var gmSO = new SerializedObject(gameMgr);
                gmSO.FindProperty("_cardDetailPopup").objectReferenceValue = cardDetailPopupComp;
                gmSO.ApplyModifiedPropertiesWithoutUndo();
            }

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

        // ── TopBar / EnemyInfoStrip (DEV-9) ──────────────────────────────────

        private static GameObject CreateTopBar(Transform parent,
            out Text enemyRuneInfoText, out Text enemyDeckInfoText)
        {
            var go = new GameObject("TopBar");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -36f);
            rt.offsetMax = new Vector2(0f, 0f);

            var img = go.AddComponent<Image>();
            img.color = GameColors.InfoStripBg;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 2, 2);
            hlg.spacing = 10f;

            CreateTMPText(go.transform, "EnemyScore", "AI: 0/8", GameColors.EnemyRed, 16, TextAnchor.MiddleLeft);
            CreateTMPText(go.transform, "RoundInfo", "回合 1 · 你的回合", GameColors.GoldLight, 16, TextAnchor.MiddleCenter);
            enemyRuneInfoText = CreateTMPText(go.transform, "EnemyRuneInfo", "符文:0/12", GameColors.GoldDark, 14, TextAnchor.MiddleRight);
            enemyDeckInfoText = CreateTMPText(go.transform, "EnemyDeckInfo", "牌库:0", GameColors.GoldDark, 14, TextAnchor.MiddleRight);
            CreateTMPText(go.transform, "PlayerScore", "玩家: 0/8", GameColors.PlayerGreen, 16, TextAnchor.MiddleRight);

            return go;
        }

        // Legacy overload for backward compat (call from older code paths)
        private static GameObject CreateTopBar(Transform parent)
        {
            return CreateTopBar(parent, out _, out _);
        }

        // ── BoardWrapper (DEV-9: 7x5 grid-based board) ─────────────────────────

        private static GameObject CreateBoardWrapper(Transform parent,
            out Image[] playerScoreCircleImages, out Image[] enemyScoreCircleImages,
            out Text playerDeckCount, out Text enemyDeckCount,
            out Text playerRunePileCount, out Text enemyRunePileCount,
            out Text playerDiscardCount, out Text enemyDiscardCount,
            out Text playerExileCount, out Text enemyExileCount,
            out Image bf1CtrlBadge, out Image bf2CtrlBadge,
            out Text bf1CtrlBadgeText, out Text bf2CtrlBadgeText,
            out Transform playerHeroContainer, out Transform enemyHeroContainer,
            out Text playerLegendText, out Button legendSkillBtn, out Text enemyLegendText)
        {
            var go = new GameObject("BoardWrapper");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // ════════════════════════════════════════════════════════════
            // Grid layout matching original CSS:
            //   Cols: 4.5% | 13% | 13% | 1fr(39%) | 13% | 13% | 4.5%
            //   Rows: 12%  | 13% | 50%(center) | 13% | 12%
            //
            // ENEMY (top, rows 1-2):
            //   C2 R1-2: ELegend | C3 R1-2: EHero | C4 R1: EBase
            //   C5-6 R1: EDiscard+Exile | C4 R2: ERunes
            //   C5 R2: ERunePile | C6 R2: EMainPile
            //
            // CENTER (row 3): C2-6 Battlefields
            //
            // PLAYER (bottom, rows 4-5) — diagonal mirror:
            //   C2 R4: PMainPile | C3 R4: PRunePile
            //   C2-3 R5 upper: PDiscard+Exile | C4 R4: PRunes
            //   C4 R5: PBase | C5 R4-5: PHero | C6 R4-5: PLegend
            //
            // SCORES: C1 R3-5 = Player | C7 R1-3 = Enemy
            // ════════════════════════════════════════════════════════════

            // ── Score tracks ──
            playerScoreCircleImages = new Image[9];
            CreateScoreTrack(go.transform, "PlayerScoreTrack", true,
                0.00f, 0.045f, 0.00f, 0.75f, playerScoreCircleImages);

            enemyScoreCircleImages = new Image[9];
            CreateScoreTrack(go.transform, "EnemyScoreTrack", false,
                0.955f, 1.00f, 0.25f, 1.00f, enemyScoreCircleImages);

            // ── ENEMY SIDE (top) ──
            var enemyLegendZone = CreatePlayerLegendZone(go.transform, "EnemyLegendZone",
                false, 0.045f, 0.175f, 0.75f, 1.00f,
                out enemyLegendText, out _);

            CreateHeroZone(go.transform, "EnemyHeroZone",
                0.175f, 0.305f, 0.75f, 1.00f, out enemyHeroContainer);

            CreateHorizontalZoneAnchored(go.transform, "EnemyBase",
                0.305f, 0.695f, 0.87f, 1.00f);

            CreateDiscardExileZone(go.transform, "Enemy",
                0.695f, 0.955f, 0.87f, 1.00f,
                out enemyDiscardCount, out enemyExileCount);

            CreateHorizontalZoneAnchored(go.transform, "EnemyRunes",
                0.305f, 0.695f, 0.75f, 0.87f);

            CreateDeckPile(go.transform, "EnemyRunePile", "符文堆",
                0.695f, 0.825f, 0.75f, 0.87f, out enemyRunePileCount);

            CreateDeckPile(go.transform, "EnemyMainPile", "主牌堆",
                0.825f, 0.955f, 0.75f, 0.87f, out enemyDeckCount);

            // ── PLAYER SIDE (bottom) — diagonal mirror ──
            CreateDeckPile(go.transform, "PlayerMainPile", "主牌堆",
                0.045f, 0.175f, 0.13f, 0.25f, out playerDeckCount);

            CreateDeckPile(go.transform, "PlayerRunePile", "符文堆",
                0.175f, 0.305f, 0.13f, 0.25f, out playerRunePileCount);

            CreateDiscardExileZone(go.transform, "Player",
                0.045f, 0.305f, 0.00f, 0.13f,
                out playerDiscardCount, out playerExileCount);

            CreateHorizontalZoneAnchored(go.transform, "PlayerRunes",
                0.305f, 0.695f, 0.13f, 0.25f);

            CreateHorizontalZoneAnchored(go.transform, "PlayerBase",
                0.305f, 0.695f, 0.00f, 0.13f);

            CreateHeroZone(go.transform, "PlayerHeroZone",
                0.695f, 0.825f, 0.00f, 0.25f, out playerHeroContainer);

            var playerLegendZone = CreatePlayerLegendZone(go.transform, "PlayerLegendZone",
                true, 0.825f, 0.955f, 0.00f, 0.25f,
                out playerLegendText, out legendSkillBtn);

            // ── Battlefields: col 2-6, row 3 (center) ──
            var bfArea = CreateAnchoredZone(go.transform, "BattlefieldsArea",
                0.045f, 0.955f, 0.25f, 0.75f);
            {
                var hlg = bfArea.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.spacing = 5f;

                CreateBattlefieldPanel(bfArea.transform, "BF1Panel", "BF1EnemyUnits", "战场1", "BF1Label", "BF1PlayerUnits",
                    out bf1CtrlBadge, out bf1CtrlBadgeText);
                CreateBattlefieldPanel(bfArea.transform, "BF2Panel", "BF2EnemyUnits", "战场2", "BF2Label", "BF2PlayerUnits",
                    out bf2CtrlBadge, out bf2CtrlBadgeText);
            }

            return go;
        }

        // ── Legacy CreateMainArea (kept as wrapper for backward compat) ──────

        private static GameObject CreateMainArea(Transform parent)
        {
            // DEV-9: replaced by CreateBoardWrapper; this stub exists only for compile compat
            return CreateBoardWrapper(parent,
                out _, out _, out _, out _, out _, out _, out _, out _, out _, out _,
                out _, out _, out _, out _, out _, out _, out _, out _, out _);
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

        // ── Anchored zone helper (DEV-9) ─────────────────────────────────────

        private static GameObject CreateAnchoredZone(Transform parent, string name,
            float xMin, float xMax, float yMin, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            return go;
        }

        private static void CreateHorizontalZoneAnchored(Transform parent, string name,
            float xMin, float xMax, float yMin, float yMax)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;
        }

        // ── Score track (DEV-9) ──────────────────────────────────────────────

        private static void CreateScoreTrack(Transform parent, string name, bool isPlayer,
            float xMin, float xMax, float yMin, float yMax, Image[] circleImages)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(2, 2, 4, 4);
            vlg.childAlignment = isPlayer ? TextAnchor.UpperCenter : TextAnchor.LowerCenter;

            // Player: 8 at top, 0 at bottom (reversed). Enemy: 0 at top, 8 at bottom
            for (int raw = 0; raw < 9; raw++)
            {
                int num = isPlayer ? (8 - raw) : raw;

                var circleGO = new GameObject($"Score_{num}");
                circleGO.transform.SetParent(go.transform, false);

                var le = circleGO.AddComponent<LayoutElement>();
                le.preferredWidth = 26f;
                le.preferredHeight = 26f;

                var img = circleGO.AddComponent<Image>();
                img.color = GameColors.ScoreCircleInactive;

                var numText = CreateTMPText(circleGO.transform, "Num", num.ToString(),
                    GameColors.GoldLight, 12, TextAnchor.MiddleCenter);
                var numRT = numText.GetComponent<RectTransform>();
                numRT.anchorMin = Vector2.zero;
                numRT.anchorMax = Vector2.one;
                numRT.offsetMin = Vector2.zero;
                numRT.offsetMax = Vector2.zero;

                circleImages[num] = img;
            }
        }

        // ── Discard + Exile zone (DEV-9) ─────────────────────────────────────

        private static void CreateDiscardExileZone(Transform parent, string prefix,
            float xMin, float xMax, float yMin, float yMax,
            out Text discardCount, out Text exileCount)
        {
            var go = CreateAnchoredZone(parent, $"{prefix}DiscardExile", xMin, xMax, yMin, yMax);

            var img = go.AddComponent<Image>();
            img.color = GameColors.PileBackground;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 2f;
            hlg.padding = new RectOffset(2, 2, 2, 2);

            // Discard half (clickable — DEV-10)
            var discardGO = new GameObject("Discard");
            discardGO.transform.SetParent(go.transform, false);
            discardGO.AddComponent<RectTransform>();
            var discardImg = discardGO.AddComponent<Image>();
            discardImg.color = new Color(0f, 0f, 0f, 0.01f); // transparent but clickable
            discardGO.AddComponent<Button>(); // clickable for viewer
            var discardLE = discardGO.AddComponent<LayoutElement>();
            discardLE.flexibleWidth = 1f;
            var discardVLG = discardGO.AddComponent<VerticalLayoutGroup>();
            discardVLG.childControlWidth = true;
            discardVLG.childControlHeight = true;
            discardVLG.childForceExpandWidth = true;
            discardVLG.childForceExpandHeight = false;
            discardVLG.childAlignment = TextAnchor.MiddleCenter;
            discardVLG.spacing = 1f;
            CreateTMPText(discardGO.transform, "DiscardLabel", "弃牌", GameColors.GoldDark, 10, TextAnchor.MiddleCenter);
            discardCount = CreateTMPText(discardGO.transform, "DiscardCount", "0", GameColors.GoldLight, 14, TextAnchor.MiddleCenter);

            // Exile half (clickable — DEV-10)
            var exileGO = new GameObject("Exile");
            exileGO.transform.SetParent(go.transform, false);
            exileGO.AddComponent<RectTransform>();
            var exileImg = exileGO.AddComponent<Image>();
            exileImg.color = new Color(0f, 0f, 0f, 0.01f); // transparent but clickable
            exileGO.AddComponent<Button>(); // clickable for viewer
            var exileLE = exileGO.AddComponent<LayoutElement>();
            exileLE.flexibleWidth = 1f;
            var exileVLG = exileGO.AddComponent<VerticalLayoutGroup>();
            exileVLG.childControlWidth = true;
            exileVLG.childControlHeight = true;
            exileVLG.childForceExpandWidth = true;
            exileVLG.childForceExpandHeight = false;
            exileVLG.childAlignment = TextAnchor.MiddleCenter;
            exileVLG.spacing = 1f;
            CreateTMPText(exileGO.transform, "ExileLabel", "放逐", GameColors.GoldDark, 10, TextAnchor.MiddleCenter);
            exileCount = CreateTMPText(exileGO.transform, "ExileCount", "0", GameColors.GoldLight, 14, TextAnchor.MiddleCenter);
        }

        // ── Hero zone (DEV-9) ────────────────────────────────────────────────

        private static void CreateHeroZone(Transform parent, string name,
            float xMin, float xMax, float yMin, float yMax,
            out Transform heroContainer)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            var img = go.AddComponent<Image>();
            img.color = GameColors.PileBackground;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(2, 2, 2, 2);

            var tagLE = CreateTMPText(go.transform, "HeroTag", "英雄位", GameColors.Gold, 10, TextAnchor.MiddleCenter);
            var tagLEComp = tagLE.gameObject.AddComponent<LayoutElement>();
            tagLEComp.preferredHeight = 14f;
            tagLEComp.preferredWidth = 80f;

            // Card slot — fixed size, centered
            var slotGO = new GameObject("HeroSlot");
            slotGO.transform.SetParent(go.transform, false);
            slotGO.AddComponent<RectTransform>();
            var slotLE = slotGO.AddComponent<LayoutElement>();
            slotLE.preferredWidth = 80f;
            slotLE.preferredHeight = 110f;
            var slotHLG = slotGO.AddComponent<HorizontalLayoutGroup>();
            slotHLG.childControlWidth = false;
            slotHLG.childControlHeight = false;
            slotHLG.childForceExpandWidth = false;
            slotHLG.childForceExpandHeight = false;
            slotHLG.childAlignment = TextAnchor.MiddleCenter;

            heroContainer = slotGO.transform;
        }

        // ── Deck pile (DEV-9) ────────────────────────────────────────────────

        private static void CreateDeckPile(Transform parent, string name, string label,
            float xMin, float xMax, float yMin, float yMax, out Text countText)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            var img = go.AddComponent<Image>();
            img.color = GameColors.PileBackground;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(2, 2, 2, 2);

            CreateTMPText(go.transform, "PileLabel", label, GameColors.GoldDark, 10, TextAnchor.MiddleCenter);

            // Card back image (colored rect)
            var backGO = new GameObject("CardBack");
            backGO.transform.SetParent(go.transform, false);
            var backLE = backGO.AddComponent<LayoutElement>();
            backLE.preferredWidth = 30f;
            backLE.preferredHeight = 40f;
            var backImg = backGO.AddComponent<Image>();
            backImg.color = GameColors.CardFaceDown;

            countText = CreateTMPText(go.transform, "Count", "0", GameColors.GoldLight, 14, TextAnchor.MiddleCenter);
        }

        // ── Legend zone (DEV-9, grid-positioned) ─────────────────────────────

        private static GameObject CreatePlayerLegendZone(Transform parent, string name,
            bool isPlayer, float xMin, float xMax, float yMin, float yMax,
            out Text legendText, out Button skillBtn)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            var img = go.AddComponent<Image>();
            img.color = isPlayer ? new Color(0.1f, 0.05f, 0.2f, 0.9f) : new Color(0.2f, 0.05f, 0.05f, 0.9f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 2, 2);
            vlg.spacing = 2f;

            // Title
            string titleStr = isPlayer ? "传奇" : "AI传奇";
            Color titleColor = isPlayer ? new Color(1f, 0.85f, 0.3f, 1f) : new Color(1f, 0.5f, 0.5f, 1f);
            var titleGO = new GameObject("LegendTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 14f;
            var titleT = titleGO.AddComponent<Text>();
            titleT.text = titleStr;
            titleT.color = titleColor;
            titleT.fontSize = 10;
            titleT.alignment = TextAnchor.MiddleCenter;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleT.font = _font;

            // Legend info text (NO HP — legends are indestructible, Rule 167.3)
            var textGO = new GameObject(isPlayer ? "LegendText" : "EnemyLegendText");
            textGO.transform.SetParent(go.transform, false);
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.preferredHeight = 22f;
            legendText = textGO.AddComponent<Text>();
            legendText.text = isPlayer ? "卡莎" : "易大师";
            legendText.color = Color.white;
            legendText.fontSize = 11;
            legendText.alignment = TextAnchor.MiddleCenter;
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) legendText.font = _font;

            // Skill button (player only)
            skillBtn = null;
            if (isPlayer)
            {
                skillBtn = CreateDebugButton(go.transform, "虚空感知", new Color(0.4f, 0.1f, 0.8f, 1f));
                var skillLE = skillBtn.GetComponent<LayoutElement>();
                if (skillLE != null) skillLE.preferredHeight = 22f;
            }
            else
            {
                var passiveGO = new GameObject("PassiveText");
                passiveGO.transform.SetParent(go.transform, false);
                var passiveLE = passiveGO.AddComponent<LayoutElement>();
                passiveLE.preferredHeight = 16f;
                var passiveT = passiveGO.AddComponent<Text>();
                passiveT.text = "[被动] 无极剑道";
                passiveT.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                passiveT.fontSize = 9;
                passiveT.alignment = TextAnchor.MiddleCenter;
                passiveT.horizontalOverflow = HorizontalWrapMode.Wrap;
                passiveT.verticalOverflow   = VerticalWrapMode.Overflow;
                if (_font != null) passiveT.font = _font;
            }

            return go;
        }

        // ── Battlefield panel (DEV-9: with control badge) ────────────────────

        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName,
            out Image ctrlBadge, out Text ctrlBadgeText)
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

            // Label row with control badge — fixed height, not stretched
            var labelRow = new GameObject("LabelRow");
            labelRow.transform.SetParent(panel.transform, false);
            labelRow.AddComponent<RectTransform>();
            var labelRowLE = labelRow.AddComponent<LayoutElement>();
            labelRowLE.preferredHeight = 28f;
            labelRowLE.flexibleHeight = 0f;
            var labelRowHLG = labelRow.AddComponent<HorizontalLayoutGroup>();
            labelRowHLG.childControlWidth = false;
            labelRowHLG.childControlHeight = false;
            labelRowHLG.childForceExpandWidth = false;
            labelRowHLG.childForceExpandHeight = false;
            labelRowHLG.spacing = 4f;
            labelRowHLG.childAlignment = TextAnchor.MiddleCenter;

            // Control badge (small colored dot, fixed 20×20)
            var badgeGO = new GameObject("CtrlBadge");
            badgeGO.transform.SetParent(labelRow.transform, false);
            var badgeRT = badgeGO.AddComponent<RectTransform>();
            badgeRT.sizeDelta = new Vector2(20f, 20f);
            ctrlBadge = badgeGO.AddComponent<Image>();
            ctrlBadge.color = GameColors.ScoreCircleInactive;
            ctrlBadgeText = CreateTMPText(badgeGO.transform, "BadgeText", "—",
                Color.white, 10, TextAnchor.MiddleCenter);
            var badgeTextRT = ctrlBadgeText.GetComponent<RectTransform>();
            badgeTextRT.anchorMin = Vector2.zero;
            badgeTextRT.anchorMax = Vector2.one;
            badgeTextRT.offsetMin = Vector2.zero;
            badgeTextRT.offsetMax = Vector2.zero;

            // BF name label
            var lbl = CreateTMPText(labelRow.transform, labelName, labelText, Color.white, 18, TextAnchor.MiddleCenter);

            // Player units zone
            CreateHorizontalZone(panel.transform, playerZoneName);

            // Add button to panel for BF click
            panel.AddComponent<Button>();
        }

        // Keep old overload for backward compat (unused but prevents compile errors)
        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName)
        {
            CreateBattlefieldPanel(parent, panelName, enemyZoneName, labelText,
                labelName, playerZoneName, out _, out _);
        }

        // ── BottomBar: PlayerInfoStrip + ActionPanel (DEV-9) ─────────────────

        private static GameObject CreateBottomBar(Transform parent,
            out Text manaDisplay, out Text phaseDisplay,
            out Button endTurnButton, out Text schDisplay, out Button reactBtn,
            out Text playerRuneInfoText, out Text playerDeckInfoText,
            out Button tapAllRunesBtn, out Button cancelRunesBtn,
            out Button confirmRunesBtn, out Button skipReactionBtn)
        {
            // Outer container holding both strips
            var go = new GameObject("BottomBar");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(-200f, 80f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;
            vlg.spacing = 2f;

            // ── Player info strip (top half) ──
            var infoStrip = new GameObject("PlayerInfoStrip");
            infoStrip.transform.SetParent(go.transform, false);
            infoStrip.AddComponent<RectTransform>();
            var infoLE = infoStrip.AddComponent<LayoutElement>();
            infoLE.flexibleHeight = 1f;
            var infoImg = infoStrip.AddComponent<Image>();
            infoImg.color = GameColors.InfoStripBg;

            var infoHLG = infoStrip.AddComponent<HorizontalLayoutGroup>();
            infoHLG.childControlWidth = true;
            infoHLG.childControlHeight = true;
            infoHLG.childForceExpandWidth = true;
            infoHLG.childForceExpandHeight = true;
            infoHLG.padding = new RectOffset(10, 10, 2, 2);
            infoHLG.spacing = 10f;

            manaDisplay = CreateTMPText(infoStrip.transform, "ManaDisplay", "法力: 0", GameColors.PlayerGreen, 16, TextAnchor.MiddleLeft);
            schDisplay  = CreateTMPText(infoStrip.transform, "SchDisplay", "符能: -", GameColors.GoldLight, 14, TextAnchor.MiddleLeft);
            playerRuneInfoText = CreateTMPText(infoStrip.transform, "PlayerRuneInfo", "符文:0/12", GameColors.GoldDark, 14, TextAnchor.MiddleCenter);
            playerDeckInfoText = CreateTMPText(infoStrip.transform, "PlayerDeckInfo", "牌库:0", GameColors.GoldDark, 14, TextAnchor.MiddleCenter);

            // ── Action panel (bottom half) ──
            var actionPanel = new GameObject("ActionPanel");
            actionPanel.transform.SetParent(go.transform, false);
            actionPanel.AddComponent<RectTransform>();
            var actionLE = actionPanel.AddComponent<LayoutElement>();
            actionLE.flexibleHeight = 1f;

            var actionHLG = actionPanel.AddComponent<HorizontalLayoutGroup>();
            actionHLG.childControlWidth = true;
            actionHLG.childControlHeight = true;
            actionHLG.childForceExpandWidth = false;
            actionHLG.childForceExpandHeight = true;
            actionHLG.padding = new RectOffset(6, 6, 2, 2);
            actionHLG.spacing = 6f;
            actionHLG.childAlignment = TextAnchor.MiddleCenter;

            phaseDisplay = CreateTMPText(actionPanel.transform, "PhaseDisplay", "阶段: -", GameColors.GoldLight, 14, TextAnchor.MiddleCenter);
            var phaseLEComp = phaseDisplay.gameObject.AddComponent<LayoutElement>();
            phaseLEComp.flexibleWidth = 1f;

            tapAllRunesBtn = CreateActionButton(actionPanel.transform, "TapAllRunesBtn", "全部横置", GameColors.ActionBtnSecondary);
            cancelRunesBtn = CreateActionButton(actionPanel.transform, "CancelRunesBtn", "取消", GameColors.ActionBtnDanger);
            confirmRunesBtn = CreateActionButton(actionPanel.transform, "ConfirmRunesBtn", "确认符文操作", GameColors.ActionBtnPrimary);
            skipReactionBtn = CreateActionButton(actionPanel.transform, "SkipReactionBtn", "跳过响应", GameColors.ActionBtnSecondary);
            endTurnButton = CreateActionButton(actionPanel.transform, "EndTurnButton", "结束行动", GameColors.ActionBtnPrimary);
            reactBtn = CreateActionButton(actionPanel.transform, "ReactButton", "反应", new Color(1f, 0.55f, 0f, 1f));

            return go;
        }

        // Legacy overload for backward compat
        private static GameObject CreateBottomBar(Transform parent,
            out Text manaDisplay, out Text phaseDisplay,
            out Button endTurnButton, out Text schDisplay, out Button reactBtn)
        {
            return CreateBottomBar(parent, out manaDisplay, out phaseDisplay,
                out endTurnButton, out schDisplay, out reactBtn,
                out _, out _, out _, out _, out _, out _);
        }

        private static Button CreateActionButton(Transform parent, string name, string label, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 100f;
            le.preferredHeight = 30f;

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lbl = lblGO.AddComponent<Text>();
            lbl.text = label;
            lbl.color = Color.white;
            lbl.fontSize = 13;
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

            messageText = CreateTMPText(go.transform, "MessageText", "", Color.white, 13, TextAnchor.UpperLeft);
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;   // wrap within panel width
            messageText.verticalOverflow   = VerticalWrapMode.Overflow;
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

        // ── Banner Panel ──────────────────────────────────────────────────────

        private static GameObject CreateBannerPanel(Transform parent, out Text bannerText)
        {
            // Non-blocking centered overlay — shows event name for ~1.8 s then hides
            var go = new GameObject("BannerPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.35f);
            rt.anchorMax = new Vector2(0.85f, 0.65f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.78f);

            bannerText = CreateTMPText(go.transform, "BannerText", "", Color.yellow, 44, TextAnchor.MiddleCenter);
            var btRT = bannerText.GetComponent<RectTransform>();
            btRT.anchorMin = Vector2.zero;
            btRT.anchorMax = Vector2.one;
            btRT.offsetMin = Vector2.zero;
            btRT.offsetMax = Vector2.zero;
            bannerText.fontStyle = FontStyle.Bold;

            go.SetActive(false);
            return go;
        }

        // ── Toast panel ───────────────────────────────────────────────────────

        private static GameObject CreateToastPanel(Transform parent, out Text toastText)
        {
            // Anchored top-center, narrow strip for battlefield effect notifications
            var go = new GameObject("ToastPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.82f);
            rt.anchorMax = new Vector2(0.8f, 0.92f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.04f, 0.06f, 0.18f, 0.88f);

            go.AddComponent<CanvasGroup>();

            toastText = CreateTMPText(go.transform, "ToastText", "", new Color(0.94f, 0.86f, 0.42f), 22, TextAnchor.MiddleCenter);
            var ttRT = toastText.GetComponent<RectTransform>();
            ttRT.anchorMin = new Vector2(0.02f, 0f);
            ttRT.anchorMax = new Vector2(0.98f, 1f);
            ttRT.offsetMin = Vector2.zero;
            ttRT.offsetMax = Vector2.zero;
            toastText.fontStyle = FontStyle.Bold;

            go.SetActive(false);
            return go;
        }

        // ── Startup panels ────────────────────────────────────────────────────

        private static GameObject CreateCoinFlipPanel(Transform parent,
            out Text coinFlipText, out Button okButton)
        {
            var go = CreateFullscreenPanel(parent, "CoinFlipPanel", new Color(0f, 0f, 0f, 0.85f));

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 30f;

            coinFlipText = CreateTMPText(go.transform, "CoinFlipText", "掷硬币",
                Color.white, 40, TextAnchor.MiddleCenter);
            okButton = CreateButton(go.transform, "OkButton", "开始");

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateMulliganPanel(Transform parent,
            out Text titleText, out Transform cardContainer,
            out Button confirmButton, out Text confirmLabel)
        {
            var go = CreateFullscreenPanel(parent, "MulliganPanel", new Color(0f, 0f, 0f, 0.85f));

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20f;

            titleText = CreateTMPText(go.transform, "MulliganTitle", "梦想手牌调度",
                Color.white, 28, TextAnchor.MiddleCenter);

            // Card container (horizontal layout for up to 4 cards)
            var containerGO = new GameObject("MulliganCardContainer");
            containerGO.transform.SetParent(go.transform, false);
            var containerRT = containerGO.AddComponent<RectTransform>();
            containerRT.sizeDelta = new Vector2(500f, 150f);
            var hlg = containerGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 12f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            cardContainer = containerGO.transform;

            // Confirm button with label
            confirmButton = CreateButton(go.transform, "ConfirmButton", "确认");
            confirmLabel  = confirmButton.GetComponentInChildren<Text>();

            go.SetActive(false);
            return go;
        }

        // ── Debug Panel ───────────────────────────────────────────────────────

        /// <summary>
        /// Creates a small debug panel anchored to the bottom-left corner.
        /// Buttons: 摸法术 / 摸装备 / 摸单位 / 摸反应牌 / +5法力
        /// </summary>
        private static GameObject CreateDebugPanel(Transform parent,
            out Button spellBtn, out Button equipBtn,
            out Button unitBtn, out Button reactiveBtn, out Button manaBtn)
        {
            var go = new GameObject("DebugPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor bottom-left, 130px wide × 215px tall (5 buttons)
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(5f, 105f); // just above BottomBar
            rt.sizeDelta = new Vector2(130f, 215f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4f;

            // Title label
            var titleGO = new GameObject("DebugTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 22f;
            var titleT = titleGO.AddComponent<Text>();
            titleT.text = "── DEBUG ──";
            titleT.color = new Color(1f, 0.8f, 0.2f, 1f);
            titleT.fontSize = 13;
            titleT.alignment = TextAnchor.MiddleCenter;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleT.font = _font;

            spellBtn    = CreateDebugButton(go.transform, "摸法术牌", new Color(0.5f, 0.2f, 0.8f, 1f));
            equipBtn    = CreateDebugButton(go.transform, "摸装备牌", new Color(0.2f, 0.6f, 0.3f, 1f));
            unitBtn     = CreateDebugButton(go.transform, "摸单位牌", new Color(0.2f, 0.4f, 0.8f, 1f));
            reactiveBtn = CreateDebugButton(go.transform, "摸反应牌", new Color(0.8f, 0.4f, 0.1f, 1f));
            manaBtn     = CreateDebugButton(go.transform, "+5 法力",  new Color(0.7f, 0.4f, 0.1f, 1f));

            return go;
        }

        private static Button CreateDebugButton(Transform parent, string label, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 32f;

            var img = go.AddComponent<Image>();
            img.color = color;

            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.highlightedColor = Color.white;
            btn.colors = cb;

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lbl = lblGO.AddComponent<Text>();
            lbl.text = label;
            lbl.color = Color.white;
            lbl.fontSize = 14;
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            lbl.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) lbl.font = _font;

            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;

            return btn;
        }

        // ── Reactive Window Panel ─────────────────────────────────────────────

        private static GameObject CreateReactiveWindowPanel(Transform parent,
            out Text contextText, out Transform cardContainer)
        {
            // Full-screen dark overlay
            var panel = CreateFullscreenPanel(parent, "ReactiveWindowPanel",
                new Color(0f, 0f, 0f, 0.75f));

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 12f;

            // Context text
            var ctGO = new GameObject("ContextText");
            ctGO.transform.SetParent(panel.transform, false);
            contextText = ctGO.AddComponent<Text>();
            contextText.text = "选择要打出的反应牌（必须打出一张）";
            contextText.color = Color.white;
            contextText.fontSize = 22;
            contextText.alignment = TextAnchor.MiddleCenter;
            contextText.horizontalOverflow = HorizontalWrapMode.Wrap;
            contextText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) contextText.font = _font;
            var ctLE = ctGO.AddComponent<LayoutElement>();
            ctLE.preferredWidth  = 700f;
            ctLE.preferredHeight = 70f;

            // Card container (horizontal row)
            var ccGO = new GameObject("CardContainer");
            ccGO.transform.SetParent(panel.transform, false);
            var ccLE = ccGO.AddComponent<LayoutElement>();
            ccLE.preferredWidth  = 700f;
            ccLE.preferredHeight = 140f;
            var hlg = ccGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            cardContainer = ccGO.transform;

            panel.SetActive(false);
            return panel;
        }

        // ── Legend Panels (DEV-5) ─────────────────────────────────────────────

        /// <summary>Player legend panel (Kaisa): bottom-left, above debug panel.</summary>
        private static GameObject CreatePlayerLegendPanel(Transform parent,
            out Text legendText, out Button skillBtn)
        {
            var go = new GameObject("PlayerLegendPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(5f, 330f); // above debug panel (105+215+10)
            rt.sizeDelta = new Vector2(130f, 95f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.05f, 0.2f, 0.9f); // dark purple

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4f;

            // Title
            var titleGO = new GameObject("LegendTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 18f;
            var titleT = titleGO.AddComponent<Text>();
            titleT.text = "── 传奇 ──";
            titleT.color = new Color(1f, 0.85f, 0.3f, 1f);
            titleT.fontSize = 12;
            titleT.alignment = TextAnchor.MiddleCenter;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleT.font = _font;

            // Legend info text
            var textGO = new GameObject("LegendText");
            textGO.transform.SetParent(go.transform, false);
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.preferredHeight = 32f;
            legendText = textGO.AddComponent<Text>();
            legendText.text = "卡莎·传奇\nHP 20/20";
            legendText.color = Color.white;
            legendText.fontSize = 11;
            legendText.alignment = TextAnchor.MiddleLeft;
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) legendText.font = _font;

            // Skill button
            skillBtn = CreateDebugButton(go.transform, "虚空感知", new Color(0.4f, 0.1f, 0.8f, 1f));
            var skillLE = skillBtn.GetComponent<LayoutElement>();
            if (skillLE != null) skillLE.preferredHeight = 28f;

            return go;
        }

        /// <summary>Enemy legend panel (Masteryi): top-left, just below top bar.</summary>
        private static GameObject CreateEnemyLegendPanel(Transform parent, out Text legendText)
        {
            var go = new GameObject("EnemyLegendPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(5f, -105f); // below top bar (100px)
            rt.sizeDelta = new Vector2(130f, 65f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.05f, 0.05f, 0.9f); // dark red

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4f;

            // Title
            var titleGO = new GameObject("EnemyLegendTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 18f;
            var titleT = titleGO.AddComponent<Text>();
            titleT.text = "── AI传奇 ──";
            titleT.color = new Color(1f, 0.5f, 0.5f, 1f);
            titleT.fontSize = 12;
            titleT.alignment = TextAnchor.MiddleCenter;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleT.font = _font;

            // Legend info text
            var textGO = new GameObject("EnemyLegendText");
            textGO.transform.SetParent(go.transform, false);
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.preferredHeight = 32f;
            legendText = textGO.AddComponent<Text>();
            legendText.text = "易大师·传奇\nHP 20/20";
            legendText.color = Color.white;
            legendText.fontSize = 11;
            legendText.alignment = TextAnchor.MiddleLeft;
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) legendText.font = _font;

            return go;
        }

        // ── Card Detail Popup (DEV-8) ─────────────────────────────────────────

        private static GameObject CreateCardDetailPopup(Transform parent,
            out Image artImage, out Text nameText, out Text costText,
            out Text atkText, out Text keywordsText, out Text effectText,
            out Text stateText, out Button closeButton)
        {
            // Fullscreen dimmed overlay (click to close)
            var overlay = CreateFullscreenPanel(parent, "CardDetailPopup", new Color(0f, 0f, 0f, 0.7f));
            closeButton = overlay.AddComponent<Button>();
            // Make the background image act as click target
            overlay.GetComponent<Image>().raycastTarget = true;

            // Center detail panel (500x700)
            var panel = new GameObject("DetailPanel");
            panel.transform.SetParent(overlay.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.19f, 0.95f); // #1e1e2f-ish
            panelImg.raycastTarget = true; // block clicks from going to overlay

            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(500f, 700f);

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth  = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 8f;

            // -- Card Art (240px tall)
            var artGO = new GameObject("CDPArt");
            artGO.transform.SetParent(panel.transform, false);
            var artLE = artGO.AddComponent<LayoutElement>();
            artLE.preferredHeight = 240f;
            artImage = artGO.AddComponent<Image>();
            artImage.color = new Color(0.2f, 0.2f, 0.3f, 1f);
            artImage.preserveAspect = true;

            // -- Card Name (gold, 28pt)
            nameText = CreateDetailText(panel.transform, "CDPName", "", 28,
                new Color(0.98f, 0.75f, 0.15f, 1f), TextAnchor.MiddleCenter, 36f);

            // -- Cost & Type
            costText = CreateDetailText(panel.transform, "CDPCost", "", 16,
                new Color(0.91f, 0.85f, 0.75f, 1f), TextAnchor.MiddleLeft, 24f);

            // -- ATK/HP
            atkText = CreateDetailText(panel.transform, "CDPAtk", "", 16,
                Color.white, TextAnchor.MiddleLeft, 24f);

            // -- Keywords (multi-line)
            keywordsText = CreateDetailText(panel.transform, "CDPKeywords", "", 14,
                new Color(0.6f, 0.85f, 1f, 1f), TextAnchor.UpperLeft, 100f, true);

            // -- Effect description (multi-line)
            effectText = CreateDetailText(panel.transform, "CDPEffect", "", 14,
                new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.UpperLeft, 80f, true);

            // -- Runtime state
            stateText = CreateDetailText(panel.transform, "CDPState", "", 13,
                new Color(1f, 0.9f, 0.5f, 1f), TextAnchor.UpperLeft, 60f, true);

            overlay.SetActive(false);
            return overlay;
        }

        private static Text CreateDetailText(Transform parent, string name, string defaultText,
            int fontSize, Color color, TextAnchor alignment, float height, bool multiLine = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            if (multiLine) le.flexibleHeight = 1f;
            var t = go.AddComponent<Text>();
            t.text = defaultText;
            t.color = color;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.horizontalOverflow = multiLine ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) t.font = _font;
            return t;
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

            // CardName — top banner (semi-transparent dark strip)
            var nameBannerGO = new GameObject("NameBanner");
            nameBannerGO.transform.SetParent(root.transform, false);
            var nameBannerImg = nameBannerGO.AddComponent<Image>();
            nameBannerImg.color = new Color(0f, 0f, 0f, 0.6f);
            nameBannerImg.raycastTarget = false;
            var nameBannerRT = nameBannerGO.GetComponent<RectTransform>();
            nameBannerRT.anchorMin = new Vector2(0f, 0.8f);
            nameBannerRT.anchorMax = new Vector2(1f, 1f);
            nameBannerRT.offsetMin = Vector2.zero;
            nameBannerRT.offsetMax = Vector2.zero;

            var cardName = CreateTMPText(nameBannerGO.transform, "CardName", "卡名", Color.white, 11, TextAnchor.MiddleCenter);
            var nameRT = cardName.GetComponent<RectTransform>();
            nameRT.anchorMin = Vector2.zero;
            nameRT.anchorMax = Vector2.one;
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            // CostText — top-left cost badge (gold circle)
            var costBadgeGO = new GameObject("CostBadge");
            costBadgeGO.transform.SetParent(root.transform, false);
            var costBadgeImg = costBadgeGO.AddComponent<Image>();
            costBadgeImg.color = new Color(0.78f, 0.67f, 0.43f, 0.9f);
            costBadgeImg.raycastTarget = false;
            var costBadgeRT = costBadgeGO.GetComponent<RectTransform>();
            costBadgeRT.anchorMin = new Vector2(0f, 0.8f);
            costBadgeRT.anchorMax = new Vector2(0.3f, 1f);
            costBadgeRT.offsetMin = new Vector2(1f, 1f);
            costBadgeRT.offsetMax = new Vector2(-1f, -1f);

            var costText = CreateTMPText(costBadgeGO.transform, "CostText", "0", Color.white, 13, TextAnchor.MiddleCenter);
            costText.fontStyle = FontStyle.Bold;
            var costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = Vector2.zero;
            costRT.anchorMax = Vector2.one;
            costRT.offsetMin = Vector2.zero;
            costRT.offsetMax = Vector2.zero;

            // AtkText — bottom banner (semi-transparent dark strip)
            var atkBannerGO = new GameObject("AtkBanner");
            atkBannerGO.transform.SetParent(root.transform, false);
            var atkBannerImg = atkBannerGO.AddComponent<Image>();
            atkBannerImg.color = new Color(0f, 0f, 0f, 0.6f);
            atkBannerImg.raycastTarget = false;
            var atkBannerRT = atkBannerGO.GetComponent<RectTransform>();
            atkBannerRT.anchorMin = new Vector2(0f, 0f);
            atkBannerRT.anchorMax = new Vector2(1f, 0.2f);
            atkBannerRT.offsetMin = Vector2.zero;
            atkBannerRT.offsetMax = Vector2.zero;

            var atkText = CreateTMPText(atkBannerGO.transform, "AtkText", "0", Color.white, 15, TextAnchor.MiddleCenter);
            atkText.fontStyle = FontStyle.Bold;
            var atkRT = atkText.GetComponent<RectTransform>();
            atkRT.anchorMin = Vector2.zero;
            atkRT.anchorMax = Vector2.one;
            atkRT.offsetMin = Vector2.zero;
            atkRT.offsetMax = Vector2.zero;

            // ArtImage — fills entire card (text overlays on top)
            var artGO = new GameObject("ArtImage");
            artGO.transform.SetParent(root.transform, false);
            var artImg = artGO.AddComponent<Image>();
            artImg.preserveAspect = true; // keep aspect ratio, no distortion
            artImg.color = Color.white;
            artImg.raycastTarget = false;
            var artRT = artGO.GetComponent<RectTransform>();
            artRT.anchorMin = Vector2.zero;
            artRT.anchorMax = Vector2.one;
            artRT.offsetMin = new Vector2(3f, 3f);
            artRT.offsetMax = new Vector2(-3f, -3f);

            // Description text — below art area (small)
            var descText = CreateTMPText(root.transform, "DescText", "", new Color(0.3f, 0.3f, 0.3f, 1f), 8, TextAnchor.UpperCenter);
            var descRT = descText.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.05f, 0.05f);
            descRT.anchorMax = new Vector2(0.95f, 0.2f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            // ── DEV-8: Stunned overlay (fullscreen red tint, starts inactive) ──
            var stunnedGO = new GameObject("StunnedOverlay");
            stunnedGO.transform.SetParent(root.transform, false);
            var stunnedImg = stunnedGO.AddComponent<Image>();
            stunnedImg.color = new Color(1f, 0.3f, 0.3f, 0.3f);
            stunnedImg.raycastTarget = false;
            var stunnedRT = stunnedGO.GetComponent<RectTransform>();
            stunnedRT.anchorMin = Vector2.zero;
            stunnedRT.anchorMax = Vector2.one;
            stunnedRT.offsetMin = Vector2.zero;
            stunnedRT.offsetMax = Vector2.zero;
            stunnedGO.SetActive(false);

            // ── DEV-8: Buff token icon (top-right corner, gold) ──
            var buffGO = new GameObject("BuffTokenIcon");
            buffGO.transform.SetParent(root.transform, false);
            var buffImg = buffGO.AddComponent<Image>();
            buffImg.color = new Color(0.98f, 0.75f, 0.15f, 0.9f);
            buffImg.raycastTarget = false;
            var buffRT = buffGO.GetComponent<RectTransform>();
            buffRT.anchorMin = new Vector2(0.7f, 0.8f);
            buffRT.anchorMax = new Vector2(1f, 1f);
            buffRT.offsetMin = new Vector2(-2f, -2f);
            buffRT.offsetMax = new Vector2(-2f, -2f);
            var buffText = CreateTMPText(buffGO.transform, "BuffText", "+1", Color.white, 10, TextAnchor.MiddleCenter);
            var buffTextRT = buffText.GetComponent<RectTransform>();
            buffTextRT.anchorMin = Vector2.zero;
            buffTextRT.anchorMax = Vector2.one;
            buffTextRT.offsetMin = Vector2.zero;
            buffTextRT.offsetMax = Vector2.zero;
            buffGO.SetActive(false);

            // ── DEV-8: CardGlow (glow border material) ──
            var cardGlow = root.AddComponent<FWTCG.UI.CardGlow>();
            {
                EnsureDirectory("Assets/Materials");
                var glowMat = LoadOrCreateMaterial("Assets/Materials/CardGlowMat.mat", "UI/CardGlow");
                if (glowMat != null)
                {
                    glowMat.SetFloat("_GlowIntensity", 0f);
                    rootImg.material = glowMat;
                }
            }

            // ── DEV-10: CardHoverScale (lightweight hover zoom, replaces CardTilt) ──
            root.AddComponent<FWTCG.UI.CardHoverScale>();

            // Wire CardView serialized fields
            var so = new SerializedObject(cardView);
            so.FindProperty("_nameText").objectReferenceValue  = cardName;
            so.FindProperty("_costText").objectReferenceValue  = costText;
            so.FindProperty("_atkText").objectReferenceValue   = atkText;
            so.FindProperty("_descText").objectReferenceValue  = descText;
            so.FindProperty("_artImage").objectReferenceValue  = artImg;
            so.FindProperty("_cardBg").objectReferenceValue    = rootImg;
            so.FindProperty("_clickButton").objectReferenceValue = root.GetComponent<Button>();
            so.FindProperty("_stunnedOverlay").objectReferenceValue = stunnedImg;
            so.FindProperty("_buffTokenIcon").objectReferenceValue  = buffGO;
            so.FindProperty("_buffTokenText").objectReferenceValue  = buffText;
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
            // ── Kaisa (虚空) deck ─────────────────────────────────────────────
            // noxus_recruit x2
            CD("noxus_recruit",      "诺克萨斯新兵", 4, 4, RuneType.Blazing, 0,
               "鼓舞：其他盟友入场时手牌费用-1（最低0）",
               CardKeyword.Inspire, "noxus_recruit_enter");

            // alert_sentinel x3
            CD("alert_sentinel",     "警觉的哨兵",   2, 2, RuneType.Blazing, 0,
               "绝念：阵亡时摸1张牌",
               CardKeyword.Deathwish, "alert_sentinel_die");

            // yordel_instructor x3
            CD("yordel_instructor",  "约德尔教官",   3, 2, RuneType.Blazing, 0,
               "壁垒。入场：摸1张牌",
               CardKeyword.Barrier, "yordel_instructor_enter");

            // bad_poro x2
            CD("bad_poro",           "坏坏魄罗",     2, 2, RuneType.Blazing, 0,
               "征服：生成1张已横置的「硬币」装备牌",
               CardKeyword.Conquest, "bad_poro_conquer");

            // rengar x2
            CD("rengar",             "雷恩加尔·暴起", 3, 3, RuneType.Blazing, 1,
               "反应。强攻：进攻时额外+2战力",
               CardKeyword.Reactive | CardKeyword.StrongAtk, "");

            // kaisa_hero x1 (hero card — extracted to hero zone at game start)
            CD("kaisa_hero",         "卡莎·九死一生", 4, 4, RuneType.Blazing, 1,
               "急速（支付1炽烈符能进场时为活跃状态）。征服：本回合可额外打出1张牌",
               CardKeyword.Haste | CardKeyword.Conquest, "kaisa_hero_conquer",
               isHero: true);

            // darius x1
            CD("darius",             "德莱厄斯",     5, 5, RuneType.Blazing, 1,
               "入场：若本回合已打出其他牌，+2战力并变为活跃状态",
               CardKeyword.None, "darius_second_card");

            // thousand_tail x3
            CD("thousand_tail",      "千尾监视者",   7, 7, RuneType.Radiant, 1,
               "急速（支付1灵光符能进场时为活跃状态）。入场：所有敌方单位-3战力（最低1）",
               CardKeyword.Haste, "thousand_tail_enter");

            // foresight_mech x2
            CD("foresight_mech",     "先见机甲",     2, 2, RuneType.Blazing, 0,
               "预知：入场时查看牌库顶1张牌，可选择将其置底",
               CardKeyword.Foresight, "foresight_mech_enter");

            // ── MasterYi (伊欧尼亚) deck ──────────────────────────────────────
            // yi_hero x1 (hero card — extracted to hero zone at game start)
            CD("yi_hero",            "易·锋芒毕现",  7, 6, RuneType.Crushing, 1,
               "游走。急速（支付1摧破符能进场时为活跃状态）",
               CardKeyword.Roam | CardKeyword.Haste, "",
               isHero: true);

            // jax x2
            CD("jax",                "贾克斯·万般皆武", 5, 5, RuneType.Verdant, 1,
               "法盾（敌方法术需额外1符能才能选中）。入场：手牌装备获得反应关键词",
               CardKeyword.SpellShield, "jax_enter");

            // tiyana_warden x2
            CD("tiyana_warden",      "缇亚娜·冕卫",  7, 4, RuneType.Verdant, 2,
               "法盾。在场时对手无法获得据守分",
               CardKeyword.SpellShield, "tiyana_enter");

            // wailing_poro x3
            CD("wailing_poro",       "哀哀魄罗",     2, 2, RuneType.Verdant, 0,
               "绝念：独自阵亡时摸1张牌",
               CardKeyword.Deathwish, "wailing_poro_die");

            // sandshoal_deserter x2
            CD("sandshoal_deserter", "沙塔啸匪",     6, 5, RuneType.Verdant, 0,
               "无法被敌方法术或技能选中",
               CardKeyword.SpellShield, "");

            // ── Yi Equipment ──────────────────────────────────────────────────
            CD("zhonya",             "中娅沙漏",     2, 0, RuneType.Verdant, 0,
               "待命（可以面朝下，0费用反应）。保护附着单位免于阵亡，改为休眠返回基地",
               CardKeyword.Standby | CardKeyword.Reactive, "",
               isEquipment: true, equipAtkBonus: 0,
               equipRuneType: RuneType.Verdant, equipRuneCost: 0);

            CD("trinity_force",      "三相之力",     4, 0, RuneType.Crushing, 0,
               "附着单位据守时额外+1分。+2战力",
               CardKeyword.None, "trinity_equip",
               isEquipment: true, equipAtkBonus: 2,
               equipRuneType: RuneType.Crushing, equipRuneCost: 1);

            CD("guardian_angel",     "守护天使",     2, 0, RuneType.Verdant, 0,
               "附着单位阵亡时改为休眠返回基地。+1战力",
               CardKeyword.None, "guardian_equip",
               isEquipment: true, equipAtkBonus: 1,
               equipRuneType: RuneType.Verdant, equipRuneCost: 1);

            CD("dorans_blade",       "多兰之刃",     2, 0, RuneType.Crushing, 0,
               "+2战力",
               CardKeyword.None, "dorans_equip",
               isEquipment: true, equipAtkBonus: 2,
               equipRuneType: RuneType.Crushing, equipRuneCost: 1);

            // ── Kaisa spells ──────────────────────────────────────────────────
            CDS("hex_ray",        "虚空射线",   1, RuneType.Blazing, 1,
                "急速。对目标敌方单位造成3点伤害",
                SpellTargetType.EnemyUnit, "hex_ray", CardKeyword.Haste);

            CDS("void_seek",      "虚空追迹",   3, RuneType.Blazing, 1,
                "急速。对目标敌方单位造成4点伤害，摸1张牌",
                SpellTargetType.EnemyUnit, "void_seek", CardKeyword.Haste);

            CDS("stardrop",       "星陨",       2, RuneType.Blazing, 2,
                "对目标敌方单位造成3点伤害两次（共6点）",
                SpellTargetType.EnemyUnit, "stardrop");

            CDS("starburst",      "星爆",       6, RuneType.Radiant, 2,
                "对目标敌方单位造成6点伤害（原为2目标，DEV-3简化）",
                SpellTargetType.EnemyUnit, "starburst");

            CDS("evolve_day",     "进化之日",   6, RuneType.Radiant, 1,
                "摸4张牌",
                SpellTargetType.None, "evolve_day");

            CDS("akasi_storm",    "阿卡希狂暴", 7, RuneType.Radiant, 2,
                "对随机敌方单位造成2点伤害，共6次",
                SpellTargetType.None, "akasi_storm");

            // ── Yi spells ─────────────────────────────────────────────────────
            CDS("rally_call",     "集结号令",   2, RuneType.Verdant, 0,
                "急速。所有己方单位进入活跃状态，摸1张牌",
                SpellTargetType.None, "rally_call", CardKeyword.Haste);

            CDS("balance_resolve","平衡意志",   3, RuneType.Verdant, 0,
                "急速。摸1张牌，召出1张符文",
                SpellTargetType.None, "balance_resolve", CardKeyword.Haste);

            CDS("slam",           "冲击",       2, RuneType.Crushing, 0,
                "急速+回响。使目标敌方单位眩晕（法盾可抵消）",
                SpellTargetType.EnemyUnit, "slam", CardKeyword.Haste | CardKeyword.Echo);

            CDS("strike_ask_later","先斩后奏",  1, RuneType.Crushing, 2,
                "急速。使目标己方单位本回合+5战力",
                SpellTargetType.FriendlyUnit, "strike_ask_later", CardKeyword.Haste);

            // ── Kaisa reactive spells ─────────────────────────────────────────
            CDS("swindle",        "诡计",       1, RuneType.Blazing, 1,
                "反应。目标敌方单位本回合-1战力，摸1张牌",
                SpellTargetType.None, "swindle", CardKeyword.Reactive);

            CDS("retreat_rune",   "撤退符文",   1, RuneType.Blazing, 1,
                "反应。召回一个己方战场单位（休眠），回收一张符文获得1符能",
                SpellTargetType.None, "retreat_rune", CardKeyword.Reactive);

            CDS("guilty_pleasure","罪恶乐趣",   2, RuneType.Blazing, 0,
                "反应。弃置一张手牌，对目标敌方单位造成2点伤害",
                SpellTargetType.None, "guilty_pleasure", CardKeyword.Reactive);

            CDS("smoke_bomb",     "烟雾弹",     1, RuneType.Radiant, 1,
                "反应。目标敌方单位本回合-4战力",
                SpellTargetType.None, "smoke_bomb", CardKeyword.Reactive);

            // ── Yi reactive spells ────────────────────────────────────────────
            CDS("scoff",          "嘲讽",       1, RuneType.Verdant, 1,
                "反应。无效化费用≤4的法术",
                SpellTargetType.None, "scoff", CardKeyword.Reactive);

            CDS("duel_stance",    "决斗姿态",   1, RuneType.Verdant, 1,
                "反应。己方一个单位永久获得+1/+1",
                SpellTargetType.None, "duel_stance", CardKeyword.Reactive);

            CDS("well_trained",   "精英训练",   2, RuneType.Verdant, 0,
                "反应。己方一个单位本回合+2战力，摸1张牌",
                SpellTargetType.None, "well_trained", CardKeyword.Reactive);

            CDS("wind_wall",      "风墙",       2, RuneType.Verdant, 0,
                "反应。无效化任意法术",
                SpellTargetType.None, "wind_wall", CardKeyword.Reactive);

            CDS("flash_counter",  "闪电反制",   1, RuneType.Crushing, 1,
                "反应。反制一个敌方法术",
                SpellTargetType.None, "flash_counter", CardKeyword.Reactive);

            // ── DEV-10: Legend CardData (for display only, not in decks) ──────
            CD("kaisa_legend",       "卡莎·虚空之女", 0, 0, RuneType.Blazing, 0,
               "主动：虚空感知（反应，休眠自身+1炽烈符能）\n被动：进化（盟友4种关键词→Lv.2 +3/+3）");

            CD("yi_legend",          "易大师·无极剑圣", 0, 0, RuneType.Crushing, 0,
               "被动：独影剑鸣（防守单位仅1名时+2战力）");
        }

        // Shorthand alias for spell cards
        private static CardData CDS(string id, string name, int cost,
            RuneType runeType, int runeCost, string desc,
            SpellTargetType targetType, string effectId,
            CardKeyword kw = CardKeyword.None)
        {
            return CreateCardData(id, name, cost, 0, runeType, runeCost, desc, kw, effectId,
                isSpell: true, spellTargetType: targetType);
        }

        /// <summary>
        /// Sets TextureImporterType.Sprite on every PNG/JPG in Assets/Resources/CardArt/.
        /// Uses Directory.GetFiles so it works even on first import (no .meta yet).
        /// </summary>
        private static void EnsureCardArtImportedAsSprite()
        {
            string artFolder = "Assets/Resources/CardArt";
            if (!System.IO.Directory.Exists(artFolder)) return;

            var files = new System.Collections.Generic.List<string>();
            files.AddRange(System.IO.Directory.GetFiles(artFolder, "*.png"));
            files.AddRange(System.IO.Directory.GetFiles(artFolder, "*.jpg"));

            foreach (string fullPath in files)
            {
                // Normalise to forward-slash Unity asset path
                string assetPath = fullPath.Replace('\\', '/');
                if (!assetPath.StartsWith("Assets/"))
                {
                    // Strip absolute prefix up to "Assets/"
                    int idx = assetPath.IndexOf("Assets/");
                    if (idx >= 0) assetPath = assetPath.Substring(idx);
                    else continue;
                }

                // Force-import so the asset database knows about the file
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType         = TextureImporterType.Sprite;
                    importer.spriteImportMode    = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    Debug.Log($"[SceneBuilder] 设为Sprite: {assetPath}");
                }
            }

            AssetDatabase.Refresh();
        }

        private static CardData CD(string id, string name, int cost, int atk,
            RuneType runeType, int runeCost, string desc,
            CardKeyword kw = CardKeyword.None, string effectId = "",
            bool isEquipment = false, int equipAtkBonus = 0,
            RuneType equipRuneType = RuneType.Blazing, int equipRuneCost = 0,
            bool isHero = false)
        {
            return CreateCardData(id, name, cost, atk, runeType, runeCost, desc,
                                  kw, effectId, isEquipment, equipAtkBonus, equipRuneType, equipRuneCost,
                                  isHero: isHero);
        }

        private static CardData CreateCardData(string id, string cardName,
            int cost, int atk, RuneType runeType, int runeCost, string description,
            CardKeyword keywords = CardKeyword.None, string effectId = "",
            bool isEquipment = false, int equipAtkBonus = 0,
            RuneType equipRuneType = RuneType.Blazing, int equipRuneCost = 0,
            bool isSpell = false, SpellTargetType spellTargetType = SpellTargetType.None,
            bool isHero = false)
        {
            string path = $"Assets/Resources/Cards/{id}.asset";

            CardData data = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<CardData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.EditorSetup(id, cardName, cost, atk, runeType, runeCost, description,
                             keywords, effectId, isEquipment, equipAtkBonus, equipRuneType, equipRuneCost,
                             isSpell, spellTargetType, isHero);

            // Assign art sprite from Resources/CardArt/{id}.png if it exists
            string[] exts = { ".png", ".jpg" };
            foreach (string ext in exts)
            {
                string artPath = $"Assets/Resources/CardArt/{id}{ext}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(artPath);
                if (sprite != null)
                {
                    var so = new SerializedObject(data);
                    so.FindProperty("_artSprite").objectReferenceValue = sprite;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
            }

            EditorUtility.SetDirty(data);
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
            Button endTurnButton,
            Transform messageContainer,
            Text messageTextPrefab,
            GameObject gameOverPanel,
            Text gameOverText,
            Button restartButton,
            GameObject bannerPanel, Text bannerText,
            Text playerLegendText, Text enemyLegendText, Button legendSkillBtn,
            // DEV-9 additions
            Image[] playerScoreCircleImages, Image[] enemyScoreCircleImages,
            Text playerDeckCountText, Text enemyDeckCountText,
            Text playerRunePileCountText, Text enemyRunePileCountText,
            Text playerDiscardCountText, Text enemyDiscardCountText,
            Text playerExileCountText, Text enemyExileCountText,
            Image bf1CtrlBadge, Image bf2CtrlBadge,
            Text bf1CtrlBadgeText, Text bf2CtrlBadgeText,
            Transform playerHeroContainer, Transform enemyHeroContainer,
            Button tapAllRunesBtn, Button cancelRunesBtn,
            Button confirmRunesBtn, Button skipReactionBtn,
            Text playerRuneInfoText, Text enemyRuneInfoText,
            Text playerDeckInfoText, Text enemyDeckInfoText,
            // DEV-10 additions
            Transform playerLegendContainer, Transform enemyLegendContainer,
            GameObject logPanel, Button logToggleBtn, Text logToggleText,
            RectTransform boardWrapperOuter,
            GameObject viewerPanel, Text viewerTitle, Transform viewerCardContainer, Button viewerCloseBtn,
            GameObject timerDisplay, Image timerFill, Text timerText,
            RectTransform playerHandZoneRT, RectTransform enemyHandZoneRT)
        {
            var so = new SerializedObject(gameUI);

            so.FindProperty("_playerScoreText").objectReferenceValue  = playerScoreText;
            so.FindProperty("_enemyScoreText").objectReferenceValue   = enemyScoreText;
            so.FindProperty("_roundPhaseText").objectReferenceValue   = roundPhaseText;

            so.FindProperty("_playerManaText").objectReferenceValue   = playerManaText;
            so.FindProperty("_playerSchText").objectReferenceValue    = playerSchText;

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

            so.FindProperty("_playerRuneContainer").objectReferenceValue  = playerRuneContainer;
            so.FindProperty("_enemyRuneContainer").objectReferenceValue   = enemyRuneContainer;
            so.FindProperty("_runeButtonPrefab").objectReferenceValue     = runePrefab;

            so.FindProperty("_endTurnButton").objectReferenceValue        = endTurnButton;
            var endTurnLabel = endTurnButton.GetComponentInChildren<UnityEngine.UI.Text>();
            so.FindProperty("_endTurnLabel").objectReferenceValue         = endTurnLabel;

            so.FindProperty("_messageContainer").objectReferenceValue     = messageContainer;
            so.FindProperty("_messageTextPrefab").objectReferenceValue    = messageTextPrefab;

            so.FindProperty("_gameOverPanel").objectReferenceValue        = gameOverPanel;
            so.FindProperty("_gameOverText").objectReferenceValue         = gameOverText;
            so.FindProperty("_restartButton").objectReferenceValue        = restartButton;

            so.FindProperty("_bannerPanel").objectReferenceValue          = bannerPanel;
            so.FindProperty("_bannerText").objectReferenceValue           = bannerText;

            // Legend zone
            so.FindProperty("_playerLegendText").objectReferenceValue     = playerLegendText;
            so.FindProperty("_enemyLegendText").objectReferenceValue      = enemyLegendText;
            so.FindProperty("_legendSkillBtn").objectReferenceValue       = legendSkillBtn;

            // ── DEV-9: Score track circles ──
            var pScoreProp = so.FindProperty("_playerScoreCircles");
            pScoreProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
                pScoreProp.GetArrayElementAtIndex(i).objectReferenceValue = playerScoreCircleImages[i];

            var eScoreProp = so.FindProperty("_enemyScoreCircles");
            eScoreProp.arraySize = 9;
            for (int i = 0; i < 9; i++)
                eScoreProp.GetArrayElementAtIndex(i).objectReferenceValue = enemyScoreCircleImages[i];

            // ── DEV-9: Pile count texts ──
            so.FindProperty("_playerDeckCountText").objectReferenceValue     = playerDeckCountText;
            so.FindProperty("_enemyDeckCountText").objectReferenceValue      = enemyDeckCountText;
            so.FindProperty("_playerRunePileCountText").objectReferenceValue = playerRunePileCountText;
            so.FindProperty("_enemyRunePileCountText").objectReferenceValue  = enemyRunePileCountText;
            so.FindProperty("_playerDiscardCountText").objectReferenceValue  = playerDiscardCountText;
            so.FindProperty("_enemyDiscardCountText").objectReferenceValue   = enemyDiscardCountText;
            so.FindProperty("_playerExileCountText").objectReferenceValue    = playerExileCountText;
            so.FindProperty("_enemyExileCountText").objectReferenceValue     = enemyExileCountText;

            // ── DEV-9: BF control badges ──
            so.FindProperty("_bf1CtrlBadge").objectReferenceValue      = bf1CtrlBadge;
            so.FindProperty("_bf2CtrlBadge").objectReferenceValue      = bf2CtrlBadge;
            so.FindProperty("_bf1CtrlBadgeText").objectReferenceValue  = bf1CtrlBadgeText;
            so.FindProperty("_bf2CtrlBadgeText").objectReferenceValue  = bf2CtrlBadgeText;

            // ── DEV-9: Hero zones ──
            so.FindProperty("_playerHeroContainer").objectReferenceValue = playerHeroContainer;
            so.FindProperty("_enemyHeroContainer").objectReferenceValue  = enemyHeroContainer;

            // ── DEV-9: Action buttons ──
            so.FindProperty("_tapAllRunesBtn").objectReferenceValue   = tapAllRunesBtn;
            so.FindProperty("_cancelRunesBtn").objectReferenceValue   = cancelRunesBtn;
            so.FindProperty("_confirmRunesBtn").objectReferenceValue  = confirmRunesBtn;
            so.FindProperty("_skipReactionBtn").objectReferenceValue  = skipReactionBtn;

            // ── DEV-9: Info strip texts ──
            so.FindProperty("_playerRuneInfoText").objectReferenceValue = playerRuneInfoText;
            so.FindProperty("_enemyRuneInfoText").objectReferenceValue  = enemyRuneInfoText;
            so.FindProperty("_playerDeckInfoText").objectReferenceValue = playerDeckInfoText;
            so.FindProperty("_enemyDeckInfoText").objectReferenceValue  = enemyDeckInfoText;

            // ── DEV-10: Legend containers ──
            if (playerLegendContainer != null)
                so.FindProperty("_playerLegendContainer").objectReferenceValue = playerLegendContainer;
            if (enemyLegendContainer != null)
                so.FindProperty("_enemyLegendContainer").objectReferenceValue = enemyLegendContainer;

            // ── DEV-10: Log panel toggle ──
            so.FindProperty("_logPanel").objectReferenceValue        = logPanel;
            so.FindProperty("_logToggleBtn").objectReferenceValue    = logToggleBtn;
            so.FindProperty("_logToggleText").objectReferenceValue   = logToggleText;
            so.FindProperty("_boardWrapperOuter").objectReferenceValue = boardWrapperOuter;

            // ── DEV-10: Viewer ──
            so.FindProperty("_viewerPanel").objectReferenceValue        = viewerPanel;
            so.FindProperty("_viewerTitle").objectReferenceValue        = viewerTitle;
            so.FindProperty("_viewerCardContainer").objectReferenceValue = viewerCardContainer;
            so.FindProperty("_viewerCloseBtn").objectReferenceValue     = viewerCloseBtn;

            // ── DEV-10: Timer ──
            so.FindProperty("_timerDisplay").objectReferenceValue = timerDisplay;
            so.FindProperty("_timerFill").objectReferenceValue    = timerFill;
            so.FindProperty("_timerText").objectReferenceValue    = timerText;

            // ── DEV-10: Hand zone RTs (for log toggle animation) ──
            so.FindProperty("_playerHandZoneRT").objectReferenceValue = playerHandZoneRT;
            so.FindProperty("_enemyHandZoneRT").objectReferenceValue  = enemyHandZoneRT;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Wire GameManager ──────────────────────────────────────────────────

        private static void WireGameManager(
            FWTCG.GameManager gameMgr,
            FWTCG.Systems.TurnManager turnMgr,
            FWTCG.Systems.CombatSystem combatSys,
            FWTCG.Systems.ScoreManager scoreMgr,
            FWTCG.AI.SimpleAI simpleAI,
            FWTCG.UI.GameUI gameUI,
            FWTCG.Systems.EntryEffectSystem entryEffects,
            FWTCG.Systems.DeathwishSystem deathwish,
            FWTCG.Systems.SpellSystem spellSys,
            FWTCG.Systems.ReactiveSystem reactiveSys,
            FWTCG.UI.StartupFlowUI startupFlowUI,
            FWTCG.UI.ReactiveWindowUI reactiveWindowUI,
            GameObject coinFlipPanel, Text coinFlipText, Button coinFlipOkButton,
            GameObject mulliganPanel, Text mulliganTitleText, Transform mulliganCardContainer,
            Button mulliganConfirmButton, Text mulliganConfirmLabel, GameObject cardPrefab,
            GameObject reactivePanel, Text reactiveContextText,
            Transform reactiveCardContainer, Button reactBtn,
            FWTCG.Systems.LegendSystem legendSys, Button legendSkillBtn,
            FWTCG.Systems.BattlefieldSystem bfSys,
            Button debugSpellBtn, Button debugEquipBtn, Button debugUnitBtn, Button debugReactiveBtn, Button debugManaBtn,
            Button tapAllRunesBtn = null, Button skipReactionBtn = null)
        {
            var so = new SerializedObject(gameMgr);
            so.FindProperty("_turnMgr").objectReferenceValue        = turnMgr;
            so.FindProperty("_combatSys").objectReferenceValue      = combatSys;
            so.FindProperty("_scoreMgr").objectReferenceValue       = scoreMgr;
            so.FindProperty("_ai").objectReferenceValue             = simpleAI;
            so.FindProperty("_ui").objectReferenceValue             = gameUI;
            so.FindProperty("_entryEffects").objectReferenceValue   = entryEffects;
            so.FindProperty("_spellSys").objectReferenceValue       = spellSys;
            so.FindProperty("_startupFlowUI").objectReferenceValue  = startupFlowUI;
            so.FindProperty("_reactiveSys").objectReferenceValue    = reactiveSys;
            so.FindProperty("_reactiveWindowUI").objectReferenceValue = reactiveWindowUI;

            // Wire debug buttons
            so.FindProperty("_debugSpellBtn").objectReferenceValue    = debugSpellBtn;
            so.FindProperty("_debugEquipBtn").objectReferenceValue    = debugEquipBtn;
            so.FindProperty("_debugUnitBtn").objectReferenceValue     = debugUnitBtn;
            so.FindProperty("_debugReactiveBtn").objectReferenceValue = debugReactiveBtn;
            so.FindProperty("_debugManaBtn").objectReferenceValue     = debugManaBtn;

            // Wire StartupFlowUI panels
            var startupSO = new SerializedObject(startupFlowUI);
            startupSO.FindProperty("_coinFlipPanel").objectReferenceValue      = coinFlipPanel;
            startupSO.FindProperty("_coinFlipText").objectReferenceValue       = coinFlipText;
            startupSO.FindProperty("_coinFlipOkButton").objectReferenceValue   = coinFlipOkButton;
            startupSO.FindProperty("_mulliganPanel").objectReferenceValue      = mulliganPanel;
            startupSO.FindProperty("_mulliganTitleText").objectReferenceValue  = mulliganTitleText;
            startupSO.FindProperty("_mulliganCardContainer").objectReferenceValue = mulliganCardContainer;
            startupSO.FindProperty("_cardViewPrefab").objectReferenceValue     = cardPrefab;
            startupSO.FindProperty("_mulliganConfirmButton").objectReferenceValue = mulliganConfirmButton;
            startupSO.FindProperty("_mulliganConfirmLabel").objectReferenceValue  = mulliganConfirmLabel;
            startupSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire ReactiveWindowUI panels
            var reactiveWindowSO = new SerializedObject(reactiveWindowUI);
            reactiveWindowSO.FindProperty("_panel").objectReferenceValue       = reactivePanel;
            reactiveWindowSO.FindProperty("_contextText").objectReferenceValue = reactiveContextText;
            reactiveWindowSO.FindProperty("_cardContainer").objectReferenceValue = reactiveCardContainer;
            reactiveWindowSO.FindProperty("_cardViewPrefab").objectReferenceValue = cardPrefab;
            reactiveWindowSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire React button and Legend skill button into GameManager
            so.FindProperty("_reactBtn").objectReferenceValue        = reactBtn;
            so.FindProperty("_legendSkillBtn").objectReferenceValue  = legendSkillBtn;

            // Wire DEV-9 action buttons into GameManager
            if (tapAllRunesBtn != null)
                so.FindProperty("_tapAllRunesBtn").objectReferenceValue   = tapAllRunesBtn;
            if (skipReactionBtn != null)
                so.FindProperty("_skipReactionBtn").objectReferenceValue  = skipReactionBtn;

            // Wire LegendSystem + BattlefieldSystem into GameManager
            so.FindProperty("_legendSys").objectReferenceValue       = legendSys;
            so.FindProperty("_bfSys").objectReferenceValue           = bfSys;

            // Wire DeathwishSystem + LegendSystem + BattlefieldSystem into CombatSystem
            var combatSO = new SerializedObject(combatSys);
            combatSO.FindProperty("_deathwish").objectReferenceValue  = deathwish;
            combatSO.FindProperty("_legendSys").objectReferenceValue  = legendSys;
            combatSO.FindProperty("_bfSys").objectReferenceValue      = bfSys;
            combatSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire BattlefieldSystem into ScoreManager
            var scoreSO = new SerializedObject(scoreMgr);
            scoreSO.FindProperty("_bfSys").objectReferenceValue       = bfSys;
            scoreSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire BattlefieldSystem into SpellSystem
            var spellSO = new SerializedObject(spellSys);
            spellSO.FindProperty("_bfSys").objectReferenceValue       = bfSys;
            spellSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire CardData arrays — full decks (unique card types; copies handled at runtime)
            var kaisaCards = new CardData[]
            {
                LoadCard("noxus_recruit"),
                LoadCard("alert_sentinel"),
                LoadCard("yordel_instructor"),
                LoadCard("bad_poro"),
                LoadCard("rengar"),
                LoadCard("kaisa_hero"),
                LoadCard("darius"),
                LoadCard("thousand_tail"),
                LoadCard("foresight_mech"),
                // Kaisa non-reactive spells
                LoadCard("hex_ray"),
                LoadCard("void_seek"),
                LoadCard("stardrop"),
                LoadCard("starburst"),
                LoadCard("evolve_day"),
                LoadCard("akasi_storm"),
                // Kaisa reactive spells (DEV-4)
                LoadCard("swindle"),
                LoadCard("retreat_rune"),
                LoadCard("guilty_pleasure"),
                LoadCard("smoke_bomb"),
            };
            var yiCards = new CardData[]
            {
                LoadCard("yi_hero"),
                LoadCard("jax"),
                LoadCard("tiyana_warden"),
                LoadCard("wailing_poro"),
                LoadCard("sandshoal_deserter"),
                LoadCard("zhonya"),
                LoadCard("trinity_force"),
                LoadCard("guardian_angel"),
                LoadCard("dorans_blade"),
                // Yi non-reactive spells
                LoadCard("rally_call"),
                LoadCard("balance_resolve"),
                LoadCard("slam"),
                LoadCard("strike_ask_later"),
                // Yi reactive spells (DEV-4)
                LoadCard("scoff"),
                LoadCard("duel_stance"),
                LoadCard("well_trained"),
                LoadCard("wind_wall"),
                LoadCard("flash_counter"),
            };

            var kaisaProp = so.FindProperty("_kaisaDeck");
            kaisaProp.arraySize = kaisaCards.Length;
            for (int i = 0; i < kaisaCards.Length; i++)
                kaisaProp.GetArrayElementAtIndex(i).objectReferenceValue = kaisaCards[i];

            var yiProp = so.FindProperty("_yiDeck");
            yiProp.arraySize = yiCards.Length;
            for (int i = 0; i < yiCards.Length; i++)
                yiProp.GetArrayElementAtIndex(i).objectReferenceValue = yiCards[i];

            // ── DEV-10: Legend CardData for display ──
            var kaisaLegend = LoadCard("kaisa_legend");
            var yiLegend = LoadCard("yi_legend");
            if (kaisaLegend != null) so.FindProperty("_kaisaLegendData").objectReferenceValue = kaisaLegend;
            if (yiLegend != null)    so.FindProperty("_yiLegendData").objectReferenceValue    = yiLegend;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // ── DEV-10: Viewer Panel (discard/exile card browser) ────────────────

        private static GameObject CreateViewerPanel(Transform parent,
            out Text titleText, out Transform cardContainer, out Button closeBtn)
        {
            var go = CreateFullscreenPanel(parent, "ViewerPanel", new Color(0f, 0f, 0f, 0.85f));
            go.SetActive(false);

            // Title
            var titleGO = new GameObject("ViewerTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.1f, 0.88f);
            titleRT.anchorMax = new Vector2(0.9f, 0.96f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;
            titleText = titleGO.AddComponent<Text>();
            titleText.text = "弃牌堆 (0)";
            titleText.color = GameColors.GoldLight;
            titleText.fontSize = 22;
            titleText.alignment = TextAnchor.MiddleCenter;
            if (_font != null) titleText.font = _font;

            // Scroll view with grid
            var scrollGO = new GameObject("ViewerScroll");
            scrollGO.transform.SetParent(go.transform, false);
            var scrollRT = scrollGO.AddComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.1f, 0.1f);
            scrollRT.anchorMax = new Vector2(0.9f, 0.86f);
            scrollRT.offsetMin = Vector2.zero;
            scrollRT.offsetMax = Vector2.zero;

            var contentGO = new GameObject("ViewerContent");
            contentGO.transform.SetParent(scrollGO.transform, false);
            var contentRT = contentGO.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = new Vector2(0f, 600f);

            var glg = contentGO.AddComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(90f, 130f);
            glg.spacing = new Vector2(8f, 8f);
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 8;
            glg.childAlignment = TextAnchor.UpperCenter;

            var csf = contentGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollView = scrollGO.AddComponent<ScrollRect>();
            scrollView.content = contentRT;
            scrollView.horizontal = false;
            scrollView.vertical = true;
            scrollView.movementType = ScrollRect.MovementType.Clamped;
            var scrollImg = scrollGO.AddComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollView.viewport = scrollRT;

            cardContainer = contentGO.transform;

            // Close button
            var closeBtnGO = new GameObject("ViewerClose");
            closeBtnGO.transform.SetParent(go.transform, false);
            var closeRT = closeBtnGO.AddComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0.85f, 0.88f);
            closeRT.anchorMax = new Vector2(0.95f, 0.96f);
            closeRT.offsetMin = Vector2.zero;
            closeRT.offsetMax = Vector2.zero;
            var closeImg = closeBtnGO.AddComponent<Image>();
            closeImg.color = new Color(0.6f, 0.15f, 0.15f, 1f);
            closeBtn = closeBtnGO.AddComponent<Button>();

            var closeLabel = new GameObject("CloseLabel");
            closeLabel.transform.SetParent(closeBtnGO.transform, false);
            var closeLabelRT = closeLabel.AddComponent<RectTransform>();
            closeLabelRT.anchorMin = Vector2.zero;
            closeLabelRT.anchorMax = Vector2.one;
            closeLabelRT.offsetMin = Vector2.zero;
            closeLabelRT.offsetMax = Vector2.zero;
            var closeLabelTxt = closeLabel.AddComponent<Text>();
            closeLabelTxt.text = "X";
            closeLabelTxt.color = Color.white;
            closeLabelTxt.fontSize = 18;
            closeLabelTxt.alignment = TextAnchor.MiddleCenter;
            if (_font != null) closeLabelTxt.font = _font;

            return go;
        }

        // ── DEV-10: Timer display ────────────────────────────────────────────

        private static GameObject CreateTimerDisplay(Transform parent,
            out Image fillImage, out Text timerText)
        {
            var go = new GameObject("TimerDisplay");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(10f, 86f);
            rt.sizeDelta = new Vector2(50f, 50f);

            // Background circle
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            // Fill circle (radial fill)
            var fillGO = new GameObject("TimerFill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0.1f, 0.1f);
            fillRT.anchorMax = new Vector2(0.9f, 0.9f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            fillImage = fillGO.AddComponent<Image>();
            fillImage.color = GameColors.PlayerGreen;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            fillImage.fillAmount = 1f;

            // Timer text
            var textGO = new GameObject("TimerText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            timerText = textGO.AddComponent<Text>();
            timerText.text = "30";
            timerText.color = Color.white;
            timerText.fontSize = 16;
            timerText.alignment = TextAnchor.MiddleCenter;
            if (_font != null) timerText.font = _font;

            return go;
        }

        // ── DEV-10: Zone label helper ────────────────────────────────────────

        private static void AddZoneLabel(Transform zone, string labelText)
        {
            if (zone == null) return;

            var labelGO = new GameObject("ZoneLabel");
            labelGO.transform.SetParent(zone, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(0.5f, 1f);
            labelRT.pivot = new Vector2(0f, 1f);
            labelRT.anchoredPosition = new Vector2(3f, -2f);
            labelRT.sizeDelta = new Vector2(100f, 16f);

            var txt = labelGO.AddComponent<Text>();
            txt.text = labelText;
            txt.color = new Color(GameColors.GoldLight.r, GameColors.GoldLight.g, GameColors.GoldLight.b, 0.7f);
            txt.fontSize = 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.UpperLeft;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) txt.font = _font;

            // Add shadow for readability
            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        // ── DEV-10: Zone border helper ───────────────────────────────────────

        private static void AddZoneBorder(Transform zone, Color color)
        {
            if (zone == null) return;

            // Use a more visible outline (2px, higher alpha)
            var outline = zone.gameObject.GetComponent<Outline>();
            if (outline == null) outline = zone.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

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

        /// <summary>
        /// Load an existing material asset, or create a new one from shader if not found.
        /// Handles batch mode (-nographics) where Shader.Find returns null by falling back
        /// to already-saved .mat assets from a previous editor-mode build.
        /// </summary>
        private static Material LoadOrCreateMaterial(string assetPath, string shaderName)
        {
            // Try loading existing asset first (works even in -nographics batch mode)
            var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (existing != null) return existing;

            // Try creating from shader (only works with GPU available)
            var shader = Shader.Find(shaderName);
            if (shader != null)
            {
                var mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, assetPath);
                return mat;
            }

            Debug.LogWarning($"[SceneBuilder] Cannot find shader '{shaderName}' and no existing material at '{assetPath}'. Skipping.");
            return null;
        }

        // ── URP Post Processing Setup (DEV-8) ─────────────────────────────────

        private static void SetupPostProcessing(GameObject cameraGO)
        {
            // Enable post-processing on camera
            var urpCamData = cameraGO.GetComponent<UniversalAdditionalCameraData>();
            if (urpCamData == null)
                urpCamData = cameraGO.AddComponent<UniversalAdditionalCameraData>();
            urpCamData.renderPostProcessing = true;

            // Create Volume Profile asset
            EnsureDirectory("Assets/Settings");
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Bloom
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.value = 1.2f;
            bloom.intensity.value = 0.8f;

            // Color Adjustments
            var colorAdj = profile.Add<ColorAdjustments>(true);
            colorAdj.postExposure.value = 0.1f;
            colorAdj.contrast.value = 10f;

            // Vignette
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.value = 0.3f;

            // Film Grain
            var filmGrain = profile.Add<FilmGrain>(true);
            filmGrain.intensity.value = 0.1f;

            string profilePath = "Assets/Settings/PostProcessProfile.asset";
            AssetDatabase.CreateAsset(profile, profilePath);

            // Create Volume GameObject
            var volumeGO = new GameObject("PostProcessVolume");
            var volume = volumeGO.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
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
