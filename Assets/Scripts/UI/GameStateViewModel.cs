using System;
using FWTCG.Core;
using FWTCG.Systems;

namespace FWTCG.UI
{
    /// <summary>
    /// DEV-32 A2: UI ViewModel 骨架。
    ///
    /// **目的**：统一 UI 层对 GameState / ScoreManager / TurnManager 事件的订阅入口，
    /// 为逐步替换 RefreshUI() 整体重建铺路。
    ///
    /// **当前状态**：骨架 + mana 绑定示例（已由 A1 GameState.OnManaChanged 驱动）。
    /// 其他状态（score / phase / turn）继续走原事件路径；后续迁移可逐条加绑定。
    ///
    /// **使用方式**：
    ///   - 构造：new GameStateViewModel(gs)，GameManager.InitGame 中创建并持有
    ///   - UI 订阅：vm.OnPlayerManaChanged += (newVal) => label.text = newVal.ToString();
    ///   - Dispose：游戏结束 / 场景切换时 vm.Dispose() 解绑订阅
    ///
    /// **契约**：ViewModel 是 **纯代理层**，不缓存状态副本（查值用 gs 属性）；
    /// 目的是避免 dual-source-of-truth，减少不一致可能。
    /// </summary>
    public class GameStateViewModel : IDisposable
    {
        private GameState _gs;
        private bool _disposed;

        // ── 事件 — 按 owner 区分分发（UI 不必自己判 owner） ──────────────────
        public event Action<int> OnPlayerManaChanged;   // newValue
        public event Action<int> OnEnemyManaChanged;    // newValue
        public event Action<int> OnPlayerScoreChanged;  // newValue
        public event Action<int> OnEnemyScoreChanged;   // newValue

        public GameStateViewModel(GameState gs)
        {
            _gs = gs ?? throw new ArgumentNullException(nameof(gs));
            _gs.OnManaChanged  += HandleManaChanged;
            ScoreManager.OnScoreAdded += HandleScoreAdded;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_gs != null) _gs.OnManaChanged  -= HandleManaChanged;
            ScoreManager.OnScoreAdded -= HandleScoreAdded;
            _gs = null;
        }

        private void HandleManaChanged(string owner, int oldVal, int newVal)
        {
            if (owner == GameRules.OWNER_PLAYER) OnPlayerManaChanged?.Invoke(newVal);
            else                                  OnEnemyManaChanged?.Invoke(newVal);
        }

        private void HandleScoreAdded(string owner, int newScore)
        {
            if (owner == GameRules.OWNER_PLAYER) OnPlayerScoreChanged?.Invoke(newScore);
            else                                  OnEnemyScoreChanged?.Invoke(newScore);
        }

        // ── 同步查询（UI 需要时） ────────────────────────────────────────────
        public int PlayerMana  => _gs?.PMana  ?? 0;
        public int EnemyMana   => _gs?.EMana  ?? 0;
        public int PlayerScore => _gs?.PScore ?? 0;
        public int EnemyScore  => _gs?.EScore ?? 0;
        public string CurrentPhase => _gs?.Phase ?? "";
        public bool GameOver   => _gs?.GameOver ?? false;
    }
}
