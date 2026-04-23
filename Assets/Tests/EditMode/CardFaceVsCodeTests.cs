using NUnit.Framework;
using UnityEngine;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.Systems;

namespace FWTCG.Tests
{
    /// <summary>
    /// 对账修复测试（2026-04-23 card-vs-code-diff）：
    ///   D-1/D-2/D-4 balance_resolve 费用-3 / dormant 符文 / 仅 toWin≤3 触发
    ///   D-7        trifarian_warcamp & back_alley_bar 对 AI 单位也触发
    ///   D-10       增益指示物只加战力，不加 HP（消耗时对称）
    ///   D-11       wailing_poro 基地孤独阵亡也触发绝念
    /// </summary>
    [TestFixture]
    public class CardFaceVsCodeTests
    {
        private GameState _gs;
        private BattlefieldSystem _bfSys;
        private DeathwishSystem _dwSys;
        private SpellSystem _spellSys;

        private CardData MakeCard(string id, string effectId,
            int cost = 1, int atk = 2, RuneType rune = RuneType.Verdant, int runeCost = 0,
            CardKeyword kw = CardKeyword.None, bool isSpell = false, string name = null)
        {
            var cd = ScriptableObject.CreateInstance<CardData>();
            cd.EditorSetup(id, name ?? id, cost, atk, rune, runeCost, "", kw, effectId,
                           isEquipment: false, equipAtkBonus: 0, equipRuneType: RuneType.Blazing,
                           equipRuneCost: 0, isSpell: isSpell);
            return cd;
        }

        private UnitInstance MakeUnit(CardData cd, string owner = GameRules.OWNER_PLAYER)
            => _gs.MakeUnit(cd, owner);

        private static int _runeUid = 9000;
        private RuneInstance MakeRune(RuneType type = RuneType.Verdant)
            => new RuneInstance(_runeUid++, type);

        [SetUp]
        public void SetUp()
        {
            GameState.ResetUidCounter();
            _gs = new GameState();
            _gs.Round = 2;
            _gs.Turn = GameRules.OWNER_PLAYER;
            _gs.Phase = GameRules.PHASE_ACTION;
            _gs.SetMana(GameRules.OWNER_PLAYER, 10);
            _gs.SetMana(GameRules.OWNER_ENEMY, 10);
            for (int i = 0; i < _gs.BFNames.Length; i++) _gs.BFNames[i] = "none";

            _bfSys = new GameObject("BFSys").AddComponent<BattlefieldSystem>();
            _dwSys = new GameObject("DW").AddComponent<DeathwishSystem>();
            _spellSys = new GameObject("Spell").AddComponent<SpellSystem>();
        }

        // ── D-1/D-4: balance_resolve 费用减免 ─────────────────────────────────

        [Test]
        public void BalanceResolve_ReducesCostBy3_WhenOpponentNearWin()
        {
            var spell = MakeUnit(MakeCard("balance_resolve", "balance_resolve", cost: 3, isSpell: true));
            // opponent score makes toWin = WIN_SCORE - oppScore ≤ 3
            int oppScoreNearWin = GameRules.WIN_SCORE - 3;
            _gs.EScore = (oppScoreNearWin);

            int eff = GameRules.GetSpellEffectiveCost(spell, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(0, eff, "对手离胜≤3 时 balance_resolve 费用 3-3=0");
        }

        [Test]
        public void BalanceResolve_NoReduction_WhenOpponentFarFromWin()
        {
            var spell = MakeUnit(MakeCard("balance_resolve", "balance_resolve", cost: 3, isSpell: true));
            // oppScore=0 → toWin = WIN_SCORE > 3（假设 WIN_SCORE >= 5）
            _gs.EScore = (0);

            int eff = GameRules.GetSpellEffectiveCost(spell, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(3, eff, "对手离胜>3 时不减费（原费用 3）");
        }

        [Test]
        public void BalanceResolve_CostFloorIsZero()
        {
            var spell = MakeUnit(MakeCard("balance_resolve", "balance_resolve", cost: 2, isSpell: true));
            _gs.EScore = (GameRules.WIN_SCORE - 1); // toWin=1

            int eff = GameRules.GetSpellEffectiveCost(spell, GameRules.OWNER_PLAYER, _gs);
            Assert.AreEqual(0, eff, "费用不得低于 0");
        }

        // ── D-2: balance_resolve 召出的符文应为休眠 ──────────────────────────

        [Test]
        public void BalanceResolve_SummonedRuneIsDormant()
        {
            var spell = MakeUnit(MakeCard("balance_resolve", "balance_resolve", cost: 3, isSpell: true));
            _gs.GetHand(GameRules.OWNER_PLAYER).Add(spell);
            var rune = MakeRune(RuneType.Verdant);
            rune.Tapped = false;
            _gs.GetRuneDeck(GameRules.OWNER_PLAYER).Add(rune);

            _spellSys.CastSpell(spell, GameRules.OWNER_PLAYER, null, _gs);

            var runes = _gs.GetRunes(GameRules.OWNER_PLAYER);
            Assert.AreEqual(1, runes.Count, "应召出 1 枚符文");
            Assert.IsTrue(runes[0].Tapped, "卡面要求「休眠」→ Tapped=true");
        }

        // ── D-7: trifarian_warcamp & back_alley_bar 对 AI 单位也触发 ─────────

        [Test]
        public void TrifarianWarcamp_TriggersForEnemyUnit()
        {
            _gs.BFNames[0] = "trifarian_warcamp";
            var enemy = MakeUnit(MakeCard("goon", "", atk: 2), owner: GameRules.OWNER_ENEMY);
            int atkBefore = enemy.CurrentAtk;

            _bfSys.OnUnitEnterBattlefield(enemy, 0, GameRules.OWNER_ENEMY, _gs);

            Assert.AreEqual(atkBefore + 1, enemy.CurrentAtk, "AI 单位进入战营也 +1 战力");
            Assert.AreEqual(1, enemy.BuffTokens);
        }

        [Test]
        public void BackAlleyBar_TriggersForEnemyUnit()
        {
            _gs.BFNames[0] = "back_alley_bar";
            var enemy = MakeUnit(MakeCard("scoundrel", "", atk: 2), owner: GameRules.OWNER_ENEMY);

            _bfSys.OnUnitLeaveBattlefield(enemy, 0, GameRules.OWNER_ENEMY, _gs);

            Assert.AreEqual(1, enemy.TempAtkBonus, "AI 离开酒馆也 +1 本回合战力");
        }

        // ── D-10: 增益指示物只加战力 ─────────────────────────────────────────

        [Test]
        public void TrifarianWarcamp_BuffToken_OnlyAddsAtk_NotHp()
        {
            _gs.BFNames[0] = "trifarian_warcamp";
            var u = MakeUnit(MakeCard("u", "", atk: 3), owner: GameRules.OWNER_PLAYER);
            int hpBefore = u.CurrentHp;

            _bfSys.OnUnitEnterBattlefield(u, 0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(4, u.CurrentAtk, "+1 战力");
            Assert.AreEqual(hpBefore, u.CurrentHp, "HP 不变（卡面只写 +1战力）");
        }

        [Test]
        public void HiranaConquest_ConsumesBuffToken_OnlyReducesAtk()
        {
            _gs.BFNames[0] = "hirana";
            var u = MakeUnit(MakeCard("u", "", atk: 3), owner: GameRules.OWNER_PLAYER);
            _gs.GetBase(GameRules.OWNER_PLAYER).Add(u);
            u.BuffTokens = 1;
            u.CurrentAtk = 4; // 已经加过
            int hpBefore = u.CurrentHp;
            _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit(MakeCard("draw", "", atk: 1)));

            _bfSys.OnConquest(0, GameRules.OWNER_PLAYER, _gs);

            Assert.AreEqual(0, u.BuffTokens);
            Assert.AreEqual(3, u.CurrentAtk, "消耗增益 -1 战力");
            Assert.AreEqual(hpBefore, u.CurrentHp, "HP 不变");
        }

        // ── D-11: wailing_poro 基地孤独阵亡也触发 ────────────────────────────

        [Test]
        public void WailingPoro_Alone_InBase_TriggersDrawOnDeath()
        {
            var poro = MakeUnit(MakeCard("wailing_poro", "wailing_poro_die",
                atk: 2, kw: CardKeyword.Deathwish), owner: GameRules.OWNER_PLAYER);
            // 模拟死亡前已从 base 移出（DeathwishSystem 调用时 unit 已被移除）
            // 基地内不再有其它友方单位
            _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit(MakeCard("drawn", "", atk: 1)));
            int handBefore = _gs.GetHand(GameRules.OWNER_PLAYER).Count;

            _dwSys.OnUnitsDied(new System.Collections.Generic.List<UnitInstance> { poro }, -1, _gs);

            Assert.AreEqual(handBefore + 1, _gs.GetHand(GameRules.OWNER_PLAYER).Count,
                "基地孤独阵亡也应触发绝念摸牌");
        }

        [Test]
        public void WailingPoro_NotAlone_InBase_DoesNotTrigger()
        {
            var poro = MakeUnit(MakeCard("wailing_poro", "wailing_poro_die",
                atk: 2, kw: CardKeyword.Deathwish), owner: GameRules.OWNER_PLAYER);
            var mate = MakeUnit(MakeCard("mate", "", atk: 1), owner: GameRules.OWNER_PLAYER);
            _gs.GetBase(GameRules.OWNER_PLAYER).Add(mate); // 基地还有一个友方
            _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit(MakeCard("drawn", "", atk: 1)));
            int handBefore = _gs.GetHand(GameRules.OWNER_PLAYER).Count;

            _dwSys.OnUnitsDied(new System.Collections.Generic.List<UnitInstance> { poro }, -1, _gs);

            Assert.AreEqual(handBefore, _gs.GetHand(GameRules.OWNER_PLAYER).Count,
                "基地还有友方 → 不触发");
        }

        // ── N-1: bad_poro 征服时召出「硬币」装备指示物 ────────────────────────

        [Test]
        public void BadPoro_CoinEquipAsset_Exists_AsDormantEquipmentToken()
        {
            var coin = Resources.Load<CardData>("Cards/coin_equip");
            Assert.IsNotNull(coin, "coin_equip.asset 应可从 Resources/Cards 加载");
            Assert.IsTrue(coin.IsEquipment, "卡面「装备指示物」→ IsEquipment=true");
            Assert.AreEqual(0, coin.Cost, "卡面未提供支付费用，cost=0");
            Assert.AreEqual(1, coin.EquipAtkBonus, "卡面 +1战力 → equipAtkBonus=1");
            Assert.AreEqual(0, coin.EquipRuneCost, "卡面 装配[0] → equipRuneCost=0");
        }

        [Test]
        public void BadPoro_ConquestSummonsDormantCoin_IntoBase()
        {
            var combatGO = new GameObject("Combat");
            var combatSys = combatGO.AddComponent<CombatSystem>();
            var attacker = GameRules.OWNER_PLAYER;

            var badPoro = MakeUnit(MakeCard("bad_poro", "bad_poro_conquer",
                atk: 2, rune: RuneType.Blazing, kw: CardKeyword.Conquest), owner: attacker);

            int baseBefore = _gs.GetBase(attacker).Count;

            var mi = typeof(CombatSystem).GetMethod("SummonCoinEquipment",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(mi, "SummonCoinEquipment 方法应存在");
            mi.Invoke(combatSys, new object[] { attacker, _gs, badPoro });

            var baseList = _gs.GetBase(attacker);
            Assert.AreEqual(baseBefore + 1, baseList.Count, "征服应向基地添加 1 张卡");
            var coin = baseList[baseList.Count - 1];
            Assert.AreEqual("coin_equip", coin.CardData.Id, "召出的应为 coin_equip");
            Assert.IsTrue(coin.Exhausted, "卡面要求「休眠」→ Exhausted=true");
            Assert.IsTrue(coin.CardData.IsEquipment, "应为装备");
        }

        [Test]
        public void WailingPoro_AloneOnBF_StillTriggers()
        {
            var poro = MakeUnit(MakeCard("wailing_poro", "wailing_poro_die",
                atk: 2, kw: CardKeyword.Deathwish), owner: GameRules.OWNER_PLAYER);
            _gs.GetDeck(GameRules.OWNER_PLAYER).Add(MakeUnit(MakeCard("drawn", "", atk: 1)));
            int handBefore = _gs.GetHand(GameRules.OWNER_PLAYER).Count;

            // bfId=0, PlayerUnits 为空（死前已移除） → 孤独
            _dwSys.OnUnitsDied(new System.Collections.Generic.List<UnitInstance> { poro }, 0, _gs);

            Assert.AreEqual(handBefore + 1, _gs.GetHand(GameRules.OWNER_PLAYER).Count,
                "战场孤独阵亡仍应触发（回归保护）");
        }
    }
}
