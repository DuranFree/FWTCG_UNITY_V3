using UnityEngine;

namespace FWTCG.UI
{
    /// <summary>
    /// VFX-7g: Card back variant manager with PlayerPrefs persistence.
    /// Provides sprites for face-down card backs. Add new variants by extending the enum
    /// and placing corresponding sprites in Resources/CardArt/.
    /// </summary>
    public static class CardBackManager
    {
        public enum CardBackVariant
        {
            Default = 0,   // card_back_01 (geometric overlay — current fallback)
            Back01  = 1,   // card_back_01.png sprite
        }

        private const string PREF_KEY = "FWTCG_CardBack";
        private static CardBackVariant _current = CardBackVariant.Back01;
        private static bool _loaded;
        private static Sprite _cachedSprite;

        public static CardBackVariant Current
        {
            get
            {
                if (!_loaded) Load();
                return _current;
            }
        }

        public static void SetPlayerCardBack(CardBackVariant variant)
        {
            _current = variant;
            _loaded = true;
            _cachedSprite = null; // force reload
            PlayerPrefs.SetInt(PREF_KEY, (int)variant);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns the sprite for the current card back, or null if Default (use geometric overlay).
        /// </summary>
        public static Sprite GetCardBackSprite()
        {
            if (!_loaded) Load();
            if (_current == CardBackVariant.Default) return null;
            if (_cachedSprite != null) return _cachedSprite;

            string path = _current switch
            {
                CardBackVariant.Back01 => "CardArt/card_back_01",
                _ => null
            };

            if (path != null)
                _cachedSprite = Resources.Load<Sprite>(path);

            return _cachedSprite;
        }

        private static void Load()
        {
            _loaded = true;
            // Always use Back01 sprite — ignore PlayerPrefs legacy value
            _current = CardBackVariant.Back01;
        }

        /// <summary>Test hook: reset cached state to Default variant.</summary>
        public static void ResetForTest()
        {
            _loaded = true;  // prevent Load() from overriding the reset
            _current = CardBackVariant.Default;
            _cachedSprite = null;
        }
    }
}
