using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.Bot
{
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

            // 应用速度倍数
            _savedTimeScale = Time.timeScale;
            Time.timeScale  = SpeedMultiplier;
            AI.SimpleAI.SkipDelays = SpeedMultiplier > 1f;
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

                    yield return PlayOneGame(gm);
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

            // 1. 横置所有符文获取法力
            bool hasUntappedRunes = gs.PRunes.Any(r => !r.Tapped);
            if (hasUntappedRunes)
            {
                _currentGame?.Log($"[操作] 横置所有符文（{gs.PRunes.Count(r=>!r.Tapped)} 张）");
                gm.OnTapAllRunesClicked();
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break; // 每帧只做一个主操作
            }

            // 2. 出牌（手牌 → 基地）
            var playable = gs.PHand
                .Where(u => !u.CardData.IsSpell && CanAfford(gs, u))
                .OrderByDescending(u => u.CardData.Cost)   // 贵的先打，尽量花完法力
                .ToList();

            if (playable.Count > 0)
            {
                var unit = playable[0];
                _currentGame?.Log($"[操作] 打出手牌 {unit.UnitName}（费用:{unit.CardData.Cost} 法力:{gs.PMana}）");
                RecordTiming("play_card_start");
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
                gm.OnSpellDraggedOut(spell);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 2c. 出英雄
            if (gs.PHero != null && CanAffordHero(gs, gs.PHero))
            {
                _currentGame?.Log($"[操作] 出英雄 {gs.PHero.UnitName}");
                gm.OnDragHeroToBase(gs.PHero);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 3. 基地单位上战场
            var baseUnits = gs.PBase.Where(u => !u.Exhausted).ToList();
            if (baseUnits.Count > 0)
            {
                int bfId = PickBestBattlefield(gs, baseUnits);
                _currentGame?.Log($"[操作] 派遣 {baseUnits.Count} 个单位到战场 {bfId}");
                RecordTiming("move_to_bf_start");
                gm.OnDragUnitsToBF(baseUnits, bfId);
                yield return new WaitForSeconds(ActionDelay);
                _lastProgressTime = Time.time;
                yield break;
            }

            // 4. 没有其他操作 → 结束回合
            float turnDuration = Time.realtimeSinceStartup - turnStart;
            _currentGame?.Log($"[操作] 结束回合（本回合耗时 {turnDuration:F2}s，操作数：{_actionCount}）");
            _currentGame?.RecordTurnTime(gs.Round, turnDuration);
            gm.OnEndTurnClicked();
            yield return new WaitForSeconds(ActionDelay);
            _lastProgressTime = Time.time;
        }

        // ── 决策辅助 ──────────────────────────────────────────────────────────

        private bool CanAfford(GameState gs, UnitInstance unit)
        {
            if (gs.PMana < unit.CardData.Cost) return false;
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
            // 选玩家单位最多的战场（已有优势继续扩大）
            // 若平局，选玩家 vs 敌方差值最大的
            int best = 0;
            int bestScore = int.MinValue;
            for (int i = 0; i < GameRules.BATTLEFIELD_COUNT; i++)
            {
                int score = gs.BF[i].PlayerUnits.Count - gs.BF[i].EnemyUnits.Count;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        // ── 状态验证（每个行动前）────────────────────────────────────────────

        private void ValidateState(GameState gs)
        {
            // 法力不能为负
            if (gs.PMana < 0)
                LogBug("非法状态", $"玩家法力为负: {gs.PMana}");
            if (gs.EMana < 0)
                LogBug("非法状态", $"AI 法力为负: {gs.EMana}");

            // 分数不能为负
            if (gs.PScore < 0 || gs.EScore < 0)
                LogBug("非法状态", $"分数为负: 玩家={gs.PScore} AI={gs.EScore}");

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
            if (type == LogType.Exception || type == LogType.Error)
            {
                // 过滤已知的非 bug 日志
                if (msg.Contains("[AutoSave]") || msg.Contains("[Bot]")) return;
                LogBug(type == LogType.Exception ? "Exception" : "Error", msg + "\n" + stackTrace);
            }
        }

        // ── 报告记录 ──────────────────────────────────────────────────────────

        private void LogBug(string category, string detail)
        {
            _bugsFound++;
            string entry = $"[{category}] 第{_gamesPlayed+1}局 {detail}";
            _currentGame?.AddBug(entry);
            _report?.Bugs.Add(entry);
            Debug.LogWarning($"[Bot][Bug] {entry}");
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

            // Bug 列表
            sb.AppendLine($"── Bug / 异常 ({Bugs.Count}) ─────────────────");
            if (Bugs.Count == 0)
            {
                sb.AppendLine("✅ 未发现问题");
            }
            else
            {
                foreach (var b in Bugs)
                    sb.AppendLine("  " + b);
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
