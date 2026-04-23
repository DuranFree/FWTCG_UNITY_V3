using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FWTCG.Core;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-32 A4: 反应窗口抽象。
    /// <see cref="ReactiveWindowUI"/> 是生产环境 MonoBehaviour 实现；
    /// 测试可提供 mock 直接返回预设 card，无需实例化 UI。
    /// </summary>
    public interface IReactionWindow
    {
        /// <summary>
        /// 等待玩家从 cards 中选一张反应牌；返回所选，或 null（跳过/取消）。
        /// </summary>
        Task<UnitInstance> WaitForReaction(
            List<UnitInstance> cards,
            string contextMsg,
            GameState gs,
            Action<UnitInstance> onHoverEnter = null,
            Action<UnitInstance> onHoverExit = null);

        /// <summary>跳过反应（外部调用；内部实现应 TrySetResult(null)）。</summary>
        void SkipReaction();
    }
}
