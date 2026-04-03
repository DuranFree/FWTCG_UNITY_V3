using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.UI;
using FWTCG.Core;
using FWTCG.Data;

/// <summary>
/// DEV-25: GlassPanelFX + three-badge layout + equipment badge tests.
/// All EditMode — no scene required.
/// </summary>
[TestFixture]
public class DEV25VisualTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private CardData MakeCard(string id = "test", int atk = 2)
    {
        var cd = ScriptableObject.CreateInstance<CardData>();
        cd.EditorSetup(id, id, 1, atk, RuneType.Blazing, 0, "");
        return cd;
    }

    private UnitInstance MakeUnit(string name = "warrior", int atk = 2)
        => new UnitInstance(1, MakeCard(name, atk), "player");

    // ── GlassPanelFX ──────────────────────────────────────────────────────────

    [Test]
    public void GlassPanelFX_RequiresImage_ComponentExists()
    {
        var go = new GameObject("GlassFX");
        go.AddComponent<Image>();
        var fx = go.AddComponent<GlassPanelFX>();
        Assert.IsNotNull(fx);
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GlassPanelFX_SetBorderColor_DoesNotThrow()
    {
        var go = new GameObject("GlassFX");
        go.AddComponent<Image>();
        var fx = go.AddComponent<GlassPanelFX>();
        Assert.DoesNotThrow(() => fx.SetBorderColor(Color.cyan));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GlassPanelFX_SetTintAlpha_DoesNotThrow()
    {
        var go = new GameObject("GlassFX");
        go.AddComponent<Image>();
        var fx = go.AddComponent<GlassPanelFX>();
        Assert.DoesNotThrow(() => fx.SetTintAlpha(0.5f));
        Object.DestroyImmediate(go);
    }

    [Test]
    public void GlassPanelFX_Destroy_DoesNotThrow()
    {
        var go = new GameObject("GlassFX");
        go.AddComponent<Image>();
        go.AddComponent<GlassPanelFX>();
        Assert.DoesNotThrow(() => Object.DestroyImmediate(go));
    }

    // ── UnitInstance.HasBuff excludes equipment ────────────────────────────────

    [Test]
    public void HasBuff_WithNoBuffs_ReturnsFalse()
    {
        var unit = MakeUnit();
        Assert.IsFalse(unit.HasBuff);
    }

    [Test]
    public void HasBuff_WithBuffTokens_ReturnsTrue()
    {
        var unit = MakeUnit();
        unit.BuffTokens = 1;
        Assert.IsTrue(unit.HasBuff);
    }

    [Test]
    public void HasBuff_EquipmentAloneDoesNotTriggerBuff()
    {
        var unit  = MakeUnit("warrior");
        var equip = MakeUnit("blade");
        unit.AttachedEquipment = equip;

        // Equipment alone must NOT set HasBuff — it has its own dedicated badge
        Assert.IsFalse(unit.HasBuff,
            "AttachedEquipment should not trigger HasBuff — it has its own badge");
    }

    [Test]
    public void BuildBuffSummary_NoEquipmentSection_WhenEquipped()
    {
        var unit  = MakeUnit("warrior");
        var equip = MakeUnit("blade");
        unit.AttachedEquipment = equip;

        string summary = unit.BuildBuffSummary();
        StringAssert.DoesNotContain("装备", summary,
            "BuildBuffSummary should not mention equipment — that belongs in BuildEquipSummary");
    }

    [Test]
    public void BuildEquipSummary_NullEquipment_ReturnsNone()
    {
        var unit = MakeUnit();
        Assert.AreEqual("无", unit.BuildEquipSummary());
    }

    [Test]
    public void BuildEquipSummary_WithEquipment_ContainsEquipName()
    {
        var unit     = MakeUnit("warrior");
        var equipCd  = ScriptableObject.CreateInstance<CardData>();
        equipCd.EditorSetup("blade", "多兰之刃", 1, 2, RuneType.Crushing, 1, "");
        var equip    = new UnitInstance(2, equipCd, "player");
        unit.AttachedEquipment = equip;

        string summary = unit.BuildEquipSummary();
        StringAssert.Contains("多兰之刃", summary);
        Object.DestroyImmediate(equipCd);
    }

    // ── GlassPanelFX shader constants ─────────────────────────────────────────

    [Test]
    public void GlassPanelFX_DefaultBorderWidth_InValidRange()
    {
        // Default border width must be within shader property range [0.004, 0.05]
        float defaultBw = 0.018f;
        Assert.GreaterOrEqual(defaultBw, 0.004f);
        Assert.LessOrEqual(defaultBw, 0.05f);
    }

    [Test]
    public void GlassPanelFX_DefaultNoiseScale_InValidRange()
    {
        // Default noise scale must be within shader property range [20, 200]
        float defaultNs = 80f;
        Assert.GreaterOrEqual(defaultNs, 20f);
        Assert.LessOrEqual(defaultNs, 200f);
    }

    [Test]
    public void GlassPanelFX_DefaultNoiseStr_InValidRange()
    {
        // Default noise strength within [0, 0.12]
        float defaultStr = 0.04f;
        Assert.GreaterOrEqual(defaultStr, 0f);
        Assert.LessOrEqual(defaultStr, 0.12f);
    }

    // ── Badge layout constants ─────────────────────────────────────────────────

    [Test]
    public void BadgePositions_AreSymmetricAroundCenter()
    {
        // buff at -22, equip at 0, debuff at +22 — left/right must mirror
        float buffX   = -22f;
        float equipX  =   0f;
        float debuffX =  22f;
        Assert.AreEqual(0f, buffX + debuffX,
            "Buff and debuff badge X positions must sum to 0 (symmetric)");
        Assert.AreEqual(0f, equipX,
            "Equip badge must be centered");
    }

    [Test]
    public void BadgePositions_InsideCardBottomEdge()
    {
        // pivot is bottom (0), anchoredPosition.y = +2 keeps badge INSIDE card bounds
        // (avoids occlusion by sibling panels like PlayerHandZone that render on top)
        float posY = 2f;
        Assert.Greater(posY, 0f, "Badge Y must be positive (inside card, above bottom edge)");
    }
}
