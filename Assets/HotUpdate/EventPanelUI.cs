using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;
using Symbiosis.Models;

namespace Symbiosis.UI
{
    /// <summary>
    /// 事件弹窗 — 显示事件描述 + 选择分支
    /// </summary>
    public class EventPanelUI : MonoBehaviour
    {
        private Text _titleText;
        private Text _descText;
        private Transform _choiceRoot;
        private Button _closeButton;
        private ChatUI _chatUI;
        private StatusBarUI _statusBar;

        private string _eventId;

        public void Init(ChatUI chatUI, StatusBarUI statusBar)
        {
            _chatUI = chatUI;
            _statusBar = statusBar;

            _titleText = FindText("Title");
            _descText = FindText("Description");
            _closeButton = FindInChildren<Button>("CloseButton");

            var layout = GetComponentInChildren<VerticalLayoutGroup>();
            if (layout != null)
                _choiceRoot = layout.transform;

            if (_closeButton != null)
                _closeButton.onClick.AddListener(Close);
        }

        public void ShowEvent(string eventId, string title, string description, List<EventChoice> choices)
        {
            _eventId = eventId;
            gameObject.SetActive(true);

            if (_titleText != null) _titleText.text = title;
            if (_descText != null) _descText.text = description;

            if (choices != null && choices.Count > 0 && _choiceRoot != null)
            {
                foreach (var choice in choices)
                {
                    var btnGo = CreateChoiceButton(choice.text);
                    string choiceId = choice.id;
                    btnGo.GetComponent<Button>().onClick.AddListener(
                        delegate { OnChoiceSelected(choiceId); }
                    );
                }
            }
            else
            {
                // 无选择分支，直接触发
                CompleteEvent(null);
            }
        }

        private async void OnChoiceSelected(string choiceId)
        {
            await CompleteEvent(choiceId);
        }

        private async System.Threading.Tasks.Task CompleteEvent(string choiceId)
        {
            try
            {
                var gm = GameManager.Instance;
                string url = gm.Api.ServerUrl + "/events/complete";

                string json = JsonUtility.ToJson(new EventCompleteReq
                {
                    user_id = gm.UserId,
                    event_id = _eventId,
                    choice_id = choiceId != null ? choiceId : ""
                });

                var response = await gm.Api.PostRaw("/events/complete", json);

                if (response != null)
                {
                    gm.RefreshState();
                    if (_chatUI != null)
                    {
                        string reply = ParseReply(response);
                        if (!string.IsNullOrEmpty(reply))
                            _chatUI.AddAIMessage("[事件] " + reply);
                    }
                    if (_statusBar != null)
                        _statusBar.Refresh();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("完成事件失败: " + e.Message);
            }

            Close();
        }

        private string ParseReply(string json)
        {
            // 简单提取 reply 字段
            int idx = json.IndexOf("\"reply\":");
            if (idx < 0) return "";
            int start = json.IndexOf("\"", idx + 8) + 1;
            int end = json.IndexOf("\"", start);
            if (start > 0 && end > start)
                return json.Substring(start, end - start);
            return "";
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private GameObject CreateChoiceButton(string text)
        {
            var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(_choiceRoot, false);
            go.GetComponent<Image>().color = new Color(0.4f, 0.6f, 1f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(260, 45);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var txt = textGo.GetComponent<Text>();
            txt.text = text;
            txt.fontSize = 16;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var txtRt = textGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = Vector2.zero;
            txtRt.offsetMax = Vector2.zero;

            return go;
        }

        private Text FindText(string name)
        {
            var all = GetComponentsInChildren<Text>(true);
            foreach (var t in all)
                if (t.gameObject.name == name) return t;
            return null;
        }

        private T FindInChildren<T>(string name) where T : Component
        {
            var all = GetComponentsInChildren<T>(true);
            foreach (var c in all)
                if (c.gameObject.name == name) return c;
            return null;
        }
    }

    [System.Serializable]
    public class EventChoice
    {
        public string id;
        public string text;
    }

    [System.Serializable]
    public class EventCompleteReq
    {
        public int user_id;
        public string event_id;
        public string choice_id;
    }
}
