using UnityEngine;

namespace FWTCG.Data
{
    [CreateAssetMenu(menuName = "FWTCG/CardData", fileName = "NewCardData")]
    public class CardData : ScriptableObject
    {
        [SerializeField] private string _id;
        [SerializeField] private string _cardName;
        [SerializeField] private int _cost;
        [SerializeField] private int _atk;
        [SerializeField] private RuneType _runeType;
        [SerializeField] private int _runeCost;
        [SerializeField] [TextArea(2, 4)] private string _description;
        [SerializeField] private Sprite _artSprite;

        public string Id => _id;
        public string CardName => _cardName;
        public int Cost => _cost;
        public int Atk => _atk;
        public RuneType RuneType => _runeType;
        public int RuneCost => _runeCost;
        public string Description => _description;
        public Sprite ArtSprite => _artSprite;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only helper to set fields when creating CardData via script.
        /// </summary>
        public void EditorSetup(string id, string cardName, int cost, int atk,
                                RuneType runeType, int runeCost, string description)
        {
            _id = id;
            _cardName = cardName;
            _cost = cost;
            _atk = atk;
            _runeType = runeType;
            _runeCost = runeCost;
            _description = description;
        }
#endif
    }
}
