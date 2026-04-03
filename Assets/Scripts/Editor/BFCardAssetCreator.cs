using UnityEngine;
using UnityEditor;
using FWTCG.Data;
using FWTCG.Core;

namespace FWTCG.Editor
{
    /// <summary>
    /// DEV-32: Creates CardData .asset files for all 19 battlefield cards.
    /// BF cards have no cost / atk / keywords — they exist solely as data
    /// carriers so BattlefieldSystem can look them up by ID at runtime.
    /// </summary>
    public static class BFCardAssetCreator
    {
        private const string OUTPUT_PATH = "Assets/Resources/Cards/BF";

        [MenuItem("FWTCG/Create BF Card Assets")]
        public static void CreateAll()
        {
            // Ensure output folder exists
            if (!AssetDatabase.IsValidFolder(OUTPUT_PATH))
            {
                System.IO.Directory.CreateDirectory(
                    System.IO.Path.Combine(Application.dataPath,
                    "Resources/Cards/BF"));
                AssetDatabase.Refresh();
            }

            var cards = new (string id, string name, string desc)[]
            {
                ("altar_unity",        "团结祭坛",      "【据守】在基地召唤1/1新兵"),
                ("aspirant_climb",     "试炼者之阶",    "【据守】支付1法力，基地单位+1战力"),
                ("back_alley_bar",     "暗巷酒吧",      "【被动】移动离开时+1战力"),
                ("bandle_tree",        "班德尔城神树",  "【据守】场上≥3种特性+1法力"),
                ("hirana",             "希拉娜修道院",  "【征服】消耗增益指示物抽1牌"),
                ("reaver_row",         "掠夺者之街",    "【征服】从废牌堆捞费用≤2单位（OUT OF SCOPE）"),
                ("reckoner_arena",     "清算人竞技场",  "【被动】战力≥5自动获得强攻/坚守"),
                ("dreaming_tree",      "梦幻树",        "【被动】每回合首次法术抽1牌"),
                ("vile_throat_nest",   "卑鄙之喉的巢穴","【限制】此处单位禁止撤回基地"),
                ("rockfall_path",      "落岩之径",      "【限制】禁止直接出牌到此战场"),
                ("sunken_temple",      "沉没神庙",      "【防守失败】支付2法力抽1牌"),
                ("trifarian_warcamp",  "崔法利战营",    "【入场】获得增益指示物"),
                ("void_gate",          "虚空之门",      "【被动】法术伤害额外+1"),
                ("zaun_undercity",     "祖安地沟",      "【征服】弃1牌抽1牌"),
                ("strength_obelisk",   "力量方尖碑",    "【据守】额外召出1张符文"),
                ("star_peak",          "星尖峰",        "【据守】召出1枚休眠符文"),
                ("thunder_rune",       "雷霆之纹",      "【征服】回收1张符文"),
                ("ascending_stairs",   "攀圣长阶",      "【被动】据守/征服时额外+1分"),
                ("forgotten_monument", "遗忘丰碑",      "【被动】第三回合前无据守分"),
            };

            int created = 0;
            int skipped = 0;

            foreach (var (id, name, desc) in cards)
            {
                string assetPath = $"{OUTPUT_PATH}/{id}.asset";
                if (AssetDatabase.LoadAssetAtPath<CardData>(assetPath) != null)
                {
                    Debug.Log($"[BFCardAssetCreator] Skip (already exists): {id}");
                    skipped++;
                    continue;
                }

                var cd = ScriptableObject.CreateInstance<CardData>();
                cd.EditorSetup(
                    id:          id,
                    cardName:    name,
                    cost:        0,
                    atk:         0,
                    runeType:    RuneType.Blazing,
                    runeCost:    0,
                    description: desc
                );

                AssetDatabase.CreateAsset(cd, assetPath);
                created++;
                Debug.Log($"[BFCardAssetCreator] Created: {id} → {name}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[BFCardAssetCreator] Done — created {created}, skipped {skipped}.");
        }
    }
}
