using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FWTCG.Bot;

namespace FWTCG.Editor
{
    /// <summary>
    /// GameBot 无头运行入口 —— 命令行 batchmode 可调用。
    ///
    /// 用法：
    ///   Unity -batchmode -projectPath . -executeMethod FWTCG.Editor.GameBotHeadless.Run -quit
    ///         [-botGames N] [-botSpeed M] [-botSeed S] [-botStrategy Strategy]
    ///
    /// 例：跑 50 局，10x 速度，固定种子 42，Rotate 策略
    ///   Unity -batchmode -projectPath . -executeMethod FWTCG.Editor.GameBotHeadless.Run \
    ///         -botGames 50 -botSpeed 10 -botSeed 42 -botStrategy Rotate -quit
    ///
    /// 产出：
    ///   - logs/bot-bugs.md
    ///   - logs/bot-report.txt
    ///   - logs/bot-replay.jsonl
    ///   - logs/screenshots/*.png（High 级 bug）
    /// </summary>
    public static class GameBotHeadless
    {
        /// <summary>命令行入口。读取 -bot* 参数，进入 Play Mode，跑 bot，跑完自动 quit。</summary>
        public static void Run()
        {
            int         games    = ParseIntArg("-botGames",    10);
            float       speed    = ParseFloatArg("-botSpeed",   10f);
            int         seed     = ParseIntArg("-botSeed",      0);
            string      stratStr = ParseStringArg("-botStrategy", "Rotate");
            BotStrategy strategy = ParseStrategy(stratStr);

            Debug.Log($"[GameBotHeadless] 启动 — 局数:{games} 速度:{speed}x 种子:{seed} 策略:{strategy}");

            // 确保游戏场景加载
            var scenePath = "Assets/Scenes/GameScene.unity";
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[GameBotHeadless] 场景不存在: {scenePath}");
                EditorApplication.Exit(2);
                return;
            }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 注册 Play Mode 状态回调：进入 Play 后挂 bot，跑完退出
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _pendingGames = games;
            _pendingSpeed = speed;
            _pendingSeed  = seed;
            _pendingStrat = strategy;

            EditorApplication.EnterPlaymode();
        }

        // ── 挂接到 Play Mode 事件 ────────────────────────────────────────────

        private static int         _pendingGames;
        private static float       _pendingSpeed;
        private static int         _pendingSeed;
        private static BotStrategy _pendingStrat;

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            // 等一帧让 GameManager 初始化
            EditorApplication.delayCall += AttachBot;
        }

        private static void AttachBot()
        {
            var gm = UnityEngine.Object.FindObjectOfType<FWTCG.GameManager>();
            if (gm == null)
            {
                Debug.LogError("[GameBotHeadless] 场景中没有 GameManager");
                EditorApplication.Exit(3);
                return;
            }

            var bot = gm.gameObject.AddComponent<GameBot>();
            bot.TargetGames      = _pendingGames;
            bot.SpeedMultiplier  = _pendingSpeed;
            bot.RandomSeed       = _pendingSeed;
            bot.Strategy         = _pendingStrat;
            bot.AutoStart        = false;
            bot.StartBot();

            // 轮询：bot 跑完或超时就退 Play + 退 Editor
            EditorApplication.update += () => PollUntilDone(bot);
        }

        private static double _startTime = -1;
        private static void PollUntilDone(GameBot bot)
        {
            if (_startTime < 0) _startTime = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - _startTime;

            // 超时：每局按最多 60s 估算，加 30s 缓冲
            double timeoutSec = bot.TargetGames * 60.0 + 30.0;
            bool done = !bot.Running;
            bool timedOut = elapsed > timeoutSec;

            if (!done && !timedOut) return;

            if (timedOut)
                Debug.LogError($"[GameBotHeadless] 超时（{timeoutSec}s），强制退出。已跑 {bot.GamesPlayed} 局。");
            else
                Debug.Log($"[GameBotHeadless] 完成 — {bot.GamesPlayed} 局，发现 {bot.BugsFound} bug（🔴{bot.BugsHigh} 🟡{bot.BugsMedium} 🔵{bot.BugsLow}）");

            // 退 Play Mode
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            // batchmode 下整体 quit；非 batchmode 保留 editor
            if (Application.isBatchMode)
            {
                int exitCode = bot.BugsHigh > 0 ? 1 : 0; // High 级 bug 用非 0 退出码，CI 可抓
                EditorApplication.Exit(exitCode);
            }
        }

        // ── 命令行参数解析 ────────────────────────────────────────────────────

        private static string ParseStringArg(string name, string def)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return def;
        }

        private static int ParseIntArg(string name, int def)
            => int.TryParse(ParseStringArg(name, ""), out var v) ? v : def;

        private static float ParseFloatArg(string name, float def)
            => float.TryParse(ParseStringArg(name, ""), out var v) ? v : def;

        private static BotStrategy ParseStrategy(string s)
            => Enum.TryParse<BotStrategy>(s, true, out var v) ? v : BotStrategy.Rotate;
    }
}
