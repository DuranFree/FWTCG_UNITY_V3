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
            { CardKeyword.Ephemeral,  "瞬息" },
            { CardKeyword.Swift,      "迅捷" },
        };

        private static readonly Dictionary<CardKeyword, string> KeywordDescriptions = new Dictionary<CardKeyword, string>
        {
            { CardKeyword.Haste,      "可选支付[1]+[C]以活跃状态进场（Rule 717）" },
            { CardKeyword.Barrier,    "战斗中必须先承受致命伤害（Rule 727）" },
            { CardKeyword.SpellShield,"对手须支付符能才能将我选为目标（Rule 721）" },
            { CardKeyword.Inspire,    "本回合已打出过其他牌时触发（Rule 724）" },
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
            { CardKeyword.Ephemeral,  "下回合开始前销毁（Rule 728）" },
            { CardKeyword.Swift,      "可在法术对决期间打出（Rule 718）" },
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
                    sb.Append($"  符能: {cd.RuneCost} {cd.RuneType.ToChinese()}");
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

            // Runtime state (rich text: green=buff, gold=equip, red=debuff)
            if (_stateText != null)
            {
                _stateText.supportRichText = true;
                _stateText.text = BuildStateText(unit);
            }
        }

        /// <summary>Show detail for a non-unit item (battlefield, legend, etc).</summary>
        public void ShowSimple(string cardName, string description, Sprite art = null)
        {
            if (_panel == null) return;
            _panel.SetActive(true);

            if (_artImage != null)
            {
                if (art != null) { _artImage.sprite = art; _artImage.enabled = true; }
                else _artImage.enabled = false;
            }
            if (_nameText != null) _nameText.text = cardName;
            if (_costText != null) _costText.text = "[战场]";
            if (_atkText != null) _atkText.text = "";
            if (_keywordsText != null) _keywordsText.text = "";
            if (_effectText != null) _effectText.text = description;
            if (_stateText != null) _stateText.text = "";
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

        // hex constants for the three badge colors
        private const string C_BUFF  = "#4ade80"; // green  — buff
        private const string C_EQUIP = "#c8aa6e"; // gold   — equipment
        private const string C_DEBUF = "#f87171"; // red    — debuff

        private string BuildStateText(UnitInstance unit)
        {
            var sb = new StringBuilder();

            // ── Green: buff ───────────────────────────────────────────────────
            if (unit.HasBuff)
            {
                sb.AppendLine($"<color={C_BUFF}><b>▲ 强化</b></color>");
                foreach (var line in unit.BuildBuffSummary().Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"<color={C_BUFF}>  {line.Trim()}</color>");
            }

            // ── Gold: equipment ───────────────────────────────────────────────
            if (unit.AttachedEquipment != null)
            {
                sb.AppendLine($"<color={C_EQUIP}><b>▲ 装备</b></color>");
                foreach (var line in unit.BuildEquipSummary().Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"<color={C_EQUIP}>  {line.Trim()}</color>");
            }

            // ── Red: debuff ───────────────────────────────────────────────────
            if (unit.HasDebuff)
            {
                sb.AppendLine($"<color={C_DEBUF}><b>▼ 削弱</b></color>");
                foreach (var line in unit.BuildDebuffSummary().Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line))
                        sb.AppendLine($"<color={C_DEBUF}>  {line.Trim()}</color>");
            }

            // ── Plain state flags ─────────────────────────────────────────────
            if (unit.Exhausted)      sb.AppendLine("状态: 休眠");
            if (unit.HasSpellShield) sb.AppendLine("状态: 法盾 (激活)");
            if (unit.HasStrongAtk)   sb.AppendLine("状态: 强攻");
            if (unit.HasGuard)       sb.AppendLine("状态: 坚守");

            return sb.ToString().TrimEnd();
        }

    }
}
