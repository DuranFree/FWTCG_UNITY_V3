using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using DG.Tweening;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Bot
{
    // ── 决策策略 ──────────────────────────────────────────────────────────────
    public enum BotStrategy
    {
        Greedy,     // 贵的先打 + 己方优势战场（默认）
        Aggro,      // 便宜的优先，立刻上战场推进
        Random,     // 从可出牌中随机抽一张
        Suicidal,   // 英雄优先上战场，卡死也不结束回合
        RuneHoard,  // 只在必要时横置符文
        Strategic,  // 调用 SimpleAI 做策略决策（玩家侧也跑同款 AI，自对弈用）
        Rotate      // 每局轮换，覆盖所有策略
    }

    // ── Bug 严重度 ────────────────────────────────────────────────────────────
    public enum BugSeverity { High, Medium, Low }

    /// <summary>
    /// GameBot — 自动玩游戏的机器人。
    ///
    /// 用途：
    ///   1. 自动执行玩家回合操作（出牌、移动、结束回合）
    ///   2. 自动应答所有弹窗（AskPromptUI）
    ///   3. 捕获运行时异常和不合法状态，记录完整操作序列
    ///   4. 测量客观手感指标（回合时长、动作响应帧数）
    ///   5. 跑完 N 局后输出报告到 logs/bot-report.txt
    ///
    /// 使用方式：
    ///   - 通过 GameBotWindow（Editor 菜单 FWTCG > Bot Tester）启动
    ///   - 或直接挂到场景 GameManager GameObject 上，勾选 AutoStart
    /// </summary>
    public class GameBot : MonoBehaviour
    {
        // ── 配置 ──────────────────────────────────────────────────────────────
        [Header("Bot 配置")]
        [Tooltip("是否在 Start 时自动启动")]
        public bool AutoStart = false;

        [Tooltip("计划跑的总局数（0 = 无限）")]
        public int TargetGames = 10;

        [Tooltip("每个操作之间的延迟（秒）")]
        [Range(0.05f, 2f)]
        public float ActionDelay = 0.15f;

        [Tooltip("弹窗出现后等待多久再自动确认（秒）")]
        [Range(0.05f, 1f)]
        public float DialogDelay = 0.1f;

        [Tooltip("对局超过此秒数无进展判定为卡死")]
        public float StuckTimeout = 30f;

        [Tooltip("是否对弹窗总是选「确认/是」")]
        public bool AlwaysConfirmDialogs = true;

        [Tooltip("游戏速度倍数（1=正常，10=10倍速）。加速时 AI 延迟也会跳过。")]
        [Range(1f, 20f)]
        public float SpeedMultiplier = 1f;

        [Tooltip("随机种子（0 = 用时间戳）。固定种子 + 固定操作 = 可回放。")]
        public int RandomSeed = 0;

        [Tooltip("发现 bug 时自动截图到 logs/screenshots/")]
        public bool CaptureScreenshotOnBug = true;

        [Tooltip("把每次游戏操作写到 logs/bot-replay.jsonl（用于后续回放）")]
        public bool RecordReplay = true;

        [Tooltip("每帧掉到此 FPS 以下连续 10 帧 → 报告性能 bug")]
        public float FpsWarnThreshold = 20f;

        [Tooltip("每局结束时 tween / GC 泄漏阈值（活跃 tween 数）")]
        public int TweenLeakThreshold = 50;

        [Tooltip("每局 GC 增长 > 此 MB → 报告内存泄漏")]
        // 40MB：GC.Collect() 后仍有 20~30MB 稳态驻留（装备材质 + 事件闭包），真泄漏才超 40MB
        public float GcLeakThresholdMB = 40f;

        [Tooltip("决策策略。Rotate 会在每局轮换前 5 种策略（覆盖率最高）。")]
        public BotStrategy Strategy = BotStrategy.Greedy;

        // ── 状态（只读，供 Editor 窗口显示）─────────────────────────────────
        [Header("运行状态（只读）")]
        [SerializeField] private bool _running;
        [SerializeField] private int  _gamesPlayed;
        [SerializeField] private int  _bugsFound;
        [SerializeField] private int  _stuckCount;

        // ── 内部 ──────────────────────────────────────────────────────────────
        private Coroutine  _botRoutine;
        private BotReport  _report;
        private float      _savedTimeScale = 1f;
        private GameRecord _currentGame;
        private float      _lastProgressTime;
        private string     _lastPhase;
        private int        _lastRound;
        private int        _actionCount;

        // ── UI 时序监控状态 ───────────────────────────────────────────────────
        // 上一帧各面板的显示状态，用于检测非法叠加和意外关闭
        private bool _prevAskPrompt;
        private bool _prevSpellTarget;
        private bool _prevSpellShowcase;
        private bool _prevSpellDuel;
        private bool _prevReactiveWindow;
        private bool _prevCoinFlip;
        private bool _prevMulligan;
        // 面板开启时间（真实时间），用于检测异常滞留
        private float _askPromptOpenTime;
        private float _spellTargetOpenTime;
        private const float MAX_PANEL_DURATION = 8f; // 超过此秒数面板仍在 → 报异常滞留

        // 公开给 Editor 窗口查询
        public bool Running       => _running;
        public int  GamesPlayed   => _gamesPlayed;
        public int  BugsFound     => _bugsFound;

        // ── 实时 bug 日志（markdown，每发现一条 bug 立即 append） ─────────────
        private string _bugLogPath;
        // Shader 扫描去重：同一个路径 + 原因只报一次
        private readonly HashSet<string> _seenShaderIssues = new HashSet<string>();
        // 日志消息去重：同一条 Error/Warning 只报前 3 次
        private readonly Dictionary<string, int> _seenLogMsgs = new Dictionary<string, int>();
        private const int MAX_SAME_LOG = 3;
        // Shader 扫描频率（真实时间，秒）—— 0.25s 能抓到短暂的紫色闪帧
        private const float SHADER_SCAN_INTERVAL = 0.25f;
        private float _lastShaderScanTime;

        // 回放记录
        private string _replayLogPath;
        private int _actualSeed;
        // 每局泄漏基线
        private int _gameStartTweens;
        private long _gameStartGC;
        private int _gameStartParticles;
        // FPS 监控：连续低帧计数
        private int _lowFpsFrames;
        private float _lastLowFpsReportTime;
        // 规则守门去重
        private readonly HashSet<string> _seenInvariants = new HashSet<string>();
        // 状态转移监控：上帧快照
        private int _prevPScore = -1, _prevEScore = -1;
        private int _prevPDiscardCount = -1, _prevEDiscardCount = -1;
        private int _prevPExileCount = -1, _prevEExileCount = -1;
        // 得分事件对账：分数变化时应同时在 BFScoredThisTurn / BFConqueredThisTurn 里新增条目
        private int _prevBFScoredCount = -1, _prevBFConqueredCount = -1;
        private int _prevRound = -1;
        private string _prevTurn = null;
        private bool _prevGameOver = false;
        private const int INITIAL_DECK_SIZE = 40; // 每侧初始牌堆数（含英雄）
        // 本局实际策略（Rotate 模式下按局轮换解析成具体策略）
        private BotStrategy _activeStrategy = BotStrategy.Greedy;
        // 各严重度 bug 计数（Editor 面板实时展示）
        public int BugsHigh   { get; private set; }
        public int BugsMedium { get; private set; }
        public int BugsLow    { get; private set; }
        public float LastFps  { get; private set; }
        public int ActiveTweens => SafeGetActiveTweens();
        public long CurrentGcBytes => GC.GetTotalMemory(false);

        // 分类 → 严重度映射
        private static readonly Dictionary<string, BugSeverity> _severityMap =
            new Dictionary<string, BugSeverity>
        {
            { "Exception",     BugSeverity.High },
            { "Error",         BugSeverity.High },
            { "Assert",        BugSeverity.High },
            { "ShaderMissing", BugSeverity.High },
            { "非法状态",      BugSeverity.High },
            { "重复单位",      BugSeverity.High },
            { "回合倒退",      BugSeverity.High },
            { "Score越界",     BugSeverity.High },
            { "符文守恒",      BugSeverity.High },
            { "符文超上限",    BugSeverity.High },
            { "卡死",          BugSeverity.High },
            { "Tween泄漏",     BugSeverity.Medium },
            { "GC泄漏",        BugSeverity.Medium },
            { "粒子泄漏",      BugSeverity.Medium },
            { "UI叠加",        BugSeverity.Medium },
            { "UI滞留",        BugSeverity.Medium },
            { "UI残留",        BugSeverity.Medium },
            { "时序错误",      BugSeverity.Medium },
            // 玩法状态转移类（多为 High，因为规则破坏）
            { "分数倒退",      BugSeverity.High },
            { "分数跳变",      BugSeverity.Medium },
            { "得分无事件",    BugSeverity.High },
            { "重复据守",      BugSeverity.High },
            { "重复征服",      BugSeverity.High },
            { "游戏未结束",    BugSeverity.High },
            { "游戏结束后分数变化", BugSeverity.High },
            { "弃牌堆缩水",    BugSeverity.High },
            { "放逐堆缩水",    BugSeverity.High },
            { "牌堆守恒",      BugSeverity.High },
            { "英雄重复",      BugSeverity.High },
            { "HP为负",        BugSeverity.High },
            { "HP超上限",      BugSeverity.Medium },
            { "BuffToken越界", BugSeverity.High },
            { "Atk为负",       BugSeverity.High },
            { "眩晕仍贡献战力", BugSeverity.High },
            { "待命单位在战场", BugSeverity.High },
            { "装备链断裂",    BugSeverity.High },
            { "手牌临时加成残留", BugSeverity.Medium },
            { "手牌Stun残留",  BugSeverity.Medium },
            { "手牌Exhausted残留", BugSeverity.Medium },
            { "Warning",       BugSeverity.Low },
            { "性能",          BugSeverity.Low },
            { "Tween激增",     BugSeverity.Medium },
        };

        private static BugSeverity GetSeverity(string category)
            => _severityMap.TryGetValue(category, out var s) ? s : BugSeverity.Low;

        // ── Unity 生命周期 ────────────────────────────────────────────────────

        private void Awake()
        {
            Application.logMessageReceived += OnUnityLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            StopBot();
        }

        private void Start()
        {
            if (AutoStart) StartBot();
        }

        // ── 公开控制 ──────────────────────────────────────────────────────────

        public void StartBot()
        {
            if (_running) return;
            _running     = true;
            _report      = new BotReport();
            _gamesPlayed = 0;
            _bugsFound   = 0;
            _stuckCount  = 0;
            _seenShaderIssues.Clear();
            _seenLogMsgs.Clear();
            _seenInvariants.Clear();
            _lastShaderScanTime = 0f;
            _lowFpsFrames = 0;
            _lastLowFpsReportTime = 0f;
            BugsHigh = BugsMedium = BugsLow = 0;
            LastFps = 0f;

            // 随机种子：0 = 时间戳，否则固定，便于回放
            _actualSeed = RandomSeed != 0 ? RandomSeed : (int)(DateTime.Now.Ticks & 0x7fffffff);
            UnityEngine.Random.InitState(_actualSeed);
            Debug.Log($"[Bot] 随机种子: {_actualSeed}");

            InitBugLogFile();
            InitReplayLogFile();

            // 应用速度倍数
            _savedTimeScale = Time.timeScale;
            Time.timeScale  = SpeedMultiplier;
            AI.SimpleAI.SkipDelays = SpeedMultiplier > 1f;
            // 把速度同步到中央 GameTiming，让所有 Task.Delay 也按倍数缩短
            // （Task.Delay 用 wall-clock 时间，不受 Time.timeScale 影响，必须单独同步）
            FWTCG.Core.GameTiming.SpeedMultiplier = SpeedMultiplier;
            UI.StartupFlowUI.BotAutoAdvance = true;

            // Bot 自身 ActionDelay 也按倍数压缩（最低 1 帧）
            ActionDelay = Mathf.Max(ActionDelay / SpeedMultiplier, 0.016f);
            DialogDelay = Mathf.Max(DialogDelay / SpeedMultiplier, 0.016f);

            _botRoutine = StartCoroutine(BotMainLoop());
            Debug.Log($"[Bot] 启动，目标局数：{(TargetGames == 0 ? "无限" : TargetGames.ToString())}，速度倍数：{SpeedMultiplier}x");
        }

        public void StopBot()
        {
            if (!_running) return;
            _running = false;
            if (_botRoutine != null) { StopCoroutine(_botRoutine); _botRoutine = null; }

            // 恢复时间缩放和 AI 延迟
            Time.timeScale = _savedTimeScale;
            AI.SimpleAI.SkipDelays = false;
            FWTCG.Core.GameTiming.Reset();
            UI.StartupFlowUI.BotAutoAdvance = false;

            SaveReport();
            Debug.Log($"[Bot] 已停止。已跑 {_gamesPlayed} 局，发现 {_bugsFound} 个问题。");
        }

        // ── 主循环 ────────────────────────────────────────────────────────────

        private IEnumerator BotMainLoop()
        {
            // 等游戏初始化
            yield return new WaitForSeconds(1.5f);

            while (_running)
            {
                var gm = GameManager.Instance;
                if (gm == null) { yield return new WaitForSeconds(0.5f); continue; }

                var gs = gm.GetState();
                if (gs == null) { yield return new WaitForSeconds(0.5f); continue; }

                // 新局开始
                if (!gs.GameOver)
                {
                    _currentGame  = new GameRecord(_gamesPlayed + 1);
                    _lastProgressTime = Time.realtimeSinceStartup;  // 用真实时间，不受 timeScale 影响
                    _lastPhase    = gs.Phase;
                    _lastRound    = gs.Round;

                    // 采样本局起始基线（tween / GC / particle）用于结束时对比泄漏
                    SampleLeakBaseline();
                    // 新局开始：重置玩法状态快照（避免跨局误报分数倒退等）
                    _prevPScore = _prevEScore = -1;
                    _prevPDiscardCount = _prevEDiscardCount = -1;
                    _prevPExileCount = _prevEExileCount = -1;
                    _prevBFScoredCount = _prevBFConqueredCount = -1;
                    _prevRound = -1;
                    _prevTurn = null;
                    _prevGameOver = false;
                    _uiDumpedThisGame = false; // 每局允许 dump 一次
                    // 解析本局活跃策略（Rotate 会按局轮换）
                    _activeStrategy = ResolveStrategy();
                    _currentGame?.Log($"[Bot] 本局策略: {_activeStrategy}");

                    yield return PlayOneGame(gm);

                    // 游戏结束：检查泄漏
                    CheckLeakDelta();
                }

                // 等待 GameOver 状态
                float waitStart = Time.time;
                while (!IsGameOver() && Time.time - waitStart < 60f)
                    yield return new WaitForSeconds(0.2f);

                // 结束本局
                if (_currentGame != null)
                {
                    _currentGame.Finish(gs);
                    _report.Games.Add(_currentGame);
                }
                _gamesPlayed++;

                Debug.Log($"[Bot] 第 {_gamesPlayed} 局结束。得分 玩家:{gs.PScore} AI:{gs.EScore}");

                if (TargetGames > 0 && _gamesPlayed >= TargetGames)
                {
                    _running = false;
                    SaveReport();
                    Debug.Log($"[Bot] 全部 {TargetGames} 局完成，报告已保存。");
                    yield break;
                }

                // 重新加载场景开始新局
                yield return RestartGame();
            }
        }

        private IEnumerator PlayOneGame(GameManager gm)
        {
            while (_running)
            {
                var gs = gm.GetState();
                if (gs == null || gs.GameOver) yield break;

                // 卡死检测（用真实时间，不受 timeScale 影响）
                bool phaseChanged  = gs.Phase != _lastPhase;
                bool roundChanged  = gs.Round != _lastRound;
                if (phaseChanged || roundChanged)
                {
                    _lastProgressTime = Time.realtimeSinceStartup;
                    _lastPhase  = gs.Phase;
                    _lastRound  = gs.Round;
                }
                if (Time.realtimeSinceStartup - _lastProgressTime > StuckTimeout)
                {
                    LogBug("卡死", $"超过 {StuckTimeout}s 无进展。Phase={gs.Phase} Round={gs.Round} Turn={gs.Turn}");
                    _stuckCount++;
                    _lastProgressTime = Time.realtimeSinceStartup;
                    // 强制结束回合尝试解卡
                    if (gm.IsPlayerActionPhase()) gm.OnEndTurnClicked();
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                // ── UI 时序监控（每帧）──────────────────────────────────────
                CheckUIOverlap(gs);

                // ── 玩法状态转移监控（每帧）────────────────────────────────
                ValidateGameplayInvariants(gs);

                // ── 速度保险：其他代码（慢动作 / cut scene）可能把 Time.timeScale
                //    拉回 1f，检测到就立刻恢复 SpeedMultiplier。
                if (SpeedMultiplier > 1.01f && Mathf.Abs(Time.timeScale - SpeedMultiplier) > 0.1f)
                {
                    Time.timeScale = SpeedMultiplier;
                }

                // ── FPS 监控（每帧）──────────────────────────────────────────
                CheckFps();

                // ── Shader/材质健康扫描（节流，~1s 一次）────────────────────
                if (Time.realtimeSinceStartup - _lastShaderScanTime >= SHADER_SCAN_INTERVAL)
                {
                    _lastShaderScanTime = Time.realtimeSinceStartup;
                    ScanForShaderIssues();
                }

                // ── Bot 主动点击启动流按钮（不依赖 BotAutoAdvance 时机）──
                var sfu = UI.StartupFlowUI.Instance;
                if (sfu != null)
                {
                    if (sfu.TryBotClickCoinFlip())
                    {
                        _currentGame?.Log("[Bot] 自动点击硬币确认");
                        _lastProgressTime = Time.realtimeSinceStartup;
                        yield return new WaitForSeconds(DialogDelay);
                        continue;
                    }
                    if (sfu.TryBotClickMulligan())
                    {
                        _currentGame?.Log("[Bot] 自动点击换牌确认");
                        _lastProgressTime = Time.realtimeSinceStartup;
                        yield return new WaitForSeconds(DialogDelay);
                        continue;
                    }
                }

                // 处理弹窗
                if (UI.AskPromptUI.IsShowing)
                {
                    yield return new WaitForSeconds(DialogDelay);
                    if (UI.AskPromptUI.IsShowing && UI.AskPromptUI.Instance != null)
                    {
                        string dialogCtx = $"Phase={gs.Phase} Round={gs.Round}";
                        _currentGame?.Log($"[对话框] 自动{(AlwaysConfirmDialogs ? "确认" : "取消")} {dialogCtx}");
                        UI.AskPromptUI.Instance.BotAutoAnswer(AlwaysConfirmDialogs);
                    }
                    yield return new WaitForSeconds(DialogDelay);
                    _lastProgressTime = Time.realtimeSinceStartup;
                    continue;
                }

                // 不是玩家行动阶段 → 等待
                if (!gm.IsPlayerActionPhase())
                {
                    yield return null;
                    continue;
                }

                // 验证当前状态合法性
                ValidateState(gs);

                // 执行一个操作
                _actionCount = 0;
                yield return ExecutePlayerTurn(gm, gs);

                yield return new WaitForSeconds(ActionDelay);
            }
        }

        // ── 玩家回合逻辑 ──────────────────────────────────────────────────────

        private IEnumerator ExecutePlayerTurn(GameManager gm, GameState gs)
        {
            float turnStart = Time.realtimeSinceStartup;

            // Strategic 策略：整回合交给 SimpleAI（玩家侧自对弈路径）
            // SimpleAI 内部会调 turnMgr.EndTurn() 结束玩家回合
            if (_activeStrategy == BotStrategy.Strategic)
            {
                _currentGame?.Log("[操作] Strategic 模式 — 调用 SimpleAI 处理玩家回合");
                RecordReplayAction("strategic_turn");
                var aiTask = gm.RunStrategicPlayerTurn();
                while (aiTask != null && !aiTask.IsCompleted)
                {
                    yield return null;
                }
                if (aiTask != null && aiTask.IsFaulted)
                {
                    LogBug("Exception", $"SimpleAI 玩家回合异常: {aiTask.Exception?.GetBaseException().Message}");
                }
                float stratDuration = Time.realtimeSinceStartup - turnStart;
                _currentGame?.RecordTurnTime(gs.Round, stratDuration);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 1. 标记所有未横置符文为"待横置"（1b 改为标记模式）
            // RuneHoard: 只在手里有牌付不起时才标记
            // 其他策略：已有未横置符文 → 一次性全标
            bool hasUntappedRunes = gs.PRunes.Any(r => !r.Tapped);
            bool alreadyPrepared = gm.HasPreparedRunes();
            bool needMoreMana = gs.PHand.Any(u => u.CardData.Cost > gs.PMana);
            bool wantTap = hasUntappedRunes && !alreadyPrepared &&
                           (_activeStrategy != BotStrategy.RuneHoard || needMoreMana);
            if (wantTap)
            {
                _currentGame?.Log($"[操作] 标记所有符文为待横置（{gs.PRunes.Count(r=>!r.Tapped)} 张，commit 时生效）");
                RecordReplayAction("tap_all_runes");
                gm.OnTapAllRunesClicked();
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break; // 每帧只做一个主操作
            }

            // 2. 出牌（手牌 → 基地）— 根据策略排序
            var playable = gs.PHand
                .Where(u => !u.CardData.IsSpell && CanAfford(gs, u))
                .ToList();
            playable = SortByStrategy(playable);

            if (playable.Count > 0)
            {
                var unit = playable[0];
                _currentGame?.Log($"[操作] 打出手牌 {unit.UnitName}（费用:{unit.CardData.Cost} 法力:{gs.PMana}）");
                RecordTiming("play_card_start");
                RecordReplayAction("play_unit",
                    $"{{\"uid\":{unit.Uid},\"name\":\"{JsonEscape(unit.UnitName)}\",\"cost\":{unit.CardData.Cost}}}");
                gm.OnDragCardToBase(unit);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 2b. 出法术
            var spells = gs.PHand
                .Where(u => u.CardData.IsSpell && CanAfford(gs, u))
                .ToList();

            if (spells.Count > 0)
            {
                var spell = spells[0];
                _currentGame?.Log($"[操作] 施法 {spell.UnitName}（费用:{spell.CardData.Cost}）");
                RecordReplayAction("cast_spell",
                    $"{{\"uid\":{spell.Uid},\"name\":\"{JsonEscape(spell.UnitName)}\",\"cost\":{spell.CardData.Cost}}}");
                gm.OnSpellDraggedOut(spell);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 2c. 出英雄（Suicidal 策略：英雄优先，如果能付得起立刻上）
            // 其他策略：等没别的牌可出了再考虑
            if (gs.PHero != null && CanAffordHero(gs, gs.PHero))
            {
                _currentGame?.Log($"[操作] 出英雄 {gs.PHero.UnitName}");
                RecordReplayAction("deploy_hero",
                    $"{{\"uid\":{gs.PHero.Uid},\"name\":\"{JsonEscape(gs.PHero.UnitName)}\"}}");
                gm.OnDragHeroToBase(gs.PHero);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 3. 基地单位上战场 — UI-OVERHAUL-1a: 单选化，每回合只派单张
            var baseUnits = gs.PBase.Where(u => !u.Exhausted).ToList();
            if (baseUnits.Count > 0)
            {
                int bfId = PickBestBattlefield(gs, baseUnits);
                _currentGame?.Log($"[操作] 派遣 {baseUnits[0].UnitName} 到战场 {bfId}");
                RecordTiming("move_to_bf_start");
                RecordReplayAction("move_to_bf",
                    $"{{\"uid\":{baseUnits[0].Uid},\"name\":\"{JsonEscape(baseUnits[0].UnitName)}\",\"bf\":{bfId}}}");
                gm.OnDragUnitToBF(baseUnits[0], bfId);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 4. 没有其他操作 → 结束回合
            float turnDuration = Time.realtimeSinceStartup - turnStart;
            _currentGame?.Log($"[操作] 结束回合（本回合耗时 {turnDuration:F2}s，操作数：{_actionCount}）");
            _currentGame?.RecordTurnTime(gs.Round, turnDuration);
            RecordReplayAction("end_turn", $"{{\"duration\":{turnDuration:F2}}}");
            gm.OnEndTurnClicked();
            yield return new WaitForSeconds(ActionDelay);
            _lastProgressTime = Time.time;
        }

        // ── 决策辅助 ──────────────────────────────────────────────────────────

        private bool CanAfford(GameState gs, UnitInstance unit)
        {
            // Hotfix-13: 考虑 UI-OVERHAUL-1b 的 prepared 资源池 —
            // 已标记待横置的符文 commit 时会 +mana；必须计入否则 bot 永不出牌
            var gm = FWTCG.GameManager.Instance;
            int preparedTapCount = gm != null ? gm.GetPreparedTapIdxs().Count : 0;
            int availableMana = gs.PMana + preparedTapCount;
            if (availableMana < unit.CardData.Cost) return false;
            if (unit.CardData.RuneCost > 0)
            {
                int haveSch = gs.GetSch(GameRules.OWNER_PLAYER, unit.CardData.RuneType);
                if (haveSch < unit.CardData.RuneCost) return false;
            }
            return true;
        }

        private bool CanAffordHero(GameState gs, UnitInstance hero)
        {
            if (hero == null) return false;
            // 已上场（在基地或战场）则不再重复出
            bool deployed = gs.PBase.Contains(hero)
                         || System.Array.Exists(gs.BF, bf => bf.PlayerUnits.Contains(hero));
            if (deployed) return false;
            return CanAfford(gs, hero);
        }

        private int PickBestBattlefield(GameState gs, List<UnitInstance> units)
        {
            // 根据策略选战场：
            //   Aggro    → 敌方最弱的战场（快速征服）
            //   Suicidal → 敌方最强的战场（自找死）
            //   Random   → 随机
            //   其他     → 己方差值最大（稳守优势）
            if (_activeStrategy == BotStrategy.Random)
                return UnityEngine.Random.Range(0, GameRules.BATTLEFIELD_COUNT);

            int best = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                int pCount = gs.BF[i].PlayerUnits.Count;
                int eCount = gs.BF[i].EnemyUnits.Count;
                int score;
                switch (_activeStrategy)
                {
                    case BotStrategy.Aggro:    score = -eCount; break;          // 敌方最弱
                    case BotStrategy.Suicidal: score = eCount; break;           // 敌方最强
                    default:                    score = pCount - eCount; break; // Greedy / RuneHoard
                }
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // 根据策略排序手牌
        private List<UnitInstance> SortByStrategy(List<UnitInstance> units)
        {
            if (units.Count <= 1) return units;
            switch (_activeStrategy)
            {
                case BotStrategy.Aggro:
                    // 便宜的优先（抢节奏）
                    return units.OrderBy(u => u.CardData.Cost).ToList();
                case BotStrategy.Random:
                    // 随机抽一张排头
                    return units.OrderBy(_ => UnityEngine.Random.value).ToList();
                case BotStrategy.Suicidal:
                    // 便宜的优先 + 随机（不计后果）
                    return units.OrderBy(u => u.CardData.Cost)
                                .ThenBy(_ => UnityEngine.Random.value).ToList();
                default:
                    // Greedy / RuneHoard: 贵的先打，花光法力
                    return units.OrderByDescending(u => u.CardData.Cost).ToList();
            }
        }

        // ── 状态验证（每个行动前）────────────────────────────────────────────

        private void ValidateState(GameState gs)
        {
            // 法力不能为负
            if (gs.PMana < 0)
                LogBug("非法状态", $"玩家法力为负: {gs.PMana}");
            if (gs.EMana < 0)
                LogBug("非法状态", $"AI 法力为负: {gs.EMana}");

            // 分数不能为负 / 超上限
            if (gs.PScore < 0 || gs.EScore < 0)
                LogBug("非法状态", $"分数为负: 玩家={gs.PScore} AI={gs.EScore}");
            if (gs.PScore > GameRules.WIN_SCORE || gs.EScore > GameRules.WIN_SCORE)
                ReportInvariantOnce("Score越界", $"分数超上限({GameRules.WIN_SCORE}): 玩家={gs.PScore} AI={gs.EScore}");

            // 符文区容量 ≤ MAX_RUNES_IN_PLAY
            if (gs.PRunes.Count > GameRules.MAX_RUNES_IN_PLAY)
                ReportInvariantOnce("符文超上限",
                    $"玩家符文 {gs.PRunes.Count} > {GameRules.MAX_RUNES_IN_PLAY}");
            if (gs.ERunes.Count > GameRules.MAX_RUNES_IN_PLAY)
                ReportInvariantOnce("符文超上限",
                    $"AI 符文 {gs.ERunes.Count} > {GameRules.MAX_RUNES_IN_PLAY}");

            // 符文守恒：PRunes + PRuneDeck 应等于初始符文总数（24）
            const int INITIAL_RUNE_COUNT =
                GameRules.RUNE_DECK_BLAZING + GameRules.RUNE_DECK_RADIANT +
                GameRules.RUNE_DECK_VERDANT + GameRules.RUNE_DECK_CRUSHING;
            int pTotal = gs.PRunes.Count + gs.PRuneDeck.Count;
            int eTotal = gs.ERunes.Count + gs.ERuneDeck.Count;
            if (pTotal > INITIAL_RUNE_COUNT)
                ReportInvariantOnce("符文守恒",
                    $"玩家符文总量 {pTotal} > 初始 {INITIAL_RUNE_COUNT}（PRunes={gs.PRunes.Count} PRuneDeck={gs.PRuneDeck.Count}）");
            if (eTotal > INITIAL_RUNE_COUNT)
                ReportInvariantOnce("符文守恒",
                    $"AI 符文总量 {eTotal} > 初始 {INITIAL_RUNE_COUNT}（ERunes={gs.ERunes.Count} ERuneDeck={gs.ERuneDeck.Count}）");

            // 同一张牌不能同时在两个区域
            var allUnits = new List<UnitInstance>();
            allUnits.AddRange(gs.PHand);
            allUnits.AddRange(gs.PBase);
            allUnits.AddRange(gs.EHand);
            allUnits.AddRange(gs.EBase);
            foreach (var bf in gs.BF)
            {
                allUnits.AddRange(bf.PlayerUnits);
                allUnits.AddRange(bf.EnemyUnits);
            }
            var seen = new HashSet<int>();
            foreach (var u in allUnits)
            {
                if (!seen.Add(u.Uid))
                    LogBug("重复单位", $"单位 {u.UnitName}(uid={u.Uid}) 同时存在于多个区域");
            }

            // 回合数不能倒退
            if (gs.Round < _lastRound)
                LogBug("回合倒退", $"Round 从 {_lastRound} → {gs.Round}");
        }

        // 规则守门去重报告：同一条 invariant 在同一局里只报一次
        private void ReportInvariantOnce(string cat, string detail)
        {
            string key = $"{_gamesPlayed}|{cat}|{detail}";
            if (!_seenInvariants.Add(key)) return;
            LogBug(cat, detail);
        }

        // ── 玩法状态转移 / 细节层检测（每帧调用） ─────────────────────────────
        // 覆盖：攻击 / 被攻击 / buff / debuff / 死亡 / 战胜 / 牌堆增减等
        private void ValidateGameplayInvariants(GameState gs)
        {
            // ── 1. 得分机制 ─────────────────────────────────────────────────
            int pScoreDelta = _prevPScore >= 0 ? gs.PScore - _prevPScore : 0;
            int eScoreDelta = _prevEScore >= 0 ? gs.EScore - _prevEScore : 0;

            // 1a. 分数倒退
            if (pScoreDelta < 0)
                ReportInvariantOnce("分数倒退", $"玩家分数 {_prevPScore} → {gs.PScore}");
            if (eScoreDelta < 0)
                ReportInvariantOnce("分数倒退", $"AI 分数 {_prevEScore} → {gs.EScore}");

            // 1b. 分数单次跳变 > 3 可疑（单次据守/征服最多 +2）
            if (pScoreDelta > 3)
                ReportInvariantOnce("分数跳变", $"玩家分数一次 +{pScoreDelta}（{_prevPScore}→{gs.PScore}）");
            if (eScoreDelta > 3)
                ReportInvariantOnce("分数跳变", $"AI 分数一次 +{eScoreDelta}（{_prevEScore}→{gs.EScore}）");

            // 1c. 分数变化必须对应 BF 得分事件（hold 或 conquer）
            int bfScoredDelta = _prevBFScoredCount >= 0 ? gs.BFScoredThisTurn.Count - _prevBFScoredCount : 0;
            int bfConqDelta   = _prevBFConqueredCount >= 0 ? gs.BFConqueredThisTurn.Count - _prevBFConqueredCount : 0;
            if ((pScoreDelta > 0 || eScoreDelta > 0) && bfScoredDelta == 0 && bfConqDelta == 0)
            {
                // 允许 ascending_stairs 额外 +1 等特殊情况（通过 BF 事件追加触发，不单独加条目）
                // 但"无任何 BF 事件却加分"就不该发生
                if (pScoreDelta + eScoreDelta > 1) // 允许 +1 的特殊附加分
                    ReportInvariantOnce("得分无事件",
                        $"分数变化(P+{pScoreDelta} E+{eScoreDelta}) 但 BFScored/BFConquered 未新增");
            }

            // 1d. 同一战场同回合不应 hold 两次
            for (int i = 0; i < gs.BFScoredThisTurn.Count; i++)
                for (int j = i + 1; j < gs.BFScoredThisTurn.Count; j++)
                    if (gs.BFScoredThisTurn[i] == gs.BFScoredThisTurn[j])
                        ReportInvariantOnce("重复据守",
                            $"战场 {gs.BFScoredThisTurn[i]} 同回合据守两次 [Round={gs.Round} Turn={gs.Turn}]");

            // 1e. 同一战场同回合不应 conquer 两次
            for (int i = 0; i < gs.BFConqueredThisTurn.Count; i++)
                for (int j = i + 1; j < gs.BFConqueredThisTurn.Count; j++)
                    if (gs.BFConqueredThisTurn[i] == gs.BFConqueredThisTurn[j])
                        ReportInvariantOnce("重复征服",
                            $"战场 {gs.BFConqueredThisTurn[i]} 同回合征服两次 [Round={gs.Round} Turn={gs.Turn}]");

            // 1f. 回合切换（Turn 变化）时 BFScoredThisTurn / BFConqueredThisTurn 应清空
            if (_prevTurn != null && _prevTurn != gs.Turn)
            {
                // 回合刚切换：列表应为空；允许 1 帧延迟（清空可能发生在 phase 切换时）
                if (gs.BFScoredThisTurn.Count > 0 || gs.BFConqueredThisTurn.Count > 0)
                {
                    // 但如果已经切了好几帧还没清 → 报
                    // 这里简化：下一帧才真正判定
                }
            }

            // 1g. 游戏结束条件：任一方达到 EffectiveWinScore → GameOver 必须 true
            // 注意：攀圣长阶（ascending_stairs）会把胜利门槛 +1，不能硬编码 WIN_SCORE=8
            int effWin = FWTCG.Systems.BattlefieldSystem.EffectiveWinScore(gs);
            if ((gs.PScore >= effWin || gs.EScore >= effWin) && !gs.GameOver)
                ReportInvariantOnce("游戏未结束",
                    $"已达 {effWin} 分但 GameOver=false [玩家={gs.PScore} AI={gs.EScore}]");

            // 1h. GameOver 后分数不应再变化（锁分）
            if (_prevGameOver && gs.GameOver && (pScoreDelta != 0 || eScoreDelta != 0))
                ReportInvariantOnce("游戏结束后分数变化",
                    $"GameOver=true 但分数仍变 P+{pScoreDelta} E+{eScoreDelta}");

            _prevPScore = gs.PScore;
            _prevEScore = gs.EScore;
            _prevBFScoredCount = gs.BFScoredThisTurn.Count;
            _prevBFConqueredCount = gs.BFConqueredThisTurn.Count;
            _prevRound = gs.Round;
            _prevTurn = gs.Turn;
            _prevGameOver = gs.GameOver;

            // ── 2. 弃牌堆缩水（弃牌只能增加；重洗入牌堆是合法的，但数量应同步） ──
            if (_prevPDiscardCount >= 0 && gs.PDiscard.Count < _prevPDiscardCount)
            {
                // 允许的场景：弃牌堆重洗到牌堆（deck 空时触发）
                bool deckReshuffled = gs.PDeck.Count >= (_prevPDiscardCount - gs.PDiscard.Count);
                if (!deckReshuffled)
                    ReportInvariantOnce("弃牌堆缩水",
                        $"玩家弃牌堆 {_prevPDiscardCount} → {gs.PDiscard.Count}（牌堆未对应增加）");
            }
            if (_prevEDiscardCount >= 0 && gs.EDiscard.Count < _prevEDiscardCount)
            {
                bool deckReshuffled = gs.EDeck.Count >= (_prevEDiscardCount - gs.EDiscard.Count);
                if (!deckReshuffled)
                    ReportInvariantOnce("弃牌堆缩水",
                        $"AI 弃牌堆 {_prevEDiscardCount} → {gs.EDiscard.Count}（牌堆未对应增加）");
            }
            _prevPDiscardCount = gs.PDiscard.Count;
            _prevEDiscardCount = gs.EDiscard.Count;

            // ── 3. 放逐堆缩水（放逐绝对不可逆） ──────────────────────────────
            if (_prevPExileCount >= 0 && gs.PExile.Count < _prevPExileCount)
                ReportInvariantOnce("放逐堆缩水",
                    $"玩家放逐堆 {_prevPExileCount} → {gs.PExile.Count}（放逐应不可逆）");
            if (_prevEExileCount >= 0 && gs.EExile.Count < _prevEExileCount)
                ReportInvariantOnce("放逐堆缩水",
                    $"AI 放逐堆 {_prevEExileCount} → {gs.EExile.Count}（放逐应不可逆）");
            _prevPExileCount = gs.PExile.Count;
            _prevEExileCount = gs.EExile.Count;

            // ── 4. 牌堆守恒：每侧所有区域单位总数应 = 初始牌堆大小 ─────────
            // （排除在飞行中的 UI 态，仅检查数量是否合理）
            int pTotal = gs.PDeck.Count + gs.PHand.Count + gs.PBase.Count +
                         gs.PDiscard.Count + gs.PExile.Count +
                         BFUnitCount(gs, GameRules.OWNER_PLAYER);
            if (gs.PHero != null && !ContainsHero(gs, GameRules.OWNER_PLAYER)) pTotal += 1;
            int eTotal = gs.EDeck.Count + gs.EHand.Count + gs.EBase.Count +
                         gs.EDiscard.Count + gs.EExile.Count +
                         BFUnitCount(gs, GameRules.OWNER_ENEMY);
            if (gs.EHero != null && !ContainsHero(gs, GameRules.OWNER_ENEMY)) eTotal += 1;
            // 允许轻微误差（入场 VFX 飞行过程中临时不在任何列表）但不应大幅偏离
            if (pTotal > INITIAL_DECK_SIZE + 5)
                ReportInvariantOnce("牌堆守恒", $"玩家单位总数 {pTotal} > 初始 {INITIAL_DECK_SIZE}（区域列表重复？）");
            if (pTotal < INITIAL_DECK_SIZE - 5)
                ReportInvariantOnce("牌堆守恒", $"玩家单位总数 {pTotal} < 初始 {INITIAL_DECK_SIZE}（单位消失？）");
            if (eTotal > INITIAL_DECK_SIZE + 5)
                ReportInvariantOnce("牌堆守恒", $"AI 单位总数 {eTotal} > 初始 {INITIAL_DECK_SIZE}");
            if (eTotal < INITIAL_DECK_SIZE - 5)
                ReportInvariantOnce("牌堆守恒", $"AI 单位总数 {eTotal} < 初始 {INITIAL_DECK_SIZE}");

            // ── 5. 英雄唯一性：PHero/EHero 不应同时在多个区域 ────────────────
            CheckHeroUniqueness(gs, GameRules.OWNER_PLAYER);
            CheckHeroUniqueness(gs, GameRules.OWNER_ENEMY);

            // ── 6. 战场单位逐个检查 ──────────────────────────────────────────
            foreach (var bf in gs.BF)
            {
                foreach (var u in bf.PlayerUnits) CheckUnitInvariants(u, "BF" + bf.Id);
                foreach (var u in bf.EnemyUnits)  CheckUnitInvariants(u, "BF" + bf.Id);
            }
            // 基地也查
            foreach (var u in gs.PBase) CheckUnitInvariants(u, "PBase");
            foreach (var u in gs.EBase) CheckUnitInvariants(u, "EBase");
            // 手牌里的单位不应有 TempAtkBonus / Stunned / 剩余 BuffTokens > 0 的残留（离场应清零）
            foreach (var u in gs.PHand) CheckHandResidue(u, "PHand");
            foreach (var u in gs.EHand) CheckHandResidue(u, "EHand");
        }

        // 战场单位数（某一方）
        private static int BFUnitCount(GameState gs, string owner)
        {
            int n = 0;
            foreach (var bf in gs.BF)
                n += owner == GameRules.OWNER_PLAYER ? bf.PlayerUnits.Count : bf.EnemyUnits.Count;
            return n;
        }

        // 英雄是否已在各区域（基地/战场/手牌/弃牌/放逐），用于守恒计数避免重复
        private static bool ContainsHero(GameState gs, string owner)
        {
            var hero = owner == GameRules.OWNER_PLAYER ? gs.PHero : gs.EHero;
            if (hero == null) return false;
            if (gs.GetHand(owner).Contains(hero)) return true;
            if (gs.GetBase(owner).Contains(hero)) return true;
            if (gs.GetDeck(owner).Contains(hero)) return true;
            if (gs.GetDiscard(owner).Contains(hero)) return true;
            if (gs.GetExile(owner).Contains(hero)) return true;
            foreach (var bf in gs.BF)
            {
                var list = owner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits;
                if (list.Contains(hero)) return true;
            }
            return false;
        }

        private void CheckHeroUniqueness(GameState gs, string owner)
        {
            var hero = owner == GameRules.OWNER_PLAYER ? gs.PHero : gs.EHero;
            if (hero == null) return;
            int locations = 0;
            if (gs.GetHand(owner).Contains(hero)) locations++;
            if (gs.GetBase(owner).Contains(hero)) locations++;
            if (gs.GetDeck(owner).Contains(hero)) locations++;
            if (gs.GetDiscard(owner).Contains(hero)) locations++;
            if (gs.GetExile(owner).Contains(hero)) locations++;
            foreach (var bf in gs.BF)
            {
                var list = owner == GameRules.OWNER_PLAYER ? bf.PlayerUnits : bf.EnemyUnits;
                if (list.Contains(hero)) locations++;
            }
            if (locations > 1)
                ReportInvariantOnce("英雄重复",
                    $"{owner} 英雄 {hero.UnitName} 同时存在于 {locations} 个区域");
        }

        private void CheckUnitInvariants(UnitInstance u, string where)
        {
            if (u == null) return;
            // HP 不能为负（应该死了才对）
            if (u.CurrentHp < 0)
                ReportInvariantOnce("HP为负",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: HP={u.CurrentHp} 但仍在场");
            // HP 上限 = CurrentAtk（装备/buff 已反映在 CurrentAtk 里）
            // 注意：TempAtkBonus 只影响战斗战力，不影响 HP 上限。TempAtkBonus<0（debuff）
            // 时不能把下限算成 HP 天花板，否则会对正常单位误报
            int maxHp = u.CurrentAtk;
            if (u.CurrentHp > maxHp && maxHp > 0)
                ReportInvariantOnce("HP超上限",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: HP={u.CurrentHp} > CurrentAtk={maxHp} (TempBonus={u.TempAtkBonus})");
            // BuffTokens 必须在 [0,1]（规则 702）
            if (u.BuffTokens < 0 || u.BuffTokens > 1)
                ReportInvariantOnce("BuffToken越界",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: BuffTokens={u.BuffTokens} ∉ [0,1]");
            // Atk 基础值不应负
            if (u.Atk < 0)
                ReportInvariantOnce("Atk为负",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: Atk={u.Atk}");
            // Stunned 单位贡献战力应为 0
            if (u.Stunned && u.EffectiveAtk() != 0)
                ReportInvariantOnce("眩晕仍贡献战力",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: Stunned=true 但 EffectiveAtk={u.EffectiveAtk()}");
            // Standby 单位应该在 Base 区；不应出现在战场
            if (u.IsStandby && where.StartsWith("BF"))
                ReportInvariantOnce("待命单位在战场",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: IsStandby=true 却在战场");
            // 装备双向链：AttachedEquipment.AttachedTo 应 = 自己
            if (u.AttachedEquipment != null && u.AttachedEquipment.AttachedTo != u)
                ReportInvariantOnce("装备链断裂",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: AttachedEquipment={u.AttachedEquipment.UnitName} 但其 AttachedTo={u.AttachedEquipment.AttachedTo?.UnitName ?? "null"}");
            if (u.AttachedTo != null && u.AttachedTo.AttachedEquipment != u)
                ReportInvariantOnce("装备链断裂",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: AttachedTo={u.AttachedTo.UnitName} 但其 AttachedEquipment={u.AttachedTo.AttachedEquipment?.UnitName ?? "null"}");
        }

        // 手牌残留检查：离场后临时状态应被清理
        private void CheckHandResidue(UnitInstance u, string where)
        {
            if (u == null) return;
            if (u.TempAtkBonus != 0)
                ReportInvariantOnce("手牌临时加成残留",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: TempAtkBonus={u.TempAtkBonus}（回到手牌应清零）");
            if (u.Stunned)
                ReportInvariantOnce("手牌Stun残留",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: Stunned=true（回到手牌应清除）");
            if (u.Exhausted)
                ReportInvariantOnce("手牌Exhausted残留",
                    $"{u.UnitName}(uid={u.Uid}) @ {where}: Exhausted=true（回到手牌应重置）");
        }

        // ── UI 时序检测 ───────────────────────────────────────────────────────

        private void CheckUIOverlap(GameState gs)
        {
            string ctx = $"Phase={gs.Phase} Round={gs.Round} Turn={gs.Turn}";

            bool askPrompt     = UI.AskPromptUI.IsShowing;
            bool spellTarget   = UI.SpellTargetPopup.IsShowing;
            bool spellShowcase = UI.SpellShowcaseUI.Instance != null && UI.SpellShowcaseUI.Instance.IsShowing;
            bool spellDuel     = UI.SpellDuelUI.Instance != null && UI.SpellDuelUI.Instance.IsShowing;
            bool reactiveWin   = IsReactiveWindowOpen();
            bool coinFlip      = IsCoinFlipOpen();
            bool mulligan      = IsMulliganOpen();

            // ── 1. 检测弹窗叠加（多个互斥面板同时显示）────────────────────
            // AskPromptUI 和 SpellTargetPopup 不应同时出现
            if (askPrompt && spellTarget)
                LogBug("UI叠加", $"AskPromptUI + SpellTargetPopup 同时显示 [{ctx}]");

            // AskPromptUI 和 SpellShowcase 不应同时出现（showcase 应先关闭）
            if (askPrompt && spellShowcase)
                LogBug("UI叠加", $"AskPromptUI + SpellShowcaseUI 同时显示 [{ctx}]");

            // SpellDuel 和 SpellShowcase 不应同时出现
            if (spellDuel && spellShowcase)
                LogBug("UI叠加", $"SpellDuelUI + SpellShowcaseUI 同时显示 [{ctx}]");

            // 启动流面板（硬币/换牌）不应和游戏弹窗同时出现
            if ((coinFlip || mulligan) && (askPrompt || spellTarget || spellShowcase || spellDuel))
                LogBug("UI叠加", $"启动流面板与游戏弹窗同时显示 coinFlip={coinFlip} mulligan={mulligan} [{ctx}]");

            // 换牌和硬币不应同时出现
            if (coinFlip && mulligan)
                LogBug("UI叠加", $"CoinFlipPanel + MulliganPanel 同时显示 [{ctx}]");

            // ── 2. 检测面板在游戏结束后仍残留 ────────────────────────────
            if (gs.GameOver && (askPrompt || spellTarget || spellShowcase || spellDuel || reactiveWin))
                LogBug("UI残留", $"游戏已结束但面板仍显示 ask={askPrompt} spellTarget={spellTarget} showcase={spellShowcase} duel={spellDuel} reactive={reactiveWin}");

            // ── 3. 检测面板异常滞留（开启超过阈值还未关闭）───────────────
            float now = Time.realtimeSinceStartup;

            if (askPrompt && !_prevAskPrompt)   _askPromptOpenTime   = now;
            if (spellTarget && !_prevSpellTarget) _spellTargetOpenTime = now;

            if (askPrompt   && now - _askPromptOpenTime   > MAX_PANEL_DURATION)
                LogBug("UI滞留", $"AskPromptUI 已显示超过 {MAX_PANEL_DURATION}s 未关闭 [{ctx}]");
            if (spellTarget && now - _spellTargetOpenTime > MAX_PANEL_DURATION)
                LogBug("UI滞留", $"SpellTargetPopup 已显示超过 {MAX_PANEL_DURATION}s 未关闭 [{ctx}]");

            // ── 4. 检测非玩家回合时出现了需要玩家操作的弹窗 ─────────────
            bool isPlayerTurn = gs.Turn == GameRules.OWNER_PLAYER;
            if (!isPlayerTurn && askPrompt && !spellDuel)
                LogBug("时序错误", $"AI 回合出现了 AskPromptUI [{ctx}]");

            // ── 更新上一帧状态 ────────────────────────────────────────────
            _prevAskPrompt      = askPrompt;
            _prevSpellTarget    = spellTarget;
            _prevSpellShowcase  = spellShowcase;
            _prevSpellDuel      = spellDuel;
            _prevReactiveWindow = reactiveWin;
            _prevCoinFlip       = coinFlip;
            _prevMulligan       = mulligan;
        }

        // 通过 GameObject 活跃状态检测（ReactiveWindowUI 没有静态 IsShowing）
        private bool IsReactiveWindowOpen()
        {
            var rwUI = FindObjectOfType<UI.ReactiveWindowUI>();
            if (rwUI == null) return false;
            var panel = rwUI.transform.Find("Panel") ?? rwUI.transform.GetChild(0);
            return panel != null && panel.gameObject.activeSelf;
        }

        private bool IsCoinFlipOpen()
        {
            var sfu = FindObjectOfType<UI.StartupFlowUI>();
            if (sfu == null) return false;
            // 通过 BotAutoAdvance 静态标志和 panel 活跃状态综合判断
            var panel = sfu.transform.Find("CoinFlipPanel");
            return panel != null && panel.gameObject.activeSelf;
        }

        private bool IsMulliganOpen()
        {
            var sfu = FindObjectOfType<UI.StartupFlowUI>();
            if (sfu == null) return false;
            var panel = sfu.transform.Find("MulliganPanel");
            return panel != null && panel.gameObject.activeSelf;
        }

        // ── 异常捕获 ──────────────────────────────────────────────────────────

        private void OnUnityLog(string msg, string stackTrace, LogType type)
        {
            if (!_running) return;

            // 过滤 bot 自身日志，避免递归
            if (msg.Contains("[Bot]") || msg.Contains("[AutoSave]")) return;

            string category;
            if (type == LogType.Exception) category = "Exception";
            else if (type == LogType.Error) category = "Error";
            else if (type == LogType.Assert) category = "Assert";
            else if (type == LogType.Warning)
            {
                // 只记可能揭示真实问题的 warning；噪音 warning 过滤掉
                if (!IsInterestingWarning(msg)) return;
                category = "Warning";
            }
            else return;

            // 同一条消息去重：只报前 MAX_SAME_LOG 次
            string key = category + "|" + Truncate(msg, 160);
            _seenLogMsgs.TryGetValue(key, out int count);
            if (count >= MAX_SAME_LOG) return;
            _seenLogMsgs[key] = count + 1;

            string suffix = count == MAX_SAME_LOG - 1 ? $" (后续同类消息已静默)" : "";
            LogBug(category, msg + suffix + (string.IsNullOrEmpty(stackTrace) ? "" : "\n" + stackTrace));
        }

        // 白名单：这些 warning 通常提示真实 bug
        private static readonly string[] _warningKeywords =
        {
            "has been destroyed", "missing", "null reference",
            "Shader", "Material", "sprite", "MissingReferenceException",
            "LookRotation", "Divide By Zero", "NaN"
        };

        private static bool IsInterestingWarning(string msg)
        {
            foreach (var k in _warningKeywords)
                if (msg.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // ── Shader / 材质 健康扫描 ────────────────────────────────────────────
        // 检测紫色 magenta 的两种典型成因：
        //   1. Image.material.shader == null
        //   2. Image.material.shader.name == "Hidden/InternalErrorShader"
        //   3. SpriteRenderer 同理
        private void ScanForShaderIssues()
        {
            // Image（包括所有 UI 图）
            var images = FindObjectsOfType<UnityEngine.UI.Image>(includeInactive: false);
            foreach (var img in images)
            {
                if (img == null || !img.enabled) continue;
                // ── 直接检测 "渲染出紫色"（最可靠、绕过 shader 检查） ──
                CheckMagentaColor(img);
                var mat = img.material;
                if (mat == null) continue;
                if (mat == img.defaultMaterial) continue;
                CheckMaterial(mat, GetPath(img.transform), "Image");
            }

            // RawImage（SpellShowcase / Showcase 卡面等常用）
            var rawImages = FindObjectsOfType<UnityEngine.UI.RawImage>(includeInactive: false);
            foreach (var ri in rawImages)
            {
                if (ri == null || !ri.enabled) continue;
                var mat = ri.material;
                if (mat == null) continue;
                if (mat == ri.defaultMaterial) continue;
                CheckMaterial(mat, GetPath(ri.transform), "RawImage");
                // RawImage 主贴图丢失也会渲染成紫色/白底
                if (ri.texture == null)
                    ReportRawImageTextureIssue(GetPath(ri.transform));
            }

            // SpriteRenderer
            var sprites = FindObjectsOfType<SpriteRenderer>(includeInactive: false);
            foreach (var sr in sprites)
            {
                if (sr == null || !sr.enabled) continue;
                CheckMaterial(sr.sharedMaterial, GetPath(sr.transform), "SpriteRenderer");
            }

            // MeshRenderer（3D 元素）
            var renderers = FindObjectsOfType<MeshRenderer>(includeInactive: false);
            foreach (var mr in renderers)
            {
                if (mr == null || !mr.enabled) continue;
                var mats = mr.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                    CheckMaterial(mats[i], GetPath(mr.transform) + $"[mat{i}]", "MeshRenderer");
            }

            // ── 活跃 tween 数快速膨胀检测（紫色尸体帧的间接信号） ─────────────
            // 正常情况下 tween 数应在 10~100 之间波动；>300 通常说明
            // 有组件死亡但 tween 未 Kill（正在访问已销毁 Image → 紫色）。
            // 阈值 400：spark burst + dissolve 双栈 ≤ 360 是正常峰值，400+ 才是真泄漏
            int activeTweens = SafeGetActiveTweens();
            if (activeTweens > 400)
            {
                ReportInvariantOnce("Tween激增", $"活跃 tween 数 {activeTweens}（可能有 target destroyed 泄漏）");
                // 在 tween 爆量时一次性 dump 整个 UI 树的嫌疑元素（每局最多一次）
                DumpSuspiciousUIOnce();
            }
        }

        private bool _uiDumpedThisGame = false;

        /// <summary>
        /// Dump 所有可疑 UI 元素到 logs/bot-ui-dump.txt —— 包括：
        /// - 每个 Image / RawImage 的路径、尺寸、color、material name、sprite name、texture name
        /// - 尤其关注 "full-rect 尺寸 + 非默认材质 + 无 sprite" 这类潜在紫色源
        /// 紫色问题靠颜色 / shader / 材质扫描抓不到时，用这个全量 dump 来人肉定位。
        /// </summary>
        private void DumpSuspiciousUIOnce()
        {
            if (_uiDumpedThisGame) return;
            _uiDumpedThisGame = true;
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "bot-ui-dump.txt");
                var sb = new StringBuilder();
                sb.AppendLine($"# Bot UI Dump @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# 第{_gamesPlayed+1}局 | 活跃 tween={SafeGetActiveTweens()} | GC={GC.GetTotalMemory(false)/1024/1024}MB");
                sb.AppendLine();

                sb.AppendLine("## Images（path | size | color | sprite | material | shader）");
                foreach (var img in FindObjectsOfType<UnityEngine.UI.Image>(includeInactive: false))
                {
                    if (img == null || !img.enabled) continue;
                    DumpGraphic(sb, img, "Image", img.sprite != null ? img.sprite.name : "null");
                }
                sb.AppendLine();
                sb.AppendLine("## RawImages");
                foreach (var ri in FindObjectsOfType<UnityEngine.UI.RawImage>(includeInactive: false))
                {
                    if (ri == null || !ri.enabled) continue;
                    DumpGraphic(sb, ri, "RawImage", ri.texture != null ? ri.texture.name : "null");
                }
                File.WriteAllText(path, sb.ToString());
                Debug.LogWarning($"[Bot] UI dump 已写到: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bot] Dump UI 失败: {e.Message}");
            }
        }

        private void DumpGraphic(StringBuilder sb, UnityEngine.UI.Graphic g, string kind, string src)
        {
            try
            {
                var rt = g.rectTransform;
                var c = g.color;
                string path = GetPath(g.transform);
                string matName = "null";
                string shaderName = "null";
                var m = g.material;
                if (m != null && m != g.defaultMaterial)
                {
                    matName = SafeMatName(m);
                    try { shaderName = m.shader != null ? m.shader.name : "null"; } catch { shaderName = "<destroyed>"; }
                }
                Vector2 sz = rt != null ? rt.rect.size : Vector2.zero;
                sb.AppendLine($"{kind} | {path} | size=({sz.x:F0}x{sz.y:F0}) | color=({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2}) | src={src} | mat={matName} | shader={shaderName}");
            }
            catch (Exception e) { sb.AppendLine($"{kind} | <error {e.Message}>"); }
        }

        private void ReportRawImageTextureIssue(string path)
        {
            string key = $"{path}|RawImage|texture=null";
            if (!_seenShaderIssues.Add(key)) return;
            LogBug("ShaderMissing", $"RawImage @ {path} → texture=null（会渲染成默认白/紫）");
        }

        // Image 当前渲染颜色接近 Unity magenta (1,0,1) → 大概率是 shader 丢失或
        // 自定义材质失败回退到 Hidden/InternalErrorShader。直接抓颜色最可靠，
        // 不依赖 shader 状态。
        private void CheckMagentaColor(UnityEngine.UI.Image img)
        {
            var c = img.color;
            // 同时满足：红高(>0.85) + 蓝高(>0.85) + 绿低(<0.15) + 不透明(>0.5) + size 足够大（>30px）
            if (c.r <= 0.85f || c.b <= 0.85f) return;
            if (c.g >= 0.15f) return;
            if (c.a < 0.5f) return;
            var rt = img.rectTransform;
            if (rt == null) return;
            Vector2 sz = rt.rect.size;
            if (sz.x < 30f || sz.y < 30f) return;

            string path = GetPath(img.transform);
            string key = $"{path}|Image|magenta-color";
            if (!_seenShaderIssues.Add(key)) return;
            LogBug("ShaderMissing",
                $"Image @ {path} 渲染色接近 magenta(r={c.r:F2} g={c.g:F2} b={c.b:F2} a={c.a:F2}) size={sz.x:F0}x{sz.y:F0}（可疑紫色块）");
        }

        private void CheckMaterial(Material mat, string path, string renderer)
        {
            if (mat == null) return;
            string issue = null;
            try
            {
                var shader = mat.shader;
                if (shader == null)
                    issue = "shader=null";
                else if (shader.name == "Hidden/InternalErrorShader")
                    issue = "shader=Hidden/InternalErrorShader (紫色 magenta)";
                else if (!shader.isSupported)
                    issue = $"shader 不被当前平台支持: {shader.name}";
                // mainTexture 检查只对非 UI 渲染器有意义：
                // Image / RawImage 通过 CanvasRenderer 传 [PerRendererData] _MainTex，
                // material.mainTexture 恒为 null，不代表真缺贴图
                else if (IsSuspectCustomShader(shader.name) && mat.mainTexture == null
                         && renderer != "Image" && renderer != "RawImage"
                         && !IsIgnorablePath(path))
                    issue = $"自定义 shader '{shader.name}' 缺 mainTexture（会渲染紫/白）";
            }
            catch (MissingReferenceException)
            {
                // material 的 Unity Object 已被 Destroy 但 Image 还持有引用 → 常见的紫色源
                issue = "material 已被销毁但仍被引用 (destroyed Material)";
            }

            if (issue == null) return;

            string key = $"{path}|{renderer}|{issue}";
            if (!_seenShaderIssues.Add(key)) return; // 去重

            LogBug("ShaderMissing", $"{renderer} '{SafeMatName(mat)}' @ {path} → {issue}");
        }

        // 已知项目内需要 mainTexture 的自定义 shader
        // 已知"初始化时材质 mainTexture 还没绑定但实际渲染正常"的路径白名单
        // 避免 bot 启动头两帧扫到 CountdownRing 的 Gem* 产生大量假阳性
        // 注：EnergyGemUI / EnergyGemGlowUI 的 _MainTex 声明为 [PerRendererData]，
        // UI.Image 通过 CanvasRenderer 传贴图，material.mainTexture 恒为 null — 非真实 bug
        private static bool IsIgnorablePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return path.Contains("CountdownRing/GemDimBase")
                || path.Contains("CountdownRing/GemGlow")
                || path.Contains("CountdownRing/GemSeg")
                || path.Contains("CountdownRing/Gem_");
        }

        private static bool IsSuspectCustomShader(string name)
        {
            return name == "UI/CardShine"
                || name == "FWTCG/UIDissolve"
                || name == "FWTCG/UIBlur"
                || name == "FWTCG/GlassPanel"
                || name == "FWTCG/EnergyGemUI"
                || name == "FWTCG/EnergyGemGlowUI";
        }

        private static string SafeMatName(Material m)
        {
            try { return m.name; } catch { return "<destroyed>"; }
        }

        private static string GetPath(Transform t)
        {
            if (t == null) return "<null>";
            var sb = new StringBuilder(t.name);
            var cur = t.parent;
            while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
            return sb.ToString();
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));

        // ── 报告记录 ──────────────────────────────────────────────────────────

        private void LogBug(string category, string detail)
        {
            _bugsFound++;
            var severity = GetSeverity(category);
            switch (severity)
            {
                case BugSeverity.High:   BugsHigh++;   break;
                case BugSeverity.Medium: BugsMedium++; break;
                case BugSeverity.Low:    BugsLow++;    break;
            }
            string entry = $"[{severity}][{category}] 第{_gamesPlayed+1}局 {detail}";
            _currentGame?.AddBug(entry);
            _report?.Bugs.Add(entry);
            Debug.LogWarning($"[Bot][Bug] {entry}");
            // High severity 才截图（省磁盘；紫色 shader 是 High，抓得到）
            string screenshot = (CaptureScreenshotOnBug && severity == BugSeverity.High)
                ? CaptureBugScreenshot(category) : null;
            AppendBugLog(category, detail, severity, screenshot);
        }

        // ── 策略解析 ──────────────────────────────────────────────────────────

        private BotStrategy ResolveStrategy()
        {
            if (Strategy != BotStrategy.Rotate) return Strategy;
            // Rotate: 按局轮换全部 6 种策略（含 Strategic 自对弈）
            BotStrategy[] pool =
            {
                BotStrategy.Greedy, BotStrategy.Aggro, BotStrategy.Random,
                BotStrategy.Suicidal, BotStrategy.RuneHoard, BotStrategy.Strategic
            };
            return pool[_gamesPlayed % pool.Length];
        }

        // ── 截图 ──────────────────────────────────────────────────────────────

        private string CaptureBugScreenshot(string category)
        {
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "logs", "screenshots");
                Directory.CreateDirectory(dir);
                string safeCategory = string.Join("_", category.Split(Path.GetInvalidFileNameChars()));
                string filename = $"bug-{_bugsFound:D4}-{safeCategory}-{DateTime.Now:HHmmss}.png";
                string path = Path.Combine(dir, filename);
                ScreenCapture.CaptureScreenshot(path);
                // ScreenCapture 异步写入，返回相对路径让用户知道去哪找
                return $"logs/screenshots/{filename}";
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bot] 截图失败: {e.Message}");
                return null;
            }
        }

        // ── 泄漏基线采样 / 校验 ───────────────────────────────────────────────

        private void SampleLeakBaseline()
        {
            _gameStartTweens    = SafeGetActiveTweens();
            // 强制回收两轮，拿到真实驻留内存（避免把瞬时未回收对象算成泄漏）
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            _gameStartGC        = GC.GetTotalMemory(false);
            _gameStartParticles = FindObjectsOfType<ParticleSystem>().Length;
        }

        private void CheckLeakDelta()
        {
            int tweensNow = SafeGetActiveTweens();
            int tweenDelta = tweensNow - _gameStartTweens;
            if (tweenDelta > TweenLeakThreshold)
                LogBug("Tween泄漏",
                    $"一局后活跃 tween 净增 {tweenDelta}（起始 {_gameStartTweens} → 结束 {tweensNow}），阈值 {TweenLeakThreshold}");

            // 同样强制回收后再读，真实泄漏才会残留
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long gcNow = GC.GetTotalMemory(false);
            float gcDeltaMB = (gcNow - _gameStartGC) / 1024f / 1024f;
            if (gcDeltaMB > GcLeakThresholdMB)
                LogBug("GC泄漏",
                    $"一局 GC 净增 {gcDeltaMB:F1}MB（起始 {_gameStartGC/1024/1024}MB → 结束 {gcNow/1024/1024}MB），阈值 {GcLeakThresholdMB}MB");

            int particlesNow = FindObjectsOfType<ParticleSystem>().Length;
            int particleDelta = particlesNow - _gameStartParticles;
            if (particleDelta > 50) // 粒子系统阈值硬编码
                LogBug("粒子泄漏",
                    $"一局后 ParticleSystem 净增 {particleDelta}（起始 {_gameStartParticles} → 结束 {particlesNow}）");
        }

        private static int SafeGetActiveTweens()
        {
            try { return DOTween.TotalActiveTweens(); }
            catch { return 0; }
        }

        // ── FPS 监控 ──────────────────────────────────────────────────────────

        private void CheckFps()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f) return;
            float fps = 1f / dt;
            // 平滑 FPS（供面板显示）：EMA
            LastFps = LastFps <= 0f ? fps : (LastFps * 0.9f + fps * 0.1f);

            // 用 unscaled FPS 判断（SpeedMultiplier 下 Time.deltaTime 会被缩放，误判）
            if (fps < FpsWarnThreshold)
            {
                _lowFpsFrames++;
                // 连续 10 帧 + 距离上一次报告 >5s，才报（避免一次掉帧刷屏）
                if (_lowFpsFrames >= 10 &&
                    Time.realtimeSinceStartup - _lastLowFpsReportTime > 5f)
                {
                    _lastLowFpsReportTime = Time.realtimeSinceStartup;
                    LogBug("性能",
                        $"连续 {_lowFpsFrames} 帧低于 {FpsWarnThreshold}fps，当前 {fps:F1}fps");
                }
            }
            else
            {
                _lowFpsFrames = 0;
            }
        }

        // ── 实时 bug 日志文件（markdown） ─────────────────────────────────────

        private void InitBugLogFile()
        {
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "logs");
                Directory.CreateDirectory(dir);
                _bugLogPath = Path.Combine(dir, "bot-bugs.md");
                string header =
                    $"# Bot 实时 Bug 日志\n\n" +
                    $"- 启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                    $"- 速度倍数: {SpeedMultiplier}x\n" +
                    $"- 目标局数: {(TargetGames == 0 ? "无限" : TargetGames.ToString())}\n\n" +
                    $"---\n\n";
                File.WriteAllText(_bugLogPath, header);
                Debug.Log($"[Bot] Bug 日志已初始化: {_bugLogPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bot] 初始化 bug 日志失败: {e.Message}");
                _bugLogPath = null;
            }
        }

        private void AppendBugLog(string category, string detail, BugSeverity severity, string screenshotPath = null)
        {
            if (string.IsNullOrEmpty(_bugLogPath)) return;
            try
            {
                var gs = GameManager.Instance?.GetState();
                string phase = gs != null ? $"Phase={gs.Phase} Round={gs.Round} Turn={gs.Turn}" : "Phase=?";
                string sevIcon = severity == BugSeverity.High   ? "🔴" :
                                 severity == BugSeverity.Medium ? "🟡" : "🔵";
                var sb = new StringBuilder();
                sb.AppendLine($"## {sevIcon} [{severity}][{category}] 第{_gamesPlayed+1}局 ({DateTime.Now:HH:mm:ss})");
                sb.AppendLine();
                sb.AppendLine($"- 状态: {phase}");
                sb.AppendLine($"- 策略: {_activeStrategy}");
                sb.AppendLine($"- Seed: {_actualSeed}");
                if (!string.IsNullOrEmpty(screenshotPath))
                    sb.AppendLine($"- 截图: `{screenshotPath}`");
                sb.AppendLine($"- 详情:");
                sb.AppendLine();
                sb.AppendLine("```");
                sb.AppendLine(detail);
                sb.AppendLine("```");
                sb.AppendLine();
                File.AppendAllText(_bugLogPath, sb.ToString());
            }
            catch { /* 日志写失败不能影响主流程 */ }
        }

        // ── 回放日志（jsonl） ────────────────────────────────────────────────

        private void InitReplayLogFile()
        {
            if (!RecordReplay) { _replayLogPath = null; return; }
            try
            {
                string dir = Path.Combine(Application.dataPath, "..", "logs");
                Directory.CreateDirectory(dir);
                _replayLogPath = Path.Combine(dir, "bot-replay.jsonl");
                // 第一行 = header：seed / 时间 / 版本
                var header = new StringBuilder();
                header.Append('{');
                header.Append($"\"type\":\"header\",\"seed\":{_actualSeed},");
                header.Append($"\"speed\":{SpeedMultiplier},");
                header.Append($"\"started\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
                header.Append("}\n");
                File.WriteAllText(_replayLogPath, header.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bot] 初始化回放日志失败: {e.Message}");
                _replayLogPath = null;
            }
        }

        // 公开方法：主循环里调用。每一次对 GameManager 的交互前记一行。
        private void RecordReplayAction(string action, string paramsJson = "")
        {
            if (string.IsNullOrEmpty(_replayLogPath)) return;
            try
            {
                var gs = GameManager.Instance?.GetState();
                string phase = gs != null ? gs.Phase : "?";
                int round = gs != null ? gs.Round : -1;
                var sb = new StringBuilder();
                sb.Append('{');
                sb.Append($"\"type\":\"action\",\"game\":{_gamesPlayed+1},");
                sb.Append($"\"frame\":{Time.frameCount},");
                sb.Append($"\"phase\":\"{phase}\",\"round\":{round},");
                sb.Append($"\"action\":\"{action}\"");
                if (!string.IsNullOrEmpty(paramsJson))
                    sb.Append($",\"params\":{paramsJson}");
                sb.Append("}\n");
                File.AppendAllText(_replayLogPath, sb.ToString());
            }
            catch { }
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private void RecordTiming(string label)
        {
            _currentGame?.RecordTiming(label, Time.time);
        }

        private bool IsGameOver()
        {
            var gs = GameManager.Instance?.GetState();
            return gs == null || gs.GameOver;
        }

        private IEnumerator RestartGame()
        {
            yield return new WaitForSeconds(0.3f);  // 等 GameOver UI 短暂显示
            var gm = GameManager.Instance;
            if (gm == null) yield break;
            gm.RestartGameInPlace();
            // 等启动流程重新开始（硬币动画 + 换牌，由 BotAutoAdvance 自动推进）
            yield return new WaitForSeconds(0.5f);
        }

        private void SaveReport()
        {
            if (_report == null) return;
            try
            {
                string dir  = Path.Combine(Application.dataPath, "..", "logs");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "bot-report.txt");
                File.WriteAllText(path, _report.Build(_gamesPlayed));
                Debug.Log($"[Bot] 报告已保存到: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bot] 保存报告失败: {e.Message}");
            }
        }
    }

    // ── 报告数据结构 ──────────────────────────────────────────────────────────

    public class BotReport
    {
        public List<string>     Bugs  = new List<string>();
        public List<GameRecord> Games = new List<GameRecord>();

        public string Build(int totalGames)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FWTCG GameBot 测试报告 ===");
            sb.AppendLine($"生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"总局数: {totalGames}");
            sb.AppendLine();

            // 胜负统计
            int pWins = Games.Count(g => g.Winner == "player");
            int eWins = Games.Count(g => g.Winner == "enemy");
            int draws = Games.Count(g => g.Winner == "draw");
            sb.AppendLine($"── 胜负 ──────────────────────────────");
            sb.AppendLine($"玩家胜: {pWins}  AI胜: {eWins}  平局: {draws}");
            sb.AppendLine();

            // 回合时长
            var turnTimes = Games.SelectMany(g => g.TurnTimes).ToList();
            if (turnTimes.Count > 0)
            {
                sb.AppendLine($"── 回合时长 ───────────────────────────");
                sb.AppendLine($"平均: {turnTimes.Average():F2}s  最长: {turnTimes.Max():F2}s  最短: {turnTimes.Min():F2}s");
                var slow = turnTimes.Where(t => t > 10f).ToList();
                if (slow.Count > 0)
                    sb.AppendLine($"⚠️ 超过 10s 的回合数: {slow.Count}（可能卡顿或逻辑卡住）");
                sb.AppendLine();
            }

            // Bug 列表（按严重度分组：High → Medium → Low）
            sb.AppendLine($"── Bug / 异常 ({Bugs.Count}) ─────────────────");
            if (Bugs.Count == 0)
            {
                sb.AppendLine("✅ 未发现问题");
            }
            else
            {
                int high   = Bugs.Count(b => b.StartsWith("[High]"));
                int medium = Bugs.Count(b => b.StartsWith("[Medium]"));
                int low    = Bugs.Count(b => b.StartsWith("[Low]"));
                sb.AppendLine($"🔴 High: {high}   🟡 Medium: {medium}   🔵 Low: {low}");
                sb.AppendLine();
                foreach (var prefix in new[] { "[High]", "[Medium]", "[Low]" })
                    foreach (var b in Bugs)
                        if (b.StartsWith(prefix)) sb.AppendLine("  " + b);
            }
            sb.AppendLine();

            // 各局详情
            sb.AppendLine($"── 各局详情 ───────────────────────────");
            foreach (var g in Games)
            {
                sb.AppendLine($"第{g.GameIndex}局  {g.Rounds}回合  玩家{g.FinalPScore}:{g.FinalEScore}AI  胜:{g.Winner}  Bug:{g.Bugs.Count}");
                foreach (var bug in g.Bugs)
                    sb.AppendLine($"    ⚠ {bug}");
            }

            return sb.ToString();
        }
    }

    public class GameRecord
    {
        public int    GameIndex;
        public int    Rounds;
        public int    FinalPScore;
        public int    FinalEScore;
        public string Winner;
        public List<string> Bugs      = new List<string>();
        public List<string> ActionLog = new List<string>();
        public List<float>  TurnTimes = new List<float>();

        private Dictionary<string, float> _timingMarks = new Dictionary<string, float>();

        public GameRecord(int index) { GameIndex = index; }

        public void Log(string msg)
        {
            ActionLog.Add($"[{Time.time:F1}s] {msg}");
        }

        public void AddBug(string bug)
        {
            Bugs.Add(bug);
        }

        public void RecordTiming(string label, float time)
        {
            _timingMarks[label] = time;
        }

        public void RecordTurnTime(int round, float duration)
        {
            TurnTimes.Add(duration);
        }

        public void Finish(GameState gs)
        {
            Rounds      = gs.Round;
            FinalPScore = gs.PScore;
            FinalEScore = gs.EScore;
            Winner      = gs.PScore > gs.EScore ? "player"
                        : gs.EScore > gs.PScore ? "enemy"
                        : "draw";
        }
    }
}
