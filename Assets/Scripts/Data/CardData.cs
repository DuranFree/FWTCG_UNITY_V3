using UnityEngine;

namespace FWTCG.Data
{
    [CreateAssetMenu(menuName = "FWTCG/CardData", fileName = "NewCardData")]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _id;
        [SerializeField] private string _cardName;

        [Header("Cost")]
        [SerializeField] private int _cost;
        [SerializeField] private RuneType _runeType;
        [SerializeField] private int _runeCost;

        [Header("Stats")]
        [SerializeField] private int _atk;

        [Header("Keywords")]
        [SerializeField] private CardKeyword _keywords;

        [Header("Effects")]
        [SerializeField] private string _effectId;   // onSummon / deathwish effect identifier

        [Header("Equipment (isEquipment=true only)")]
        [SerializeField] private bool _isEquipment;
        [SerializeField] private int _equipAtkBonus;
        [SerializeField] private RuneType _equipRuneType;
        [SerializeField] private int _equipRuneCost;

        [Header("Spell (isSpell=true only)")]
        [SerializeField] private bool _isSpell;
        [SerializeField] private SpellTargetType _spellTargetType;

        [Header("Display")]
        [SerializeField] [TextArea(2, 4)] private string _description;
        [SerializeField] private Sprite _artSprite;

        // ── Properties ─────────────────────────────────────────────────────
        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public RuneType RuneType => _runeType;
        public int RuneCost => _runeCost;
        public int Atk => _atk;
        public CardKeyword Keywords => _keywords;
        public string EffectId => _effectId;
        public bool IsEquipment => _isEquipment;
        public int EquipAtkBonus => _equipAtkBonus;
        public RuneType EquipRuneType => _equipRuneType;
        public int EquipRuneCost => _equipRuneCost;
        public bool IsSpell => _isSpell;
        public SpellTargetType SpellTargetType => _spellTargetType;
        public string Description => _description;
        public Sprite ArtSprite => _artSprite;

        public bool HasKeyword(CardKeyword kw) => (_keywords & kw) != 0;

#if UNITY_EDITOR
        public void EditorSetup(string id, string cardName, int cost, int atk,
                                RuneType runeType, int runeCost, string description,
                                CardKeyword keywords = CardKeyword.None,
                                string effectId = "",
                                bool isEquipment = false,
                                int equipAtkBonus = 0,
                                RuneType equipRuneType = RuneType.Blazing,
                                int equipRuneCost = 0,
                                bool isSpell = false,
                                SpellTargetType spellTargetType = SpellTargetType.None)
        {
            _id = id;
            _cardName = cardName;
            _cost = cost;
            _atk = atk;
            _runeType = runeType;
            _runeCost = runeCost;
            _description = description;
            _keywords = keywords;
            _effectId = effectId;
            _isEquipment = isEquipment;
            _equipAtkBonus = equipAtkBonus;
            _equipRuneType = equipRuneType;
            _equipRuneCost = equipRuneCost;
            _isSpell = isSpell;
            _spellTargetType = spellTargetType;
        }
#endif
    }
}
