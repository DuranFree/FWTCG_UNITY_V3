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

                // CSS: .hand-zone bg linear-gradient(135deg, rgba(3,14,25,0.85), rgba(1,10,19,0.9))
                var ehImg = enemyHandZone.AddComponent<Image>();
                ehImg.color = new Color(2f/255f, 12f/255f, 22f/255f, 0.87f);

                var ehHLG = enemyHandZone.AddComponent<HorizontalLayoutGroup>();
                ehHLG.childControlWidth = false;
                ehHLG.childControlHeight = true;
                ehHLG.childForceExpandWidth = false;
                ehHLG.childForceExpandHeight = true;
                ehHLG.childAlignment = TextAnchor.MiddleCenter;
                ehHLG.spacing = 4f; // CSS: gap 4px
                ehHLG.padding = new RectOffset(10, 10, 4, 4); // CSS: padding 6px 10px
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
                out var boardEnemyLegendText,
                out var bf1CardArt, out var bf2CardArt,
                out var bf1Glow, out var bf2Glow);

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

                // CSS: .hand-zone bg + border
                var phImg = playerHandZone.AddComponent<Image>();
                phImg.color = new Color(2f/255f, 12f/255f, 22f/255f, 0.87f);
                var phOutline = playerHandZone.AddComponent<Outline>();
                phOutline.effectColor = new Color(200f/255f, 170f/255f, 110f/255f, 0.2f);
                phOutline.effectDistance = new Vector2(1f, -1f);

                var phHLG = playerHandZone.AddComponent<HorizontalLayoutGroup>();
                phHLG.childControlWidth = false;
                phHLG.childControlHeight = true;
                phHLG.childForceExpandWidth = false;
                phHLG.childForceExpandHeight = true;
                phHLG.childAlignment = TextAnchor.MiddleCenter;
                phHLG.spacing = 4f; // CSS: gap 4px
                phHLG.padding = new RectOffset(10, 10, 6, 4); // CSS: padding 6px 10px
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

            // ── EventBanner (DEV-18b) ─────────────────────────────────────────
            var eventBannerGO = CreateEventBannerPanel(canvasGO.transform);

            // ── SpellShowcasePanel (DEV-16) ───────────────────────────────────
            var spellShowcaseGO = CreateSpellShowcasePanel(canvasGO.transform);

            // ── SpellTargetPopup (DEV-16b) ────────────────────────────────────
            var spellTargetPopupGO = CreateSpellTargetPopup(canvasGO.transform);

            // ── CombatResultPanel (DEV-10: shows power comparison after combat) ──
            var combatResultPanel = CreateCombatResultPanel(canvasGO.transform,
                out var crAttackerText, out var crDefenderText,
                out var crVsText, out var crOutcomeText, out var crBfNameText);

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

            // ── DEV-18: BoardFlashOverlay ─────────────────────────────────────
            // Full-screen transparent Image that briefly flashes when a card is played.
            var boardFlashGO = new GameObject("BoardFlashOverlay");
            boardFlashGO.transform.SetParent(canvasGO.transform, false);
            var boardFlashRT = boardFlashGO.AddComponent<RectTransform>();
            boardFlashRT.anchorMin = Vector2.zero;
            boardFlashRT.anchorMax = Vector2.one;
            boardFlashRT.offsetMin = Vector2.zero;
            boardFlashRT.offsetMax = Vector2.zero;
            var boardFlashImg = boardFlashGO.AddComponent<Image>();
            boardFlashImg.color = new Color(0.78f, 0.67f, 0.43f, 0f); // starts fully transparent
            boardFlashImg.raycastTarget = false;

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
                out var debugUnitBtn, out var debugReactiveBtn, out var debugManaBtn,
                out var debugSchBtn, out var debugFloatBtn,
                out var debugDmgInput, out var debugTakeHitBtn, out var debugDealHitBtn);

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
            var combatAnimator = gmGO.AddComponent<FWTCG.UI.CombatAnimator>(); // DEV-18

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
                enemyHandZone.GetComponent<RectTransform>(),
                // DEV-18 additions
                bf1Glow, bf2Glow, bf1CardArt, bf2CardArt, boardFlashImg);

            WireGameManager(gameMgr, turnMgr, combatSys, scoreMgr, simpleAI, gameUI,
                            entryEffects, deathwish, spellSys, reactiveSys,
                            startupFlowUI, reactiveWindowUI,
                            coinFlipPanel, coinFlipText, coinFlipOkButton,
                            mulliganPanel, mulliganTitleText, mulliganCardContainer,
                            mulliganConfirmButton, mulliganConfirmLabel, cardPrefab,
                            reactivePanel, reactiveContextText, reactiveCardContainer,
                            reactBtn, legendSys, legendSkillBtn, bfSys,
                            debugSpellBtn, debugEquipBtn, debugUnitBtn, debugReactiveBtn, debugManaBtn, debugSchBtn, debugFloatBtn,
                            debugDmgInput, debugTakeHitBtn, debugDealHitBtn,
                            tapAllRunesBtn, skipReactionBtn,
                            spellShowcaseGO, spellTargetPopupGO);

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

            // Wire debug panel toggle into GameUI (DEV-10)
            {
                var guiSO2 = new SerializedObject(gameUI);
                guiSO2.FindProperty("_debugPanel").objectReferenceValue = debugPanel;
                var debugTitleBtn = debugPanel.transform.Find("DebugTitle")?.GetComponent<Button>();
                guiSO2.FindProperty("_debugToggleBtn").objectReferenceValue = debugTitleBtn;

                // Combat result panel
                guiSO2.FindProperty("_combatResultPanel").objectReferenceValue = combatResultPanel;
                guiSO2.FindProperty("_crAttackerText").objectReferenceValue = crAttackerText;
                guiSO2.FindProperty("_crDefenderText").objectReferenceValue = crDefenderText;
                guiSO2.FindProperty("_crVsText").objectReferenceValue = crVsText;
                guiSO2.FindProperty("_crOutcomeText").objectReferenceValue = crOutcomeText;
                guiSO2.FindProperty("_crBfNameText").objectReferenceValue = crBfNameText;

                guiSO2.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── DEV-18: Wire CombatAnimator ──────────────────────────────────
            {
                var caSO = new SerializedObject(combatAnimator);
                caSO.FindProperty("_bf1Panel").objectReferenceValue =
                    bf1Panel?.GetComponent<RectTransform>();
                caSO.FindProperty("_bf2Panel").objectReferenceValue =
                    bf2Panel?.GetComponent<RectTransform>();
                caSO.ApplyModifiedPropertiesWithoutUndo();
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
            out Text playerLegendText, out Button legendSkillBtn, out Text enemyLegendText,
            out Image bf1CardArt, out Image bf2CardArt,
            out BattlefieldGlow bf1Glow, out BattlefieldGlow bf2Glow)
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
            // CSS: #p-main-pile { grid-column: 2; grid-row: 5; }
            CreateDeckPile(go.transform, "PlayerMainPile", "主牌堆",
                0.045f, 0.175f, 0.00f, 0.12f, out playerDeckCount);

            // CSS: #p-rune-pile { grid-column: 3; grid-row: 5; }
            CreateDeckPile(go.transform, "PlayerRunePile", "符文堆",
                0.175f, 0.305f, 0.00f, 0.12f, out playerRunePileCount);

            // CSS: #pdiscard-exile-wrap { grid-column: 2/4; grid-row: 4; }
            CreateDiscardExileZone(go.transform, "Player",
                0.045f, 0.305f, 0.12f, 0.25f,
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
                    out bf1CtrlBadge, out bf1CtrlBadgeText, out bf1CardArt, out bf1Glow);
                CreateBattlefieldPanel(bfArea.transform, "BF2Panel", "BF2EnemyUnits", "战场2", "BF2Label", "BF2PlayerUnits",
                    out bf2CtrlBadge, out bf2CtrlBadgeText, out bf2CardArt, out bf2Glow);
            }

            return go;
        }

        // ── Legacy CreateMainArea (kept as wrapper for backward compat) ──────

        private static GameObject CreateMainArea(Transform parent)
        {
            // DEV-9: replaced by CreateBoardWrapper; this stub exists only for compile compat
            return CreateBoardWrapper(parent,
                out _, out _, out _, out _, out _, out _, out _, out _, out _, out _,
                out _, out _, out _, out _, out _, out _, out _, out _, out _,
                out _, out _, out _, out _);
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

        // CSS reference colors for zones:
        // Base zones: rgba(4,16,28,0.9)  |  Other zones: rgba(3,14,26,0.88)
        // All zone border: 1px solid rgba(200,155,60,0.18)
        private static readonly Color ZoneBgBase = new Color(4f/255f, 16f/255f, 28f/255f, 0.9f);
        private static readonly Color ZoneBgDefault = new Color(3f/255f, 14f/255f, 26f/255f, 0.88f);
        private static readonly Color ZoneBorderColor = new Color(200f/255f, 155f/255f, 60f/255f, 0.18f);

        private static void CreateHorizontalZoneAnchored(Transform parent, string name,
            float xMin, float xMax, float yMin, float yMax)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            // CSS: background rgba(4,16,28,0.9), border 1px solid rgba(200,155,60,0.18)
            bool isBase = name.Contains("Base");
            var img = go.AddComponent<Image>();
            img.color = isBase ? ZoneBgBase : ZoneBgDefault;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ZoneBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 4f;
            hlg.padding = new RectOffset(2, 2, 2, 2);
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
            // Diagonal: player circles cluster at BOTTOM-left, enemy at TOP-right
            vlg.childAlignment = isPlayer ? TextAnchor.LowerCenter : TextAnchor.UpperCenter;

            // Player: 8 at top, 0 at bottom (reversed). Enemy: 0 at top, 8 at bottom
            for (int raw = 0; raw < 9; raw++)
            {
                int num = isPlayer ? (8 - raw) : raw;

                var circleGO = new GameObject($"Score_{num}");
                circleGO.transform.SetParent(go.transform, false);

                var le = circleGO.AddComponent<LayoutElement>();
                le.preferredWidth = 28f;  // CSS: max-width 28px
                le.preferredHeight = 28f;

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

            // CSS: rgba(3,14,26,0.88), border 1px solid rgba(200,155,60,0.18)
            var img = go.AddComponent<Image>();
            img.color = ZoneBgDefault;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ZoneBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

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

            // CSS: rgba(3,14,26,0.88), border rgba(200,155,60,0.18)
            var img = go.AddComponent<Image>();
            img.color = ZoneBgDefault;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ZoneBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            // Card slot — fills entire zone (same size as legend zone)
            var slotGO = new GameObject("HeroSlot");
            slotGO.transform.SetParent(go.transform, false);
            var slotRT = slotGO.AddComponent<RectTransform>();
            slotRT.anchorMin = Vector2.zero;
            slotRT.anchorMax = Vector2.one;
            slotRT.offsetMin = Vector2.zero;
            slotRT.offsetMax = Vector2.zero;

            heroContainer = slotGO.transform;
        }

        // ── Deck pile (DEV-9) ────────────────────────────────────────────────

        private static void CreateDeckPile(Transform parent, string name, string label,
            float xMin, float xMax, float yMin, float yMax, out Text countText)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            // CSS: rgba(3,14,26,0.88), border 1px solid rgba(200,155,60,0.18)
            var img = go.AddComponent<Image>();
            img.color = ZoneBgDefault;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = ZoneBorderColor;
            outline.effectDistance = new Vector2(1f, -1f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 1f;
            vlg.padding = new RectOffset(3, 3, 3, 3);

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

            // CSS: rgba(3,14,26,0.88), border rgba(200,155,60,0.18)
            var img = go.AddComponent<Image>();
            img.color = ZoneBgDefault;
            var lgOutline = go.AddComponent<Outline>();
            lgOutline.effectColor = ZoneBorderColor;
            lgOutline.effectDistance = new Vector2(1f, -1f);

            // Card-shaped area centered (same 80x120 as hand cards)
            // -- LegendArt will be overlaid at runtime by RefreshLegendArt (ignoreLayout)

            // Legend name text — top overlay
            var textGO = new GameObject(isPlayer ? "LegendText" : "EnemyLegendText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0f, 0.78f);
            textRT.anchorMax = new Vector2(1f, 0.95f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            legendText = textGO.AddComponent<Text>();
            legendText.text = isPlayer ? "卡莎" : "易大师";
            legendText.color = Color.white;
            legendText.fontSize = 10;
            legendText.alignment = TextAnchor.MiddleCenter;
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) legendText.font = _font;
            // Shadow for readability over art
            var textShadow = textGO.AddComponent<Shadow>();
            textShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            textShadow.effectDistance = new Vector2(1f, -1f);

            // Skill button (player only) — bottom overlay
            skillBtn = null;
            if (isPlayer)
            {
                var skillGO = new GameObject("SkillBtn");
                skillGO.transform.SetParent(go.transform, false);
                var skillRT = skillGO.AddComponent<RectTransform>();
                skillRT.anchorMin = new Vector2(0.05f, 0.02f);
                skillRT.anchorMax = new Vector2(0.95f, 0.18f);
                skillRT.offsetMin = Vector2.zero;
                skillRT.offsetMax = Vector2.zero;
                var skillImg = skillGO.AddComponent<Image>();
                skillImg.color = new Color(0.4f, 0.1f, 0.8f, 1f);
                skillBtn = skillGO.AddComponent<Button>();
                var skillLabel = new GameObject("Label");
                skillLabel.transform.SetParent(skillGO.transform, false);
                var skillLabelRT = skillLabel.AddComponent<RectTransform>();
                skillLabelRT.anchorMin = Vector2.zero;
                skillLabelRT.anchorMax = Vector2.one;
                skillLabelRT.offsetMin = Vector2.zero;
                skillLabelRT.offsetMax = Vector2.zero;
                var skillText = skillLabel.AddComponent<Text>();
                skillText.text = "虚空感知";
                skillText.color = Color.white;
                skillText.fontSize = 10;
                skillText.alignment = TextAnchor.MiddleCenter;
                if (_font != null) skillText.font = _font;
            }
            else
            {
                // Enemy passive text — bottom overlay
                var passiveGO = new GameObject("PassiveText");
                passiveGO.transform.SetParent(go.transform, false);
                var passiveRT = passiveGO.AddComponent<RectTransform>();
                passiveRT.anchorMin = new Vector2(0f, 0.02f);
                passiveRT.anchorMax = new Vector2(1f, 0.18f);
                passiveRT.offsetMin = Vector2.zero;
                passiveRT.offsetMax = Vector2.zero;
                var passiveT = passiveGO.AddComponent<Text>();
                passiveT.text = "[被动] 无极剑道";
                passiveT.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                passiveT.fontSize = 9;
                passiveT.alignment = TextAnchor.MiddleCenter;
                passiveT.horizontalOverflow = HorizontalWrapMode.Wrap;
                passiveT.verticalOverflow   = VerticalWrapMode.Overflow;
                if (_font != null) passiveT.font = _font;
                var passiveShadow = passiveGO.AddComponent<Shadow>();
                passiveShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
                passiveShadow.effectDistance = new Vector2(1f, -1f);
            }

            return go;
        }

        // ── Battlefield panel (DEV-9: control badge; DEV-18: glow + standby + BF art) ──

        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName,
            out Image ctrlBadge, out Text ctrlBadgeText,
            out Image bfCardArtImg, out BattlefieldGlow bfGlow)
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
            vlg.childForceExpandHeight = false;  // StandbyZone must stay fixed-height
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
            var lbl = CreateTMPText(labelRow.transform, labelName, labelText, Color.white, 13, TextAnchor.MiddleCenter);
            lbl.fontStyle = FontStyle.Bold;
            lbl.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            lbl.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // BF card art — independent card-shaped slot next to label
            var bfArtGO = new GameObject("BFCardArt");
            bfArtGO.transform.SetParent(labelRow.transform, false);
            var bfArtRT = bfArtGO.AddComponent<RectTransform>();
            bfArtRT.sizeDelta = new Vector2(38f, 26f); // landscape card (small, ~half card)
            var bfArtImg = bfArtGO.AddComponent<Image>();
            bfArtImg.color = new Color(0.15f, 0.2f, 0.3f, 0.6f); // placeholder
            bfArtImg.preserveAspect = true;
            // Border on slot
            var bfArtOutline = bfArtGO.AddComponent<Outline>();
            bfArtOutline.effectColor = new Color(0.47f, 0.35f, 0.16f, 0.5f);
            bfArtOutline.effectDistance = new Vector2(1f, -1f);

            // Expose BF card art Image for GameUI.UpdateBFCardArt
            bfCardArtImg = bfArtImg;

            // Player units zone
            CreateHorizontalZone(panel.transform, playerZoneName);

            // DEV-18: Standby zone (face-down cards, compact height)
            var standbyZone = new GameObject("StandbyZone");
            standbyZone.transform.SetParent(panel.transform, false);
            standbyZone.AddComponent<RectTransform>();
            var standbyLE = standbyZone.AddComponent<LayoutElement>();
            standbyLE.preferredHeight = 14f;
            standbyLE.flexibleHeight = 0f;
            var standbyHLG = standbyZone.AddComponent<HorizontalLayoutGroup>();
            standbyHLG.childControlWidth = false;
            standbyHLG.childControlHeight = false;
            standbyHLG.childForceExpandWidth = false;
            standbyHLG.childForceExpandHeight = false;
            standbyHLG.spacing = 2f;
            standbyHLG.childAlignment = TextAnchor.MiddleCenter;
            var standbyBg = standbyZone.AddComponent<Image>();
            standbyBg.color = new Color(0.05f, 0.05f, 0.15f, 0.25f);
            var standbyLabel = CreateTMPText(standbyZone.transform, "StandbyLabel", "待命区",
                new Color(0.78f, 0.67f, 0.43f, 0.9f), 11, TextAnchor.MiddleCenter);
            var standbyLabelLE = standbyLabel.gameObject.AddComponent<LayoutElement>();
            standbyLabelLE.minWidth = 30f;

            // DEV-18: Ambient breathe overlay (full-panel, pointer pass-through)
            var ambientGO = new GameObject("AmbientOverlay");
            ambientGO.transform.SetParent(panel.transform, false);
            var ambientRT = ambientGO.AddComponent<RectTransform>();
            ambientRT.anchorMin = Vector2.zero;
            ambientRT.anchorMax = Vector2.one;
            ambientRT.offsetMin = Vector2.zero;
            ambientRT.offsetMax = Vector2.zero;
            var ambientImg = ambientGO.AddComponent<Image>();
            ambientImg.color = new Color(0.05f, 0.15f, 0.30f, 0.02f);
            ambientImg.raycastTarget = false;

            // DEV-18: Control glow overlay (full-panel, pointer pass-through)
            var ctrlGlowGO = new GameObject("CtrlGlowOverlay");
            ctrlGlowGO.transform.SetParent(panel.transform, false);
            var ctrlGlowRT = ctrlGlowGO.AddComponent<RectTransform>();
            ctrlGlowRT.anchorMin = Vector2.zero;
            ctrlGlowRT.anchorMax = Vector2.one;
            ctrlGlowRT.offsetMin = Vector2.zero;
            ctrlGlowRT.offsetMax = Vector2.zero;
            var ctrlGlowImg = ctrlGlowGO.AddComponent<Image>();
            ctrlGlowImg.color = new Color(0f, 0f, 0f, 0f);
            ctrlGlowImg.raycastTarget = false;

            // Attach BattlefieldGlow component to panel
            bfGlow = panel.AddComponent<BattlefieldGlow>();
            var glowSO = new UnityEditor.SerializedObject(bfGlow);
            glowSO.FindProperty("_ambientOverlay").objectReferenceValue = ambientImg;
            glowSO.FindProperty("_ctrlGlowOverlay").objectReferenceValue = ctrlGlowImg;
            glowSO.ApplyModifiedProperties();

            // Add button to panel for BF click
            panel.AddComponent<Button>();
        }

        // Keep old overload for backward compat (unused but prevents compile errors)
        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName)
        {
            CreateBattlefieldPanel(parent, panelName, enemyZoneName, labelText,
                labelName, playerZoneName, out _, out _, out _, out _);
        }

        private static void CreateBattlefieldPanel(Transform parent,
            string panelName, string enemyZoneName, string labelText,
            string labelName, string playerZoneName,
            out Image ctrlBadge, out Text ctrlBadgeText)
        {
            CreateBattlefieldPanel(parent, panelName, enemyZoneName, labelText,
                labelName, playerZoneName, out ctrlBadge, out ctrlBadgeText, out _, out _);
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
            // Centered in battlefield area (between top bar ~8% and player hand ~18%)
            rt.anchorMin = new Vector2(0.2f, 0.4f);
            rt.anchorMax = new Vector2(0.8f, 0.6f);
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

        // ── Event Banner Panel (DEV-18b) ─────────────────────────────────────

        private static GameObject CreateEventBannerPanel(Transform parent)
        {
            // Small screen-center event banner; visibility controlled by CanvasGroup.
            // Anchored center, sits above the big BannerPanel in z-order.
            var go = new GameObject("EventBannerPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0.52f);
            rt.anchorMax = new Vector2(0.75f, 0.62f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.1f, 0.88f);

            // Text
            var text = CreateTMPText(go.transform, "EventBannerText", "", GameColors.GoldLight, 20, TextAnchor.MiddleCenter);
            var tRT = text.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.offsetMin = new Vector2(8f, 4f);
            tRT.offsetMax = new Vector2(-8f, -4f);
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;

            // Attach EventBanner component and wire fields
            var eb = go.AddComponent<FWTCG.UI.EventBanner>();
            var ebSO = new SerializedObject(eb);
            ebSO.FindProperty("_bannerText").objectReferenceValue = text;
            ebSO.FindProperty("_bannerBg").objectReferenceValue   = bg;
            ebSO.FindProperty("_bannerRT").objectReferenceValue   = rt;
            ebSO.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ── Spell Showcase Panel (DEV-16) ─────────────────────────────────────

        private static GameObject CreateSpellShowcasePanel(Transform parent)
        {
            // Full-screen semi-transparent overlay; starts inactive
            var go = new GameObject("SpellShowcasePanel");
            go.transform.SetParent(parent, false);

            // Full-screen RT
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Semi-transparent dark background
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            // CanvasGroup for alpha animation
            var cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false; // don't block input during showcase

            // ── Card container (animated) ──
            var cardPanelGO = new GameObject("CardPanel");
            cardPanelGO.transform.SetParent(go.transform, false);
            var cpRT = cardPanelGO.AddComponent<RectTransform>();
            cpRT.anchorMin = new Vector2(0.3f, 0.3f);
            cpRT.anchorMax = new Vector2(0.7f, 0.75f);
            cpRT.offsetMin = Vector2.zero;
            cpRT.offsetMax = Vector2.zero;

            var cpImg = cardPanelGO.AddComponent<Image>();
            cpImg.color = new Color(0.02f, 0.06f, 0.14f, 0.95f);
            var cpOutline = cardPanelGO.AddComponent<Outline>();
            cpOutline.effectColor = new Color(200f/255f, 170f/255f, 110f/255f, 0.8f);
            cpOutline.effectDistance = new Vector2(2f, -2f);

            // Owner label (top of card panel)
            var ownerLabel = CreateTMPText(cardPanelGO.transform, "OwnerLabel", "玩家",
                new Color(0.29f, 0.87f, 0.5f), 22, TextAnchor.UpperCenter);
            var olRT = ownerLabel.GetComponent<RectTransform>();
            olRT.anchorMin = new Vector2(0f, 0.82f);
            olRT.anchorMax = new Vector2(1f, 1f);
            olRT.offsetMin = new Vector2(8f, 0f);
            olRT.offsetMax = new Vector2(-8f, -4f);
            ownerLabel.fontStyle = FontStyle.Bold;

            // Art image (optional, center)
            var artGO = new GameObject("ArtImage");
            artGO.transform.SetParent(cardPanelGO.transform, false);
            var artRT = artGO.AddComponent<RectTransform>();
            artRT.anchorMin = new Vector2(0.15f, 0.46f);
            artRT.anchorMax = new Vector2(0.85f, 0.82f);
            artRT.offsetMin = Vector2.zero;
            artRT.offsetMax = Vector2.zero;
            var artImg = artGO.AddComponent<Image>();
            artImg.preserveAspect = true;
            artImg.color = Color.white;

            // Card name (large, center)
            var cardNameText = CreateTMPText(cardPanelGO.transform, "CardNameText", "法术名",
                new Color(240f/255f, 230f/255f, 210f/255f), 32, TextAnchor.MiddleCenter);
            var cnRT = cardNameText.GetComponent<RectTransform>();
            cnRT.anchorMin = new Vector2(0f, 0.28f);
            cnRT.anchorMax = new Vector2(1f, 0.46f);
            cnRT.offsetMin = new Vector2(8f, 0f);
            cnRT.offsetMax = new Vector2(-8f, 0f);
            cardNameText.fontStyle = FontStyle.Bold;

            // Effect text (smaller, bottom)
            var effectText = CreateTMPText(cardPanelGO.transform, "EffectText", "",
                new Color(180f/255f, 180f/255f, 180f/255f), 16, TextAnchor.UpperCenter);
            var etRT = effectText.GetComponent<RectTransform>();
            etRT.anchorMin = new Vector2(0f, 0f);
            etRT.anchorMax = new Vector2(1f, 0.28f);
            etRT.offsetMin = new Vector2(10f, 4f);
            etRT.offsetMax = new Vector2(-10f, 0f);
            effectText.horizontalOverflow = HorizontalWrapMode.Wrap;

            // ── Attach SpellShowcaseUI component ──
            var showcase = go.AddComponent<FWTCG.UI.SpellShowcaseUI>();
            var showcaseSO = new UnityEditor.SerializedObject(showcase);
            showcaseSO.FindProperty("_canvasGroup").objectReferenceValue = cg;
            showcaseSO.FindProperty("_cardPanel").objectReferenceValue   = cpRT;
            showcaseSO.FindProperty("_ownerLabel").objectReferenceValue  = ownerLabel;
            showcaseSO.FindProperty("_cardNameText").objectReferenceValue = cardNameText;
            showcaseSO.FindProperty("_effectText").objectReferenceValue  = effectText;
            showcaseSO.FindProperty("_artImage").objectReferenceValue    = artImg;
            showcaseSO.ApplyModifiedPropertiesWithoutUndo();

            // Do NOT SetActive(false) — CanvasGroup controls visibility; panel must stay active.
            return go;
        }

        // ── Spell Target Popup (DEV-16b) ──────────────────────────────────────

        private static GameObject CreateSpellTargetPopup(Transform parent)
        {
            // Full-screen dimming backdrop
            var backdrop = new GameObject("SpellTargetPopup");
            backdrop.transform.SetParent(parent, false);
            var bdRT = backdrop.AddComponent<RectTransform>();
            bdRT.anchorMin = Vector2.zero;
            bdRT.anchorMax = Vector2.one;
            bdRT.offsetMin = Vector2.zero;
            bdRT.offsetMax = Vector2.zero;
            var bdImg = backdrop.AddComponent<Image>();
            bdImg.color = new Color(0f, 0f, 0f, 0.65f);
            var bdCG = backdrop.AddComponent<CanvasGroup>();

            // Centered panel box
            var box = new GameObject("PopupBox");
            box.transform.SetParent(backdrop.transform, false);
            var boxRT = box.AddComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot     = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(520f, 0f);  // height auto via ContentSizeFitter
            var boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.04f, 0.09f, 0.16f, 0.97f);
            var boxVLG = box.AddComponent<VerticalLayoutGroup>();
            boxVLG.childControlWidth     = true;
            boxVLG.childControlHeight    = true;
            boxVLG.childForceExpandWidth = true;
            boxVLG.childForceExpandHeight = false;
            boxVLG.padding  = new RectOffset(12, 12, 10, 10);
            boxVLG.spacing  = 8f;
            var boxCSF = box.AddComponent<ContentSizeFitter>();
            boxCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(box.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 26f;
            var titleT = titleGO.AddComponent<Text>();
            titleT.text      = "选择法术目标";
            titleT.color     = new Color(0.94f, 0.84f, 0.43f);
            titleT.fontSize  = 16;
            titleT.fontStyle = FontStyle.Bold;
            titleT.alignment = TextAnchor.MiddleCenter;
            titleT.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleT.font = _font;

            // ── Enemy section ──────────────────────────────────────────────────
            var enemyHeader = new GameObject("EnemyHeader");
            enemyHeader.transform.SetParent(box.transform, false);
            var ehLE = enemyHeader.AddComponent<LayoutElement>();
            ehLE.preferredHeight = 20f;
            var ehT = enemyHeader.AddComponent<Text>();
            ehT.text      = "── 敌方单位 ──";
            ehT.color     = new Color(0.97f, 0.44f, 0.44f);
            ehT.fontSize  = 13;
            ehT.alignment = TextAnchor.MiddleCenter;
            ehT.horizontalOverflow = HorizontalWrapMode.Overflow;
            ehT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) ehT.font = _font;

            var enemyRow = new GameObject("EnemyContainer");
            enemyRow.transform.SetParent(box.transform, false);
            var erVLG = enemyRow.AddComponent<VerticalLayoutGroup>();
            erVLG.childControlWidth      = true;
            erVLG.childControlHeight     = true;
            erVLG.childForceExpandWidth  = true;
            erVLG.childForceExpandHeight = false;
            erVLG.spacing = 4f;
            var erCSF = enemyRow.AddComponent<ContentSizeFitter>();
            erCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Player section ─────────────────────────────────────────────────
            var playerHeader = new GameObject("PlayerHeader");
            playerHeader.transform.SetParent(box.transform, false);
            var phLE = playerHeader.AddComponent<LayoutElement>();
            phLE.preferredHeight = 20f;
            var phT = playerHeader.AddComponent<Text>();
            phT.text      = "── 己方单位 ──";
            phT.color     = new Color(0.29f, 0.87f, 0.5f);
            phT.fontSize  = 13;
            phT.alignment = TextAnchor.MiddleCenter;
            phT.horizontalOverflow = HorizontalWrapMode.Overflow;
            phT.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) phT.font = _font;

            var playerRow = new GameObject("PlayerContainer");
            playerRow.transform.SetParent(box.transform, false);
            var prVLG = playerRow.AddComponent<VerticalLayoutGroup>();
            prVLG.childControlWidth      = true;
            prVLG.childControlHeight     = true;
            prVLG.childForceExpandWidth  = true;
            prVLG.childForceExpandHeight = false;
            prVLG.spacing = 4f;
            var prCSF = playerRow.AddComponent<ContentSizeFitter>();
            prCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Cancel button ──────────────────────────────────────────────────
            var cancelGO = new GameObject("CancelBtn");
            cancelGO.transform.SetParent(box.transform, false);
            var cancelLE = cancelGO.AddComponent<LayoutElement>();
            cancelLE.preferredHeight = 34f;
            var cancelImg = cancelGO.AddComponent<Image>();
            cancelImg.color = new Color(0.45f, 0.45f, 0.45f, 0.9f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            var cancelLblGO = new GameObject("Label");
            cancelLblGO.transform.SetParent(cancelGO.transform, false);
            var cancelLblRT = cancelLblGO.AddComponent<RectTransform>();
            cancelLblRT.anchorMin = Vector2.zero;
            cancelLblRT.anchorMax = Vector2.one;
            cancelLblRT.offsetMin = Vector2.zero;
            cancelLblRT.offsetMax = Vector2.zero;
            var cancelLbl = cancelLblGO.AddComponent<Text>();
            cancelLbl.text      = "取消";
            cancelLbl.color     = Color.white;
            cancelLbl.fontSize  = 14;
            cancelLbl.alignment = TextAnchor.MiddleCenter;
            cancelLbl.horizontalOverflow = HorizontalWrapMode.Overflow;
            cancelLbl.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) cancelLbl.font = _font;

            // ── Wire SpellTargetPopup component ───────────────────────────────
            var popup = backdrop.AddComponent<FWTCG.UI.SpellTargetPopup>();
            var popupSO = new UnityEditor.SerializedObject(popup);
            popupSO.FindProperty("_canvasGroup").objectReferenceValue    = bdCG;
            popupSO.FindProperty("_enemyContainer").objectReferenceValue = enemyRow.transform;
            popupSO.FindProperty("_playerContainer").objectReferenceValue = playerRow.transform;
            popupSO.FindProperty("_cancelBtn").objectReferenceValue      = cancelBtn;
            popupSO.ApplyModifiedPropertiesWithoutUndo();

            // Do NOT SetActive(false) — CanvasGroup controls visibility; panel must stay active.
            return backdrop;
        }

        // ── Toast panel ───────────────────────────────────────────────────────

        private static GameObject CreateToastPanel(Transform parent, out Text toastText)
        {
            // Anchored top-center, narrow strip for battlefield effect notifications
            var go = new GameObject("ToastPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.46f);
            rt.anchorMax = new Vector2(0.8f, 0.54f);
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
            var go = CreateFullscreenPanel(parent, "CoinFlipPanel", new Color(0.02f, 0.04f, 0.07f, 0.97f));

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
            out Button unitBtn, out Button reactiveBtn, out Button manaBtn,
            out Button schBtn, out Button floatBtn,
            out InputField dmgInput, out Button takeHitBtn, out Button dealHitBtn)
        {
            var go = new GameObject("DebugPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            // Anchor bottom-left, 130px wide × 360px tall (7 buttons + input + hit row)
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot     = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(5f, 105f); // just above BottomBar
            rt.sizeDelta = new Vector2(130f, 360f);

            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.spacing = 4f;

            // Title bar with collapse toggle
            var titleGO = new GameObject("DebugTitle");
            titleGO.transform.SetParent(go.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 22f;
            var titleImg = titleGO.AddComponent<Image>();
            titleImg.color = new Color(0f, 0f, 0f, 0.3f);
            var titleBtn = titleGO.AddComponent<Button>();

            // Text as child (Image and Text can't coexist on same GO)
            var titleTextGO = new GameObject("TitleText");
            titleTextGO.transform.SetParent(titleGO.transform, false);
            var titleTextRT = titleTextGO.AddComponent<RectTransform>();
            titleTextRT.anchorMin = Vector2.zero;
            titleTextRT.anchorMax = Vector2.one;
            titleTextRT.offsetMin = Vector2.zero;
            titleTextRT.offsetMax = Vector2.zero;
            var titleT = titleTextGO.AddComponent<Text>();
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
            schBtn      = CreateDebugButton(go.transform, "+5 全符能", new Color(0.1f, 0.5f, 0.7f, 1f));
            floatBtn    = CreateDebugButton(go.transform, "⚡[1] 战力+2(buff)", new Color(0.6f, 0.1f, 0.5f, 1f));

            // ── Damage input row: "伤害:" label + InputField ──────────────────
            var dmgRowGO = new GameObject("DmgInputRow");
            dmgRowGO.transform.SetParent(go.transform, false);
            var dmgRowLE = dmgRowGO.AddComponent<LayoutElement>();
            dmgRowLE.preferredHeight = 32f;
            var dmgRowImg = dmgRowGO.AddComponent<Image>();
            dmgRowImg.color = new Color(0f, 0f, 0f, 0.3f);
            var dmgRowHLG = dmgRowGO.AddComponent<HorizontalLayoutGroup>();
            dmgRowHLG.childControlWidth = true;
            dmgRowHLG.childControlHeight = true;
            dmgRowHLG.childForceExpandWidth = false;
            dmgRowHLG.childForceExpandHeight = true;
            dmgRowHLG.spacing = 4f;
            dmgRowHLG.padding = new RectOffset(4, 4, 4, 4);

            // Label
            var dmgLabelGO = new GameObject("DmgLabel");
            dmgLabelGO.transform.SetParent(dmgRowGO.transform, false);
            var dmgLabelLE = dmgLabelGO.AddComponent<LayoutElement>();
            dmgLabelLE.preferredWidth = 42f;
            dmgLabelLE.flexibleWidth = 0f;
            var dmgLabelTxt = dmgLabelGO.AddComponent<Text>();
            dmgLabelTxt.text = "伤害:";
            dmgLabelTxt.color = Color.white;
            dmgLabelTxt.fontSize = 13;
            dmgLabelTxt.alignment = TextAnchor.MiddleRight;
            if (_font != null) dmgLabelTxt.font = _font;

            // InputField
            var dmgInputGO = new GameObject("DmgInput");
            dmgInputGO.transform.SetParent(dmgRowGO.transform, false);
            var dmgInputLE = dmgInputGO.AddComponent<LayoutElement>();
            dmgInputLE.flexibleWidth = 1f;
            var dmgInputImg = dmgInputGO.AddComponent<Image>();
            dmgInputImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            dmgInput = dmgInputGO.AddComponent<InputField>();
            dmgInput.text = "3";
            dmgInput.contentType = InputField.ContentType.IntegerNumber;

            var dmgTextGO = new GameObject("Text");
            dmgTextGO.transform.SetParent(dmgInputGO.transform, false);
            var dmgText = dmgTextGO.AddComponent<Text>();
            dmgText.text = "3";
            dmgText.color = Color.white;
            dmgText.fontSize = 15;
            dmgText.alignment = TextAnchor.MiddleCenter;
            if (_font != null) dmgText.font = _font;
            var dmgTextRT = dmgTextGO.GetComponent<RectTransform>();
            dmgTextRT.anchorMin = Vector2.zero;
            dmgTextRT.anchorMax = Vector2.one;
            dmgTextRT.offsetMin = new Vector2(4, 2);
            dmgTextRT.offsetMax = new Vector2(-4, -2);
            dmgInput.textComponent = dmgText;

            // ── Hit buttons row (受击 | 施击) ──────────────────────────────────
            var hitRowGO = new GameObject("HitBtnRow");
            hitRowGO.transform.SetParent(go.transform, false);
            var hitRowLE = hitRowGO.AddComponent<LayoutElement>();
            hitRowLE.preferredHeight = 36f;
            var hitRowHLG = hitRowGO.AddComponent<HorizontalLayoutGroup>();
            hitRowHLG.childControlWidth = true;
            hitRowHLG.childControlHeight = true;
            hitRowHLG.childForceExpandWidth = true;
            hitRowHLG.childForceExpandHeight = true;
            hitRowHLG.spacing = 4f;
            hitRowHLG.padding = new RectOffset(0, 0, 0, 0);

            takeHitBtn = CreateDebugButton(hitRowGO.transform, "受击", new Color(0.75f, 0.1f, 0.1f, 1f));
            dealHitBtn = CreateDebugButton(hitRowGO.transform, "施击", new Color(0.85f, 0.45f, 0.05f, 1f));
            // Remove LayoutElement preferredHeight from these two (parent HLG drives height)
            var takeLe = takeHitBtn.GetComponent<LayoutElement>();
            if (takeLe != null) takeLe.preferredHeight = -1f;
            var dealLe = dealHitBtn.GetComponent<LayoutElement>();
            if (dealLe != null) dealLe.preferredHeight = -1f;

            // Default: collapsed (only title visible)
            spellBtn.gameObject.SetActive(false);
            equipBtn.gameObject.SetActive(false);
            unitBtn.gameObject.SetActive(false);
            reactiveBtn.gameObject.SetActive(false);
            manaBtn.gameObject.SetActive(false);
            schBtn.gameObject.SetActive(false);
            floatBtn.gameObject.SetActive(false);
            dmgRowGO.SetActive(false);
            hitRowGO.SetActive(false);
            rt.sizeDelta = new Vector2(130f, 30f);
            titleT.text = "▶ DEBUG";

            // Runtime toggle is wired in GameUI.Awake via _debugToggleBtn

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
            var overlay = CreateFullscreenPanel(parent, "CardDetailPopup", new Color(0f, 0f, 0f, 0.8f));
            closeButton = overlay.AddComponent<Button>();
            overlay.GetComponent<Image>().raycastTarget = true;

            // Horizontal layout: large art on LEFT, text info on RIGHT
            var panel = new GameObject("DetailPanel");
            panel.transform.SetParent(overlay.transform, false);
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            panelImg.raycastTarget = true;

            var panelRT = panel.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.5f, 0.5f);
            panelRT.anchorMax = new Vector2(0.5f, 0.5f);
            panelRT.pivot     = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(700f, 500f);

            var hlgPanel = panel.AddComponent<HorizontalLayoutGroup>();
            hlgPanel.childControlWidth  = false;
            hlgPanel.childControlHeight = true;
            hlgPanel.childForceExpandWidth = false;
            hlgPanel.childForceExpandHeight = true;
            hlgPanel.padding = new RectOffset(12, 12, 12, 12);
            hlgPanel.spacing = 16f;

            // -- LEFT: Large card art (fills left half, ~300px wide)
            var artGO = new GameObject("CDPArt");
            artGO.transform.SetParent(panel.transform, false);
            var artLE = artGO.AddComponent<LayoutElement>();
            artLE.preferredWidth = 300f;
            artImage = artGO.AddComponent<Image>();
            artImage.color = Color.white; // full brightness, no dimming
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;

            // -- RIGHT: text info column
            var infoColumn = new GameObject("InfoColumn");
            infoColumn.transform.SetParent(panel.transform, false);
            var infoLE = infoColumn.AddComponent<LayoutElement>();
            infoLE.flexibleWidth = 1f;
            var infoVLG = infoColumn.AddComponent<VerticalLayoutGroup>();
            infoVLG.childControlWidth  = true;
            infoVLG.childControlHeight = false;
            infoVLG.childForceExpandWidth = true;
            infoVLG.childForceExpandHeight = false;
            infoVLG.padding = new RectOffset(0, 0, 4, 4);
            infoVLG.spacing = 6f;

            // Card Name (gold, 24pt)
            nameText = CreateDetailText(infoColumn.transform, "CDPName", "", 24,
                new Color(0.98f, 0.75f, 0.15f, 1f), TextAnchor.MiddleLeft, 32f);

            // Cost & Type
            costText = CreateDetailText(infoColumn.transform, "CDPCost", "", 15,
                new Color(0.91f, 0.85f, 0.75f, 1f), TextAnchor.MiddleLeft, 22f);

            // ATK/HP
            atkText = CreateDetailText(infoColumn.transform, "CDPAtk", "", 15,
                Color.white, TextAnchor.MiddleLeft, 22f);

            // Keywords (multi-line)
            keywordsText = CreateDetailText(infoColumn.transform, "CDPKeywords", "", 13,
                new Color(0.6f, 0.85f, 1f, 1f), TextAnchor.UpperLeft, 90f, true);

            // Effect description (multi-line)
            effectText = CreateDetailText(infoColumn.transform, "CDPEffect", "", 13,
                new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.UpperLeft, 80f, true);

            // Runtime state
            stateText = CreateDetailText(infoColumn.transform, "CDPState", "", 12,
                new Color(1f, 0.9f, 0.5f, 1f), TextAnchor.UpperLeft, 50f, true);

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
            rootRT.sizeDelta = new Vector2(76f, 110f); // CSS: .card { width:76px; height:110px }

            // Button on root
            root.AddComponent<Button>();

            // CardView component
            var cardView = root.AddComponent<FWTCG.UI.CardView>();

            // ── ArtImage — FIRST child = bottom layer (behind all text) ──
            var artGO = new GameObject("ArtImage");
            artGO.transform.SetParent(root.transform, false);
            var artImg = artGO.AddComponent<Image>();
            artImg.preserveAspect = true;
            artImg.color = Color.white;
            artImg.raycastTarget = false;
            var artRT = artGO.GetComponent<RectTransform>();
            artRT.anchorMin = Vector2.zero;
            artRT.anchorMax = Vector2.one;
            artRT.offsetMin = new Vector2(2f, 2f);
            artRT.offsetMax = new Vector2(-2f, -2f);

            // ── Bottom half overlay (gradient fade from transparent to dark) ──
            var bottomOverlay = new GameObject("BottomOverlay");
            bottomOverlay.transform.SetParent(root.transform, false);
            var bottomImg = bottomOverlay.AddComponent<Image>();
            bottomImg.color = new Color(0f, 0f, 0f, 0.75f);
            bottomImg.raycastTarget = false;
            var bottomRT = bottomOverlay.GetComponent<RectTransform>();
            bottomRT.anchorMin = new Vector2(0f, 0f);
            bottomRT.anchorMax = new Vector2(1f, 0.48f);
            bottomRT.offsetMin = Vector2.zero;
            bottomRT.offsetMax = Vector2.zero;

            // ── CardName — bottom half, centered ──
            var cardName = CreateTMPText(root.transform, "CardName", "卡名", Color.white, 10, TextAnchor.MiddleCenter);
            cardName.fontStyle = FontStyle.Bold;
            cardName.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            cardName.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            var nameRT = cardName.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0.05f, 0.25f);
            nameRT.anchorMax = new Vector2(0.95f, 0.48f);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            // ── Description text — below name ──
            var descText = CreateTMPText(root.transform, "DescText", "", Color.white, 7, TextAnchor.UpperCenter);
            descText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var descRT = descText.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.05f, 0.02f);
            descRT.anchorMax = new Vector2(0.95f, 0.25f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            // ── CostText — top-left cost badge (above bottom overlay) ──
            var costBadgeGO = new GameObject("CostBadge");
            costBadgeGO.transform.SetParent(root.transform, false);
            var costBadgeImg = costBadgeGO.AddComponent<Image>();
            costBadgeImg.color = new Color(0.78f, 0.67f, 0.43f, 0.95f);
            costBadgeImg.raycastTarget = false;
            var costBadgeRT = costBadgeGO.GetComponent<RectTransform>();
            costBadgeRT.anchorMin = new Vector2(0f, 0.85f);
            costBadgeRT.anchorMax = new Vector2(0.25f, 1f);
            costBadgeRT.offsetMin = new Vector2(1f, 1f);
            costBadgeRT.offsetMax = new Vector2(-1f, -1f);

            var costText = CreateTMPText(costBadgeGO.transform, "CostText", "0", Color.white, 12, TextAnchor.MiddleCenter);
            costText.fontStyle = FontStyle.Bold;
            costText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = Vector2.zero;
            costRT.anchorMax = Vector2.one;
            costRT.offsetMin = Vector2.zero;
            costRT.offsetMax = Vector2.zero;

            // ── AtkText — top-right badge (deep orange bg, black text) ──
            var atkBadgeGO = new GameObject("AtkBadge");
            atkBadgeGO.transform.SetParent(root.transform, false);
            var atkBadgeImg = atkBadgeGO.AddComponent<Image>();
            atkBadgeImg.color = new Color(0.85f, 0.35f, 0.15f, 0.95f);
            atkBadgeImg.raycastTarget = false;
            var atkBadgeRT = atkBadgeGO.GetComponent<RectTransform>();
            atkBadgeRT.anchorMin = new Vector2(0.75f, 0.85f);
            atkBadgeRT.anchorMax = new Vector2(1f, 1f);
            atkBadgeRT.offsetMin = new Vector2(1f, 1f);
            atkBadgeRT.offsetMax = new Vector2(-1f, -1f);

            var atkText = CreateTMPText(atkBadgeGO.transform, "AtkText", "0", new Color(0.05f, 0.05f, 0.05f, 1f), 12, TextAnchor.MiddleCenter);
            atkText.fontStyle = FontStyle.Bold;
            atkText.gameObject.AddComponent<Shadow>().effectColor = new Color(1f, 1f, 1f, 0.3f);
            var atkRT = atkText.GetComponent<RectTransform>();
            atkRT.anchorMin = Vector2.zero;
            atkRT.anchorMax = Vector2.one;
            atkRT.offsetMin = Vector2.zero;
            atkRT.offsetMax = Vector2.zero;

            // ── Schematic cost display (below desc, colored bg) ──
            var schBgGO = new GameObject("SchCostBg");
            schBgGO.transform.SetParent(root.transform, false);
            var schBgImg = schBgGO.AddComponent<Image>();
            schBgImg.color = new Color(1f, 0.55f, 0.1f, 0.8f); // default blazing, runtime changes
            schBgImg.raycastTarget = false;
            var schBgRT = schBgGO.GetComponent<RectTransform>();
            schBgRT.anchorMin = new Vector2(0.05f, 0.02f);
            schBgRT.anchorMax = new Vector2(0.55f, 0.12f);
            schBgRT.offsetMin = Vector2.zero;
            schBgRT.offsetMax = Vector2.zero;
            schBgGO.SetActive(false);

            var schText = CreateTMPText(schBgGO.transform, "SchCostText", "炽×1", Color.white, 8, TextAnchor.MiddleCenter);
            schText.fontStyle = FontStyle.Bold;
            schText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var schTextRT = schText.GetComponent<RectTransform>();
            schTextRT.anchorMin = Vector2.zero;
            schTextRT.anchorMax = Vector2.one;
            schTextRT.offsetMin = Vector2.zero;
            schTextRT.offsetMax = Vector2.zero;

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

            // ── DEV-10: Exhausted overlay (gray dim, starts inactive) ──
            var exhaustedGO = new GameObject("ExhaustedOverlay");
            exhaustedGO.transform.SetParent(root.transform, false);
            var exhaustedImg = exhaustedGO.AddComponent<Image>();
            exhaustedImg.color = new Color(0.15f, 0.15f, 0.2f, 0.55f); // dark gray semi-transparent
            exhaustedImg.raycastTarget = false;
            var exhaustedRT = exhaustedGO.GetComponent<RectTransform>();
            exhaustedRT.anchorMin = Vector2.zero;
            exhaustedRT.anchorMax = Vector2.one;
            exhaustedRT.offsetMin = Vector2.zero;
            exhaustedRT.offsetMax = Vector2.zero;
            exhaustedGO.SetActive(false);

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
            so.FindProperty("_schCostText").objectReferenceValue    = schText;
            so.FindProperty("_schCostBg").objectReferenceValue      = schBgImg;
            so.FindProperty("_exhaustedOverlay").objectReferenceValue = exhaustedImg;
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
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.sizeDelta = new Vector2(46f, 46f); // just the circle, label is overlay

            // No layout — circle fills root

            // ── Circular rune image (tap button) ──
            var circleGO = new GameObject("RuneCircle");
            circleGO.transform.SetParent(root.transform, false);
            var circleRT = circleGO.AddComponent<RectTransform>();
            circleRT.anchorMin = Vector2.zero;
            circleRT.anchorMax = Vector2.one;
            circleRT.offsetMin = new Vector2(2f, 2f);
            circleRT.offsetMax = new Vector2(-2f, -2f);

            // Circular mask
            var mask = circleGO.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            var circleImg = circleGO.AddComponent<Image>();
            circleImg.color = new Color(0.4f, 0.6f, 0.3f, 1f); // default green tint, overridden at runtime

            // Art image inside circle (filled by runtime based on rune type)
            var artGO = new GameObject("RuneArt");
            artGO.transform.SetParent(circleGO.transform, false);
            var artImg = artGO.AddComponent<Image>();
            artImg.color = Color.white;
            artImg.preserveAspect = false; // fill circle
            artImg.raycastTarget = false;
            var artRT = artGO.GetComponent<RectTransform>();
            artRT.anchorMin = Vector2.zero;
            artRT.anchorMax = Vector2.one;
            artRT.offsetMin = Vector2.zero;
            artRT.offsetMax = Vector2.zero;

            // Border ring (outline)
            var borderGO = new GameObject("RuneBorder");
            borderGO.transform.SetParent(circleGO.transform, false);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0f, 0f, 0f, 0f); // transparent center
            borderImg.raycastTarget = false;
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = Vector2.zero;
            borderRT.offsetMax = Vector2.zero;
            var borderOutline = borderGO.AddComponent<Outline>();
            borderOutline.effectColor = GameColors.GoldDark;
            borderOutline.effectDistance = new Vector2(2f, -2f);

            // Tap button on circle
            circleGO.AddComponent<Button>();

            // ── Label text overlaid ON the circle ──
            var labelText = CreateTMPText(circleGO.transform, "RuneTypeText", "炽", Color.white, 11, TextAnchor.MiddleCenter);
            labelText.fontStyle = FontStyle.Bold;
            labelText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            labelText.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            var labelRT = labelText.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelLE = labelText.gameObject.AddComponent<LayoutElement>();
            labelLE.ignoreLayout = true;

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
               CardKeyword.Reactive | CardKeyword.StrongAtk, "rengar_enter");

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
               CardKeyword.Roam | CardKeyword.Haste, "yi_hero_enter",
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
               CardKeyword.SpellShield, "sandshoal_deserter_enter");

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

            CDS("furnace_blast",  "熔炉烈焰",   3, RuneType.Blazing, 1,
                "回响。对至多3个敌方单位各造成1点伤害",
                SpellTargetType.None, "furnace_blast", CardKeyword.Echo);

            CDS("time_warp",      "时间扭曲",   8, RuneType.Radiant, 3,
                "获得一个额外回合",
                SpellTargetType.None, "time_warp");

            CDS("divine_ray",     "神圣光芒",   4, RuneType.Blazing, 2,
                "回响。对目标敌方单位造成2点伤害两次（共4点）",
                SpellTargetType.EnemyUnit, "divine_ray", CardKeyword.Echo);

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
            RectTransform playerHandZoneRT, RectTransform enemyHandZoneRT,
            // DEV-18 additions
            BattlefieldGlow bf1Glow, BattlefieldGlow bf2Glow,
            Image bf1CardArt, Image bf2CardArt,
            Image boardFlashOverlay)
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

            // ── DEV-18: BF glow + BF card art + board flash ──
            if (bf1Glow != null) so.FindProperty("_bf1Glow").objectReferenceValue = bf1Glow;
            if (bf2Glow != null) so.FindProperty("_bf2Glow").objectReferenceValue = bf2Glow;
            if (bf1CardArt != null) so.FindProperty("_bf1CardArt").objectReferenceValue = bf1CardArt;
            if (bf2CardArt != null) so.FindProperty("_bf2CardArt").objectReferenceValue = bf2CardArt;
            if (boardFlashOverlay != null) so.FindProperty("_boardFlashOverlay").objectReferenceValue = boardFlashOverlay;

            // ── DEV-18b: score/rune zone RTs for FloatText positioning ──
            if (playerScoreCircleImages != null && playerScoreCircleImages.Length > 0 && playerScoreCircleImages[0] != null)
                so.FindProperty("_playerScoreZoneRT").objectReferenceValue = playerScoreCircleImages[0].transform.parent.GetComponent<RectTransform>();
            if (enemyScoreCircleImages != null && enemyScoreCircleImages.Length > 0 && enemyScoreCircleImages[0] != null)
                so.FindProperty("_enemyScoreZoneRT").objectReferenceValue = enemyScoreCircleImages[0].transform.parent.GetComponent<RectTransform>();
            if (playerRuneContainer != null)
                so.FindProperty("_playerRuneZoneRT").objectReferenceValue = playerRuneContainer.GetComponent<RectTransform>();
            if (enemyRuneContainer != null)
                so.FindProperty("_enemyRuneZoneRT").objectReferenceValue = enemyRuneContainer.GetComponent<RectTransform>();

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
            Button debugSpellBtn, Button debugEquipBtn, Button debugUnitBtn, Button debugReactiveBtn, Button debugManaBtn, Button debugSchBtn, Button debugFloatBtn,
            InputField debugDmgInput, Button debugTakeHitBtn, Button debugDealHitBtn,
            Button tapAllRunesBtn = null, Button skipReactionBtn = null,
            GameObject spellShowcaseGO = null,
            GameObject spellTargetPopupGO = null)
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
            so.FindProperty("_debugSchBtn").objectReferenceValue      = debugSchBtn;
            so.FindProperty("_debugFloatBtn").objectReferenceValue    = debugFloatBtn;
            so.FindProperty("_debugDmgInput").objectReferenceValue   = debugDmgInput;
            so.FindProperty("_debugTakeHitBtn").objectReferenceValue = debugTakeHitBtn;
            so.FindProperty("_debugDealHitBtn").objectReferenceValue = debugDealHitBtn;

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

            // DEV-16: Wire SpellShowcaseUI
            if (spellShowcaseGO != null)
            {
                var showcase = spellShowcaseGO.GetComponent<FWTCG.UI.SpellShowcaseUI>();
                if (showcase != null)
                    so.FindProperty("_spellShowcase").objectReferenceValue = showcase;
            }

            // DEV-16b: Wire SpellTargetPopup
            if (spellTargetPopupGO != null)
            {
                var popup = spellTargetPopupGO.GetComponent<FWTCG.UI.SpellTargetPopup>();
                if (popup != null)
                    so.FindProperty("_spellTargetPopup").objectReferenceValue = popup;
            }

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
                LoadCard("furnace_blast"),
                LoadCard("time_warp"),
                LoadCard("divine_ray"),
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

        // ── DEV-10: Combat Result Panel ──────────────────────────────────────

        private static GameObject CreateCombatResultPanel(Transform parent,
            out Text attackerText, out Text defenderText,
            out Text vsText, out Text outcomeText, out Text bfNameText)
        {
            var go = new GameObject("CombatResultPanel");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.2f, 0.35f);
            rt.anchorMax = new Vector2(0.8f, 0.65f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.06f, 0.12f, 0.92f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.67f, 0.43f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            // BF name (top)
            bfNameText = CreateTMPText(go.transform, "CRBFName", "战场", GameColors.Gold, 16, TextAnchor.MiddleCenter);
            bfNameText.fontStyle = FontStyle.Bold;
            var bfRT = bfNameText.GetComponent<RectTransform>();
            bfRT.anchorMin = new Vector2(0.1f, 0.8f);
            bfRT.anchorMax = new Vector2(0.9f, 0.95f);
            bfRT.offsetMin = Vector2.zero;
            bfRT.offsetMax = Vector2.zero;

            // Attacker (left)
            attackerText = CreateTMPText(go.transform, "CRAttacker", "玩家\n⚔ 0", GameColors.PlayerGreen, 28, TextAnchor.MiddleCenter);
            attackerText.fontStyle = FontStyle.Bold;
            attackerText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var atkTextRT = attackerText.GetComponent<RectTransform>();
            atkTextRT.anchorMin = new Vector2(0.05f, 0.25f);
            atkTextRT.anchorMax = new Vector2(0.4f, 0.78f);
            atkTextRT.offsetMin = Vector2.zero;
            atkTextRT.offsetMax = Vector2.zero;

            // VS
            vsText = CreateTMPText(go.transform, "CRVS", "VS", GameColors.GoldLight, 32, TextAnchor.MiddleCenter);
            vsText.fontStyle = FontStyle.Bold;
            vsText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var vsRT = vsText.GetComponent<RectTransform>();
            vsRT.anchorMin = new Vector2(0.4f, 0.3f);
            vsRT.anchorMax = new Vector2(0.6f, 0.7f);
            vsRT.offsetMin = Vector2.zero;
            vsRT.offsetMax = Vector2.zero;

            // Defender (right)
            defenderText = CreateTMPText(go.transform, "CRDefender", "AI\n🛡 0", GameColors.EnemyRed, 28, TextAnchor.MiddleCenter);
            defenderText.fontStyle = FontStyle.Bold;
            defenderText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var defRT = defenderText.GetComponent<RectTransform>();
            defRT.anchorMin = new Vector2(0.6f, 0.25f);
            defRT.anchorMax = new Vector2(0.95f, 0.78f);
            defRT.offsetMin = Vector2.zero;
            defRT.offsetMax = Vector2.zero;

            // Outcome (bottom)
            outcomeText = CreateTMPText(go.transform, "CROutcome", "结果", GameColors.GoldLight, 18, TextAnchor.MiddleCenter);
            outcomeText.fontStyle = FontStyle.Bold;
            outcomeText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var outRT = outcomeText.GetComponent<RectTransform>();
            outRT.anchorMin = new Vector2(0.1f, 0.05f);
            outRT.anchorMax = new Vector2(0.9f, 0.25f);
            outRT.offsetMin = Vector2.zero;
            outRT.offsetMax = Vector2.zero;

            go.SetActive(false);
            return go;
        }

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
            // Centered in battlefield area
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 175f); // 上移，避免与 ToastPanel / EventBannerPanel 重叠
            rt.sizeDelta = new Vector2(70f, 70f);

            // Dark background circle
            var bgImg = go.AddComponent<Image>();
            bgImg.color = new Color(0.02f, 0.06f, 0.12f, 0.85f);

            // Outer ring (static border)
            var ringGO = new GameObject("TimerRing");
            ringGO.transform.SetParent(go.transform, false);
            var ringRT = ringGO.AddComponent<RectTransform>();
            ringRT.anchorMin = Vector2.zero;
            ringRT.anchorMax = Vector2.one;
            ringRT.offsetMin = new Vector2(2f, 2f);
            ringRT.offsetMax = new Vector2(-2f, -2f);
            var ringImg = ringGO.AddComponent<Image>();
            ringImg.color = new Color(0.47f, 0.35f, 0.16f, 0.4f); // dim gold ring
            ringImg.type = Image.Type.Filled;
            ringImg.fillMethod = Image.FillMethod.Radial360;
            ringImg.fillOrigin = (int)Image.Origin360.Top;
            ringImg.fillClockwise = false;
            ringImg.fillAmount = 1f;

            // Countdown fill ring (animated)
            var fillGO = new GameObject("TimerFill");
            fillGO.transform.SetParent(go.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(4f, 4f);
            fillRT.offsetMax = new Vector2(-4f, -4f);
            fillImage = fillGO.AddComponent<Image>();
            fillImage.color = GameColors.PlayerGreen;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            fillImage.fillOrigin = (int)Image.Origin360.Top;
            fillImage.fillClockwise = false;
            fillImage.fillAmount = 1f;

            // Inner dark circle (donut hole)
            var innerGO = new GameObject("TimerInner");
            innerGO.transform.SetParent(go.transform, false);
            var innerRT = innerGO.AddComponent<RectTransform>();
            innerRT.anchorMin = new Vector2(0.18f, 0.18f);
            innerRT.anchorMax = new Vector2(0.82f, 0.82f);
            innerRT.offsetMin = Vector2.zero;
            innerRT.offsetMax = Vector2.zero;
            var innerImg = innerGO.AddComponent<Image>();
            innerImg.color = new Color(0.02f, 0.06f, 0.12f, 0.95f);

            // Timer text (large, bold)
            var textGO = new GameObject("TimerText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            timerText = textGO.AddComponent<Text>();
            timerText.text = "30";
            timerText.color = GameColors.PlayerGreen;
            timerText.fontSize = 22;
            timerText.fontStyle = FontStyle.Bold;
            timerText.alignment = TextAnchor.MiddleCenter;
            if (_font != null) timerText.font = _font;
            var timerShadow = textGO.AddComponent<Shadow>();
            timerShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            timerShadow.effectDistance = new Vector2(1f, -1f);

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
