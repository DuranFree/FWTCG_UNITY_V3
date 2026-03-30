using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using FWTCG.Core;
using FWTCG.Data;

namespace FWTCG.UI
{
    /// <summary>
    /// Fullscreen overlay showing detailed card information.
    /// Triggered by right-click on any visible card.
    /// Close by clicking the dimmed background or pressing Escape.
    /// </summary>
    public class CardDetailPopup : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Image _artImage;
        [SerializeField] private Text _nameText;
        [SerializeField] private Text _costText;
        [SerializeField] private Text _atkText;
        [SerializeField] private Text _keywordsText;
        [SerializeField] private Text _effectText;
        [SerializeField] private Text _stateText;
        [SerializeField] private Button _closeButton;

        // ── Keyword display names ─────────────────────────────────────────────
        private static readonly Dictionary<CardKeyword, string> KeywordNames = new Dictionary<CardKeyword, string>
        {
            { CardKeyword.Haste,      "急速" },
            { CardKeyword.Barrier,    "壁垒" },
            { CardKeyword.SpellShield,"法盾" },
            { CardKeyword.Inspire,    "鼓舞" },
            { CardKeyword.Conquest,   "征服" },
            { CardKeyword.Deathwish,  "绝念" },
            { CardKeyword.Reactive,   "反应" },
            { CardKeyword.StrongAtk,  "强攻" },
            { CardKeyword.Roam,       "游走" },
            { CardKeyword.Foresight,  "预知" },
            { CardKeyword.Standby,    "待命" },
            { CardKeyword.Stun,       "眩晕" },
            { CardKeyword.Echo,       "回响" },
            { CardKeyword.Guard,      "坚守" },
        };

        private static readonly Dictionary<CardKeyword, string> KeywordDescriptions = new Dictionary<CardKeyword, string>
        {
            { CardKeyword.Haste,      "进场不休眠，可立即行动" },
            { CardKeyword.Barrier,    "抵挡一次伤害" },
            { CardKeyword.SpellShield,"免疫指向法术" },
            { CardKeyword.Inspire,    "后续友方单位费用-1" },
            { CardKeyword.Conquest,   "征服战场时触发效果" },
            { CardKeyword.Deathwish,  "阵亡时触发效果" },
            { CardKeyword.Reactive,   "可作为反应牌打出" },
            { CardKeyword.StrongAtk,  "+1 进攻战力" },
            { CardKeyword.Roam,       "可在战场间移动" },
            { CardKeyword.Foresight,  "进场时查看牌堆顶" },
            { CardKeyword.Standby,    "面朝下部署，0费反应" },
            { CardKeyword.Stun,       "目标无法贡献战力" },
            { CardKeyword.Echo,       "本回合可再次施放" },
            { CardKeyword.Guard,      "+1 防御战力" },
        };

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
            if (_panel != null)
                _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(Hide);
        }

        private void Update()
        {
            if (_panel != null && _panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        public void Show(UnitInstance unit)
        {
            if (unit == null || _panel == null) return;

            CardData cd = unit.CardData;
            _panel.SetActive(true);

            // Art
            if (_artImage != null)
            {
                if (cd.ArtSprite != null)
                {
                    _artImage.sprite = cd.ArtSprite;
                    _artImage.enabled = true;
                }
                else
                {
                    _artImage.enabled = false;
                }
            }

            // Name
            if (_nameText != null)
                _nameText.text = cd.CardName;

            // Cost & Type
            if (_costText != null)
            {
                var sb = new StringBuilder();
                sb.Append($"费用: {cd.Cost}");
                if (cd.RuneCost > 0)
                    sb.Append($"  符能: {cd.RuneCost} {RuneTypeName(cd.RuneType)}");
                if (cd.IsSpell)
                    sb.Append("  [法术]");
                else if (cd.IsEquipment)
                    sb.Append("  [装备]");
                else
                    sb.Append("  [单位]");
                _costText.text = sb.ToString();
            }

            // ATK/HP
            if (_atkText != null)
            {
                if (cd.IsSpell)
                    _atkText.text = "";
                else if (cd.IsEquipment)
                    _atkText.text = $"装备加成: +{cd.EquipAtkBonus}";
                else
                    _atkText.text = $"基础战力: {cd.Atk}    当前: {unit.CurrentAtk}/{unit.CurrentHp}";
            }

            // Keywords
            if (_keywordsText != null)
                _keywordsText.text = BuildKeywordsText(cd.Keywords);

            // Effect
            if (_effectText != null)
                _effectText.text = string.IsNullOrEmpty(cd.Description) ? "" : cd.Description;

            // Runtime state
            if (_stateText != null)
                _stateText.text = BuildStateText(unit);
        }

        public void Hide()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        public bool IsVisible => _panel != null && _panel.activeSelf;

        private string BuildKeywordsText(CardKeyword keywords)
        {
            if (keywords == CardKeyword.None) return "";

            var sb = new StringBuilder();
            foreach (var kv in KeywordNames)
            {
                if ((keywords & kv.Key) != 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append($"[{kv.Value}]");
                    if (KeywordDescriptions.TryGetValue(kv.Key, out string desc))
                        sb.Append($" {desc}");
                }
            }
            return sb.ToString();
        }

        private string BuildStateText(UnitInstance unit)
        {
            var sb = new StringBuilder();
            if (unit.Exhausted) sb.AppendLine("状态: 休眠");
            if (unit.Stunned) sb.AppendLine("状态: 眩晕");
            if (unit.BuffTokens > 0) sb.AppendLine($"增益指示物: +{unit.BuffTokens}/+{unit.BuffTokens}");
            if (unit.TempAtkBonus != 0) sb.AppendLine($"临时战力加成: +{unit.TempAtkBonus}");
            if (unit.HasSpellShield) sb.AppendLine("状态: 法盾 (激活)");
            if (unit.HasStrongAtk) sb.AppendLine("状态: 强攻");
            if (unit.HasGuard) sb.AppendLine("状态: 坚守");
            return sb.ToString();
        }

        private string RuneTypeName(RuneType rt)
        {
            switch (rt)
            {
                case RuneType.Blazing:  return "炽烈";
                case RuneType.Radiant:  return "灵光";
                case RuneType.Verdant:  return "翠意";
                case RuneType.Crushing: return "摧破";
                case RuneType.Chaos:    return "混沌";
                case RuneType.Order:    return "秩序";
                default: return rt.ToString();
            }
        }
    }
}
