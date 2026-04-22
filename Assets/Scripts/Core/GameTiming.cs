using System.Threading.Tasks;
using UnityEngine;

namespace FWTCG.Core
{
    /// <summary>
    /// 集中管理异步 Task.Delay 时长。
    ///
    /// 问题：Unity 的 Time.timeScale 只影响 Time.deltaTime 和 WaitForSeconds / DOTween 默认
    ///       tween，但 System.Threading.Tasks.Task.Delay 用的是 wall-clock 时间，
    ///       timeScale 对其无效。
    ///
    /// 方案：所有游戏侧（非系统/日志）的 Task.Delay 统一走 GameTiming.Delay。
    ///       GameBot 启动 N 倍速时把 SpeedMultiplier 同步到这里，全部 await 自动按倍数缩短。
    ///
    /// 用法：
    ///   await GameTiming.Delay(650);                 // 650ms，bot 10x 下变 65ms
    ///   await GameTiming.Delay(GameRules.PHASE_DELAY_MS);
    /// </summary>
    public static class GameTiming
    {
        /// <summary>全局速度倍数（1=正常，10=10x）。GameBot 启动时写入。</summary>
        public static float SpeedMultiplier = 1f;

        /// <summary>true 时所有 delay 直接跳过（给 bot / CI 用，比倍速更激进）。</summary>
        public static bool SkipAll = false;

        /// <summary>
        /// 按倍数缩短后返回 Task.Delay。最低 1ms 防止 0 延迟 burst。
        /// </summary>
        public static Task Delay(int ms)
        {
            if (SkipAll || ms <= 0) return Task.CompletedTask;
            float mul = SpeedMultiplier <= 0f ? 1f : SpeedMultiplier;
            int scaled = Mathf.Max(1, Mathf.RoundToInt(ms / mul));
            return Task.Delay(scaled);
        }

        /// <summary>重置到正常速度（StopBot 时调用）。</summary>
        public static void Reset()
        {
            SpeedMultiplier = 1f;
            SkipAll = false;
        }
    }
}
