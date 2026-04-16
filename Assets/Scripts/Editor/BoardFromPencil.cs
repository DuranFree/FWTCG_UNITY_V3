using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace FWTCG.Editor
{
    /// <summary>
    /// FWTCG/Build Board (Pencil 1:1)
    /// Pixel-perfect recreation of new5.pen board (1920×1080).
    /// Layer order matches new5.pen children array exactly.
    /// </summary>
    public static class BoardFromPencil
    {
        const float W = 1920f, H = 1080f;

        // Colours from new5.pen
        static readonly Color C_Gold       = Hex("#c7ae87");
        static readonly Color C_GoldBorder = Hex("#907020");
        static readonly Color C_CardBg     = Hex("#08101a");
        static readonly Color C_BgDeep     = Hex("#04060e");

        static Font _font;

        [MenuItem("FWTCG/Build Board (Pencil 1:1)")]
        public static void BuildBoard()
        {
            _font = LoadFont();
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty) EditorSceneManager.SaveScene(active);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = C_BgDeep;
            cam.orthographic = true; cam.depth = -1;
            camGO.AddComponent<AudioListener>();

            // EventSystem
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Background canvas: NO CanvasScaler — renders at native pixel size
            // so the texture always fills the entire screen regardless of resolution.
            var bgCanvasGO = new GameObject("BackgroundCanvas");
            var bgC = bgCanvasGO.AddComponent<Canvas>();
            bgC.renderMode = RenderMode.ScreenSpaceOverlay;
            bgC.sortingOrder = -100;
            bgCanvasGO.AddComponent<GraphicRaycaster>();
            BuildBackground(bgCanvasGO.transform);

            // Main UI canvas: has CanvasScaler for 1920×1080 reference layout
            var canvas = MakeCanvas();
            BuildLayout(canvas.transform);

            EnsureDir("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, "Assets/Scenes/GameBoard_Pencil.unity");
            Debug.Log("[BoardFromPencil] → Assets/Scenes/GameBoard_Pencil.unity");
        }

        // ═══════════════════════════════════════════════════════════════════
        // BACKGROUND — lives on its own canvas (no CanvasScaler = native pixels)
        // ═══════════════════════════════════════════════════════════════════
        static void BuildBackground(Transform bgCv)
        {
            // Solid dark fallback colour — fills screen in native pixels
            var colorGO = new GameObject("BG_Color");
            colorGO.transform.SetParent(bgCv, false);
            var colorRT = colorGO.AddComponent<RectTransform>();
            colorRT.anchorMin = Vector2.zero; colorRT.anchorMax = Vector2.one;
            colorRT.offsetMin = colorRT.offsetMax = Vector2.zero;
            var colorImg = colorGO.AddComponent<Image>();
            colorImg.color = C_BgDeep; colorImg.raycastTarget = false;

            // Board texture — always fills the entire screen in native pixels
            const string bgPath = "Assets/Resources/UI/Generated/bg_board_texture.png";
            var texGO = new GameObject("BG_Texture");
            texGO.transform.SetParent(bgCv, false);
            var texRT = texGO.AddComponent<RectTransform>();
            texRT.anchorMin = Vector2.zero; texRT.anchorMax = Vector2.one;
            texRT.offsetMin = texRT.offsetMax = Vector2.zero;

            var t2d = AssetDatabase.LoadAssetAtPath<Texture2D>(bgPath);
            if (t2d != null)
            {
                var raw = texGO.AddComponent<RawImage>();
                raw.texture = t2d; raw.color = new Color(1f, 1f, 1f, 0.99f);
                raw.uvRect = new Rect(0, 0, 1, 1); raw.raycastTarget = false;
            }
            else
            {
                Debug.LogWarning("[BoardFromPencil] bg_board_texture.png not found — using fallback colour");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // BUILD LAYOUT  —  order matches new5.pen children array exactly
        // ═══════════════════════════════════════════════════════════════════
        static void BuildLayout(Transform cv)
        {
            // ── 3. Thvkj — Countdown ring (x=747,y=322, 423×423) ─────────────
            // THIS MUST BE BELOW ALL CARD SLOTS (rendered before them)
            CountdownRing(cv, 747, 322, 423, 423);

            // ── 4–9. Zone outlines ────────────────────────────────────────────
            // d05JO  ER Zone:  x=248,y=155, 1424×85
            ZoneBox(cv, "ER_Zone", 248, 155, 1424,  85);
            // Ph7c0  EB Zone:  x=248,y=242, 1424×160
            ZoneBox(cv, "EB_Zone", 248, 242, 1424, 160);
            // QKSbb  BF0 Zone: x=75,y=404,  755×260
            ZoneBox(cv, "BF0_Zone",  75, 404,  755, 260);
            // AgxQX  BF1 Zone: x=1090,y=404, 752×260
            ZoneBox(cv, "BF1_Zone",1090, 404,  752, 260);
            // vIFOt  PB Zone:  x=248,y=666, 1424×160
            ZoneBox(cv, "PB_Zone", 248, 666, 1424, 160);
            // aEcwC  PR Zone:  x=248,y=828, 1424×85
            ZoneBox(cv, "PR_Zone", 248, 828, 1424,  85);

            // ── 10–11. BF divider lines ───────────────────────────────────────
            Divider(cv, "BF0_Divider",  75, 534, 755);
            Divider(cv, "BF1_Divider",1090, 534, 752);

            // ── 12–15. Battlefield card slots ────────────────────────────────
            // T448Z  BF0 Enemy:  x=280,y=416, 352×106, 4 slots right-aligned
            BFSlots(cv, "BF0_EnemySlots",  280, 416, 352, 106);
            // pbpqk  BF0 Player: x=280,y=546, 352×106
            BFSlots(cv, "BF0_PlayerSlots", 280, 546, 352, 106);
            // f2Gj2  BF1 Enemy:  x=1288,y=416, 352×106
            BFSlots(cv, "BF1_EnemySlots",  1288, 416, 352, 106);
            // gEXWx  BF1 Player: x=1288,y=546, 352×106
            BFSlots(cv, "BF1_PlayerSlots", 1288, 546, 352, 106);

            // ── 16–19. Center BF field / standby slots ────────────────────────
            // miO3J  BF0 Field Card: x=679,y=436, 106×76
            CardSlot(cv, "BF0_FieldCard",  679, 436, 106, 76);
            // FlFzE  BF0 Standby:    x=696,y=546,  72×100
            CardSlot(cv, "BF0_Standby",    696, 546,  72, 100);
            // CDdsq  BF1 Field Card: x=1135,y=436, 106×76
            CardSlot(cv, "BF1_FieldCard", 1135, 436, 106, 76);
            // 9wu6n  BF1 Standby:    x=1152,y=546, 72×100
            CardSlot(cv, "BF1_Standby",   1152, 546,  72, 100);

            // ── 20–23. Zone labels (standalone text nodes) ────────────────────
            // U3N3M  "RUNES  符文区" x=258,y=162
            ZoneLabel(cv, "Label_ER", 258, 162, "RUNES  符文区");
            // DaavY  "BASE  基地"    x=258,y=248
            ZoneLabel(cv, "Label_EB", 258, 248, "BASE  基地");
            // mooSW  "BASE  基地"    x=258,y=671
            ZoneLabel(cv, "Label_PB", 258, 671, "BASE  基地");
            // r9Cq7  "RUNES  符文区" x=258,y=833
            ZoneLabel(cv, "Label_PR", 258, 833, "RUNES  符文区");

            // ── 24–25. Hero/Legend slots (enemy, off-screen top) ──────────────
            // CbpkH 英雄E:  x=262,y=-48, 118×154
            HeroSlot(cv, "HeroE",   262,  -48, 118, 154, "英雄",  "CHAMPION");
            // oIpjr 传说E:  x=391,y=-48, 118×154
            HeroSlot(cv, "LegendE", 391,  -48, 118, 154, "传说",  "LEGEND");

            // ── 26–49. Rune circles (ellipses 48×48, step x=26) ──────────────
            RuneCircles(cv, "EnemyRunes",  793, 173, 12, 48);
            RuneCircles(cv, "PlayerRunes", 793, 841, 12, 48);

            // ── 50–51. Hero/Legend slots (player, off-screen bottom) ──────────
            HeroSlot(cv, "HeroP",   262,  974, 118, 154, "英雄",  "CHAMPION");
            HeroSlot(cv, "LegendP", 391,  974, 118, 154, "传说",  "LEGEND");

            // ── 52–53. Base card slots (8 slots each, 72×100, gap=8) ──────────
            // Tdel6  Enemy Base: x=644,y=272, 632×100
            BaseSlots(cv, "EnemyBase",  644, 272, 632, 100);
            // UQj4Z  Player Base: x=644,y=696, 632×100
            BaseSlots(cv, "PlayerBase", 644, 696, 632, 100);

            // ── 54. fRnyd — Vignette overlay ──────────────────────────────────
            Vignette(cv);

            // ── 55–62. Deck piles (139×195, RuneE=138) ────────────────────────
            // Pencil order: MainE, RuneE, RuneP, MainP, DiscardE, ExileE, ExileP, DiscardP
            DeckPile(cv, "MainE",    1689, 274, 139, "主牌堆");
            DeckPile(cv, "RuneE",      92,  73, 138, "符文堆");  // w=138 per Pencil node zn0lO
            DeckPile(cv, "RuneP",      92, 820, 139, "符文堆");
            DeckPile(cv, "MainP",    1689, 616, 139, "主牌堆");
            DeckPile(cv, "DiscardE", 1689,  73, 139, "弃牌区");
            DeckPile(cv, "ExileE",     92, 274, 139, "放逐区");
            DeckPile(cv, "ExileP",     92, 616, 139, "放逐区");
            DeckPile(cv, "DiscardP", 1689, 820, 139, "弃牌区");

            // ── 63–76. Hand cards (fan, rotated) ─────────────────────────────
            HandCard(cv, "HandE_L14", 650f,  -115, -14f);
            HandCard(cv, "HandE_L9",  742f,   -83,  -9f);
            HandCard(cv, "HandE_L5",  819f,   -62,  -5f);
            HandCard(cv, "HandE_0",   911f,   -48,   0f);
            HandCard(cv, "HandE_R5", 1004f,   -52,   5f);
            HandCard(cv, "HandE_R9", 1072f,   -69,   9f);
            HandCard(cv, "HandE_R14",1163f,   -88,  14f);
            HandCard(cv, "HandP_L14", 644f,  1009,  14f);
            HandCard(cv, "HandP_L9",  738f,   980,   9f);
            HandCard(cv, "HandP_L5",  812f,   962,   5f);
            HandCard(cv, "HandP_0",   905f,   952,   0f);
            HandCard(cv, "HandP_R5",  997f,   954,  -5f);
            HandCard(cv, "HandP_R9", 1071f,   963,  -9f);
            HandCard(cv, "HandP_R14",1162f,   982, -14f);

            // ── 77–78. Action buttons ─────────────────────────────────────────
            // qYjKu  结束回合: x=1538,y=926, 108×34
            EndTurnBtn(cv, 1538, 926, 108, 34);
            // Xm6oL  查看弃牌堆: x=1553,y=975, 77×24
            ViewDiscardBtn(cv, 1553, 975, 77, 24);

            // ── 79–104. Score circles (22×22 ellipses + text labels) ──────────
            // Right side (enemy): x=1854, y=260..612, step=44, values 0..8
            int[] rightVals = {0,1,2,3,4,5,6,7,8};
            float[] rightYs  = {260,304,348,392,436,480,524,568,612};
            for (int i = 0; i < 9; i++)
                ScoreCircle(cv, $"ScoreR_{i}", 1854, rightYs[i], 22, rightVals[i].ToString());

            // Left side (player): x=47, y=446..798, step=44, values 8..0
            int[] leftVals = {8,7,6,5,4,3,2,1,0};
            float[] leftYs  = {446,490,534,578,622,666,710,754,798};
            for (int i = 0; i < 9; i++)
                ScoreCircle(cv, $"ScoreL_{i}", 47, leftYs[i], 22, leftVals[i].ToString());

            // ── 105. 0c05h — Logo (LAST = topmost layer) ─────────────────────
            // x=817,y=489, w=270, h=92
            // Children: LEAGUE(x=-3,y=0,w=252,h=50) OF(x=218,y=12,w=31) LEGENDS(x=15,y=42,w=252,h=50)
            Logo(cv, 817, 489, 270, 92);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Coordinate helpers
        // ═══════════════════════════════════════════════════════════════════

        /// Canvas Pencil coords → Unity anchors (direct canvas children)
        static void PR(RectTransform rt, float px, float py, float pw, float ph)
        {
            rt.anchorMin = new Vector2(px / W, 1f - (py + ph) / H);
            rt.anchorMax = new Vector2((px + pw) / W, 1f - py / H);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// Local coords within a parent frame → Unity anchors
        static void PRC(RectTransform rt, float pW, float pH, float cx, float cy, float cw, float ch)
        {
            rt.anchorMin = new Vector2(cx / pW, 1f - (cy + ch) / pH);
            rt.anchorMax = new Vector2((cx + cw) / pW, 1f - cy / pH);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Element factories
        // ═══════════════════════════════════════════════════════════════════

        static void CountdownRing(Transform cv, float px, float py, float pw, float ph)
        {
            var cont = GO(cv, "CountdownRing");
            var rt = cont.AddComponent<RectTransform>();
            PR(rt, px, py, pw, ph);

            // Three image layers stacked (matching Thvkj children: time01/02/03)
            LayeredImg(cont.transform, "Ring_Empty", "Assets/Resources/UI/Generated/countdown_empty.png", true);
            LayeredImg(cont.transform, "Ring_Blue",  "Assets/Resources/UI/Generated/countdown_blue.png",  true);
            LayeredImg(cont.transform, "Ring_Red",   "Assets/Resources/UI/Generated/countdown_red.png",   false);

            // Timer text
            var txtGO = GO(cont.transform, "TimerText");
            var txtRT = txtGO.AddComponent<RectTransform>();
            txtRT.anchorMin = new Vector2(0.28f, 0.28f);
            txtRT.anchorMax = new Vector2(0.72f, 0.72f);
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;
            var t = txtGO.AddComponent<Text>();
            t.text = "30"; t.color = new Color(0.88f, 0.96f, 1f, 1f);
            t.fontSize = 52; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            if (_font != null) t.font = _font;
        }

        static void LayeredImg(Transform parent, string name, string path, bool active)
        {
            var go = GO(parent, name);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Find sprite via LoadAllAssetsAtPath, then Texture2D+Sprite.Create, then RawImage
            Sprite lSpr = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Sprite s) { lSpr = s; break; }
            if (lSpr == null)
            {
                var t2d = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t2d != null)
                    lSpr = Sprite.Create(t2d, new Rect(0,0,t2d.width,t2d.height), new Vector2(0.5f,0.5f), 100f);
            }
            if (lSpr != null)
            {
                var img = go.AddComponent<Image>();
                img.sprite = lSpr; img.color = Color.white;
                img.preserveAspect = false; img.raycastTarget = false;
            }
            else
            {
                var rawTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (rawTex != null)
                {
                    var raw = go.AddComponent<RawImage>();
                    raw.texture = rawTex; raw.color = Color.white; raw.raycastTarget = false;
                }
                else
                {
                    var img = go.AddComponent<Image>();
                    img.color = Color.clear; img.raycastTarget = false;
                }
            }
            go.SetActive(active);
        }

        static void ZoneBox(Transform cv, string name, float px, float py, float pw, float ph)
        {
            // Transparent fill, gold border via 4 edge strips (Outline on transparent Image doesn't render)
            var go = ImgNode(cv, name, px, py, pw, ph, null, new Color(0.027f, 0.04f, 0.09f, 0.15f));
            go.GetComponent<Image>().raycastTarget = false;
            AddBorderStrips(go.transform, 2f,
                new Color(C_GoldBorder.r, C_GoldBorder.g, C_GoldBorder.b, 0.7f));
        }

        /// Add 4 thin border strips as children — works on any Image including transparent ones.
        static void AddBorderStrips(Transform parent, float t, Color col)
        {
            // Top, Bottom, Left, Right
            (string n, float ax0, float ay0, float ax1, float ay1, float px, float py) [] sides = {
                ("BT", 0, 1, 1, 1, 0.5f, 1f),
                ("BB", 0, 0, 1, 0, 0.5f, 0f),
                ("BL", 0, 0, 0, 1, 0f,   0.5f),
                ("BR", 1, 0, 1, 1, 1f,   0.5f),
            };
            foreach (var (n, ax0, ay0, ax1, ay1, pivX, pivY) in sides)
            {
                var b = new GameObject(n); b.transform.SetParent(parent, false);
                var rt = b.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(ax0, ay0);
                rt.anchorMax = new Vector2(ax1, ay1);
                rt.pivot     = new Vector2(pivX, pivY);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                // For horizontal borders set height, for vertical set width
                bool horiz = (ay0 == ay1);
                rt.sizeDelta = horiz ? new Vector2(0, t) : new Vector2(t, 0);
                var img = b.AddComponent<Image>();
                img.color = col; img.raycastTarget = false;
            }
        }

        static void Divider(Transform cv, string name, float px, float py, float pw)
        {
            var go = GO(cv, name);
            var rt = go.AddComponent<RectTransform>();
            PR(rt, px, py, pw, 1);
            var img = go.AddComponent<Image>();
            img.color = new Color(C_GoldBorder.r, C_GoldBorder.g, C_GoldBorder.b, 0.15f);
            img.raycastTarget = false;
        }

        static void BFSlots(Transform cv, string name, float px, float py, float pw, float ph)
        {
            // 4 slots of 76px, gap=16, right-aligned (justifyContent:end)
            const int cnt = 4; const float sw = 76f, gap = 16f;
            float used = cnt * sw + (cnt - 1) * gap;
            float startX = pw - used;
            var cont = ImgNode(cv, name, px, py, pw, ph, null, Color.clear);
            for (int i = 0; i < cnt; i++)
            {
                float cx = startX + i * (sw + gap);
                var sGO = GO(cont.transform, $"Slot{i}");
                var rt  = sGO.AddComponent<RectTransform>();
                PRC(rt, pw, ph, cx, 0, sw, ph);
                var img = sGO.AddComponent<Image>();
                img.color = C_CardBg; img.raycastTarget = false;
                Outline(sGO);
            }
        }

        static void CardSlot(Transform cv, string name, float px, float py, float pw, float ph)
        {
            var go = ImgNode(cv, name, px, py, pw, ph, null, C_CardBg);
            Outline(go);
        }

        static void ZoneLabel(Transform cv, string name, float px, float py, string text)
        {
            // Text node at exact position, auto-size (textGrowth: auto)
            var go = GO(cv, name);
            var rt = go.AddComponent<RectTransform>();
            PR(rt, px, py, 200, 14);  // wide enough for text, height=14 (fontSize=11)
            var t = go.AddComponent<Text>();
            t.text = text; t.color = C_Gold;
            t.fontSize = 11; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.UpperLeft; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            if (_font != null) t.font = _font;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0,0,0,0.8f); sh.effectDistance = new Vector2(1,-1);
        }

        static void HeroSlot(Transform cv, string name, float px, float py, float pw, float ph,
                             string main, string sub)
        {
            var go = ImgNode(cv, name, px, py, pw, ph, null, C_CardBg);
            Outline(go, 0.85f);

            var mGO = GO(go.transform, "Main");
            var mr = mGO.AddComponent<RectTransform>();
            PRC(mr, pw, ph, 0, ph * 0.44f, pw, 26);
            var mt = mGO.AddComponent<Text>();
            mt.text = main; mt.color = C_Gold; mt.fontSize = 18;
            mt.fontStyle = FontStyle.Bold; mt.alignment = TextAnchor.MiddleCenter;
            mt.raycastTarget = false; mt.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (_font != null) mt.font = _font;

            var sGO = GO(go.transform, "Sub");
            var sr = sGO.AddComponent<RectTransform>();
            PRC(sr, pw, ph, 0, ph * 0.63f, pw, 18);
            var st = sGO.AddComponent<Text>();
            st.text = sub; st.color = new Color(C_Gold.r, C_Gold.g, C_Gold.b, 0.7f);
            st.fontSize = 9; st.alignment = TextAnchor.MiddleCenter;
            st.raycastTarget = false; st.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (_font != null) st.font = _font;
        }

        static void RuneCircles(Transform cv, string name, float startX, float y, int count, float sz)
        {
            // Rune ellipses: 48×48, step=26. Use Unity built-in Knob sprite for true circles.
            const float step = 26f;
            var circleSpr = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var cont = GO(cv, name);
            var crt = cont.AddComponent<RectTransform>();
            float totalW = (count - 1) * step + sz;
            PR(crt, startX, y, totalW, sz);
            for (int i = 0; i < count; i++)
            {
                float cx = i * step;
                var rGO = GO(cont.transform, $"Rune{i:D2}");
                var rt  = rGO.AddComponent<RectTransform>();
                PRC(rt, totalW, sz, cx, 0, sz, sz);
                // Filled circle bg
                var bg = rGO.AddComponent<Image>();
                bg.sprite = circleSpr; bg.color = C_CardBg; bg.raycastTarget = false;
                // Circle border ring: child with gold colour
                var ring = GO(rGO.transform, "Ring");
                var ringRT = ring.AddComponent<RectTransform>();
                ringRT.anchorMin = Vector2.zero; ringRT.anchorMax = Vector2.one;
                ringRT.offsetMin = ringRT.offsetMax = Vector2.zero;
                var ringImg = ring.AddComponent<Image>();
                ringImg.sprite = circleSpr;
                ringImg.color = new Color(C_GoldBorder.r, C_GoldBorder.g, C_GoldBorder.b, 0.75f);
                ringImg.type = Image.Type.Filled;
                ringImg.fillMethod = Image.FillMethod.Radial360;
                ringImg.fillAmount = 1f; ringImg.raycastTarget = false;
                // Inner dark hole to create ring appearance
                var hole = GO(rGO.transform, "Hole");
                var holeRT = hole.AddComponent<RectTransform>();
                holeRT.anchorMin = new Vector2(0.15f, 0.15f);
                holeRT.anchorMax = new Vector2(0.85f, 0.85f);
                holeRT.offsetMin = holeRT.offsetMax = Vector2.zero;
                var holeImg = hole.AddComponent<Image>();
                holeImg.sprite = circleSpr;
                holeImg.color = C_CardBg; holeImg.raycastTarget = false;
            }
        }

        static void BaseSlots(Transform cv, string name, float px, float py, float pw, float ph)
        {
            // 8 slots of 72px, gap=8
            const float sw = 72f, gap = 8f;
            var cont = ImgNode(cv, name, px, py, pw, ph, null, Color.clear);
            for (int i = 0; i < 8; i++)
            {
                float cx = i * (sw + gap);
                var sGO = GO(cont.transform, $"Slot{i}");
                var rt  = sGO.AddComponent<RectTransform>();
                PRC(rt, pw, ph, cx, 0, sw, ph);
                var img = sGO.AddComponent<Image>();
                img.color = C_CardBg; img.raycastTarget = false;
                Outline(sGO);
            }
        }

        static void Vignette(Transform cv)
        {
            var go = ImgNode(cv, "Vignette", -23, -6, 1930, 1080, null,
                new Color(0, 0, 0, 0.28f));
            go.GetComponent<Image>().raycastTarget = false;
        }

        static void DeckPile(Transform cv, string name, float px, float py, float pw, string label)
        {
            const float ph = 195f;
            var go = ImgNode(cv, name, px, py, pw, ph,
                "Assets/Resources/CardArt/card_back_03.png");

            // Label at y=31,h=40 — fontSize=20 matching Pencil
            var lGO = GO(go.transform, "Label");
            var lr  = lGO.AddComponent<RectTransform>();
            PRC(lr, pw, ph, 0, 21, pw, 40);
            var lt = lGO.AddComponent<Text>();
            lt.text = label; lt.color = C_Gold; lt.fontSize = 20;
            lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false;
            if (_font != null) lt.font = _font;
            var lsh = lGO.AddComponent<Shadow>();
            lsh.effectColor = new Color(0,0,0,0.85f); lsh.effectDistance = new Vector2(1,-1);

            // Count at y=130,h=44 — fontSize=30 matching Pencil
            var cGO = GO(go.transform, "Count");
            var cr  = cGO.AddComponent<RectTransform>();
            PRC(cr, pw, ph, 0, 130, pw, 44);
            var ct = cGO.AddComponent<Text>();
            ct.text = "0"; ct.color = C_Gold; ct.fontSize = 30;
            ct.fontStyle = FontStyle.Bold; ct.alignment = TextAnchor.MiddleCenter;
            ct.raycastTarget = false;
            if (_font != null) ct.font = _font;
        }

        static void HandCard(Transform cv, string name, float px, float py, float rot)
        {
            var go = ImgNode(cv, name, px, py, 110, 154,
                "Assets/Resources/CardArt/card_back_03.png");
            go.GetComponent<RectTransform>().localRotation = Quaternion.Euler(0, 0, rot);
        }

        static void EndTurnBtn(Transform cv, float px, float py, float pw, float ph)
        {
            var go = ImgNode(cv, "EndTurnBtn", px, py, pw, ph,
                "Assets/Resources/UI/Generated/btn_end_turn.png");
            if (go.GetComponent<Image>()?.sprite == null)
                if (go.GetComponent<Image>() != null)
                    go.GetComponent<Image>().color = C_CardBg;
            Outline(go, 0.9f);
            go.AddComponent<Button>();
            var lGO = GO(go.transform, "Label");
            var lr  = lGO.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var lt = lGO.AddComponent<Text>();
            lt.text = "结束回合"; lt.color = C_Gold; lt.fontSize = 12;
            lt.fontStyle = FontStyle.Bold; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false;
            if (_font != null) lt.font = _font;
        }

        static void ViewDiscardBtn(Transform cv, float px, float py, float pw, float ph)
        {
            var go = ImgNode(cv, "ViewDiscardBtn", px, py, pw, ph, null, C_CardBg);
            Outline(go, 0.5f);
            go.AddComponent<Button>();
            var lGO = GO(go.transform, "Label");
            var lr  = lGO.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var lt = lGO.AddComponent<Text>();
            lt.text = "查看弃牌堆"; lt.color = new Color(C_Gold.r,C_Gold.g,C_Gold.b,0.8f);
            lt.fontSize = 9; lt.alignment = TextAnchor.MiddleCenter;
            lt.raycastTarget = false;
            if (_font != null) lt.font = _font;
        }

        static void ScoreCircle(Transform cv, string name, float px, float py, float sz, string num)
        {
            // Ellipse 22×22: Pencil uses transparent fill + gold stroke.
            // In Unity we use Knob sprite ring approach: gold outer ring, transparent hole center.
            var circleSpr = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var go = GO(cv, name);
            var rt = go.AddComponent<RectTransform>();
            PR(rt, px, py, sz, sz);
            // Gold ring (full circle, gold colour)
            var ring = go.AddComponent<Image>();
            ring.sprite = circleSpr;
            ring.color = new Color(C_GoldBorder.r, C_GoldBorder.g, C_GoldBorder.b, 0.85f);
            ring.raycastTarget = false;
            // Transparent inner hole — creates ring appearance
            var hole = GO(go.transform, "Hole");
            var holeRT = hole.AddComponent<RectTransform>();
            holeRT.anchorMin = new Vector2(0.14f, 0.14f); holeRT.anchorMax = new Vector2(0.86f, 0.86f);
            holeRT.offsetMin = holeRT.offsetMax = Vector2.zero;
            var holeImg = hole.AddComponent<Image>();
            holeImg.sprite = circleSpr;
            holeImg.color = Color.clear; holeImg.raycastTarget = false;
            // Number text — fontSize=11 matching Pencil
            var lGO = GO(go.transform, "Num");
            var lr  = lGO.AddComponent<RectTransform>();
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = lr.offsetMax = Vector2.zero;
            var lt = lGO.AddComponent<Text>();
            lt.text = num; lt.color = new Color(C_Gold.r,C_Gold.g,C_Gold.b,0.9f);
            lt.fontSize = 11; lt.fontStyle = FontStyle.Normal;
            lt.alignment = TextAnchor.MiddleCenter; lt.raycastTarget = false;
            lt.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (_font != null) lt.font = _font;
        }

        static void Logo(Transform cv, float px, float py, float pw, float ph)
        {
            // 0c05h group: LEAGUE / OF / LEGENDS
            // LEAGUE:  cx=-3,  cy=0,  cw=252, ch=50
            // OF:      cx=218, cy=12, cw=31,  ch=22
            // LEGENDS: cx=15,  cy=42, cw=252, ch=50
            var cont = GO(cv, "Logo");
            var rt   = cont.AddComponent<RectTransform>();
            PR(rt, px, py, pw, ph);

            LogoText(cont.transform, "LEAGUE",  -3,  0, 252, 50, pw, ph, "LEAGUE",  44);
            LogoText(cont.transform, "OF",      218, 12,  31, 22, pw, ph, "OF",      16);
            LogoText(cont.transform, "LEGENDS",  15, 42, 252, 50, pw, ph, "LEGENDS", 44);
        }

        static void LogoText(Transform parent, string name,
                             float cx, float cy, float cw, float ch,
                             float pW, float pH, string text, int size)
        {
            var go = GO(parent, name);
            var rt = go.AddComponent<RectTransform>();
            PRC(rt, pW, pH, cx, cy, cw, ch);
            var t = go.AddComponent<Text>();
            t.text = text; t.color = C_Gold; t.fontSize = size;
            t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (_font != null) t.font = _font;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0,0,0,0.85f); sh.effectDistance = new Vector2(2,-2);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Primitives
        // ═══════════════════════════════════════════════════════════════════

        static GameObject GO(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// Create a positioned Image node.
        /// Load order: LoadAllAssetsAtPath (sprite sub-asset) →
        ///             Texture2D + Sprite.Create → RawImage(Texture2D) → colour fallback.
        static GameObject ImgNode(Transform parent, string name,
                                  float px, float py, float pw, float ph,
                                  string path = null, Color? tint = null)
        {
            var go = GO(parent, name);
            var rt = go.AddComponent<RectTransform>();
            PR(rt, px, py, pw, ph);

            if (path != null)
            {
                // Strategy 1: find sprite sub-asset via LoadAllAssetsAtPath
                Sprite foundSpr = null;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is Sprite s) { foundSpr = s; break; }

                if (foundSpr == null)
                {
                    // Strategy 2: load Texture2D and create sprite manually
                    var tex2d = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex2d != null)
                        foundSpr = Sprite.Create(tex2d,
                            new Rect(0, 0, tex2d.width, tex2d.height),
                            new Vector2(0.5f, 0.5f), 100f);
                }

                if (foundSpr != null)
                {
                    var img = go.AddComponent<Image>();
                    img.sprite = foundSpr; img.color = tint ?? Color.white;
                    img.preserveAspect = false; img.raycastTarget = false;
                    return go;
                }

                // Strategy 3: RawImage with Texture2D (works regardless of import type)
                var rawTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (rawTex != null)
                {
                    var raw = go.AddComponent<RawImage>();
                    raw.texture = rawTex; raw.color = tint ?? Color.white;
                    raw.raycastTarget = false;
                    return go;
                }

                Debug.LogWarning($"[BoardFromPencil] Could not load image: {path}");
            }
            var fallback = go.AddComponent<Image>();
            fallback.color = tint ?? Color.clear;
            fallback.raycastTarget = false;
            return go;
        }

        /// Add 1px gold border strips to a GameObject (works on any opaque or transparent Image).
        static void Outline(GameObject go, float alpha = 0.7f)
        {
            AddBorderStrips(go.transform, 1f,
                new Color(C_GoldBorder.r, C_GoldBorder.g, C_GoldBorder.b, alpha));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Utilities
        // ═══════════════════════════════════════════════════════════════════

        static GameObject MakeCanvas()
        {
            var go = new GameObject("Canvas");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(W, H);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        static Font LoadFont()
        {
            foreach (var p in new[] { "Assets/Fonts/simhei.ttf", "Assets/Fonts/simkai.ttf", "Assets/Fonts/msyh.ttc" })
            { var f = AssetDatabase.LoadAssetAtPath<Font>(p); if (f != null) return f; }
            return Font.CreateDynamicFontFromOSFont(new[] { "SimHei", "Microsoft YaHei", "Arial" }, 14);
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); c.a = 1; return c; }

        static void EnsureDir(string p)
        {
            if (AssetDatabase.IsValidFolder(p)) return;
            var parent = System.IO.Path.GetDirectoryName(p)?.Replace('\\', '/') ?? "";
            var folder = System.IO.Path.GetFileName(p);
            if (!string.IsNullOrEmpty(parent)) AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
