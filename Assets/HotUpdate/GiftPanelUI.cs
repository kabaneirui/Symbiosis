using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;
using Symbiosis.Models;

namespace Symbiosis.UI
{
    /// <summary>
    /// 礼物面板 — 网格布局 + 送礼 + 反馈
    /// </summary>
    public class GiftPanelUI : MonoBehaviour
    {
        public Transform gridRoot;
        public Button closeButton;
        public Text feedbackText;
        public ChatUI chatUI;
        public StatusBarUI statusBar;

        private GameObject _giftItemPrefab;
        private bool _loaded;
        private bool _isSending;

        public void Init()
        {
            if (gridRoot == null)
            {
                var grid = GetComponentInChildren<GridLayoutGroup>();
                if (grid != null) gridRoot = grid.transform;
            }
            if (closeButton == null)
                closeButton = FindInChildren<Button>("CloseButton");
            if (feedbackText == null)
            {
                var t = transform.Find("FeedbackText");
                if (t != null) feedbackText = t.GetComponent<Text>();
            }

            _giftItemPrefab = Resources.Load<GameObject>("UI/GiftItem");

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
            if (feedbackText != null)
                feedbackText.text = "";

            LoadGifts();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (feedbackText != null)
                feedbackText.text = "";
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private async void LoadGifts()
        {
            if (_loaded) return;

            try
            {
                var gm = GameManager.Instance;
                var response = await gm.Api.GetGifts();

                foreach (var gift in response.gifts)
                {
                    GameObject item;
                    if (_giftItemPrefab != null)
                    {
                        item = Instantiate(_giftItemPrefab, gridRoot);
                    }
                    else
                    {
                        item = CreateDefaultGiftItem(gridRoot);
                    }

                    var texts = item.GetComponentsInChildren<Text>();
                    if (texts.Length >= 2)
                    {
                        texts[0].text = gift.name;
                        texts[1].text = gift.cost.ToString();
                    }
                    else if (texts.Length == 1)
                    {
                        texts[0].text = gift.name + "\n" + gift.cost;
                    }

                    var btn = item.GetComponent<Button>();
                    if (btn == null) btn = item.AddComponent<Button>();

                    string giftId = gift.id;
                    string giftName = gift.name;
                    btn.onClick.AddListener(delegate { OnGiftSelected(giftId, giftName); });
                }

                _loaded = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError("加载礼物列表失败: " + e.Message);
            }
        }

        private async void OnGiftSelected(string giftId, string giftName)
        {
            if (_isSending) return;
            _isSending = true;

            if (feedbackText != null)
                feedbackText.text = "正在送出" + giftName + "...";

            try
            {
                var gm = GameManager.Instance;
                var response = await gm.Api.SendGift(gm.UserId, giftId);

                if (feedbackText != null)
                    feedbackText.text = "好感 +" + response.favor_delta;

                gm.UpdateState(
                    response.favorability,
                    response.favor_stage,
                    response.mood,
                    response.expression
                );

                if (statusBar != null)
                    statusBar.Refresh();
                if (chatUI != null)
                    chatUI.AddAIMessage(response.reply);

                Hide();
            }
            catch (System.Exception e)
            {
                if (feedbackText != null)
                    feedbackText.text = "送礼失败";
                Debug.LogError("送礼失败: " + e.Message);
            }

            _isSending = false;
        }

        private T FindInChildren<T>(string name) where T : Component
        {
            var all = GetComponentsInChildren<T>(true);
            foreach (var c in all)
            {
                if (c.gameObject.name == name)
                    return c;
            }
            return null;
        }

        private GameObject CreateDefaultGiftItem(Transform parent)
        {
            var go = new GameObject("GiftItem", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var txt = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txt.transform.SetParent(go.transform, false);
            var text = txt.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 14;
            return go;
        }
    }
}
