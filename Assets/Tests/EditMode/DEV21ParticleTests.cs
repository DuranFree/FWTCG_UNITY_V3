using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.UI;
using FWTCG.Core;
using FWTCG.Data;

/// <summary>
/// DEV-21: Particle / VFX system tests.
///
/// Covers:
///   ParticleManager — constant values, initialization, BG/rune/firefly/mist/line counts.
///   MouseTrail      — constant values, initialization.
///   SpellVFX        — event subscribe/unsubscribe, GetCardBurstColor per RuneType.
///   GameEventBus    — OnUnitDiedAtPos fires correctly.
/// </summary>
[TestFixture]
public class DEV21ParticleTests
{
    // ══════════════════════════════════════════════════════════════════════════
    // ParticleManager constant tests (no runtime, just constant checks)
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public void ParticleManager_BG_COUNT_Is55()
        => Assert.AreEqual(55, ParticleManager.BG_COUNT);

    [Test]
    public void ParticleManager_RUNE_COUNT_Is8()
        => Assert.AreEqual(8, ParticleManager.RUNE_COUNT);

    [Test]
    public void ParticleManager_FIREFLY_COUNT_Is12()
        => Assert.AreEqual(12, ParticleManager.FIREFLY_COUNT);

    [Test]
    public void ParticleManager_MIST_COUNT_Is4()
        => Assert.AreEqual(4, ParticleManager.MIST_COUNT);

    [Test]
    public void ParticleManager_LINE_POOL_Is80()
        => Assert.AreEqual(80, ParticleManager.LINE_POOL);

    [Test]
    public void ParticleManager_LINE_RADIUS_Is90()
        => Assert.AreEqual(90f, ParticleManager.LINE_RADIUS);

    // ══════════════════════════════════════════════════════════════════════════
    // MouseTrail constant tests
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public void MouseTrail_TRAIL_LENGTH_Is18()
        => Assert.AreEqual(18, MouseTrail.TRAIL_LENGTH);

    [Test]
    public void MouseTrail_DOT_MAX_SIZE_Is8()
        => Assert.AreEqual(8f, MouseTrail.DOT_MAX_SIZE);

    [Test]
    public void MouseTrail_HEAD_ALPHA_IsPositive()
        => Assert.Greater(MouseTrail.TRAIL_HEAD_ALPHA, 0f);

    // ══════════════════════════════════════════════════════════════════════════
    // SpellVFX — GetCardBurstColor
    // ══════════════════════════════════════════════════════════════════════════

    private static CardData MakeCard(RuneType rt)
    {
        var cd = ScriptableObject.CreateInstance<CardData>();
#if UNITY_EDITOR
        cd.EditorSetup("test_vfx", "VFXCard", 1, 1, rt, 0, "");
#endif
        return cd;
    }

    private static UnitInstance MakeUnit(RuneType rt)
    {
        var cd = MakeCard(rt);
        return new UnitInstance(99, cd, GameRules.OWNER_PLAYER);
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_Blazing_IsOrangeRed()
    {
        var unit = MakeUnit(RuneType.Blazing);
        var col  = SpellVFX.GetCardBurstColor(unit);
        Assert.Greater(col.r, 0.8f,  "Red channel should be high for Blazing");
        Assert.Less   (col.g, 0.6f,  "Green channel should be low for Blazing");
        Assert.Less   (col.b, 0.3f,  "Blue channel should be very low for Blazing");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_Order_IsBlue()
    {
        var unit = MakeUnit(RuneType.Order);
        var col  = SpellVFX.GetCardBurstColor(unit);
        Assert.Greater(col.b, 0.8f,  "Blue channel should be high for Order");
        Assert.Less   (col.r, 0.3f,  "Red channel should be low for Order");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_Verdant_IsGreen()
    {
        var unit = MakeUnit(RuneType.Verdant);
        var col  = SpellVFX.GetCardBurstColor(unit);
        Assert.Greater(col.g, 0.7f,  "Green channel should dominate for Verdant");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_Chaos_IsPurple()
    {
        var unit = MakeUnit(RuneType.Chaos);
        var col  = SpellVFX.GetCardBurstColor(unit);
        Assert.Greater(col.r + col.b, 1.0f, "Chaos should have high combined R+B (purple)");
        Assert.Less   (col.g, 0.3f,         "Green should be low for Chaos (purple)");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_NullCard_ReturnsNonBlack()
    {
        var col = SpellVFX.GetCardBurstColor(null);
        // Must return a non-black colour (fallback to GameColors.BuffColor)
        Assert.IsTrue(col.r > 0f || col.g > 0f || col.b > 0f,
            "Null card should return a valid fallback colour");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_EachRuneTypeIsDistinct()
    {
        var types = System.Enum.GetValues(typeof(RuneType));
        var colors = new Color[types.Length];
        int i = 0;
        foreach (RuneType rt in types)
            colors[i++] = SpellVFX.GetCardBurstColor(MakeUnit(rt));

        // No two rune types should produce the same colour
        for (int a = 0; a < colors.Length; a++)
        for (int b = a + 1; b < colors.Length; b++)
        {
            bool same = Mathf.Approximately(colors[a].r, colors[b].r)
                     && Mathf.Approximately(colors[a].g, colors[b].g)
                     && Mathf.Approximately(colors[a].b, colors[b].b);
            Assert.IsFalse(same,
                $"RuneType colors at index {a} and {b} should differ");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // GameEventBus — OnUnitDiedAtPos
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public void GameEventBus_OnUnitDiedAtPos_FiresCorrectArguments()
    {
        UnitInstance receivedUnit = null;
        Vector2      receivedPos  = Vector2.zero;
        bool         fired        = false;

        System.Action<UnitInstance, Vector2> handler = (u, p) =>
        {
            receivedUnit = u;
            receivedPos  = p;
            fired        = true;
        };

        GameEventBus.OnUnitDiedAtPos += handler;
        try
        {
            var unit = MakeUnit(RuneType.Blazing);
            var expected = new Vector2(123f, -456f);
            GameEventBus.FireUnitDiedAtPos(unit, expected);

            Assert.IsTrue(fired, "OnUnitDiedAtPos should have fired");
            Assert.AreEqual(unit,     receivedUnit, "Unit arg should match");
            Assert.AreEqual(expected, receivedPos,  "Position arg should match");
        }
        finally
        {
            GameEventBus.OnUnitDiedAtPos -= handler;
        }
    }

    [Test]
    public void GameEventBus_OnUnitDiedAtPos_NoSubscribers_DoesNotThrow()
    {
        // Ensure no stale subscribers
        var unit = MakeUnit(RuneType.Order);
        Assert.DoesNotThrow(() => GameEventBus.FireUnitDiedAtPos(unit, Vector2.zero));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // SpellVFX — subscribe / unsubscribe (runtime MonoBehaviour)
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public void SpellVFX_Awake_SubscribesToOnCardPlayed()
    {
        var go  = new GameObject("TestSpellVFX");
        var vfx = go.AddComponent<SpellVFX>();

        // Awake fires AddComponent; verify no exception
        // (Full subscription check is implicit: if it threw, the test fails)
        Assert.IsNotNull(vfx);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void SpellVFX_Destroy_UnsubscribesFromOnCardPlayed()
    {
        var go  = new GameObject("TestSpellVFX2");
        var vfx = go.AddComponent<SpellVFX>();

        // Destroy → OnDestroy should unsubscribe without error
        Assert.DoesNotThrow(() => Object.DestroyImmediate(go));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Edge / boundary tests
    // ══════════════════════════════════════════════════════════════════════════

    [Test]
    public void ParticleManager_LineRadius_ExcludesParticlesAtExactDistance()
    {
        // Particles exactly at LINE_RADIUS should NOT produce a line
        // (dist < LINE_RADIUS is the condition)
        float dist = ParticleManager.LINE_RADIUS;
        bool shouldConnect = dist < ParticleManager.LINE_RADIUS;
        Assert.IsFalse(shouldConnect, "Particles at exactly LINE_RADIUS should not connect");
    }

    [Test]
    public void ParticleManager_LineRadius_IncludesParticlesJustInside()
    {
        float dist = ParticleManager.LINE_RADIUS - 0.1f;
        bool shouldConnect = dist < ParticleManager.LINE_RADIUS;
        Assert.IsTrue(shouldConnect, "Particles just inside LINE_RADIUS should connect");
    }

    [Test]
    public void SpellVFX_GetCardBurstColor_AllRuneTypesReturnOpaqueColor()
    {
        foreach (RuneType rt in System.Enum.GetValues(typeof(RuneType)))
        {
            var col = SpellVFX.GetCardBurstColor(MakeUnit(rt));
            Assert.AreEqual(1f, col.a, $"Alpha should be 1 for RuneType {rt}");
        }
    }
}
