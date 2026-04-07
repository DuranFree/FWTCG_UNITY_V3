using UnityEditor;
using UnityEngine;
using FWTCG.Bot;
using System.IO;

namespace FWTCG.Editor
{
    /// <summary>
    /// GameBot 控制面板 — FWTCG > Bot Tester 菜单打开。
    ///
    /// 功能：
    ///   - 一键启动 / 停止 Bot
    ///   - 实时显示运行状态（局数、发现的 Bug 数）
    ///   - 配置局数、操作延迟
    ///   - 查看最新报告
    /// </summary>
    public class GameBotWindow : EditorWindow
    {
        [MenuItem("FWTCG/Bot Tester")]
        public static void Open()
        {
            var win = GetWindow<GameBotWindow>("Bot Tester");
            win.minSize = new Vector2(320, 400);
        }

        // 配置
        private int   _targetGames    = 10;
        private float _actionDelay    = 0.15f;
        private float _dialogDelay    = 0.1f;
        private float _stuckTimeout   = 30f;
        private bool  _alwaysConfirm  = true;
        private float _speedMultiplier = 1f;

        private Vector2 _scroll;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("FWTCG GameBot Tester", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // 获取当前 Bot 实例
            GameBot bot = FindBotInScene();
            bool inPlayMode = Application.isPlaying;

            if (!inPlayMode)
            {
                EditorGUILayout.HelpBox("请先进入 Play Mode 再启动 Bot。", MessageType.Info);
                DrawConfig();
                return;
            }

            // 运行状态
            if (bot != null && bot.Running)
            {
                DrawRunningState(bot);
            }
            else
            {
                DrawConfig();
                EditorGUILayout.Space(8);

                EditorGUI.BeginDisabledGroup(bot == null && FindGameManager() == null);
                if (GUILayout.Button("▶ 启动 Bot", GUILayout.Height(36)))
                    StartBot();
                EditorGUI.EndDisabledGroup();

                if (bot == null && FindGameManager() == null)
                    EditorGUILayout.HelpBox("场景中未找到 GameManager，请先运行游戏场景。", MessageType.Warning);
            }

            EditorGUILayout.Space(8);

            // 报告查看
            DrawReportSection();
        }

        private void DrawConfig()
        {
            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope(1))
            {
                _targetGames    = EditorGUILayout.IntField("目标局数（0=无限）", _targetGames);
                _speedMultiplier = EditorGUILayout.Slider("速度倍数", _speedMultiplier, 1f, 20f);
                _actionDelay    = EditorGUILayout.Slider("操作间隔（秒）", _actionDelay, 0.05f, 2f);
                _dialogDelay    = EditorGUILayout.Slider("弹窗等待（秒）", _dialogDelay, 0.05f, 1f);
                _stuckTimeout   = EditorGUILayout.FloatField("卡死超时（秒）", _stuckTimeout);
                _alwaysConfirm  = EditorGUILayout.Toggle("弹窗总是确认", _alwaysConfirm);

                if (_speedMultiplier > 1f)
                    EditorGUILayout.HelpBox($"速度 {_speedMultiplier}x：AI 延迟跳过，动画加速，视觉效果不可用于手感判断。", MessageType.Info);
            }
        }

        private void DrawRunningState(GameBot bot)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("🟢 Bot 运行中", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"已完成局数:  {bot.GamesPlayed}");
                EditorGUILayout.LabelField($"发现问题数:  {bot.BugsFound}");
            }

            EditorGUILayout.Space(6);
            if (GUILayout.Button("■ 停止 Bot", GUILayout.Height(32)))
            {
                bot.StopBot();
            }

            // 刷新 Editor 以实时更新数字
            Repaint();
        }

        private void DrawReportSection()
        {
            EditorGUILayout.LabelField("最新报告", EditorStyles.boldLabel);
            string reportPath = Path.Combine(Application.dataPath, "..", "logs", "bot-report.txt");

            if (!File.Exists(reportPath))
            {
                EditorGUILayout.LabelField("（尚无报告）", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField(reportPath, EditorStyles.miniLabel);
            if (GUILayout.Button("在系统编辑器中打开报告"))
            {
                System.Diagnostics.Process.Start(Path.GetFullPath(reportPath));
            }

            EditorGUILayout.Space(4);
            string content = File.ReadAllText(reportPath);
            int lineCount = content.Split('\n').Length;
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(Mathf.Min(lineCount * 14 + 20, 200)));
            EditorGUILayout.TextArea(content, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ── 操作 ────────────────────────────────────────────────────────────

        private void StartBot()
        {
            // 先找已有的 Bot 实例
            GameBot bot = FindBotInScene();

            if (bot == null)
            {
                // 挂到 GameManager GameObject 上
                var gmObj = FindGameManager();
                if (gmObj == null)
                {
                    Debug.LogError("[BotWindow] 未找到 GameManager，无法启动 Bot。");
                    return;
                }
                bot = gmObj.AddComponent<GameBot>();
            }

            // 写入配置
            bot.TargetGames          = _targetGames;
            bot.ActionDelay          = _actionDelay;
            bot.DialogDelay          = _dialogDelay;
            bot.StuckTimeout         = _stuckTimeout;
            bot.AlwaysConfirmDialogs = _alwaysConfirm;
            bot.SpeedMultiplier      = _speedMultiplier;

            bot.StartBot();
        }

        private static GameBot FindBotInScene()
        {
            return FindObjectOfType<GameBot>();
        }

        private static GameObject FindGameManager()
        {
            var gm = FindObjectOfType<GameManager>();
            return gm != null ? gm.gameObject : null;
        }

        private void OnInspectorUpdate()
        {
            // 每秒刷新 4 次，让运行状态数字更新
            Repaint();
        }
    }
}
