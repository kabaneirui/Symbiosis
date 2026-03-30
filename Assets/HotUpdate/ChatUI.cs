using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;
using Symbiosis.Models;

namespace Symbiosis.UI
{
    /// <summary>
    /// 聊天界面 — 气泡列表 + 输入 + 送礼
    /// 支持两种模式：Inspector 拖拽引用（编辑器调试）或 Init() 自动查找（动态加载）
    /// </summary>
    public class ChatUI : MonoBehaviour
    {
        [Header("UI 引用（动态加载时自动查找）")]
        public ScrollRect scrollRect;
        public RectTransform contentRoot;
        public InputField inputField;
        public Button sendButton;
        public Button giftButton;
        public Text typingIndicator;

        [Header("气泡 Prefab")]
        public GameObject userBubblePrefab;
        public GameObject aiBubblePrefab;

        [Header("关联")]
        public StatusBarUI statusBar;

        private List<GameObject> _bubbles = new List<GameObject>();
        private bool _isSending;
        private GiftPanelUI _giftPanel;

        /// <summary>
        /// 动态加载时调用，自动从子节点查找 UI 组件
        /// </summary>
        public void Init()
        {
            if (scrollRect == null)
                scrollRect = GetComponentInChildren<ScrollRect>();
            if (scrollRect != null && contentRoot == null)
                contentRoot = scrollRect.content;
            if (inputField == null)
                inputField = GetComponentInChildren<InputField>();

            // 递归查找按钮（可能在子节点的子节点里）
            if (sendButton == null)
                sendButton = FindInChildren<Button>("SendButton");
            if (giftButton == null)
                giftButton = FindInChildren<Button>("GiftButton");

            // 递归查找打字指示器
            if (typingIndicator == null)
            {
                var t = FindInChildren<Text>("TypingIndicator");
                if (t != null) typingIndicator = t;
            }

            // 加载气泡 Prefab
            if (userBubblePrefab == null)
                userBubblePrefab = Resources.Load<GameObject>("UI/UserBubble");
            if (aiBubblePrefab == null)
                aiBubblePrefab = Resources.Load<GameObject>("UI/AIBubble");

            Debug.Log("ChatUI Init: send=" + (sendButton != null) + " gift=" + (giftButton != null)
                + " input=" + (inputField != null) + " content=" + (contentRoot != null));

            SetupListeners();
        }

        private void Start()
        {
            // 如果是 Inspector 拖拽模式，直接绑定
            if (sendButton != null)
                SetupListeners();

            if (typingIndicator != null)
                typingIndicator.gameObject.SetActive(false);
        }

        private bool _listenersSet;
        private void SetupListeners()
        {
            if (_listenersSet) return;
            _listenersSet = true;

            if (sendButton != null)
                sendButton.onClick.AddListener(OnSendClicked);
            if (giftButton != null)
                giftButton.onClick.AddListener(OnGiftClicked);
            if (inputField != null)
                inputField.onEndEdit.AddListener(OnInputEndEdit);
            if (typingIndicator != null)
                typingIndicator.gameObject.SetActive(false);
        }

        private void OnInputEndEdit(string text)
        {
            if (Input.GetKeyDown(KeyCode.Return) && !string.IsNullOrEmpty(text))
                OnSendClicked();
        }

        private async void OnSendClicked()
        {
            if (inputField == null) return;
            string message = inputField.text.Trim();
            if (string.IsNullOrEmpty(message) || _isSending) return;

            _isSending = true;
            inputField.text = "";
            if (sendButton != null) sendButton.interactable = false;

            AddBubble(message, true);
            ShowTyping(true);

            try
            {
                var gm = GameManager.Instance;
                var response = await gm.Api.Chat(gm.UserId, message);

                ShowTyping(false);
                AddBubble(response.reply, false);

                string oldStage = gm.FavorStage;
                gm.UpdateState(
                    response.favorability,
                    response.favor_stage,
                    response.mood,
                    response.expression
                );

                // 好感阶段变化通知
                if (oldStage != null && response.favor_stage != oldStage)
                {
                    string stageName = response.favor_stage == "familiar" ? "熟悉" :
                        response.favor_stage == "dependent" ? "依赖" :
                        response.favor_stage == "intimate" ? "亲密" : "";
                    if (stageName.Length > 0)
                        AddSystemMessage("关系升级！你们现在是「" + stageName + "」阶段了");
                }

                // 聊天获得心意提示
                AddSystemMessage("聊天 +5 心意");

                if (statusBar != null)
                    statusBar.Refresh();

                CheckEvents();
            }
            catch (System.Exception e)
            {
                ShowTyping(false);
                AddBubble("[连接失败] " + e.Message, false);
                Debug.LogError(e);
            }

            _isSending = false;
            if (sendButton != null) sendButton.interactable = true;
            if (inputField != null) inputField.ActivateInputField();
        }

        private async void CheckEvents()
        {
            try
            {
                var gm = GameManager.Instance;
                string json = await gm.Api.GetRaw("/events?user_id=" + gm.UserId);

                if (json.Contains("\"events\": []") || json.Contains("\"events\":[]"))
                    return;

                // 简单解析第一个事件
                string eventId = ExtractJsonValue(json, "id");
                string title = ExtractJsonValue(json, "title");
                string desc = ExtractJsonValue(json, "description");

                if (string.IsNullOrEmpty(eventId)) return;

                // 解析选择分支
                var choices = new System.Collections.Generic.List<EventChoice>();
                int choicesIdx = json.IndexOf("\"choices\":");
                if (choicesIdx > 0)
                {
                    string choicesStr = json.Substring(choicesIdx);
                    int pos = 0;
                    while (true)
                    {
                        int idIdx = choicesStr.IndexOf("\"id\":", pos);
                        if (idIdx < 0) break;
                        string cid = ExtractJsonValueFrom(choicesStr, "id", idIdx);
                        string ctext = ExtractJsonValueFrom(choicesStr, "text", idIdx);
                        if (!string.IsNullOrEmpty(cid) && !string.IsNullOrEmpty(ctext))
                        {
                            var c = new EventChoice();
                            c.id = cid;
                            c.text = ctext;
                            choices.Add(c);
                        }
                        pos = idIdx + 5;
                    }
                }

                ShowEventPanel(eventId, title, desc, choices);
            }
            catch (System.Exception e)
            {
                Debug.Log("事件检查: " + e.Message);
            }
        }

        private void ShowEventPanel(string eventId, string title, string desc,
            System.Collections.Generic.List<EventChoice> choices)
        {
            var panelGo = UIManager.Instance.Open("EventPanel");
            if (panelGo == null)
            {
                // 没有预制的 EventPanel，动态创建一个简易版
                panelGo = CreateDefaultEventPanel();
                panelGo.transform.SetParent(UIManager.Instance.uiRoot, false);
                panelGo.name = "EventPanel";
            }

            var panel = panelGo.GetComponent<EventPanelUI>();
            if (panel == null)
                panel = panelGo.AddComponent<EventPanelUI>();

            panel.Init(this, statusBar);
            panel.ShowEvent(eventId, title, desc, choices);
        }

        private GameObject CreateDefaultEventPanel()
        {
            var go = new GameObject("EventPanel", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.2f);
            rt.anchorMax = new Vector2(0.9f, 0.8f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.25f, 0.98f);

            var title = new GameObject("Title", typeof(RectTransform), typeof(Text));
            title.transform.SetParent(go.transform, false);
            var titleTxt = title.GetComponent<Text>();
            titleTxt.text = "事件";
            titleTxt.fontSize = 24;
            titleTxt.color = Color.white;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var trt = title.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.8f);
            trt.anchorMax = new Vector2(1, 1f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            var desc = new GameObject("Description", typeof(RectTransform), typeof(Text));
            desc.transform.SetParent(go.transform, false);
            var descTxt = desc.GetComponent<Text>();
            descTxt.text = "";
            descTxt.fontSize = 16;
            descTxt.color = new Color(0.8f, 0.8f, 0.85f);
            descTxt.alignment = TextAnchor.UpperCenter;
            descTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var drt = desc.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.05f, 0.35f);
            drt.anchorMax = new Vector2(0.95f, 0.78f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;

            var choiceArea = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choiceArea.transform.SetParent(go.transform, false);
            var vlg = choiceArea.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            var crt = choiceArea.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.1f, 0.05f);
            crt.anchorMax = new Vector2(0.9f, 0.35f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;

            var closeBtn = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(go.transform, false);
            closeBtn.GetComponent<Image>().color = new Color(0.7f, 0.3f, 0.3f);
            var cbrt = closeBtn.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(0.85f, 0.88f);
            cbrt.anchorMax = new Vector2(0.98f, 0.98f);
            cbrt.offsetMin = Vector2.zero;
            cbrt.offsetMax = Vector2.zero;
            var cbTxt = new GameObject("Text", typeof(RectTransform), typeof(Text));
            cbTxt.transform.SetParent(closeBtn.transform, false);
            var cbtxt = cbTxt.GetComponent<Text>();
            cbtxt.text = "X";
            cbtxt.fontSize = 18;
            cbtxt.color = Color.white;
            cbtxt.alignment = TextAnchor.MiddleCenter;
            cbtxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var cbtrt = cbTxt.GetComponent<RectTransform>();
            cbtrt.anchorMin = Vector2.zero;
            cbtrt.anchorMax = Vector2.one;
            cbtrt.offsetMin = Vector2.zero;
            cbtrt.offsetMax = Vector2.zero;

            return go;
        }

        private string ExtractJsonValue(string json, string key)
        {
            return ExtractJsonValueFrom(json, key, 0);
        }

        private string ExtractJsonValueFrom(string json, string key, int searchFrom)
        {
            string pattern = "\"" + key + "\": \"";
            int idx = json.IndexOf(pattern, searchFrom);
            if (idx < 0)
            {
                pattern = "\"" + key + "\":\"";
                idx = json.IndexOf(pattern, searchFrom);
            }
            if (idx < 0) return null;
            int start = idx + pattern.Length;
            int end = json.IndexOf("\"", start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        private void OnGiftClicked()
        {
            // 打开商店（替代简单礼物面板）
            var go = CreateShopPanel();
            go.transform.SetParent(UIManager.Instance.uiRoot, false);
            go.name = "ShopPanel";

            var shop = go.AddComponent<ShopUI>();
            shop.Init(this, statusBar);
        }

        private GameObject CreateShopPanel()
        {
            var go = new GameObject("ShopPanel", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.02f, 0.05f);
            rt.anchorMax = new Vector2(0.98f, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.22f, 0.98f);

            // 标题 + 心意
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(go.transform, false);
            SetAnchors(header, 0, 0.9f, 1, 1);

            var title = MakeText(header.transform, "Title", "商店", 22, Color.white);
            SetAnchors(title, 0.05f, 0, 0.3f, 1);

            var coins = MakeText(header.transform, "CoinsText", "0 心意", 16, new Color(1f, 0.85f, 0.3f));
            SetAnchors(coins, 0.35f, 0, 0.65f, 1);

            var loginTxt = MakeText(header.transform, "LoginText", "", 13, new Color(0.5f, 0.8f, 0.5f));
            SetAnchors(loginTxt, 0.05f, -0.5f, 0.7f, 0);

            // 关闭按钮
            var closeBtn = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(header.transform, false);
            closeBtn.GetComponent<Image>().color = new Color(0.7f, 0.3f, 0.3f);
            SetAnchors(closeBtn, 0.88f, 0.15f, 0.98f, 0.85f);
            MakeText(closeBtn.transform, "X", "X", 18, Color.white);

            // 标签按钮
            var taskTab = new GameObject("TaskTab", typeof(RectTransform), typeof(Image), typeof(Button));
            taskTab.transform.SetParent(header.transform, false);
            taskTab.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f);
            SetAnchors(taskTab, 0.7f, 0.15f, 0.85f, 0.85f);
            MakeText(taskTab.transform, "TabText", "任务", 13, Color.white);

            // 商店内容区
            var shopContent = new GameObject("ShopContent", typeof(RectTransform));
            shopContent.transform.SetParent(go.transform, false);
            SetAnchors(shopContent, 0, 0.05f, 1, 0.88f);

            // 推荐标题
            MakeText(shopContent.transform, "RecTitle", "她可能喜欢", 15, new Color(0.7f, 0.8f, 1f));

            // 推荐网格
            var recGrid = new GameObject("RecommendGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            recGrid.transform.SetParent(shopContent.transform, false);
            var rg = recGrid.GetComponent<GridLayoutGroup>();
            rg.cellSize = new Vector2(100, 90);
            rg.spacing = new Vector2(8, 8);
            rg.childAlignment = TextAnchor.UpperLeft;
            var rgrt = recGrid.GetComponent<RectTransform>();
            rgrt.anchorMin = new Vector2(0.02f, 0.55f);
            rgrt.anchorMax = new Vector2(0.98f, 0.9f);
            rgrt.offsetMin = Vector2.zero;
            rgrt.offsetMax = Vector2.zero;

            // 每日精选标题
            var dailyTitle = MakeText(shopContent.transform, "DailyTitle", "每日精选 (7折)", 15, new Color(1f, 0.7f, 0.3f));
            var dtrt = dailyTitle.GetComponent<RectTransform>();
            dtrt.anchorMin = new Vector2(0.02f, 0.45f);
            dtrt.anchorMax = new Vector2(0.5f, 0.55f);
            dtrt.offsetMin = Vector2.zero;
            dtrt.offsetMax = Vector2.zero;

            // 每日网格
            var dailyGrid = new GameObject("DailyGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            dailyGrid.transform.SetParent(shopContent.transform, false);
            var dg = dailyGrid.GetComponent<GridLayoutGroup>();
            dg.cellSize = new Vector2(100, 90);
            dg.spacing = new Vector2(8, 8);
            dg.childAlignment = TextAnchor.UpperLeft;
            var dgrt = dailyGrid.GetComponent<RectTransform>();
            dgrt.anchorMin = new Vector2(0.02f, 0.1f);
            dgrt.anchorMax = new Vector2(0.98f, 0.45f);
            dgrt.offsetMin = Vector2.zero;
            dgrt.offsetMax = Vector2.zero;

            // 神秘盒子按钮
            var mystery = new GameObject("MysteryButton", typeof(RectTransform), typeof(Image), typeof(Button));
            mystery.transform.SetParent(shopContent.transform, false);
            mystery.GetComponent<Image>().color = new Color(0.5f, 0.3f, 0.7f);
            var mrt = mystery.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.3f, 0.01f);
            mrt.anchorMax = new Vector2(0.7f, 0.09f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;
            MakeText(mystery.transform, "MText", "神秘盒子 (30心意)", 14, Color.white);

            // 任务内容区（默认隐藏）
            var taskContent = new GameObject("TaskContent", typeof(RectTransform));
            taskContent.transform.SetParent(go.transform, false);
            SetAnchors(taskContent, 0, 0.05f, 1, 0.88f);
            taskContent.SetActive(false);

            var taskList = new GameObject("TaskList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            taskList.transform.SetParent(taskContent.transform, false);
            var tlg = taskList.GetComponent<VerticalLayoutGroup>();
            tlg.spacing = 6;
            tlg.childControlWidth = true;
            tlg.childControlHeight = false;
            tlg.childForceExpandWidth = true;
            tlg.padding = new RectOffset(10, 10, 10, 10);
            SetAnchors(taskList, 0, 0, 1, 1);

            return go;
        }

        private void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private GameObject MakeText(Transform parent, string name, string text, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        public void AddBubble(string text, bool isUser)
        {
            var prefab = isUser ? userBubblePrefab : aiBubblePrefab;
            if (prefab == null || contentRoot == null)
            {
                Debug.LogWarning("气泡 Prefab 或 contentRoot 未设置");
                Debug.LogWarning("气泡 Prefab 或 contentRoot 未设置，消息: " + text);
                return;
            }

            var bubble = Instantiate(prefab, contentRoot);
            var label = bubble.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;

            // AI 气泡加载头像
            if (!isUser)
            {
                var avatar = bubble.transform.Find("Avatar");
                if (avatar != null)
                {
                    var avatarImg = avatar.GetComponent<Image>();
                    var sprite = Resources.Load<Sprite>("Head/wusaqi");
                    if (avatarImg != null && sprite != null)
                        avatarImg.sprite = sprite;
                }
            }

            _bubbles.Add(bubble);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 0f;
        }

        private void ShowTyping(bool show)
        {
            if (typingIndicator != null)
                typingIndicator.gameObject.SetActive(show);
        }

        public void AddAIMessage(string text)
        {
            AddBubble(text, false);
        }

        public void AddSystemMessage(string text)
        {
            if (contentRoot == null) return;

            var go = new GameObject("SystemMsg", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(contentRoot, false);
            var t = go.GetComponent<Text>();
            t.text = "--- " + text + " ---";
            t.fontSize = 13;
            t.color = new Color(0.6f, 0.75f, 0.4f);
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 25;

            _bubbles.Add(go);
            Canvas.ForceUpdateCanvases();
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
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
    }
}
