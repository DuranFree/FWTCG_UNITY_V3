using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using FWTCG.Core;
using FWTCG.Data;
using FWTCG.FX;
using FWTCG.UI;
using FWTCG.VFX;

namespace FWTCG.Tests.EditMode
{
    [TestFixture]
    public class VFX4ResolverTests
    {
        private static CardData MakeCard(string id, string name, int cost, int atk,
            RuneType rt, string effectId = "", CardKeyword kw = CardKeyword.None,
            bool isSpell = false)
        {
            var card = ScriptableObject.CreateInstance<CardData>();
            card.EditorSetup(id, name, cost, atk, rt, 0, "", kw, effectId, isSpell: isSpell);
            return card;
        }

        [TearDown]
        public void TearDown()
        {
            VFXResolver.ClearCache();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4a. RuneType mapping
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetSpawnFXName_ReturnsCorrectMapping_ForAllRuneTypes()
        {
            Assert.AreEqual(VFXResolver.FX_SPAWN_F, VFXResolver.GetSpawnFXName(RuneType.Blazing));
            Assert.AreEqual(VFXResolver.FX_RAYGLOW, VFXResolver.GetSpawnFXName(RuneType.Radiant));
            Assert.AreEqual(VFXResolver.FX_SPAWN_V, VFXResolver.GetSpawnFXName(RuneType.Verdant));
            Assert.AreEqual(VFXResolver.FX_HIT,     VFXResolver.GetSpawnFXName(RuneType.Crushing));
            Assert.AreEqual(VFXResolver.FX_SPAWN,   VFXResolver.GetSpawnFXName(RuneType.Chaos));
            Assert.AreEqual(VFXResolver.FX_SPAWN_W, VFXResolver.GetSpawnFXName(RuneType.Order));
        }

        [Test]
        public void GetIdleFXName_ReturnsCorrectMapping_ForAllRuneTypes()
        {
            Assert.AreEqual(VFXResolver.FX_FLAME,   VFXResolver.GetIdleFXName(RuneType.Blazing));
            Assert.AreEqual(VFXResolver.FX_RAYGLOW, VFXResolver.GetIdleFXName(RuneType.Radiant));
            Assert.AreEqual(VFXResolver.FX_LEAF,    VFXResolver.GetIdleFXName(RuneType.Verdant));
            Assert.IsNull(VFXResolver.GetIdleFXName(RuneType.Crushing));
            Assert.AreEqual(VFXResolver.FX_CAST,    VFXResolver.GetIdleFXName(RuneType.Chaos));
            Assert.AreEqual(VFXResolver.FX_WATER,   VFXResolver.GetIdleFXName(RuneType.Order));
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4a. FXConfig struct
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void FXConfig_DefaultValues_AreCorrect()
        {
            var cfg = new FXConfig("TestFX");
            Assert.AreEqual("TestFX", cfg.PrefabName);
            Assert.AreEqual(0f, cfg.Delay);
            Assert.AreEqual(1, cfg.RepeatCount);
            Assert.AreEqual(0.15f, cfg.RepeatInterval);
            Assert.AreEqual(1f, cfg.Scale);
            Assert.AreEqual(0f, cfg.Duration);
            Assert.IsFalse(cfg.HasTint);
        }

        [Test]
        public void FXConfig_CustomTint_HasTintReturnsTrue()
        {
            var cfg = new FXConfig("TestFX", tint: Color.red);
            Assert.IsTrue(cfg.HasTint);
        }

        [Test]
        public void FXConfig_RepeatCount_ClampsToMinOne()
        {
            var cfg = new FXConfig("TestFX", repeat: 0);
            Assert.AreEqual(1, cfg.RepeatCount);
            var cfg2 = new FXConfig("TestFX", repeat: -5);
            Assert.AreEqual(1, cfg2.RepeatCount);
        }

        [Test]
        public void FXConfig_Scale_ClampsToPositive()
        {
            var cfg = new FXConfig("TestFX", scale: 0f);
            Assert.AreEqual(1f, cfg.Scale);
            var cfg2 = new FXConfig("TestFX", scale: -2f);
            Assert.AreEqual(1f, cfg2.Scale);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4b. effectId → FX mapping
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Resolve_KnownEffectId_ReturnsSpecificFX()
        {
            var card = MakeCard("hex_ray", "HexRay", 3, 0, RuneType.Blazing, "hex_ray");
            var configs = VFXResolver.Resolve(card);
            Assert.IsTrue(configs.Count >= 2);
            Assert.AreEqual(VFXResolver.FX_FLAME, configs[0].PrefabName);
            Assert.AreEqual(VFXResolver.FX_HIT, configs[1].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_UnknownEffectId_FallsBackToRuneType()
        {
            var card = MakeCard("unknown_card", "Unknown", 2, 0, RuneType.Verdant, "some_unknown_effect");
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(VFXResolver.FX_SPAWN_V, configs[0].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_NoEffectId_FallsBackToRuneType()
        {
            var card = MakeCard("basic_unit", "Basic", 1, 3, RuneType.Blazing);
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(VFXResolver.FX_SPAWN_F, configs[0].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_NullCard_ReturnsEmptyList()
        {
            var configs = VFXResolver.Resolve(null);
            Assert.IsNotNull(configs);
            Assert.AreEqual(0, configs.Count);
        }

        [Test]
        public void Resolve_AkasiStorm_HasSixRepeats()
        {
            var card = MakeCard("akasi_storm", "AkasiStorm", 5, 0, RuneType.Crushing, "akasi_storm");
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(VFXResolver.FX_HIT, configs[0].PrefabName);
            Assert.AreEqual(6, configs[0].RepeatCount);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_GuardianEquip_HasPhoenixAndShield()
        {
            var card = MakeCard("guardian_angel", "GA", 0, 0, RuneType.Verdant, "guardian_equip");
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(2, configs.Count);
            Assert.AreEqual(VFXResolver.FX_PHOENIX, configs[0].PrefabName);
            Assert.AreEqual(VFXResolver.FX_SHIELD, configs[1].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_WindWall_HasLargeScale()
        {
            var card = MakeCard("wind_wall", "WindWall", 4, 0, RuneType.Order, "wind_wall");
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(1.5f, configs[0].Scale);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void Resolve_TimeWarp_HasCustomTint()
        {
            var card = MakeCard("time_warp", "TimeWarp", 6, 0, RuneType.Chaos, "time_warp");
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.IsTrue(configs[0].HasTint);
            Object.DestroyImmediate(card);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4b. Death FX mapping
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ResolveDeathFX_DefaultUnit_ReturnsDestroy()
        {
            var card = MakeCard("basic_unit", "Basic", 1, 3, RuneType.Blazing);
            var configs = VFXResolver.ResolveDeathFX(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(VFXResolver.FX_DESTROY, configs[0].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void ResolveDeathFX_WailingPoro_HasCustomDeathFX()
        {
            var card = MakeCard("wailing_poro", "WailingPoro", 1, 1, RuneType.Order, "wailing_poro_die");
            var configs = VFXResolver.ResolveDeathFX(card);
            Assert.IsTrue(configs.Count >= 2);
            Assert.AreEqual(VFXResolver.FX_SPAWN, configs[0].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void ResolveDeathFX_NullCard_ReturnsEmptyList()
        {
            var configs = VFXResolver.ResolveDeathFX(null);
            Assert.IsNotNull(configs);
            Assert.AreEqual(0, configs.Count);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4c. Prefab cache
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetPrefab_NullOrEmpty_ReturnsNull()
        {
            Assert.IsNull(VFXResolver.GetPrefab(null));
            Assert.IsNull(VFXResolver.GetPrefab(""));
        }

        [Test]
        public void GetPrefab_NonExistent_ReturnsNullAndCaches()
        {
            Assert.IsNull(VFXResolver.GetPrefab("NonExistentPrefab_12345"));
            Assert.IsNull(VFXResolver.GetPrefab("NonExistentPrefab_12345"));
        }

        [Test]
        public void ClearCache_ResetsState()
        {
            VFXResolver.GetPrefab("SomeKey");
            VFXResolver.ClearCache();
            Assert.Pass();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4d. CardView battlefield visuals
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ApplyBattlefieldVisuals_AppliesMicroRotation()
        {
            // Expect the ShouldRunBehaviour assertion from SendMessage in EditMode
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("TestCard");
            var cv = go.AddComponent<CardView>();
            var card = MakeCard("test_unit", "Test", 1, 3, RuneType.Blazing);
            var unit = new UnitInstance(0, card, "Player");
            cv.SendMessage("Awake");
            cv.Setup(unit, true, null);

            cv.ApplyBattlefieldVisuals();

            float z = cv.transform.localRotation.eulerAngles.z;
            bool inRange = z <= 1.1f || z >= 358.9f;
            Assert.IsTrue(inRange, $"Z rotation {z} not in ±1° range");

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ApplyBattlefieldVisuals_CreatesShadow()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("TestCard");
            go.AddComponent<RectTransform>();
            var cv = go.AddComponent<CardView>();
            var card = MakeCard("test_unit", "Test", 1, 3, RuneType.Blazing);
            var unit = new UnitInstance(0, card, "Player");
            cv.SendMessage("Awake");
            cv.Setup(unit, true, null);

            cv.ApplyBattlefieldVisuals();

            var shadow = go.transform.Find("CardShadow");
            Assert.IsNotNull(shadow, "Shadow child should be created");
            Assert.IsNotNull(shadow.GetComponent<Image>(), "Shadow should have Image component");

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ClearBattlefieldVisuals_ResetsRotation()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("TestCard");
            go.AddComponent<RectTransform>();
            var cv = go.AddComponent<CardView>();
            var card = MakeCard("test_unit", "Test", 1, 3, RuneType.Blazing);
            var unit = new UnitInstance(0, card, "Player");
            cv.SendMessage("Awake");
            cv.Setup(unit, true, null);

            cv.ApplyBattlefieldVisuals();
            cv.ClearBattlefieldVisuals();
            Assert.AreEqual(Quaternion.identity, cv.transform.localRotation);

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        [Test]
        public void ApplyBattlefieldVisuals_CalledTwice_DoesNotDuplicateShadow()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("TestCard");
            go.AddComponent<RectTransform>();
            var cv = go.AddComponent<CardView>();
            var card = MakeCard("test_unit", "Test", 1, 2, RuneType.Order);
            var unit = new UnitInstance(0, card, "Player");
            cv.SendMessage("Awake");
            cv.Setup(unit, true, null);

            cv.ApplyBattlefieldVisuals();
            cv.ApplyBattlefieldVisuals();

            int shadowCount = 0;
            for (int i = 0; i < go.transform.childCount; i++)
                if (go.transform.GetChild(i).name == "CardShadow") shadowCount++;
            Assert.AreEqual(1, shadowCount, "Should not create duplicate shadows");

            Object.DestroyImmediate(card);
            Object.DestroyImmediate(go);
            LogAssert.ignoreFailingMessages = false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // 4c. SpellVFX color mapping
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void GetCardBurstColor_AllRuneTypes_ReturnDistinctColors()
        {
            var colors = new HashSet<Color>();
            var runeTypes = new[] { RuneType.Blazing, RuneType.Radiant, RuneType.Verdant,
                                    RuneType.Crushing, RuneType.Chaos, RuneType.Order };
            foreach (var rt in runeTypes)
            {
                var card = MakeCard($"test_{rt}", "Test", 1, 3, rt);
                var unit = new UnitInstance(0, card, "Player");
                var color = SpellVFX.GetCardBurstColor(unit);
                colors.Add(color);
                Object.DestroyImmediate(card);
            }
            Assert.AreEqual(6, colors.Count, "Each rune type should have a distinct burst color");
        }

        [Test]
        public void GetCardBurstColor_NullUnit_ReturnsFallbackColor()
        {
            var color = SpellVFX.GetCardBurstColor(null);
            Assert.AreEqual(GameColors.BuffColor, color);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Resolve_AllKnownEffectIds_ReturnNonEmptyConfigs()
        {
            var knownEffectIds = new[]
            {
                "hex_ray", "furnace_blast", "evolve_day", "void_seek",
                "divine_ray", "starburst", "stardrop", "rally_call", "well_trained",
                "guilty_pleasure", "smoke_bomb",
                "slam", "akasi_storm", "strike_ask_later", "duel_stance",
                "time_warp", "balance_resolve",
                "wind_wall", "flash_counter", "scoff", "retreat_rune",
                "jax_enter", "rengar_enter", "tiyana_enter", "thousand_tail_enter",
                "sandshoal_deserter_enter", "foresight_mech_enter", "noxus_recruit_enter",
                "yordel_instructor_enter",
                "yi_hero_enter", "kaisa_hero_conquer",
                "swindle",
                "dorans_equip", "trinity_equip", "guardian_equip",
                "darius_second_card",
                "alert_sentinel_die", "wailing_poro_die"
            };

            foreach (var eid in knownEffectIds)
            {
                var card = MakeCard($"card_{eid}", "Test", 1, 3, RuneType.Blazing, eid);
                var configs = VFXResolver.Resolve(card);
                Assert.IsTrue(configs.Count > 0, $"effectId '{eid}' should resolve to at least 1 FXConfig");
                foreach (var cfg in configs)
                    Assert.IsFalse(string.IsNullOrEmpty(cfg.PrefabName),
                        $"effectId '{eid}' has an FXConfig with empty PrefabName");
                Object.DestroyImmediate(card);
            }
        }

        [Test]
        public void Resolve_SpellCard_FallsBackToRuneType()
        {
            var card = MakeCard("generic_spell", "Spell", 2, 0, RuneType.Order, isSpell: true);
            var configs = VFXResolver.Resolve(card);
            Assert.AreEqual(1, configs.Count);
            Assert.AreEqual(VFXResolver.FX_SPAWN_W, configs[0].PrefabName);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void ApplyBattlefieldVisuals_NullUnit_DoesNotThrow()
        {
            LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("TestCard");
            var cv = go.AddComponent<CardView>();
            cv.SendMessage("Awake");
            Assert.DoesNotThrow(() => cv.ApplyBattlefieldVisuals());
            Object.DestroyImmediate(go);

            LogAssert.ignoreFailingMessages = false;
        }
    }
}
