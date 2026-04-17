using System.Collections.Generic;
using UnityEngine;
using FWTCG.Data;

namespace FWTCG.VFX
{
    /// <summary>
    /// VFX-4: Maps CardData (RuneType + effectId) → FX prefab configs.
    /// Two-layer resolution:
    ///   1) effectId → specific FX overrides (per-card skill effects)
    ///   2) RuneType → elemental base FX (fallback for units with no effectId)
    /// </summary>
    public static class VFXResolver
    {
        // ── FX prefab names (match Assets/Prefabs/FX/*.prefab) ───────────────
        public const string FX_HIT       = "HitFX";
        public const string FX_FLAME     = "Flame";
        public const string FX_ELECTRIC  = "ElectricFX";
        public const string FX_WATER     = "WaterFX";
        public const string FX_LEAF      = "Leaf";
        public const string FX_SHIELD    = "Shield";
        public const string FX_DESTROY   = "Destroy";
        public const string FX_SPAWN     = "Spawn";
        public const string FX_SPAWN_F   = "SpawnFire";
        public const string FX_SPAWN_W   = "SpawnWater";
        public const string FX_SPAWN_V   = "SpawnForest";
        public const string FX_PHOENIX   = "Phoenix";
        public const string FX_RAYGLOW   = "RayGlow";
        public const string FX_POISONED  = "Poisoned";
        public const string FX_SILENCED  = "Silenced";
        public const string FX_POTION    = "PotionFX";
        public const string FX_DAMAGE    = "DamageFX";
        public const string FX_CAST      = "CastFX";
        public const string FX_ZZZ       = "Zzz";
        public const string FX_ROOT      = "RootFX";
        public const string FX_IMMUNE    = "Immune";
        public const string FX_SHELL     = "Shell";

        // ── Prefab cache ─────────────────────────────────────────────────────
        private static readonly Dictionary<string, GameObject> _prefabCache = new();

        /// <summary>Load FX prefab from Resources/Prefabs/FX/ (cached).</summary>
        public static GameObject GetPrefab(string fxName)
        {
            if (string.IsNullOrEmpty(fxName)) return null;
            if (_prefabCache.TryGetValue(fxName, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>($"Prefabs/FX/{fxName}");
            if (prefab == null)
                prefab = Resources.Load<GameObject>($"FX/{fxName}");
            _prefabCache[fxName] = prefab; // cache even null to avoid repeated lookups
            return prefab;
        }

        // ── RuneType → base spawn FX ─────────────────────────────────────────

        /// <summary>Elemental spawn FX by rune type.</summary>
        public static string GetSpawnFXName(RuneType rt)
        {
            switch (rt)
            {
                case RuneType.Blazing:  return FX_SPAWN_F;
                case RuneType.Radiant:  return FX_RAYGLOW;
                case RuneType.Verdant:  return FX_SPAWN_V;
                case RuneType.Crushing: return FX_HIT;
                case RuneType.Chaos:    return FX_SPAWN;
                case RuneType.Order:    return FX_SPAWN_W;
                default:                return FX_SPAWN;
            }
        }

        /// <summary>Elemental idle FX by rune type (persistent on-board particle).</summary>
        public static string GetIdleFXName(RuneType rt)
        {
            switch (rt)
            {
                case RuneType.Blazing:  return FX_FLAME;
                case RuneType.Radiant:  return FX_RAYGLOW;
                case RuneType.Verdant:  return FX_LEAF;
                case RuneType.Crushing: return null; // no persistent FX for crushing
                case RuneType.Chaos:    return FX_CAST;
                case RuneType.Order:    return FX_WATER;
                default:                return null;
            }
        }

        // ── effectId → FX configs ────────────────────────────────────────────

        /// <summary>
        /// Resolve a card into a list of FXConfig entries to play.
        /// Returns empty list if no FX should be played.
        /// </summary>
        public static List<FXConfig> Resolve(CardData card)
        {
            var result = new List<FXConfig>();
            if (card == null) return result;

            // 1) Check effectId-specific mapping
            if (!string.IsNullOrEmpty(card.EffectId) && s_effectIdMap.TryGetValue(card.EffectId, out var configs))
            {
                result.AddRange(configs);
                return result;
            }

            // 2) Fallback: RuneType base FX
            string baseFX = GetSpawnFXName(card.RuneType);
            if (baseFX != null)
                result.Add(new FXConfig(baseFX));

            return result;
        }

        /// <summary>Resolve FX for a unit death event.</summary>
        public static List<FXConfig> ResolveDeathFX(CardData card)
        {
            var result = new List<FXConfig>();
            if (card == null) return result;

            // Check for deathwish-specific FX
            if (!string.IsNullOrEmpty(card.EffectId) && s_deathFXMap.TryGetValue(card.EffectId, out var configs))
            {
                result.AddRange(configs);
                return result;
            }

            // Default death FX
            result.Add(new FXConfig(FX_DESTROY));
            return result;
        }

        // ── effectId mapping table ───────────────────────────────────────────
        // Built from the 38 known effectIds in the project.

        private static readonly Dictionary<string, FXConfig[]> s_effectIdMap = new()
        {
            // ── Blazing (fire) effects ───
            ["hex_ray"]           = new[] { new FXConfig(FX_FLAME), new FXConfig(FX_HIT, delay: 0.2f) },
            ["furnace_blast"]     = new[] { new FXConfig(FX_FLAME, repeat: 3, delay: 0.15f) },
            ["evolve_day"]        = new[] { new FXConfig(FX_FLAME), new FXConfig(FX_RAYGLOW, delay: 0.3f) },
            ["void_seek"]         = new[] { new FXConfig(FX_FLAME, tint: new Color(0.0f, 0.75f, 0.60f, 1f)) },  // dark teal (void energy, not pink)

            // ── Radiant (light) effects ───
            ["divine_ray"]        = new[] { new FXConfig(FX_RAYGLOW), new FXConfig(FX_HIT, delay: 0.2f) },
            ["starburst"]         = new[] { new FXConfig(FX_RAYGLOW, repeat: 2, delay: 0.2f) },
            ["stardrop"]          = new[] { new FXConfig(FX_RAYGLOW) },
            ["rally_call"]        = new[] { new FXConfig(FX_RAYGLOW), new FXConfig(FX_POTION, delay: 0.2f) },
            ["well_trained"]      = new[] { new FXConfig(FX_POTION) },

            // ── Verdant (nature) effects ───
            ["guilty_pleasure"]   = new[] { new FXConfig(FX_LEAF), new FXConfig(FX_POTION, delay: 0.2f) },
            ["smoke_bomb"]        = new[] { new FXConfig(FX_LEAF, tint: new Color(0.3f, 0.3f, 0.3f)) },

            // ── Crushing (physical) effects ───
            ["slam"]              = new[] { new FXConfig(FX_ELECTRIC), new FXConfig(FX_HIT, delay: 0.1f) },
            ["akasi_storm"]       = new[] { new FXConfig(FX_HIT, repeat: 6, delay: 0.15f) },
            ["strike_ask_later"]  = new[] { new FXConfig(FX_HIT, repeat: 2, delay: 0.1f) },
            ["duel_stance"]       = new[] { new FXConfig(FX_SHIELD), new FXConfig(FX_HIT, delay: 0.3f) },

            // ── Chaos (arcane) effects ───
            ["time_warp"]         = new[] { new FXConfig(FX_SPAWN, tint: new Color(0.0f, 0.40f, 0.90f, 1f)) },  // deep blue (time magic, not pink)
            ["balance_resolve"]   = new[] { new FXConfig(FX_CAST) },

            // ── Order (water/control) effects ───
            ["wind_wall"]         = new[] { new FXConfig(FX_SHIELD, scale: 1.5f) },
            ["flash_counter"]     = new[] { new FXConfig(FX_SHIELD), new FXConfig(FX_ELECTRIC, delay: 0.2f) },
            ["scoff"]             = new[] { new FXConfig(FX_SILENCED) },
            ["retreat_rune"]      = new[] { new FXConfig(FX_SPAWN_W) },

            // ── Entry effects ───
            ["jax_enter"]         = new[] { new FXConfig(FX_HIT), new FXConfig(FX_FLAME, delay: 0.2f) },
            ["rengar_enter"]      = new[] { new FXConfig(FX_HIT, repeat: 2, delay: 0.15f) },
            ["tiyana_enter"]      = new[] { new FXConfig(FX_LEAF), new FXConfig(FX_SPAWN_V, delay: 0.2f) },
            ["thousand_tail_enter"] = new[] { new FXConfig(FX_SPAWN), new FXConfig(FX_CAST, delay: 0.3f) },
            ["sandshoal_deserter_enter"] = new[] { new FXConfig(FX_WATER) },
            ["foresight_mech_enter"] = new[] { new FXConfig(FX_RAYGLOW) },
            ["noxus_recruit_enter"]  = new[] { new FXConfig(FX_SPAWN_F) },
            ["yordel_instructor_enter"] = new[] { new FXConfig(FX_POTION) },

            // ── Hero effects ───
            ["yi_hero_enter"]     = new[] { new FXConfig(FX_FLAME), new FXConfig(FX_RAYGLOW, delay: 0.2f) },
            ["kaisa_hero_conquer"] = new[] { new FXConfig(FX_SPAWN, tint: new Color(0.90f, 0.55f, 0.10f, 1f)), new FXConfig(FX_FLAME, delay: 0.3f) },  // gold (hero conquest)

            // ── Reactive spells ───
            ["swindle"]           = new[] { new FXConfig(FX_CAST, tint: new Color(0.0f, 0.70f, 0.70f, 1f)) },  // teal (shadow/deception, not pink)

            // ── Equipment effects ───
            ["dorans_equip"]      = new[] { new FXConfig(FX_HIT) },
            ["trinity_equip"]     = new[] { new FXConfig(FX_RAYGLOW) },
            ["guardian_equip"]    = new[] { new FXConfig(FX_PHOENIX), new FXConfig(FX_SHIELD, delay: 0.2f) },

            // ── Legend effects ───
            ["darius_second_card"] = new[] { new FXConfig(FX_SPAWN_F), new FXConfig(FX_FLAME, delay: 0.2f) },

            // ── BF / misc ───
            ["alert_sentinel_die"] = new[] { new FXConfig(FX_RAYGLOW) },
            ["wailing_poro_die"]   = new[] { new FXConfig(FX_SPAWN, tint: new Color(0.5f, 0.8f, 1f)) },
        };

        /// <summary>Deathwish-specific FX overrides (played on unit death).</summary>
        private static readonly Dictionary<string, FXConfig[]> s_deathFXMap = new()
        {
            ["alert_sentinel_die"] = new[] { new FXConfig(FX_RAYGLOW), new FXConfig(FX_DESTROY, delay: 0.1f) },
            ["wailing_poro_die"]   = new[] { new FXConfig(FX_SPAWN, tint: new Color(0.5f, 0.8f, 1f)), new FXConfig(FX_DESTROY, delay: 0.1f) },
        };

        /// <summary>Clear the prefab cache (useful for tests or scene transitions).</summary>
        public static void ClearCache() => _prefabCache.Clear();

        // ── VFX-8: Projectile mapping ────────────────────────────────────────

        /// <summary>
        /// Resolve the projectile FX prefab name for a card.
        /// Returns null if no projectile should be used (non-spell cards, etc.).
        /// Only spell/equipment cards with a direct-damage effectId get projectiles.
        /// </summary>
        public static string ResolveProjectile(CardData card)
        {
            if (card == null) return null;

            // Check effectId-specific projectile override
            if (!string.IsNullOrEmpty(card.EffectId) && s_projectileMap.TryGetValue(card.EffectId, out var projFX))
                return projFX;

            // Spell/equipment cards get a RuneType-based projectile
            if (card.IsSpell || card.IsEquipment)
                return GetProjectileFXName(card.RuneType);

            return null; // unit cards don't fire projectiles
        }

        /// <summary>Elemental projectile FX by rune type.</summary>
        public static string GetProjectileFXName(RuneType rt)
        {
            switch (rt)
            {
                case RuneType.Blazing:  return FX_FLAME;
                case RuneType.Radiant:  return FX_RAYGLOW;
                case RuneType.Verdant:  return FX_LEAF;
                case RuneType.Crushing: return FX_HIT;
                case RuneType.Chaos:    return FX_CAST;
                case RuneType.Order:    return FX_WATER;
                default:                return FX_HIT;
            }
        }

        /// <summary>effectId → projectile FX override (for specific spells with unique projectiles).</summary>
        private static readonly Dictionary<string, string> s_projectileMap = new()
        {
            ["hex_ray"]           = FX_FLAME,
            ["furnace_blast"]     = FX_FLAME,
            ["divine_ray"]        = FX_RAYGLOW,
            ["starburst"]         = FX_RAYGLOW,
            ["slam"]              = FX_ELECTRIC,
            ["akasi_storm"]       = FX_ELECTRIC,
            ["void_seek"]         = FX_FLAME,
            ["flash_counter"]     = FX_ELECTRIC,
            ["wind_wall"]         = FX_WATER,
        };
    }

    // ═════════════════════════════════════════════════════════════════════════
    // FXConfig — describes one FX instance to spawn
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Configuration for a single FX prefab spawn.</summary>
    public struct FXConfig
    {
        public string PrefabName;
        public float  Delay;       // seconds before spawning
        public int    RepeatCount; // how many instances (1 = single)
        public float  RepeatInterval; // seconds between repeats
        public Color  Tint;        // Color.clear = use default
        public float  Scale;       // 1 = normal size
        public float  Duration;    // lifetime override (0 = use FXTool default)

        public FXConfig(string prefabName, float delay = 0f, int repeat = 1,
                        float repeatInterval = 0.15f, Color tint = default,
                        float scale = 1f, float duration = 0f)
        {
            PrefabName = prefabName;
            Delay = delay;
            RepeatCount = Mathf.Max(1, repeat);
            RepeatInterval = repeatInterval;
            Tint = tint;
            Scale = scale > 0f ? scale : 1f;
            Duration = duration;
        }

        /// <summary>Whether a custom tint is set (not default/clear).</summary>
        public bool HasTint => Tint.a > 0.01f;
    }
}
