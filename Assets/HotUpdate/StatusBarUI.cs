using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;

namespace Symbiosis.UI
{
    /// <summary>
    /// 状态栏 — 好感度 + 阶段 + 心情
    /// </summary>
    public class StatusBarUI : MonoBehaviour
    {
        public Slider favorSlider;
        public Text favorText;
        public Text stageText;
        public Text moodText;
        public Text characterNameText;

        private static readonly string[] STAGE_NAMES = { "陌生", "熟悉", "依赖", "亲密" };
        private static readonly int[] STAGE_MAX = { 50, 100, 200, 500 };

        public void Refresh()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            AutoFind();

            if (characterNameText != null)
                characterNameText.text = gm.CharacterName != null ? gm.CharacterName : "小星";

            int favor = gm.Favorability;
            int stageIdx = GetStageIndex(gm.FavorStage);
            int maxFavor = STAGE_MAX[Mathf.Clamp(stageIdx, 0, STAGE_MAX.Length - 1)];

            if (favorSlider != null)
            {
                favorSlider.maxValue = maxFavor;
                favorSlider.value = Mathf.Min(favor, maxFavor);
            }
            if (favorText != null)
                favorText.text = favor.ToString();
            if (stageText != null)
                stageText.text = STAGE_NAMES[Mathf.Clamp(stageIdx, 0, STAGE_NAMES.Length - 1)];
            if (moodText != null)
                moodText.text = gm.MoodLabel != null ? gm.MoodLabel : "平静";
        }

        private bool _found;
        private void AutoFind()
        {
            if (_found) return;
            _found = true;

            if (favorSlider == null)
                favorSlider = GetComponentInChildren<Slider>();
            if (favorText == null)
                favorText = FindText("FavorText");
            if (stageText == null)
                stageText = FindText("StageText");
            if (moodText == null)
                moodText = FindText("MoodText");
            if (characterNameText == null)
                characterNameText = FindText("CharacterName");
        }

        private Text FindText(string name)
        {
            var all = GetComponentsInChildren<Text>(true);
            foreach (var t in all)
            {
                if (t.gameObject.name == name)
                    return t;
            }
            return null;
        }

        private int GetStageIndex(string stageName)
        {
            if (stageName == "familiar") return 1;
            if (stageName == "dependent") return 2;
            if (stageName == "intimate") return 3;
            return 0;
        }
    }
}
