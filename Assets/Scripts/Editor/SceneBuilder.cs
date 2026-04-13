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

            // 0. 静默保存当前场景，防止 NewScene 触发保存弹窗卡住 MCP
            // 用 SaveCurrentModifiedScenesIfUserWantsTo=false 等价：直接强制保存，不弹窗
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty && !string.IsNullOrEmpty(active.path))
                EditorSceneManager.SaveScene(active);
            // 若场景没有路径（Untitled），直接丢弃，不弹 SaveAs 对话框
            // NewSceneMode.Single 会替换掉它

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

            // ── Background (SVG-gen bg_game_main, fallback bg_menu, fallback HexGrid) ──
            var background = CreateFullscreenPanel(canvasGO.transform, "Background",
                HexColor("#010a13"));
            {
                var bgImg = background.GetComponent<Image>();
                var bgMenuSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/bg_game_main.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/bg_menu.png");
                if (bgMenuSpr != null)
                {
                    bgImg.sprite = bgMenuSpr;
                    bgImg.color = Color.white;
                    bgImg.type = Image.Type.Simple;
                    bgImg.preserveAspect = false; // stretch to fill full canvas regardless of source resolution
                }
                else
                {
                    // Fallback: HexGrid shader (preserved for other uses)
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
                        bgImg.material = hexMat;
                    }
                }
            }

            // ── DEV-21: Particle BG layer (behind all game UI) ───────────────
            var particleBGLayer = new GameObject("ParticleBGLayer");
            particleBGLayer.transform.SetParent(canvasGO.transform, false);
            {
                var rt = particleBGLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = particleBGLayer.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = false;
            }

            // ── Decorative ambient layer (Pencil: countdown ring 423×423 at center) ──
            var decorLayerGO = new GameObject("DecorLayer");
            decorLayerGO.transform.SetParent(canvasGO.transform, false);
            SceneryUI _scenery;
            CountdownRingUI _countdownRingUI;
            {
                var dlRT = decorLayerGO.AddComponent<RectTransform>();
                dlRT.anchorMin = Vector2.zero;
                dlRT.anchorMax = Vector2.one;
                dlRT.offsetMin = Vector2.zero;
                dlRT.offsetMax = Vector2.zero;
                _scenery = decorLayerGO.AddComponent<SceneryUI>();

                // ── Countdown Ring (Pencil: Thvkj x=747, y=322, 423×423) ─────────
                // 3 layers: base (time01) + blue fill (time02) + red fill (time03)
                var ringGO = new GameObject("CountdownRing");
                ringGO.transform.SetParent(decorLayerGO.transform, false);
                var ringRT = ringGO.AddComponent<RectTransform>();
                ringRT.anchorMin = new Vector2(747f/1920f, 1f-(322f+423f)/1080f);
                ringRT.anchorMax = new Vector2((747f+423f)/1920f, 1f-322f/1080f);
                ringRT.offsetMin = Vector2.zero;
                ringRT.offsetMax = Vector2.zero;

                // Layer 0: base ring — time01 (always visible)
                var baseGO = new GameObject("RingBase", typeof(RectTransform), typeof(Image));
                baseGO.transform.SetParent(ringGO.transform, false);
                StretchRect(baseGO.GetComponent<RectTransform>());
                var baseImg = baseGO.GetComponent<Image>();
                var baseSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/countdown_empty.png");
                if (baseSpr != null) { baseImg.sprite = baseSpr; baseImg.color = Color.white; }
                baseImg.raycastTarget = false;

                // Layer 1: blue fill — time02, Radial360
                var blueGO = new GameObject("RingBlueFill", typeof(RectTransform), typeof(Image));
                blueGO.transform.SetParent(ringGO.transform, false);
                StretchRect(blueGO.GetComponent<RectTransform>());
                var blueImg = blueGO.GetComponent<Image>();
                var blueSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/countdown_blue.png");
                if (blueSpr != null) { blueImg.sprite = blueSpr; blueImg.color = Color.white; }
                blueImg.type = Image.Type.Filled;
                blueImg.fillMethod = Image.FillMethod.Radial360;
                blueImg.fillOrigin = (int)Image.Origin360.Top;
                blueImg.fillClockwise = true;
                blueImg.fillAmount = 0f;
                blueImg.raycastTarget = false;
                blueGO.SetActive(false);

                // Layer 2: red fill — time03, Radial360
                var redGO = new GameObject("RingRedFill", typeof(RectTransform), typeof(Image));
                redGO.transform.SetParent(ringGO.transform, false);
                StretchRect(redGO.GetComponent<RectTransform>());
                var redImg = redGO.GetComponent<Image>();
                var redSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/countdown_red.png");
                if (redSpr != null) { redImg.sprite = redSpr; redImg.color = Color.white; }
                redImg.type = Image.Type.Filled;
                redImg.fillMethod = Image.FillMethod.Radial360;
                redImg.fillOrigin = (int)Image.Origin360.Top;
                redImg.fillClockwise = true;
                redImg.fillAmount = 0f;
                redImg.raycastTarget = false;
                redGO.SetActive(false);

                _countdownRingUI = ringGO.AddComponent<CountdownRingUI>();
                _countdownRingUI.baseRing = baseImg;
                _countdownRingUI.blueFill = blueImg;
                _countdownRingUI.redFill  = redImg;

                // ── Logo (Pencil: 0c05h group at x=817, y=489, ~282×97) — above ring ──
                var logoGO = new GameObject("Logo", typeof(RectTransform), typeof(Image));
                logoGO.transform.SetParent(decorLayerGO.transform, false);
                var logoRT = logoGO.GetComponent<RectTransform>();
                logoRT.anchorMin = new Vector2(814f/1920f, 1f-(489f+92f)/1080f);
                logoRT.anchorMax = new Vector2((814f+270f)/1920f, 1f-489f/1080f);
                logoRT.offsetMin = Vector2.zero;
                logoRT.offsetMax = Vector2.zero;
                var logoImg = logoGO.GetComponent<Image>();
                var logoSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/logo_fwtcg.png");
                if (logoSpr != null) { logoImg.sprite = logoSpr; logoImg.color = Color.white; }
                else logoImg.color = Color.clear;
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;
            }

            // ── TopBar / EnemyInfoStrip ──────────────────────────────────────
            var topBar = CreateTopBar(canvasGO.transform,
                out var enemyRuneInfoText, out var enemyDeckInfoText);

            // ── Enemy hand (Pencil: x=644-1276, y=242-402 — actual enemy hand label area) ──
            var enemyHandZone = new GameObject("EnemyHandZone");
            enemyHandZone.transform.SetParent(canvasGO.transform, false);
            {
                var ehRT = enemyHandZone.AddComponent<RectTransform>();
                ehRT.anchorMin = new Vector2(644f/1920f, 1f-402f/1080f);
                ehRT.anchorMax = new Vector2(1276f/1920f, 1f-242f/1080f);
                ehRT.offsetMin = Vector2.zero;
                ehRT.offsetMax = Vector2.zero;

                var ehHLG = enemyHandZone.AddComponent<HorizontalLayoutGroup>();
                ehHLG.childControlWidth = false;
                ehHLG.childControlHeight = true;
                ehHLG.childForceExpandWidth = false;
                ehHLG.childForceExpandHeight = true;
                ehHLG.childAlignment = TextAnchor.MiddleCenter;
                ehHLG.spacing = 4f;
                ehHLG.padding = new RectOffset(10, 10, 4, 4);
            }

            // ── BoardWrapper (main game board, full canvas) ──────────────────
            var boardWrapper = new GameObject("BoardWrapperOuter");
            boardWrapper.transform.SetParent(canvasGO.transform, false);
            {
                var bwRT = boardWrapper.AddComponent<RectTransform>();
                bwRT.anchorMin = Vector2.zero;
                bwRT.anchorMax = Vector2.one;
                bwRT.offsetMin = Vector2.zero;
                bwRT.offsetMax = Vector2.zero;
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

            // ── Player hand (Pencil: x=644-1276, y=666-826 — actual hand cards narrow center strip) ──
            var playerHandZone = new GameObject("PlayerHandZone");
            playerHandZone.transform.SetParent(canvasGO.transform, false);
            {
                var phRT = playerHandZone.AddComponent<RectTransform>();
                phRT.anchorMin = new Vector2(644f/1920f, 1f-826f/1080f);
                phRT.anchorMax = new Vector2(1276f/1920f, 1f-666f/1080f);
                phRT.offsetMin = Vector2.zero;
                phRT.offsetMax = Vector2.zero;

                var phHLG = playerHandZone.AddComponent<HorizontalLayoutGroup>();
                phHLG.childControlWidth = false;
                phHLG.childControlHeight = true;
                phHLG.childForceExpandWidth = false;
                phHLG.childForceExpandHeight = true;
                phHLG.childAlignment = TextAnchor.MiddleCenter;
                phHLG.spacing = 4f;
                phHLG.padding = new RectOffset(10, 10, 6, 4);
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
            messagePanel.SetActive(false); // Pencil 中无此区域

            // ── GameOverPanel ─────────────────────────────────────────────────
            var gameOverPanel = CreateGameOverPanel(canvasGO.transform,
                out var resultText, out var restartButton);

            // ── BannerPanel ───────────────────────────────────────────────────
            var bannerPanel = CreateBannerPanel(canvasGO.transform, out var bannerText);

            // ── EventBanner (DEV-18b) ─────────────────────────────────────────
            var eventBannerGO = CreateEventBannerPanel(canvasGO.transform);

            // ── LegendSkillShowcase (DOT-8) ───────────────────────────────────
            var legendShowcaseGO = new GameObject("LegendSkillShowcase");
            legendShowcaseGO.transform.SetParent(canvasGO.transform, false);
            legendShowcaseGO.AddComponent<FWTCG.UI.LegendSkillShowcase>();

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

            // ── DEV-19: AskPromptPanel ────────────────────────────────────────
            var askPromptPanel = CreateAskPromptPanel(canvasGO.transform,
                out var askTitleText, out var askMessageText,
                out var askCardContainer, out var askConfirmBtn, out var askCancelBtn,
                out var askConfirmBtnText, out var askCancelBtnText);

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
            logToggleGO.SetActive(false); // Pencil 中无此区域
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

            // ── DEV-21: Particle FG layer (top-most — mouse trail + VFX) ──────
            var particleFGLayer = new GameObject("ParticleFGLayer");
            particleFGLayer.transform.SetParent(canvasGO.transform, false);
            {
                var rt = particleFGLayer.AddComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = particleFGLayer.AddComponent<Image>();
                img.color = Color.clear;
                img.raycastTarget = false;
            }

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

            // ── Pencil: BF Field Card & Standby zones (center gap, Canvas-absolute) ──
            // BF0FieldCard: Pencil x=679,y=436,w=106,h=76
            var bf0FieldCard = CreateAnchoredZone(mainArea.transform, "BF0FieldCard",
                679f/1920f, 785f/1920f, 1f-512f/1080f, 1f-436f/1080f);
            {
                var img = bf0FieldCard.AddComponent<Image>();
                img.color = new Color(0.031f, 0.063f, 0.102f, 0.9f); // #08101a
                var ol = bf0FieldCard.AddComponent<Outline>();
                ol.effectColor = new Color(0.565f, 0.439f, 0.125f, 1f); // #907020
                ol.effectDistance = new Vector2(1f, -1f);
                CreateTwoLineLabel(bf0FieldCard.transform, "战场牌名", "FIELD CARD");
            }

            // BF0Standby: Pencil x=696,y=546,w=72,h=100
            var bf0Standby = CreateAnchoredZone(mainArea.transform, "BF0Standby",
                696f/1920f, 768f/1920f, 1f-646f/1080f, 1f-546f/1080f);
            {
                var img = bf0Standby.AddComponent<Image>();
                img.color = new Color(0.031f, 0.063f, 0.102f, 0.9f);
                var ol = bf0Standby.AddComponent<Outline>();
                ol.effectColor = new Color(0.565f, 0.439f, 0.125f, 1f);
                ol.effectDistance = new Vector2(1f, -1f);
                CreateTwoLineLabel(bf0Standby.transform, "待命区", "STANDBY");
            }

            // BF1FieldCard: Pencil x=1135,y=436,w=106,h=76
            var bf1FieldCard = CreateAnchoredZone(mainArea.transform, "BF1FieldCard",
                1135f/1920f, 1241f/1920f, 1f-512f/1080f, 1f-436f/1080f);
            {
                var img = bf1FieldCard.AddComponent<Image>();
                img.color = new Color(0.031f, 0.063f, 0.102f, 0.9f);
                var ol = bf1FieldCard.AddComponent<Outline>();
                ol.effectColor = new Color(0.565f, 0.439f, 0.125f, 1f);
                ol.effectDistance = new Vector2(1f, -1f);
                CreateTwoLineLabel(bf1FieldCard.transform, "战场牌名", "FIELD CARD");
            }

            // BF1Standby: Pencil x=1152,y=546,w=72,h=100
            var bf1Standby = CreateAnchoredZone(mainArea.transform, "BF1Standby",
                1152f/1920f, 1224f/1920f, 1f-646f/1080f, 1f-546f/1080f);
            {
                var img = bf1Standby.AddComponent<Image>();
                img.color = new Color(0.031f, 0.063f, 0.102f, 0.9f);
                var ol = bf1Standby.AddComponent<Outline>();
                ol.effectColor = new Color(0.565f, 0.439f, 0.125f, 1f);
                ol.effectDistance = new Vector2(1f, -1f);
                CreateTwoLineLabel(bf1Standby.transform, "待命区", "STANDBY");
            }

            // Hand zones (outside board)
            var enemyHand       = enemyHandZone.transform;
            var playerHand      = playerHandZone.transform;

            // ── DEV-10: Zone labels + borders ──────────────────────────────────
            // Note: playerBase, enemyBase, playerRunes, enemyRunes removed (Pencil layout)
            if (playerHeroContainer != null) AddZoneLabel(playerHeroContainer, "HERO");
            if (enemyHeroContainer != null) AddZoneLabel(enemyHeroContainer, "HERO");

            // Legend zones
            var pLegendZone = mainArea.transform.Find("PlayerLegendZone");
            var eLegendZone = mainArea.transform.Find("EnemyLegendZone");
            if (pLegendZone != null) AddZoneLabel(pLegendZone, "LEGEND");
            if (eLegendZone != null) AddZoneLabel(eLegendZone, "LEGEND");

            // Discard/Exile zone labels (now separate piles)
            var pDiscardExile = mainArea.transform.Find("PlayerDiscardPile");
            var eDiscardExile = mainArea.transform.Find("EnemyDiscardPile");
            if (pDiscardExile != null) AddZoneLabel(pDiscardExile, "DISCARD");
            if (eDiscardExile != null) AddZoneLabel(eDiscardExile, "DISCARD");

            // Borders on all zones
            Color borderColor = GameColors.GoldDark;
            if (playerHeroContainer != null) AddZoneBorder(playerHeroContainer.parent, borderColor);
            if (enemyHeroContainer != null) AddZoneBorder(enemyHeroContainer.parent, borderColor);
            if (pLegendZone != null) AddZoneBorder(pLegendZone, borderColor);
            if (eLegendZone != null) AddZoneBorder(eLegendZone, borderColor);

            // ── VFX-7: Legend glow overlays removed (gold frame replaces breathing glow) ──
            Image _playerLegendGlow = null;
            Image _enemyLegendGlow  = null;

            _scenery.spinOuter        = null;
            _scenery.spinInner        = null;
            _scenery.sigilOuter       = null;
            _scenery.sigilInner       = null;
            _scenery.dividerOrb       = null;
            _scenery.cornerGems       = null;
            _scenery.playerLegendGlow = _playerLegendGlow;
            _scenery.enemyLegendGlow  = _enemyLegendGlow;

            // ── Card Prefab ───────────────────────────────────────────────────
            EnsureDirectory("Assets/Prefabs");
            var cardPrefab = CreateCardPrefab();

            // ── Rune Prefab ───────────────────────────────────────────────────
            var runePrefab = CreateRunePrefab();

            // ── Startup flow panels (coin flip + mulligan) ────────────────────
            var coinFlipPanel = CreateCoinFlipPanel(canvasGO.transform,
                out var coinFlipText, out var coinFlipOkButton,
                out var coinCircleImage, out var coinResultText, out var scanLightImage);
            var mulliganPanel = CreateMulliganPanel(canvasGO.transform,
                out var mulliganTitleText, out var mulliganCardContainer,
                out var mulliganConfirmButton, out var mulliganConfirmLabel);

            // ── Debug Panel ───────────────────────────────────────────────────
            var debugPanel = CreateDebugPanel(canvasGO.transform,
                out var debugSpellBtn, out var debugEquipBtn,
                out var debugUnitBtn, out var debugReactiveBtn, out var debugManaBtn,
                out var debugSchBtn, out var debugFloatBtn,
                out var debugDmgInput, out var debugTakeHitBtn, out var debugDealHitBtn);
            debugPanel.SetActive(false); // Pencil 中无此区域

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
            // ── Generated UI: ensure all SVG-derived PNGs are imported as Sprite ──
            EnsureGeneratedUISprites();

            // ── CardData ScriptableObjects ────────────────────────────────────
            EnsureDirectory("Assets/Resources/Cards");
            CreateAllCardData();

            // ── Audio (VFX-5) ────────────────────────────────────────────────
            var audioGO = new GameObject("AudioTool");
            audioGO.AddComponent<FWTCG.Audio.AudioTool>();
            audioGO.AddComponent<FWTCG.Audio.AudioManager>();

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
            var combatAnimator    = gmGO.AddComponent<FWTCG.UI.CombatAnimator>(); // DEV-18
            var askPromptUI       = gmGO.AddComponent<FWTCG.UI.AskPromptUI>();  // DEV-19
            var particleManager   = gmGO.AddComponent<FWTCG.UI.ParticleManager>(); // DEV-21
            var mouseTrail        = gmGO.AddComponent<FWTCG.UI.MouseTrail>();      // DEV-21
            var spellVFX          = gmGO.AddComponent<FWTCG.UI.SpellVFX>();        // DEV-21
            gmGO.AddComponent<FWTCG.UI.MouseLineFX>();   // VFX-7p
            gmGO.AddComponent<FWTCG.UI.AimTargetFX>();   // VFX-7q

            // ── Wire UI references via SerializedObject ───────────────────────
            WireGameUI(gameUI, canvasGO.GetComponent<Canvas>(), cardPrefab, runePrefab,
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
                bf1Glow, bf2Glow, bf1CardArt, bf2CardArt, boardFlashImg,
                _countdownRingUI);

            WireGameManager(gameMgr, turnMgr, combatSys, scoreMgr, simpleAI, gameUI,
                            entryEffects, deathwish, spellSys, reactiveSys,
                            startupFlowUI, reactiveWindowUI,
                            coinFlipPanel, coinFlipText, coinFlipOkButton,
                            coinCircleImage, coinResultText, scanLightImage,
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

            // ── DEV-19: Wire AskPromptUI ─────────────────────────────────────
            {
                var apSO = new SerializedObject(askPromptUI);
                apSO.FindProperty("_panel").objectReferenceValue             = askPromptPanel;
                apSO.FindProperty("_canvasGroup").objectReferenceValue       = askPromptPanel.GetComponent<CanvasGroup>();
                apSO.FindProperty("_titleText").objectReferenceValue         = askTitleText;
                apSO.FindProperty("_messageText").objectReferenceValue       = askMessageText;
                apSO.FindProperty("_cardContainer").objectReferenceValue     = askCardContainer;
                apSO.FindProperty("_cardViewPrefab").objectReferenceValue    = cardPrefab;
                apSO.FindProperty("_confirmBtn").objectReferenceValue        = askConfirmBtn;
                apSO.FindProperty("_cancelBtn").objectReferenceValue         = askCancelBtn;
                apSO.FindProperty("_confirmBtnText").objectReferenceValue    = askConfirmBtnText;
                apSO.FindProperty("_cancelBtnText").objectReferenceValue     = askCancelBtnText;
                apSO.ApplyModifiedPropertiesWithoutUndo();

                var guiSO3 = new SerializedObject(gameUI);
                guiSO3.FindProperty("_askPromptUI").objectReferenceValue = askPromptUI;
                guiSO3.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── DEV-21: Wire ParticleManager ─────────────────────────────────
            {
                var pmSO = new SerializedObject(particleManager);
                pmSO.FindProperty("_canvasRect").objectReferenceValue =
                    canvasGO.GetComponent<RectTransform>();
                pmSO.FindProperty("_bgLayer").objectReferenceValue =
                    particleBGLayer.transform;
                pmSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── DEV-21: Wire MouseTrail ──────────────────────────────────────
            {
                var mtSO = new SerializedObject(mouseTrail);
                mtSO.FindProperty("_canvasRect").objectReferenceValue =
                    canvasGO.GetComponent<RectTransform>();
                mtSO.FindProperty("_canvas").objectReferenceValue =
                    canvasGO.GetComponent<Canvas>();
                mtSO.FindProperty("_fgLayer").objectReferenceValue =
                    particleFGLayer.transform;
                mtSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── DEV-21: Wire SpellVFX ────────────────────────────────────────
            {
                var svSO = new SerializedObject(spellVFX);
                svSO.FindProperty("_vfxLayer").objectReferenceValue =
                    particleFGLayer.transform;
                svSO.ApplyModifiedPropertiesWithoutUndo();
            }

            // ── Save scene ────────────────────────────────────────────────────
            EnsureDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameScene.unity");
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] GameScene.unity 创建成功！");
        }

        // ── SVG-gen sprite helpers ────────────────────────────────────────────

        /// <summary>
        /// Loads a PNG from Assets/Resources/UI/Generated/{stem}.png as a Sprite.
        /// Returns null if the file does not exist yet.
        /// </summary>
        private static Sprite GenSpr(string stem) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/UI/Generated/{stem}.png");

        /// <summary>
        /// If a generated sprite exists, applies it to <paramref name="img"/> in Sliced mode
        /// and sets the colour to white so the sprite renders at full quality.
        /// Falls back to the existing solid colour when the file is missing.
        /// </summary>
        private static void TryApplySvgSprite(Image img, string stem, Image.Type mode = Image.Type.Simple)
        {
            var spr = GenSpr(stem);
            if (spr == null) return;
            img.sprite           = spr;
            img.type             = mode;
            img.color            = Color.white;
            img.preserveAspect   = false;   // always fill the RectTransform
        }

        // ── DEV-23: Decorative helpers ────────────────────────────────────────

        /// <summary>Creates a square disc Image centered at canvas midpoint, for ambient decor.</summary>
        private static Image CreateDecorDisc(Transform parent, string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.one * 0.5f;
            rt.anchorMax = Vector2.one * 0.5f;
            rt.pivot = Vector2.one * 0.5f;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.one * size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Creates a fullscreen glow overlay Image as last child of the given hero container.</summary>
        private static Image CreateLegendGlowOverlay(Transform heroContainer)
        {
            var go = new GameObject("LegendGlow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(heroContainer, false);
            go.transform.SetAsLastSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(GameColors.BlueSpell.r, GameColors.BlueSpell.g, GameColors.BlueSpell.b, SceneryUI.LEGEND_GLOW_ALPHA_MIN); // blue, starts dim
            img.raycastTarget = false;
            return img;
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

            // Pencil has no top bar — make background transparent, keep text refs for game logic
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            hlg.padding = new RectOffset(10, 10, 2, 2);
            hlg.spacing = 10f;

            var eScore = CreateTMPText(go.transform, "EnemyScore", "AI: 0/8", GameColors.EnemyRed, 13, TextAnchor.MiddleLeft);
            eScore.color = new Color(1f, 0.3f, 0.3f, 0.75f);
            CreateTMPText(go.transform, "RoundInfo", "回合 1", GameColors.GoldLight, 13, TextAnchor.MiddleCenter);
            enemyRuneInfoText = CreateTMPText(go.transform, "EnemyRuneInfo", "", GameColors.GoldDark, 12, TextAnchor.MiddleRight);
            enemyDeckInfoText = CreateTMPText(go.transform, "EnemyDeckInfo", "", GameColors.GoldDark, 12, TextAnchor.MiddleRight);
            var pScore = CreateTMPText(go.transform, "PlayerScore", "玩家: 0/8", GameColors.PlayerGreen, 13, TextAnchor.MiddleRight);
            pScore.color = new Color(0.3f, 1f, 0.5f, 0.75f);

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

            // ── Score tracks (Pencil: left x=47 w=22, y=446-820; right x=1854 w=22, y=260-634) ──
            // Pencil: Player circles at x=47, y=446-798 (9×44px spacing), bottom=820
            playerScoreCircleImages = new Image[9];
            CreateScoreTrack(go.transform, "PlayerScoreTrack", true,
                47f/1920f, 69f/1920f, 1f-820f/1080f, 1f-446f/1080f, playerScoreCircleImages);

            // Pencil: Enemy circles at x=1854, y=260-612 (9×44px spacing), bottom=634
            enemyScoreCircleImages = new Image[9];
            CreateScoreTrack(go.transform, "EnemyScoreTrack", false,
                1854f/1920f, 1876f/1920f, 1f-634f/1080f, 1f-260f/1080f, enemyScoreCircleImages);

            // ── ENEMY SIDE (top) — Pencil: 传说E(391,-48,118×154), 英雄E(262,-48,118×154) ──
            var enemyLegendZone = CreatePlayerLegendZone(go.transform, "EnemyLegendZone",
                false, 391f/1920f, 509f/1920f, 1f-106f/1080f, 1f+48f/1080f,
                out enemyLegendText, out _);

            CreateHeroZone(go.transform, "EnemyHeroZone",
                262f/1920f, 380f/1920f, 1f-106f/1080f, 1f+48f/1080f, out enemyHeroContainer);

            // ── ENEMY DECK PILES (Pencil positions) ──
            // 符文堆E (left top): 92,73,138×195
            CreateDeckPile(go.transform, "EnemyRunePile", "符文堆",
                92f/1920f, 230f/1920f, 1f-268f/1080f, 1f-73f/1080f, out enemyRunePileCount);

            // 弃牌E (right top): 1689,73,139×195
            CreateDeckPile(go.transform, "EnemyDiscardPile", "弃牌",
                1689f/1920f, 1828f/1920f, 1f-268f/1080f, 1f-73f/1080f, out enemyDiscardCount);

            // 主牌堆E (right 2nd): 1689,274,139×195
            CreateDeckPile(go.transform, "EnemyMainPile", "主牌堆",
                1689f/1920f, 1828f/1920f, 1f-469f/1080f, 1f-274f/1080f, out enemyDeckCount);

            // 放逐区E (left 2nd): 92,274,139×195
            CreateDeckPile(go.transform, "EnemyExilePile", "放逐区",
                92f/1920f, 231f/1920f, 1f-469f/1080f, 1f-274f/1080f, out enemyExileCount);

            // ── PLAYER SIDE (bottom) — Pencil: 英雄P(262,974,118×154), 传说P(391,974,118×154) ──
            CreateHeroZone(go.transform, "PlayerHeroZone",
                262f/1920f, 380f/1920f, 1f-1128f/1080f, 1f-974f/1080f, out playerHeroContainer);

            var playerLegendZone = CreatePlayerLegendZone(go.transform, "PlayerLegendZone",
                true, 391f/1920f, 509f/1920f, 1f-1128f/1080f, 1f-974f/1080f,
                out playerLegendText, out legendSkillBtn);

            // ── PLAYER DECK PILES (Pencil positions) ──
            // 主牌堆P (right 3rd): 1689,616,139×195
            CreateDeckPile(go.transform, "PlayerMainPile", "主牌堆",
                1689f/1920f, 1828f/1920f, 1f-811f/1080f, 1f-616f/1080f, out playerDeckCount);

            // 符文堆P (left bottom): 92,820,138×195
            CreateDeckPile(go.transform, "PlayerRunePile", "符文堆",
                92f/1920f, 230f/1920f, 1f-1015f/1080f, 1f-820f/1080f, out playerRunePileCount);

            // 弃牌P (right bottom): 1689,820,139×195
            CreateDeckPile(go.transform, "PlayerDiscardPile", "弃牌",
                1689f/1920f, 1828f/1920f, 1f-1015f/1080f, 1f-820f/1080f, out playerDiscardCount);

            // 放逐区P (left 3rd): 92,616,139×195
            CreateDeckPile(go.transform, "PlayerExilePile", "放逐区",
                92f/1920f, 231f/1920f, 1f-811f/1080f, 1f-616f/1080f, out playerExileCount);

            // ── Pencil: EnemyRunes 条带 (y=155-240, x=248-1672) ────────────────
            var enemyRunesZone = CreateAnchoredZone(go.transform, "EnemyRunes",
                248f/1920f, 1672f/1920f, 1f-240f/1080f, 1f-155f/1080f);
            {
                var img = enemyRunesZone.AddComponent<Image>();
                img.color = new Color(0.02f, 0.06f, 0.10f, 0.75f); // #041328 semi
                // RUNES 左标签 (Pencil: "RUNES  符文区", 11pt bold, #c7ae87)
                var lbl = new GameObject("RunesLabel");
                lbl.transform.SetParent(enemyRunesZone.transform, false);
                var lblTxt = lbl.AddComponent<Text>();
                lblTxt.text = "RUNES  符文区";
                lblTxt.color = GameColors.GoldMid;
                lblTxt.fontSize = 11; lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.MiddleLeft;
                lblTxt.raycastTarget = false;
                if (_font != null) lblTxt.font = _font;
                var lblRT = lbl.GetComponent<RectTransform>();
                if (lblRT != null) { lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(0.1f, 1f); lblRT.offsetMin = new Vector2(4f, 0f); lblRT.offsetMax = Vector2.zero; }
                // 圆形符文槽 (12个，均匀分布在中间)
                CreateRuneSlotRow(enemyRunesZone.transform, 12, false);
            }

            // ── Pencil: EnemyBase 区域 (y=242-402, x=248-1672) ───────────────
            var enemyBaseZone = CreateAnchoredZone(go.transform, "EnemyBase",
                248f/1920f, 1672f/1920f, 1f-402f/1080f, 1f-242f/1080f);
            {
                var img = enemyBaseZone.AddComponent<Image>();
                img.color = new Color(0.02f, 0.06f, 0.10f, 0.55f);
                // BASE 左标签 (Pencil: "BASE  基地", 11pt bold, #c7ae87)
                var lbl = new GameObject("BaseLabel");
                lbl.transform.SetParent(enemyBaseZone.transform, false);
                var lblTxt = lbl.AddComponent<Text>();
                lblTxt.text = "BASE  基地";
                lblTxt.color = GameColors.GoldMid;
                lblTxt.fontSize = 11; lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.UpperLeft;
                lblTxt.raycastTarget = false;
                if (_font != null) lblTxt.font = _font;
                var lblRT = lbl.GetComponent<RectTransform>();
                if (lblRT != null) { lblRT.anchorMin = new Vector2(0f, 0.8f); lblRT.anchorMax = new Vector2(0.1f, 1f); lblRT.offsetMin = new Vector2(4f, 0f); lblRT.offsetMax = Vector2.zero; }
                // 4个卡槽列（左侧 x=248-644，不覆盖手牌区 x=644-1276，右侧 x=1276-1672）
                CreateBaseCardSlots(enemyBaseZone.transform, true);
            }

            // ── Pencil: PlayerBase 区域 (y=666-826, x=248-1672) ──────────────
            var playerBaseZone = CreateAnchoredZone(go.transform, "PlayerBase",
                248f/1920f, 1672f/1920f, 1f-826f/1080f, 1f-666f/1080f);
            {
                var img = playerBaseZone.AddComponent<Image>();
                img.color = new Color(0.02f, 0.06f, 0.10f, 0.55f);
                var lbl = new GameObject("BaseLabel");
                lbl.transform.SetParent(playerBaseZone.transform, false);
                var lblTxt = lbl.AddComponent<Text>();
                lblTxt.text = "BASE  基地";
                lblTxt.color = GameColors.GoldMid;
                lblTxt.fontSize = 11; lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.LowerLeft;
                lblTxt.raycastTarget = false;
                if (_font != null) lblTxt.font = _font;
                var lblRT = lbl.GetComponent<RectTransform>();
                if (lblRT != null) { lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(0.1f, 0.2f); lblRT.offsetMin = new Vector2(4f, 0f); lblRT.offsetMax = Vector2.zero; }
                CreateBaseCardSlots(playerBaseZone.transform, false);
            }

            // ── Pencil: PlayerRunes 条带 (y=828-913, x=248-1672) ─────────────
            var playerRunesZone = CreateAnchoredZone(go.transform, "PlayerRunes",
                248f/1920f, 1672f/1920f, 1f-913f/1080f, 1f-828f/1080f);
            {
                var img = playerRunesZone.AddComponent<Image>();
                img.color = new Color(0.016f, 0.047f, 0.078f, 0.5f);
                var lbl = new GameObject("RunesLabel");
                lbl.transform.SetParent(playerRunesZone.transform, false);
                var lblTxt = lbl.AddComponent<Text>();
                lblTxt.text = "RUNES  符文区";
                lblTxt.color = GameColors.GoldMid;
                lblTxt.fontSize = 11; lblTxt.fontStyle = FontStyle.Bold;
                lblTxt.alignment = TextAnchor.MiddleLeft;
                lblTxt.raycastTarget = false;
                if (_font != null) lblTxt.font = _font;
                var lblRT = lbl.GetComponent<RectTransform>();
                if (lblRT != null) { lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(0.1f, 1f); lblRT.offsetMin = new Vector2(4f, 0f); lblRT.offsetMax = Vector2.zero; }
                CreateRuneSlotRow(playerRunesZone.transform, 12, true);
            }

            // ── Battlefields (Pencil: BF0 x=280-632, BF1 x=1288-1640, y=416-652) ──
            // Pencil: BF0 x=280-632, BF1 x=1288-1640, gap=656px in center for ring visibility
            // BattlefieldsArea spans x=280-1640 (total 1360px). Each panel uses absolute anchors.
            var bfArea = CreateAnchoredZone(go.transform, "BattlefieldsArea",
                280f/1920f, 1640f/1920f, 1f-652f/1080f, 1f-416f/1080f);
            {
                // BF1Panel = left (Pencil BF0: x=280-632, relative xMin=0, xMax=352/1360)
                CreateBattlefieldPanel(bfArea.transform, "BF1Panel", "BF1EnemyUnits", "战场1", "BF1Label", "BF1PlayerUnits",
                    out bf1CtrlBadge, out bf1CtrlBadgeText, out bf1CardArt, out bf1Glow);
                var bf1RT = bfArea.transform.Find("BF1Panel").GetComponent<RectTransform>();
                bf1RT.anchorMin = new Vector2(0f, 0f);
                bf1RT.anchorMax = new Vector2(352f/1360f, 1f);
                bf1RT.offsetMin = Vector2.zero; bf1RT.offsetMax = Vector2.zero;

                // BF2Panel = right (Pencil BF1: x=1288-1640, relative xMin=1008/1360, xMax=1)
                CreateBattlefieldPanel(bfArea.transform, "BF2Panel", "BF2EnemyUnits", "战场2", "BF2Label", "BF2PlayerUnits",
                    out bf2CtrlBadge, out bf2CtrlBadgeText, out bf2CardArt, out bf2Glow);
                var bf2RT = bfArea.transform.Find("BF2Panel").GetComponent<RectTransform>();
                bf2RT.anchorMin = new Vector2(1008f/1360f, 0f);
                bf2RT.anchorMax = new Vector2(1f, 1f);
                bf2RT.offsetMin = Vector2.zero; bf2RT.offsetMax = Vector2.zero;
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

        // ── Stretch rect helper (fills parent) ──────────────────────────────
        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
            bool isBase  = name.Contains("Base");
            bool isRune  = name.Contains("Rune");
            var img = go.AddComponent<Image>();
            img.color = isBase ? ZoneBgBase : ZoneBgDefault;
            if      (isBase) TryApplySvgSprite(img, "zone_base",  Image.Type.Simple);
            else if (isRune) TryApplySvgSprite(img, "zone_rune",  Image.Type.Simple);
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

            // Score track background
            var trackBg = go.AddComponent<Image>();
            trackBg.color = new Color(0.03f, 0.08f, 0.15f, 0.85f);
            TryApplySvgSprite(trackBg, "ui_score_frame");

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
                TryApplySvgSprite(img, "deco_divider_orb");

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
            TryApplySvgSprite(img, "zone_hero", Image.Type.Simple);
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
        // Pencil layout: card_back_03.png fills entire pile, label text at y≈31,
        // count text at y≈137 (out of 195px height). No VLG — absolute overlay.

        private static void CreateDeckPile(Transform parent, string name, string label,
            float xMin, float xMax, float yMin, float yMax, out Text countText)
        {
            var go = CreateAnchoredZone(parent, name, xMin, xMax, yMin, yMax);

            // Background: card back texture fills entire pile area (Pencil: 贴图层)
            var img = go.AddComponent<Image>();
            var cardBackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/UI/Generated/card_back_pencil.png");
            if (cardBackSprite != null)
            {
                img.sprite = cardBackSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
            }
            else
            {
                // Fallback: solid dark blue
                img.color = GameColors.CardFaceDown;
            }

            // Pile label — top region, Pencil: y=31/195 → yMax≈0.84, 20pt bold #c7ae87
            var labelGO = new GameObject("PileLabel");
            labelGO.transform.SetParent(go.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0.72f);
            labelRT.anchorMax = new Vector2(1f, 0.90f);
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;
            var labelTxt = labelGO.AddComponent<Text>();
            labelTxt.text = label;
            labelTxt.font = _font;
            labelTxt.fontSize = 20;
            labelTxt.fontStyle = FontStyle.Bold;
            labelTxt.color = GameColors.GoldMid;
            labelTxt.alignment = TextAnchor.MiddleCenter;
            labelTxt.resizeTextForBestFit = false;

            // Count text — lower region, Pencil: y=137/195 → yMax≈0.30, 30pt bold #c7ae87
            var countGO = new GameObject("Count");
            countGO.transform.SetParent(go.transform, false);
            var countRT = countGO.AddComponent<RectTransform>();
            countRT.anchorMin = new Vector2(0f, 0.08f);
            countRT.anchorMax = new Vector2(1f, 0.32f);
            countRT.offsetMin = Vector2.zero;
            countRT.offsetMax = Vector2.zero;
            countText = countGO.AddComponent<Text>();
            countText.text = "0";
            countText.font = _font;
            countText.fontSize = 30;
            countText.fontStyle = FontStyle.Bold;
            countText.color = GameColors.GoldMid;
            countText.alignment = TextAnchor.MiddleCenter;
            countText.resizeTextForBestFit = false;
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
            go.AddComponent<FWTCG.UI.CardHoverScale>(); // hover zoom on legend zone

            // VFX-7: click whole card to trigger skill (replaces SkillBtn)
            skillBtn = go.AddComponent<Button>();
            var btnColors = skillBtn.colors;
            btnColors.normalColor = Color.white;
            btnColors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            skillBtn.colors = btnColors;

            // LegendArt will be overlaid at runtime by RefreshLegendArt (ignoreLayout)

            // Bottom half dark overlay (same as regular cards)
            var bottomOvl = new GameObject("BottomOverlay");
            bottomOvl.transform.SetParent(go.transform, false);
            var boImg = bottomOvl.AddComponent<Image>();
            boImg.color = new Color(0f, 0f, 0f, 0.78f);
            boImg.raycastTarget = false;
            var boRT = bottomOvl.GetComponent<RectTransform>();
            boRT.anchorMin = new Vector2(0f, 0f);
            boRT.anchorMax = new Vector2(1f, 0.50f);
            boRT.offsetMin = new Vector2(3f, 3f);
            boRT.offsetMax = new Vector2(-3f, 0f);
            var boLE = bottomOvl.AddComponent<LayoutElement>();
            boLE.ignoreLayout = true;

            // Legend name text — INSIDE bottom overlay (top portion of black area)
            var textGO = new GameObject(isPlayer ? "LegendText" : "EnemyLegendText");
            textGO.transform.SetParent(go.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.05f, 0.35f);
            textRT.anchorMax = new Vector2(0.95f, 0.50f);
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var textLE = textGO.AddComponent<LayoutElement>();
            textLE.ignoreLayout = true;
            legendText = textGO.AddComponent<Text>();
            legendText.text = isPlayer ? "卡莎" : "易大师";
            legendText.color = Color.white;
            legendText.fontSize = 10;
            legendText.fontStyle = FontStyle.Bold;
            legendText.alignment = TextAnchor.MiddleCenter;
            legendText.horizontalOverflow = HorizontalWrapMode.Wrap;
            legendText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) legendText.font = _font;
            textGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            textGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

            // Description text — INSIDE bottom overlay (below name)
            var descGO = new GameObject("LegendDesc");
            descGO.transform.SetParent(go.transform, false);
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.05f, 0.03f);
            descRT.anchorMax = new Vector2(0.95f, 0.35f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;
            var descLE = descGO.AddComponent<LayoutElement>();
            descLE.ignoreLayout = true;
            var descText = descGO.AddComponent<Text>();
            descText.text = ""; // filled at runtime
            descText.color = Color.white;
            descText.fontSize = 7;
            descText.alignment = TextAnchor.UpperCenter;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow   = VerticalWrapMode.Truncate;
            if (_font != null) descText.font = _font;
            descGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            descGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.7f);

            // VFX-7k: glow overlay for hover highlight
            var glowGO = new GameObject("LegendGlowOverlay");
            glowGO.transform.SetParent(go.transform, false);
            var glowRT = glowGO.AddComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-4f, -4f);
            glowRT.offsetMax = new Vector2(4f, 4f);
            var glowLE = glowGO.AddComponent<LayoutElement>();
            glowLE.ignoreLayout = true;
            var glowImg = glowGO.AddComponent<Image>();
            var glowSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX/card_glow.png");
            if (glowSpr != null) glowImg.sprite = glowSpr;
            glowImg.color = new Color(0.29f, 0.87f, 0.50f, 0f); // green, starts invisible
            glowImg.raycastTarget = false;
            glowImg.type = Image.Type.Simple;

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

            // SVG-gen battlefield background — preserveAspect to avoid horizontal distortion
            var bfBgImg = panel.AddComponent<Image>();
            bfBgImg.color = new Color(3f/255f, 14f/255f, 26f/255f, 0.85f);
            bfBgImg.raycastTarget = false;
            TryApplySvgSprite(bfBgImg, "zone_battlefield", Image.Type.Simple);

            // Anchor-based layout — no VLG, so content is always geometrically centred
            // regardless of panel height.  The SVG hex-gem sits at (50 %, 50 %), so we
            // pin the LabelRow pivot at y = 50 % and let the unit zones fill above/below.
            //
            //   EnemyZone   : anchor y = 0.50 → 1.00,  offsetMin.y = +14  (above label)
            //   LabelRow    : anchor y = 0.50,  offsetMin.y = -14, offsetMax.y = +14
            //   PlayerZone  : anchor y = 0.04 → 0.50,  offsetMax.y = -14  (below label)
            //   StandbyZone : anchor y = 0.00 → 0.04   (bottom strip ≈ 4 %)

            // ── Enemy units zone (Pencil: top 44.9%, y=0→106 of 236) ──────────
            // Unity Y: 55.1% → 100% (0.551 → 1.0)
            {
                var go = new GameObject(enemyZoneName);
                go.transform.SetParent(panel.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.551f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var hlg = go.AddComponent<HorizontalLayoutGroup>();
                hlg.childControlWidth  = false; hlg.childControlHeight  = false;
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 4f;
            }

            // Label row — hidden (Pencil has no visible BF label; keeps refs for GameUI)
            var labelRow = new GameObject("LabelRow");
            labelRow.transform.SetParent(panel.transform, false);
            var labelRowRT = labelRow.AddComponent<RectTransform>();
            labelRowRT.anchorMin = new Vector2(0f, 0.449f);
            labelRowRT.anchorMax = new Vector2(1f, 0.551f);
            labelRowRT.offsetMin = Vector2.zero;
            labelRowRT.offsetMax = Vector2.zero;
            var labelRowHLG = labelRow.AddComponent<HorizontalLayoutGroup>();
            labelRowHLG.childControlWidth = false;
            labelRowHLG.childControlHeight = false;
            labelRowHLG.childForceExpandWidth = false;
            labelRowHLG.childForceExpandHeight = false;
            labelRowHLG.spacing = 4f;
            labelRowHLG.childAlignment = TextAnchor.MiddleCenter;
            labelRow.SetActive(false); // Pencil has no BF label overlay

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

            // ── Player units zone (Pencil: bottom 44.9%, y=130→236 of 236) ───
            // Unity Y: 0% → 44.9% (0.0 → 0.449)
            {
                var go = new GameObject(playerZoneName);
                go.transform.SetParent(panel.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0.449f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var hlg = go.AddComponent<HorizontalLayoutGroup>();
                // childControlHeight=false: cards keep their own 110px height instead of
                // stretching to fill the zone (which can be 160-200px, causing distortion).
                hlg.childControlWidth  = false; hlg.childControlHeight  = false;
                hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.spacing = 4f;
            }

            // StandbyZone removed — Pencil standby positions are in the center gap (FlFzE/9wu6n)
            // No visible standby strip inside BF panel

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

            // Pencil: action buttons at bottom-right (x=1538-1646, y=926-999)
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1538f/1920f, 1f - 999f/1080f);
            rt.anchorMax = new Vector2(1646f/1920f, 1f - 921f/1080f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Hidden info strip — no visual, keeps GameUI refs
            var infoStrip = new GameObject("PlayerInfoStrip");
            infoStrip.transform.SetParent(go.transform, false);
            var infoRT = infoStrip.AddComponent<RectTransform>();
            infoRT.anchorMin = Vector2.zero; infoRT.anchorMax = Vector2.zero;
            infoRT.sizeDelta = Vector2.zero;

            manaDisplay = CreateTMPText(infoStrip.transform, "ManaDisplay", "", GameColors.PlayerGreen, 12, TextAnchor.MiddleLeft);
            schDisplay  = CreateTMPText(infoStrip.transform, "SchDisplay", "", GameColors.GoldLight, 12, TextAnchor.MiddleLeft);
            playerRuneInfoText = CreateTMPText(infoStrip.transform, "PlayerRuneInfo", "", GameColors.GoldDark, 12, TextAnchor.MiddleCenter);
            playerDeckInfoText = CreateTMPText(infoStrip.transform, "PlayerDeckInfo", "", GameColors.GoldDark, 12, TextAnchor.MiddleCenter);

            // ── Action panel — full width, semi-transparent ──
            var actionPanel = new GameObject("ActionPanel");
            actionPanel.transform.SetParent(go.transform, false);
            var actionRT = actionPanel.AddComponent<RectTransform>();
            actionRT.anchorMin = Vector2.zero; actionRT.anchorMax = Vector2.one;
            actionRT.offsetMin = Vector2.zero; actionRT.offsetMax = Vector2.zero;

            // Pencil: VLG — 结束回合 (top, 108×34) then 查看弃牌堆 (bottom, 77×24)
            var actionVLG = actionPanel.AddComponent<VerticalLayoutGroup>();
            actionVLG.childControlWidth = true;
            actionVLG.childControlHeight = true;   // must be true so preferredHeight is respected
            actionVLG.childForceExpandWidth = true;
            actionVLG.childForceExpandHeight = false;
            actionVLG.padding = new RectOffset(0, 0, 0, 0);
            actionVLG.spacing = 5f;
            actionVLG.childAlignment = TextAnchor.UpperCenter;

            // Hidden contextual refs (not visible by default, kept for GameUI wiring)
            phaseDisplay = CreateTMPText(actionPanel.transform, "PhaseDisplay", "", GameColors.GoldLight, 11, TextAnchor.MiddleCenter);
            phaseDisplay.gameObject.SetActive(false);
            tapAllRunesBtn = CreateActionButton(actionPanel.transform, "TapAllRunesBtn", "全部横置", GameColors.ActionBtnSecondary);
            tapAllRunesBtn.gameObject.SetActive(false);
            cancelRunesBtn = CreateActionButton(actionPanel.transform, "CancelRunesBtn", "取消", GameColors.ActionBtnDanger);
            cancelRunesBtn.gameObject.SetActive(false);
            confirmRunesBtn = CreateActionButton(actionPanel.transform, "ConfirmRunesBtn", "确认符文操作", GameColors.ActionBtnPrimary);
            confirmRunesBtn.gameObject.SetActive(false);
            skipReactionBtn = CreateActionButton(actionPanel.transform, "SkipReactionBtn", "跳过响应", GameColors.ActionBtnSecondary);
            skipReactionBtn.gameObject.SetActive(false);

            // 结束回合 — Pencil: 108×34
            endTurnButton = CreateActionButton(actionPanel.transform, "EndTurnButton", "结束回合", GameColors.ActionBtnPrimary);
            var endLE = endTurnButton.gameObject.GetComponent<LayoutElement>() ?? endTurnButton.gameObject.AddComponent<LayoutElement>();
            endLE.preferredHeight = 34f;
            // VFX-7d / SVG-gen: apply EndTurn button sprite if available
            var endTurnSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/UI/Generated/btn_end_turn.png")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/button_endturn.png");
            if (endTurnSpr != null)
            {
                var endImg = endTurnButton.GetComponent<Image>();
                endImg.sprite = endTurnSpr;
                endImg.type = Image.Type.Simple;
                endImg.color = Color.white;
            }
            // 查看弃牌堆 — Pencil: 77×24, below 结束回合
            reactBtn = CreateActionButton(actionPanel.transform, "ReactButton", "查看弃牌堆", new Color(0.2f, 0.16f, 0.08f, 0.9f));
            var reactLE = reactBtn.gameObject.GetComponent<LayoutElement>() ?? reactBtn.gameObject.AddComponent<LayoutElement>();
            reactLE.preferredHeight = 24f;

            // DEV-19: ButtonCharge hover sweep on key buttons
            AddButtonCharge(endTurnButton.gameObject);
            AddButtonCharge(reactBtn.gameObject);
            AddButtonCharge(confirmRunesBtn.gameObject);

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

            // SVG-gen: pick sprite by button name
            if      (name.Contains("EndTurn"))    TryApplySvgSprite(img, "btn_end_turn",  Image.Type.Simple);
            else if (name.Contains("React"))      TryApplySvgSprite(img, "btn_react",      Image.Type.Simple);
            else if (name.Contains("Skip") || name.Contains("SkipReact")) TryApplySvgSprite(img, "btn_skip", Image.Type.Simple);
            else if (name.Contains("Confirm"))    TryApplySvgSprite(img, "btn_confirm",    Image.Type.Simple);
            else if (name.Contains("Cancel"))     TryApplySvgSprite(img, "btn_cancel",     Image.Type.Simple);

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
            TryApplySvgSprite(img, "panel_message_log", Image.Type.Simple);

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
            TryApplySvgSprite(go.GetComponent<Image>(), "bg_game_over", Image.Type.Simple);
            // DEV-24: CanvasGroup for fade-in
            go.AddComponent<CanvasGroup>();

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
            img.color = new Color(0f, 0f, 0f, 0.96f); // near-opaque to block zone borders showing through

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
            rt.anchorMin = new Vector2(0.5f, 0.57f);
            rt.anchorMax = new Vector2(0.5f, 0.57f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(200f, 36f); // Awake() resizes per text content

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
            TryApplySvgSprite(bg, "panel_spell_showcase");

            // CanvasGroup for alpha animation
            var cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false; // don't block input during showcase

            // ── Card container (animated) ──
            var cardPanelGO = new GameObject("CardPanel");
            cardPanelGO.transform.SetParent(go.transform, false);
            var cpRT = cardPanelGO.AddComponent<RectTransform>();
            cpRT.anchorMin = new Vector2(0.40f, 0.36f);
            cpRT.anchorMax = new Vector2(0.60f, 0.64f);
            cpRT.offsetMin = Vector2.zero;
            cpRT.offsetMax = Vector2.zero;

            var cpImg = cardPanelGO.AddComponent<Image>();
            cpImg.color = new Color(0.02f, 0.06f, 0.14f, 0.95f);
            cardPanelGO.AddComponent<FWTCG.UI.GlassPanelFX>();  // DEV-25 glass effect
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

            // ── Group panel (multi-card showcase, sibling to CardPanel) ──
            var groupPanelGO = new GameObject("GroupPanel");
            groupPanelGO.transform.SetParent(go.transform, false);
            var gpRT = groupPanelGO.AddComponent<RectTransform>();
            gpRT.anchorMin = new Vector2(0.10f, 0.30f);
            gpRT.anchorMax = new Vector2(0.90f, 0.70f);
            gpRT.offsetMin = Vector2.zero;
            gpRT.offsetMax = Vector2.zero;

            var gpImg = groupPanelGO.AddComponent<Image>();
            gpImg.color = new Color(0.02f, 0.06f, 0.14f, 0.92f);
            var gpOutline = groupPanelGO.AddComponent<Outline>();
            gpOutline.effectColor = new Color(200f/255f, 170f/255f, 110f/255f, 0.7f);
            gpOutline.effectDistance = new Vector2(2f, -2f);

            // SlotsRoot: HLG row, centered inside GroupPanel
            var slotsRootGO = new GameObject("SlotsRoot");
            slotsRootGO.transform.SetParent(groupPanelGO.transform, false);
            var srRT = slotsRootGO.AddComponent<RectTransform>();
            srRT.anchorMin = new Vector2(0f, 0f);
            srRT.anchorMax = new Vector2(1f, 1f);
            srRT.offsetMin = new Vector2(16f, 16f);
            srRT.offsetMax = new Vector2(-16f, -16f);
            var hlg = slotsRootGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing             = 16f;
            hlg.childAlignment      = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(8, 8, 8, 8);

            groupPanelGO.SetActive(false); // hidden until ShowGroupAsync activates it

            // ── Attach SpellShowcaseUI component ──
            var showcase = go.AddComponent<FWTCG.UI.SpellShowcaseUI>();
            var showcaseSO = new UnityEditor.SerializedObject(showcase);
            showcaseSO.FindProperty("_canvasGroup").objectReferenceValue  = cg;
            showcaseSO.FindProperty("_cardPanel").objectReferenceValue    = cpRT;
            showcaseSO.FindProperty("_ownerLabel").objectReferenceValue   = ownerLabel;
            showcaseSO.FindProperty("_cardNameText").objectReferenceValue = cardNameText;
            showcaseSO.FindProperty("_effectText").objectReferenceValue   = effectText;
            showcaseSO.FindProperty("_artImage").objectReferenceValue     = artImg;
            showcaseSO.FindProperty("_groupPanel").objectReferenceValue   = gpRT;
            showcaseSO.FindProperty("_slotsRoot").objectReferenceValue    = slotsRootGO.transform;
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

        // ── DEV-19: AskPromptPanel (general async dialog) ────────────────────

        private static GameObject CreateAskPromptPanel(Transform parent,
            out Text titleText, out Text messageText,
            out Transform cardContainer,
            out Button confirmBtn, out Button cancelBtn,
            out Text confirmBtnText, out Text cancelBtnText)
        {
            // Full-screen dim overlay — opaque enough to hide hand cards behind it
            var panel = CreateFullscreenPanel(parent, "AskPromptPanel", new Color(0f, 0f, 0f, 0.88f));
            panel.AddComponent<CanvasGroup>();

            // ── Inner content box (centered, fixed size, dark panel) ──────────
            var boxGO = new GameObject("DialogBox");
            boxGO.transform.SetParent(panel.transform, false);
            var boxImg = boxGO.AddComponent<Image>();
            boxImg.color = new Color(0.04f, 0.08f, 0.14f, 0.97f);
            TryApplySvgSprite(boxImg, "panel_glass");
            boxGO.AddComponent<FWTCG.UI.GlassPanelFX>();  // DEV-25 glass effect
            var boxRT = boxGO.GetComponent<RectTransform>();
            boxRT.anchorMin = new Vector2(0.5f, 0.5f);
            boxRT.anchorMax = new Vector2(0.5f, 0.5f);
            boxRT.pivot     = new Vector2(0.5f, 0.5f);
            boxRT.sizeDelta = new Vector2(720f, 420f);

            var vlg = boxGO.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment         = TextAnchor.MiddleCenter;
            vlg.spacing                = 18f;
            vlg.padding                = new RectOffset(36, 36, 28, 28);

            // Title
            var titleGO = new GameObject("TitleText");
            titleGO.transform.SetParent(boxGO.transform, false);
            titleText = titleGO.AddComponent<Text>();
            titleText.text      = "标题";
            titleText.color     = GameColors.GoldLight;
            titleText.fontSize  = 26;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) titleText.font = _font;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 40f;
            titleLE.minHeight       = 40f;

            // Message — tall enough for 5+ lines, explicit sizeDelta width
            var msgGO = new GameObject("MessageText");
            msgGO.transform.SetParent(boxGO.transform, false);
            messageText = msgGO.AddComponent<Text>();
            messageText.text       = "";
            messageText.color      = new Color(0.92f, 0.92f, 0.92f, 1f);
            messageText.fontSize   = 20;
            messageText.lineSpacing = 1.3f;
            messageText.alignment  = TextAnchor.UpperCenter;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) messageText.font = _font;
            var msgLE = msgGO.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 160f;
            msgLE.minHeight       = 80f;

            // Card container (horizontal row, for card-pick mode)
            var ccGO = new GameObject("CardContainer");
            ccGO.transform.SetParent(boxGO.transform, false);
            var ccLE = ccGO.AddComponent<LayoutElement>();
            ccLE.preferredHeight = 150f;
            var hlg = ccGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth     = false;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight= true;
            hlg.spacing               = 10f;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            cardContainer             = ccGO.transform;

            // Button row
            var btnRowGO = new GameObject("ButtonRow");
            btnRowGO.transform.SetParent(boxGO.transform, false);
            var btnLE = btnRowGO.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 60f;
            btnLE.minHeight       = 60f;
            var btnHlg = btnRowGO.AddComponent<HorizontalLayoutGroup>();
            btnHlg.childControlWidth     = false;
            btnHlg.childControlHeight    = false;
            btnHlg.childForceExpandWidth = false;
            btnHlg.childForceExpandHeight= false;
            btnHlg.spacing               = 48f;
            btnHlg.childAlignment        = TextAnchor.MiddleCenter;

            confirmBtn     = CreateButton(btnRowGO.transform, "ConfirmBtn", "确认");
            TryApplySvgSprite(confirmBtn.GetComponent<Image>(), "btn_confirm");
            cancelBtn      = CreateButton(btnRowGO.transform, "CancelBtn",  "取消");
            TryApplySvgSprite(cancelBtn.GetComponent<Image>(), "btn_cancel");
            confirmBtnText = confirmBtn.GetComponentInChildren<Text>();
            cancelBtnText  = cancelBtn.GetComponentInChildren<Text>();

            panel.SetActive(false);
            return panel;
        }

        // ── Startup panels ────────────────────────────────────────────────────

        private static GameObject CreateCoinFlipPanel(Transform parent,
            out Text coinFlipText, out Button okButton,
            out Image coinCircleImage, out Text coinResultText, out Image scanLightImage)
        {
            var go = CreateFullscreenPanel(parent, "CoinFlipPanel", new Color(0.02f, 0.04f, 0.07f, 0.95f));
            // DEV-24: CanvasGroup for fade transitions
            go.AddComponent<CanvasGroup>();

            // Centered dialog frame (ignoreLayout so VLG skips it, renders behind content)
            {
                var bgBox = new GameObject("PanelBg");
                bgBox.transform.SetParent(go.transform, false);
                var le = bgBox.AddComponent<LayoutElement>(); le.ignoreLayout = true;
                var rt = bgBox.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(800f, 600f);
                var bgImg = bgBox.AddComponent<Image>();
                bgImg.color = Color.white; bgImg.raycastTarget = false;
                TryApplySvgSprite(bgImg, "bg_coin_flip");
            }

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 28f;

            // Title label — DEV-30 V1: alpha=0, animated in by TitleTextEntranceRoutine
            // ignoreLayout=true: must not be controlled by VLG (DOTween fights VLG → wrong position)
            {
                var titleText = CreateTMPText(go.transform, "CoinTitle", "掷硬币",
                    new Color(0.78f, 0.67f, 0.43f, 0f), 44, TextAnchor.MiddleCenter);
                var titleLE = titleText.gameObject.AddComponent<LayoutElement>();
                titleLE.ignoreLayout = true;
                var titleRT = titleText.rectTransform;
                titleRT.anchorMin = new Vector2(0.5f, 0.5f);
                titleRT.anchorMax = new Vector2(0.5f, 0.5f);
                titleRT.pivot    = new Vector2(0.5f, 0.5f);
                titleRT.sizeDelta = new Vector2(400f, 60f);
                titleRT.anchoredPosition = new Vector2(0f, 280f); // above coin, clear of VLG content
            }

            // ── Coin group: VLG item that wraps coin + face text ─────────────
            // CoinGroup is the VLG item (160×160). CoinContainer (with Mask) lives
            // inside it; CoinFaceText is a sibling of CoinContainer — NOT inside the
            // Mask hierarchy — so it is never clipped by the circular mask.
            var coinGroup = new GameObject("CoinGroup");
            coinGroup.transform.SetParent(go.transform, false);
            var coinGroupRT = coinGroup.AddComponent<RectTransform>();
            coinGroupRT.sizeDelta = new Vector2(160f, 160f);
            var coinGroupLE = coinGroup.AddComponent<LayoutElement>();
            coinGroupLE.preferredWidth  = 160f;
            coinGroupLE.preferredHeight = 160f;

            // CoinContainer: circular mask clips the dark outer border ring of coin sprites
            var coinContainer = new GameObject("CoinContainer");
            coinContainer.transform.SetParent(coinGroup.transform, false);
            var coinContRT = coinContainer.AddComponent<RectTransform>();
            coinContRT.anchorMin = Vector2.zero;
            coinContRT.anchorMax = Vector2.one;
            coinContRT.offsetMin = Vector2.zero;
            coinContRT.offsetMax = Vector2.zero;
            var maskImg = coinContainer.AddComponent<Image>();
            maskImg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            maskImg.color  = Color.white;
            maskImg.raycastTarget = false;
            coinContainer.AddComponent<Mask>().showMaskGraphic = false;

            // CoinCircle: the actual coin sprite — slightly oversized so border stays outside mask
            var coinGO = new GameObject("CoinCircle");
            coinGO.transform.SetParent(coinContainer.transform, false);
            var coinRT = coinGO.AddComponent<RectTransform>();
            coinRT.anchorMin = new Vector2(-0.07f, -0.07f);
            coinRT.anchorMax = new Vector2(1.07f,  1.07f);
            coinRT.offsetMin = Vector2.zero;
            coinRT.offsetMax = Vector2.zero;
            coinCircleImage = coinGO.AddComponent<Image>();
            coinCircleImage.color          = Color.white;
            coinCircleImage.preserveAspect = false;

            // CoinEdge: thin vertical strip that simulates the coin's rim/thickness
            // during the flip animation. Hidden by default; animated in StartupFlowUI.
            // Lives inside the masked container → top/bottom are rounded by the circle mask.
            {
                var edgeGO = new GameObject("CoinEdge");
                edgeGO.transform.SetParent(coinContainer.transform, false);
                var edgeRT = edgeGO.AddComponent<RectTransform>();
                edgeRT.anchorMin = new Vector2(0.5f, 0f);
                edgeRT.anchorMax = new Vector2(0.5f, 1f);
                edgeRT.pivot     = new Vector2(0.5f, 0.5f);
                edgeRT.sizeDelta = new Vector2(12f, 0f); // 12 px wide, full container height
                var edgeImg = edgeGO.AddComponent<Image>();
                edgeImg.color = new Color(0.72f, 0.55f, 0.13f, 0f); // dark gold, alpha=0
            }

            // CoinFaceText: sibling of CoinContainer — NOT clipped by the Mask above
            coinFlipText = CreateTMPText(coinGroup.transform, "CoinFaceText", "?",
                new Color(0.10f, 0.07f, 0.02f, 1f), 48, TextAnchor.MiddleCenter);
            {
                var faceRT = coinFlipText.rectTransform;
                faceRT.anchorMin = Vector2.zero;
                faceRT.anchorMax = Vector2.one;
                faceRT.offsetMin = Vector2.zero;
                faceRT.offsetMax = Vector2.zero;
            }

            // ── Result text (hidden initially) ────────────────────────────────
            coinResultText = CreateTMPText(go.transform, "CoinResultText", "",
                Color.white, 26, TextAnchor.MiddleCenter);
            // Start fully transparent — StartupFlowUI fades this in after the flip
            var rc = coinResultText.color;
            rc.a = 0f;
            coinResultText.color = rc;

            okButton = CreateButton(go.transform, "OkButton", "开始");
            TryApplySvgSprite(okButton.GetComponent<Image>(), "btn_confirm");
            AddButtonCharge(okButton.gameObject); // V4: 按钮入场动画

            // ── Scan light (absolute positioned, not in VLG) ──────────────────
            var scanGO = new GameObject("ScanLight");
            scanGO.transform.SetParent(go.transform, false);
            var scanRT = scanGO.AddComponent<RectTransform>();
            scanRT.sizeDelta = new Vector2(240f, 3f);
            scanRT.anchorMin = new Vector2(0f, 0.5f);
            scanRT.anchorMax = new Vector2(0f, 0.5f);
            scanRT.pivot = new Vector2(0.5f, 0.5f);
            scanRT.anchoredPosition = new Vector2(-960f, 0f);
            var scanLE = scanGO.AddComponent<LayoutElement>();
            scanLE.ignoreLayout = true;
            scanLightImage = scanGO.AddComponent<Image>();
            scanLightImage.color = new Color(0.37f, 0.55f, 1f, 0.35f); // translucent blue-white

            // DEV-30 V2/V3/V5 overlays removed — SVG background (bg_coin_flip) covers the visual design

            go.SetActive(false);
            return go;
        }

        private static GameObject CreateMulliganPanel(Transform parent,
            out Text titleText, out Transform cardContainer,
            out Button confirmButton, out Text confirmLabel)
        {
            var go = CreateFullscreenPanel(parent, "MulliganPanel", new Color(0f, 0f, 0f, 0.9f));
            // DEV-24: CanvasGroup for fade transitions
            go.AddComponent<CanvasGroup>();

            // Centered dialog frame (ignoreLayout so VLG skips it, renders behind content)
            {
                var bgBox = new GameObject("PanelBg");
                bgBox.transform.SetParent(go.transform, false);
                var le = bgBox.AddComponent<LayoutElement>(); le.ignoreLayout = true;
                var rt = bgBox.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(1200f, 700f);
                var bgImg = bgBox.AddComponent<Image>();
                bgImg.color = Color.white; bgImg.raycastTarget = false;
                TryApplySvgSprite(bgImg, "bg_mulligan");
            }

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
            TryApplySvgSprite(confirmButton.GetComponent<Image>(), "btn_confirm");
            AddButtonCharge(confirmButton.gameObject); // V8: 梦想手牌确认按钮动画
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
            TryApplySvgSprite(panel.GetComponent<Image>(), "panel_reactive");

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 14f;

            // ── Countdown Timer Widget (top, above everything) ─────────────────
            var timerGO = new GameObject("TimerWidget");
            timerGO.transform.SetParent(panel.transform, false);
            var timerLE = timerGO.AddComponent<LayoutElement>();
            timerLE.preferredWidth  = 80f;
            timerLE.preferredHeight = 80f;
            timerGO.AddComponent<RectTransform>();

            // Background circle (dark, subtle)
            var bgCircleGO = new GameObject("BgCircle");
            bgCircleGO.transform.SetParent(timerGO.transform, false);
            var bgRT = bgCircleGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            var bgImg = bgCircleGO.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.12f);

            // Gold radial fill (clock face draining clockwise)
            var fillGO = new GameObject("ClockFill");
            fillGO.transform.SetParent(timerGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = new Vector2(4f, 4f);
            fillRT.offsetMax = new Vector2(-4f, -4f);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(1f, 0.82f, 0.2f, 1f); // gold
            fillImg.type  = Image.Type.Filled;
            fillImg.fillMethod  = Image.FillMethod.Radial360;
            fillImg.fillOrigin  = (int)Image.Origin360.Top;
            fillImg.fillClockwise = true;
            fillImg.fillAmount  = 1f;

            // Seconds text (centered, on top)
            var secGO = new GameObject("SecondsText");
            secGO.transform.SetParent(timerGO.transform, false);
            var secRT = secGO.AddComponent<RectTransform>();
            secRT.anchorMin = Vector2.zero;
            secRT.anchorMax = Vector2.one;
            secRT.offsetMin = Vector2.zero;
            secRT.offsetMax = Vector2.zero;
            var secText = secGO.AddComponent<Text>();
            secText.text = "15";
            secText.color = Color.white;
            secText.fontSize = 28;
            secText.fontStyle = FontStyle.Bold;
            secText.alignment = TextAnchor.MiddleCenter;
            secText.horizontalOverflow = HorizontalWrapMode.Overflow;
            secText.verticalOverflow   = VerticalWrapMode.Overflow;
            secText.raycastTarget = false;
            if (_font != null) secText.font = _font;

            // ── Context text ───────────────────────────────────────────────────
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
            ctLE.preferredHeight = 40f;

            // ── Card container (horizontal row, below text) ────────────────────
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

            // Store fill/text refs for wiring to ReactiveWindowUI
            _reactiveTimerFill = fillImg;
            _reactiveTimerText = secText;

            panel.SetActive(false);
            return panel;
        }

        // Temp storage between CreateReactiveWindowPanel and WireGameManager
        private static Image _reactiveTimerFill;
        private static Text  _reactiveTimerText;

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

            go.AddComponent<FWTCG.UI.CardHoverScale>(); // hover zoom on legend panel

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

            // Skill button — with hover glow outline
            skillBtn = CreateDebugButton(go.transform, "虚空感知", new Color(0.4f, 0.1f, 0.8f, 1f));
            var skillLE = skillBtn.GetComponent<LayoutElement>();
            if (skillLE != null) skillLE.preferredHeight = 28f;
            var skillOutline = skillBtn.gameObject.AddComponent<UnityEngine.UI.Outline>();
            skillOutline.effectColor    = new Color(1f, 0.85f, 0.3f, 0f); // golden, starts invisible
            skillOutline.effectDistance = new Vector2(3f, -3f);
            skillBtn.gameObject.AddComponent<FWTCG.UI.ButtonHoverGlow>();

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
            TryApplySvgSprite(panelImg, "panel_card_detail");
            panel.AddComponent<FWTCG.UI.GlassPanelFX>();  // DEV-25 glass effect

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
            bottomRT.offsetMin = new Vector2(3f, 3f);   // VFX-7a: inset to not cover frame border
            bottomRT.offsetMax = new Vector2(-3f, 0f);

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

            // ── Circle sprite for round badges ──
            var circleSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX/Circle.png");

            // ── CostBadge — top-left circle (法力费用) ──
            var costBadgeGO = new GameObject("CostBadge");
            costBadgeGO.transform.SetParent(root.transform, false);
            var costBadgeImg = costBadgeGO.AddComponent<Image>();
            costBadgeImg.color = new Color(0.78f, 0.67f, 0.43f, 1f);
            costBadgeImg.raycastTarget = false;
            if (circleSpr != null) { costBadgeImg.sprite = circleSpr; costBadgeImg.type = Image.Type.Simple; }
            var costBadgeRT = costBadgeGO.GetComponent<RectTransform>();
            costBadgeRT.anchorMin = costBadgeRT.anchorMax = new Vector2(0f, 1f);
            costBadgeRT.pivot = new Vector2(0f, 1f);
            costBadgeRT.sizeDelta = new Vector2(22f, 22f);
            costBadgeRT.anchoredPosition = new Vector2(3f, -3f);
            costBadgeGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            costBadgeGO.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            costBadgeGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);

            var costText = CreateTMPText(costBadgeGO.transform, "CostText", "0", Color.white, 12, TextAnchor.MiddleCenter);
            costText.fontStyle = FontStyle.Bold;
            costText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var costRT = costText.GetComponent<RectTransform>();
            costRT.anchorMin = Vector2.zero;
            costRT.anchorMax = Vector2.one;
            costRT.offsetMin = Vector2.zero;
            costRT.offsetMax = Vector2.zero;

            // ── AtkBadge — top-right circle (战力) ──
            var atkBadgeGO = new GameObject("AtkBadge");
            atkBadgeGO.transform.SetParent(root.transform, false);
            var atkBadgeImg = atkBadgeGO.AddComponent<Image>();
            atkBadgeImg.color = new Color(0.85f, 0.35f, 0.15f, 1f);
            atkBadgeImg.raycastTarget = false;
            if (circleSpr != null) { atkBadgeImg.sprite = circleSpr; atkBadgeImg.type = Image.Type.Simple; }
            var atkBadgeRT = atkBadgeGO.GetComponent<RectTransform>();
            atkBadgeRT.anchorMin = atkBadgeRT.anchorMax = new Vector2(1f, 1f);
            atkBadgeRT.pivot = new Vector2(1f, 1f);
            atkBadgeRT.sizeDelta = new Vector2(22f, 22f);
            atkBadgeRT.anchoredPosition = new Vector2(-3f, -3f);
            atkBadgeGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            atkBadgeGO.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            atkBadgeGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);

            var atkText = CreateTMPText(atkBadgeGO.transform, "AtkText", "0", Color.white, 12, TextAnchor.MiddleCenter);
            atkText.fontStyle = FontStyle.Bold;
            atkText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var atkRT = atkText.GetComponent<RectTransform>();
            atkRT.anchorMin = Vector2.zero;
            atkRT.anchorMax = Vector2.one;
            atkRT.offsetMin = Vector2.zero;
            atkRT.offsetMax = Vector2.zero;

            // ── SchCostBg — below CostBadge circle (符纹费用) ──
            var schBgGO = new GameObject("SchCostBg");
            schBgGO.transform.SetParent(root.transform, false);
            var schBgImg = schBgGO.AddComponent<Image>();
            schBgImg.color = new Color(1f, 0.55f, 0.1f, 1f);
            schBgImg.raycastTarget = false;
            if (circleSpr != null) { schBgImg.sprite = circleSpr; schBgImg.type = Image.Type.Simple; }
            var schBgRT = schBgGO.GetComponent<RectTransform>();
            schBgRT.anchorMin = schBgRT.anchorMax = new Vector2(0f, 1f);
            schBgRT.pivot = new Vector2(0f, 1f);
            schBgRT.sizeDelta = new Vector2(22f, 22f);
            schBgRT.anchoredPosition = new Vector2(3f, -27f); // below cost badge (3+22+2=27)
            schBgGO.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.7f);
            schBgGO.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            schBgGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.5f);
            schBgGO.SetActive(false);

            var schText = CreateTMPText(schBgGO.transform, "SchCostText", "炽×1", Color.white, 7, TextAnchor.MiddleCenter);
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

            // ── DEV-22: CardDragHandler + PortalVFX (drag-to-play) ──────────────
            root.AddComponent<FWTCG.UI.CardDragHandler>();
            root.AddComponent<FWTCG.UI.PortalVFX>();

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
            // VFX-3: wire dissolve death material (null-safe — material may not exist yet)
            var killDissolveMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/KillDissolveFX.mat");
            so.FindProperty("_killDissolveMat").objectReferenceValue = killDissolveMat;

            // ── VFX-7k: Glow overlay — second-to-last (covers text/black area, under frame) ──
            var glowGO = new GameObject("GlowOverlay");
            glowGO.transform.SetParent(root.transform, false);
            var glowImg = glowGO.AddComponent<Image>();
            var glowSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX/card_glow.png");
            if (glowSpr != null) glowImg.sprite = glowSpr;
            glowImg.color = new Color(1f, 1f, 1f, 0f); // starts invisible
            glowImg.raycastTarget = false;
            glowImg.type = Image.Type.Simple;
            glowImg.enabled = false;
            var glowRT = glowGO.GetComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = new Vector2(-6f, -6f);
            glowRT.offsetMax = new Vector2(6f, 6f);

            // ── VFX-7a: Frame overlay — LAST child = topmost layer (covers glow) ──
            var frameGO = new GameObject("FrameOverlay");
            frameGO.transform.SetParent(root.transform, false);
            frameGO.transform.SetAsLastSibling();
            var frameImg = frameGO.AddComponent<Image>();
            frameImg.color = Color.white;
            frameImg.raycastTarget = false;
            frameImg.preserveAspect = false;
            frameImg.enabled = false; // enabled at runtime by CardView.Refresh
            var frameRT = frameGO.GetComponent<RectTransform>();
            frameRT.anchorMin = Vector2.zero;
            frameRT.anchorMax = Vector2.one;
            frameRT.offsetMin = Vector2.zero;
            frameRT.offsetMax = Vector2.zero;

            so.FindProperty("_glowOverlay").objectReferenceValue   = glowImg;     // VFX-7k
            so.FindProperty("_frameOverlay").objectReferenceValue  = frameImg;    // VFX-7a
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
                "迅捷。对目标敌方单位造成3点伤害",
                SpellTargetType.EnemyUnit, "hex_ray", CardKeyword.Swift);

            CDS("void_seek",      "虚空追迹",   3, RuneType.Blazing, 1,
                "迅捷。对目标敌方单位造成4点伤害，摸1张牌",
                SpellTargetType.EnemyUnit, "void_seek", CardKeyword.Swift);

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
                "迅捷。所有己方单位进入活跃状态，摸1张牌",
                SpellTargetType.None, "rally_call", CardKeyword.Swift);

            CDS("balance_resolve","平衡意志",   3, RuneType.Verdant, 0,
                "迅捷。摸1张牌，召出1张符文",
                SpellTargetType.None, "balance_resolve", CardKeyword.Swift);

            CDS("slam",           "冲击",       2, RuneType.Crushing, 0,
                "迅捷+回响。使目标敌方单位眩晕（法盾可抵消）",
                SpellTargetType.EnemyUnit, "slam", CardKeyword.Swift | CardKeyword.Echo);

            CDS("strike_ask_later","先斩后奏",  1, RuneType.Crushing, 2,
                "迅捷。使目标己方单位本回合+5战力",
                SpellTargetType.FriendlyUnit, "strike_ask_later", CardKeyword.Swift);

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
        /// Sets TextureImporterType.Sprite on every PNG in Assets/Resources/UI/Generated/.
        /// Called before SceneBuilder wires sprite references so assets are ready.
        /// </summary>
        private static void EnsureGeneratedUISprites()
        {
            string folder = "Assets/Resources/UI/Generated";
            if (!System.IO.Directory.Exists(folder)) return;

            foreach (string fullPath in System.IO.Directory.GetFiles(folder, "*.png"))
            {
                string assetPath = fullPath.Replace('\\', '/');
                int idx = assetPath.IndexOf("Assets/");
                if (idx >= 0) assetPath = assetPath.Substring(idx);
                else continue;

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null) continue;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType         = TextureImporterType.Sprite;
                    importer.spriteImportMode    = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.maxTextureSize      = 4096;
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    Debug.Log($"[SceneBuilder] GeneratedUI 设为Sprite: {assetPath}");
                }
            }

            // frame_gold/silver 保留原始文件，不在此重新导入
            AssetDatabase.Refresh();
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
            Canvas rootCanvas,
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
            Image boardFlashOverlay,
            // Pencil countdown ring
            CountdownRingUI countdownRingUI)
        {
            var so = new SerializedObject(gameUI);

            so.FindProperty("_rootCanvas").objectReferenceValue       = rootCanvas;

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
            so.FindProperty("_timerDisplay").objectReferenceValue   = timerDisplay;
            so.FindProperty("_timerFill").objectReferenceValue      = timerFill;
            so.FindProperty("_timerText").objectReferenceValue      = timerText;
            so.FindProperty("_countdownRingUI").objectReferenceValue = countdownRingUI;

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
            Image coinCircleImage, Text coinResultText, Image scanLightImage,
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
            startupSO.FindProperty("_coinFlipPanel").objectReferenceValue         = coinFlipPanel;
            startupSO.FindProperty("_coinFlipText").objectReferenceValue          = coinFlipText;
            startupSO.FindProperty("_coinFlipOkButton").objectReferenceValue      = coinFlipOkButton;
            startupSO.FindProperty("_coinCircleImage").objectReferenceValue       = coinCircleImage;
            startupSO.FindProperty("_coinResultText").objectReferenceValue        = coinResultText;
            startupSO.FindProperty("_scanLightImage").objectReferenceValue        = scanLightImage;
            // VFX-6: audio clips left null — assign when audio assets are provided
            // startupSO.FindProperty("_coinFlipStartClip").objectReferenceValue = ...;
            // startupSO.FindProperty("_coinFlipLandClip").objectReferenceValue  = ...;
            startupSO.FindProperty("_mulliganPanel").objectReferenceValue         = mulliganPanel;
            startupSO.FindProperty("_mulliganTitleText").objectReferenceValue     = mulliganTitleText;
            startupSO.FindProperty("_mulliganCardContainer").objectReferenceValue = mulliganCardContainer;
            startupSO.FindProperty("_cardViewPrefab").objectReferenceValue        = cardPrefab;
            startupSO.FindProperty("_mulliganConfirmButton").objectReferenceValue = mulliganConfirmButton;
            startupSO.FindProperty("_mulliganConfirmLabel").objectReferenceValue  = mulliganConfirmLabel;
            startupSO.ApplyModifiedPropertiesWithoutUndo();

            // Wire ReactiveWindowUI panels
            var reactiveWindowSO = new SerializedObject(reactiveWindowUI);
            reactiveWindowSO.FindProperty("_panel").objectReferenceValue            = reactivePanel;
            reactiveWindowSO.FindProperty("_contextText").objectReferenceValue      = reactiveContextText;
            reactiveWindowSO.FindProperty("_cardContainer").objectReferenceValue    = reactiveCardContainer;
            reactiveWindowSO.FindProperty("_cardViewPrefab").objectReferenceValue   = cardPrefab;
            reactiveWindowSO.FindProperty("_countdownFill").objectReferenceValue    = _reactiveTimerFill;
            reactiveWindowSO.FindProperty("_countdownText").objectReferenceValue    = _reactiveTimerText;
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

            // Outcome + death list (bottom half, multi-line)
            outcomeText = CreateTMPText(go.transform, "CROutcome", "结果", GameColors.GoldLight, 16, TextAnchor.UpperCenter);
            outcomeText.fontStyle = FontStyle.Bold;
            outcomeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            outcomeText.verticalOverflow   = VerticalWrapMode.Overflow;
            outcomeText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 1f);
            var outRT = outcomeText.GetComponent<RectTransform>();
            outRT.anchorMin = new Vector2(0.05f, 0.04f);
            outRT.anchorMax = new Vector2(0.95f, 0.28f);
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

        // ── Pencil: RUNES 条带圆形槽位行 ────────────────────────────────────
        private static void CreateRuneSlotRow(Transform parent, int count, bool isPlayer)
        {
            // 居中容器，留出左侧标签空间
            var row = new GameObject("RuneSlotRow");
            row.transform.SetParent(parent, false);
            var rowTxt = row.AddComponent<Text>(); // dummy to get RT
            rowTxt.enabled = false;
            var rowRT = row.GetComponent<RectTransform>();
            if (rowRT != null)
            {
                rowRT.anchorMin = new Vector2(0.1f, 0f);
                rowRT.anchorMax = new Vector2(0.95f, 1f);
                rowRT.offsetMin = Vector2.zero; rowRT.offsetMax = Vector2.zero;
            }
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = false; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 6f;

            for (int i = 0; i < count; i++)
            {
                var slot = new GameObject($"RuneSlot{i}");
                slot.transform.SetParent(row.transform, false);
                var slotImg = slot.AddComponent<Image>();
                slotImg.color = new Color(0.565f, 0.439f, 0.125f, 0.25f); // gold dim
                var slotOL = slot.AddComponent<Outline>();
                slotOL.effectColor = new Color(0.565f, 0.439f, 0.125f, 0.5f);
                slotOL.effectDistance = new Vector2(1f, -1f);
                var slotLE = slot.AddComponent<LayoutElement>();
                slotLE.preferredWidth = 24f;
            }
        }

        // ── Pencil: BASE 区域左右卡槽框（跳过中间手牌区 x=644-1276）──────────
        private static void CreateBaseCardSlots(Transform parent, bool isEnemy)
        {
            // LEFT slots: x=248-644 (within BASE zone, relative xMin=0, xMax=(644-248)/(1672-248)=396/1424=0.278)
            var leftSlots = new GameObject("BaseLeftSlots");
            leftSlots.transform.SetParent(parent, false);
            var lsTxt = leftSlots.AddComponent<Text>(); lsTxt.enabled = false;
            var lsRT = leftSlots.GetComponent<RectTransform>();
            if (lsRT != null) { lsRT.anchorMin = new Vector2(0f, 0f); lsRT.anchorMax = new Vector2(396f/1424f, 1f); lsRT.offsetMin = Vector2.zero; lsRT.offsetMax = Vector2.zero; }
            var lsHLG = leftSlots.AddComponent<HorizontalLayoutGroup>();
            lsHLG.childControlWidth = true; lsHLG.childControlHeight = true;
            lsHLG.childForceExpandWidth = true; lsHLG.childForceExpandHeight = true;
            lsHLG.spacing = 4f; lsHLG.padding = new RectOffset(4, 4, 4, 4);
            lsHLG.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 4; i++) CreateBaseSlot(leftSlots.transform, $"LS{i}");

            // RIGHT slots: x=1276-1672 (relative xMin=(1276-248)/1424=1028/1424=0.722, xMax=1)
            var rightSlots = new GameObject("BaseRightSlots");
            rightSlots.transform.SetParent(parent, false);
            var rsTxt = rightSlots.AddComponent<Text>(); rsTxt.enabled = false;
            var rsRT = rightSlots.GetComponent<RectTransform>();
            if (rsRT != null) { rsRT.anchorMin = new Vector2(1028f/1424f, 0f); rsRT.anchorMax = new Vector2(1f, 1f); rsRT.offsetMin = Vector2.zero; rsRT.offsetMax = Vector2.zero; }
            var rsHLG = rightSlots.AddComponent<HorizontalLayoutGroup>();
            rsHLG.childControlWidth = true; rsHLG.childControlHeight = true;
            rsHLG.childForceExpandWidth = true; rsHLG.childForceExpandHeight = true;
            rsHLG.spacing = 4f; rsHLG.padding = new RectOffset(4, 4, 4, 4);
            rsHLG.childAlignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 4; i++) CreateBaseSlot(rightSlots.transform, $"RS{i}");
        }

        private static void CreateBaseSlot(Transform parent, string name)
        {
            var go = new GameObject($"BaseSlot_{name}");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.031f, 0.063f, 0.102f, 0.7f); // #08101a
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0.565f, 0.439f, 0.125f, 0.7f); // #907020
            ol.effectDistance = new Vector2(1f, -1f);
        }

        // ── Pencil: 浮动区域标签（Canvas绝对坐标，左侧区域说明文字）──────────

        private static void AddFloatingAreaLabel(Transform parent, string text,
            float xCenter, float yMin, float yMax)
        {
            var go = new GameObject("FloatLabel_" + text.Replace(" ", "_"));
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<Text>();
            txt.text = text;
            txt.color = new Color(0.78f, 0.682f, 0.529f, 0.55f); // #c7ae87 semi-transparent
            txt.fontSize = 9;
            txt.fontStyle = FontStyle.Normal;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) txt.font = _font;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                // xCenter ± 40px, yMin/yMax as provided
                rt.anchorMin = new Vector2(Mathf.Max(0f, xCenter - 40f/1920f), yMin);
                rt.anchorMax = new Vector2(Mathf.Min(1f, xCenter + 40f/1920f), yMax);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }

        // ── DEV-10: Zone label helper ────────────────────────────────────────

        private static void AddZoneLabel(Transform zone, string labelText)
        {
            if (zone == null) return;

            var labelGO = new GameObject("ZoneLabel");
            labelGO.transform.SetParent(zone, false);

            // Must ignore layout BEFORE adding Text, so any parent LayoutGroup won't drive this element
            var le = labelGO.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // SetParent to a RectTransform parent auto-converts Transform → RectTransform,
            // so GetComponent is safer than AddComponent here.
            var labelRT = labelGO.GetComponent<RectTransform>() ?? labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -2f);
            labelRT.sizeDelta = new Vector2(0f, 16f);

            var txt = labelGO.AddComponent<Text>();
            txt.text = labelText;
            txt.color = new Color(GameColors.GoldLight.r, GameColors.GoldLight.g, GameColors.GoldLight.b, 0.7f);
            txt.fontSize = 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.UpperCenter;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) txt.font = _font;

            // Add shadow for readability
            var shadow = labelGO.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        // ── Pencil: Two-line zone label (bold top line + small bottom line) ──

        private static void CreateTwoLineLabel(Transform parent, string topText, string bottomText)
        {
            // Add Text first so Unity auto-creates RectTransform, then configure RT

            // Top line (Chinese, bold) — upper half
            var topGO = new GameObject("LabelTop");
            topGO.transform.SetParent(parent, false);
            var topTxt = topGO.AddComponent<Text>();
            topTxt.text = topText;
            topTxt.color = new Color(0.78f, 0.682f, 0.529f, 1f); // #c7ae87
            topTxt.fontSize = 11;
            topTxt.fontStyle = FontStyle.Bold;
            topTxt.alignment = TextAnchor.LowerCenter;
            topTxt.raycastTarget = false;
            topTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            topTxt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) topTxt.font = _font;
            var topRT = topGO.GetComponent<RectTransform>();
            if (topRT != null)
            {
                topRT.anchorMin = new Vector2(0f, 0.5f);
                topRT.anchorMax = new Vector2(1f, 1f);
                topRT.offsetMin = Vector2.zero;
                topRT.offsetMax = Vector2.zero;
            }

            // Bottom line (English, normal) — lower half
            var botGO = new GameObject("LabelBot");
            botGO.transform.SetParent(parent, false);
            var botTxt = botGO.AddComponent<Text>();
            botTxt.text = bottomText;
            botTxt.color = new Color(0.78f, 0.682f, 0.529f, 0.7f); // #c7ae87 dimmer
            botTxt.fontSize = 9;
            botTxt.fontStyle = FontStyle.Normal;
            botTxt.alignment = TextAnchor.UpperCenter;
            botTxt.raycastTarget = false;
            botTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            botTxt.verticalOverflow = VerticalWrapMode.Overflow;
            if (_font != null) botTxt.font = _font;
            var botRT = botGO.GetComponent<RectTransform>();
            if (botRT != null)
            {
                botRT.anchorMin = new Vector2(0f, 0f);
                botRT.anchorMax = new Vector2(1f, 0.5f);
                botRT.offsetMin = Vector2.zero;
                botRT.offsetMax = Vector2.zero;
            }
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

        /// <summary>
        /// DEV-19: Adds RectMask2D + ButtonCharge to a button GameObject so hover
        /// triggers the left-to-right light sweep animation.
        /// </summary>
        private static void AddButtonCharge(GameObject btnGO)
        {
            if (btnGO == null) return;
            // RectMask2D clips the sweep image so it never overflows the button bounds
            if (btnGO.GetComponent<RectMask2D>() == null)
                btnGO.AddComponent<RectMask2D>();
            if (btnGO.GetComponent<FWTCG.UI.ButtonCharge>() == null)
                btnGO.AddComponent<FWTCG.UI.ButtonCharge>();
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
