using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;
using Symbiosis.Models;

namespace Symbiosis.UI
{
    public class ShopUI : MonoBehaviour
    {
        private Text _coinsText;
        private Text _loginText;
        private Transform _recommendRoot;
        private Transform _dailyRoot;
        private Transform _taskRoot;
        private Button _mysteryButton;
        private Button _closeButton;
        private Button _taskTabButton;
        private GameObject _shopPanel;
        private GameObject _taskPanel;
        private ChatUI _chatUI;
        private StatusBarUI _statusBar;

        public void Init(ChatUI chatUI, StatusBarUI statusBar)
        {
            _chatUI = chatUI;
            _statusBar = statusBar;

            _coinsText = FindText("CoinsText");
            _loginText = FindText("LoginText");
            _closeButton = FindInChildren<Button>("CloseButton");
            _mysteryButton = FindInChildren<Button>("MysteryButton");
            _taskTabButton = FindInChildren<Button>("TaskTab");

            // 找到推荐和每日区域
            _recommendRoot = FindTransform("RecommendGrid");
            _dailyRoot = FindTransform("DailyGrid");
            _taskRoot = FindTransform("TaskList");

            _shopPanel = FindGO("ShopContent");
            _taskPanel = FindGO("TaskContent");

            if (_closeButton != null) _closeButton.onClick.AddListener(Close);
            if (_mysteryButton != null) _mysteryButton.onClick.AddListener(OnMysteryBox);
            if (_taskTabButton != null) _taskTabButton.onClick.AddListener(ToggleTask);

            LoadShop();
            LoadTasks();
        }

        private async void LoadShop()
        {
            try
            {
                var gm = GameManager.Instance;
                string json = await gm.Api.GetRaw("/shop?user_id=" + gm.UserId);

                gm.Coins = ParseInt(json, "coins");
                UpdateCoinsDisplay();

                string loginMsg = ParseString(json, "login_message");
                if (_loginText != null && loginMsg.Length > 0)
                    _loginText.text = loginMsg;

                // 解析推荐列表
                if (_recommendRoot != null)
                    PopulateItems(json, "recommended", _recommendRoot);

                // 解析每日精选
                if (_dailyRoot != null)
                    PopulateItems(json, "daily_special", _dailyRoot);
            }
            catch (System.Exception e)
            {
                Debug.LogError("加载商店失败: " + e.Message);
            }
        }

        private async void LoadTasks()
        {
            try
            {
                var gm = GameManager.Instance;
                string json = await gm.Api.GetRaw("/tasks?user_id=" + gm.UserId);

                if (_taskRoot == null) return;

                // 清空旧内容
                foreach (Transform child in _taskRoot) Destroy(child.gameObject);

                // 解析每日任务
                ParseAndAddTasks(json, "daily");
                ParseAndAddTasks(json, "growth");
            }
            catch (System.Exception e)
            {
                Debug.LogError("加载任务失败: " + e.Message);
            }
        }

        private void PopulateItems(string json, string arrayKey, Transform root)
        {
            foreach (Transform child in root) Destroy(child.gameObject);

            int searchFrom = 0;
            int arrayStart = json.IndexOf("\"" + arrayKey + "\":", searchFrom);
            if (arrayStart < 0) return;

            int pos = arrayStart;
            while (true)
            {
                int nameIdx = json.IndexOf("\"name\":", pos);
                if (nameIdx < 0 || nameIdx > json.IndexOf("]", arrayStart)) break;

                string name = ExtractString(json, "name", nameIdx);
                string cost = ExtractNumber(json, "cost", nameIdx);
                string hint = ExtractString(json, "hint", nameIdx);
                string discount = ExtractString(json, "discount", nameIdx);
                string id = ExtractString(json, "id", nameIdx);

                if (name.Length == 0) break;

                var item = CreateShopItem(name, cost, hint.Length > 0 ? hint : discount, id);
                item.transform.SetParent(root, false);

                pos = nameIdx + 10;
            }
        }

        private void ParseAndAddTasks(string json, string arrayKey)
        {
            int arrayStart = json.IndexOf("\"" + arrayKey + "\":");
            if (arrayStart < 0) return;

            int arrEnd = json.IndexOf("]", arrayStart);
            int pos = arrayStart;

            while (true)
            {
                int nameIdx = json.IndexOf("\"name\":", pos);
                if (nameIdx < 0 || nameIdx > arrEnd) break;

                string name = ExtractString(json, "name", nameIdx);
                string desc = ExtractString(json, "desc", nameIdx);
                string progress = ExtractString(json, "progress", nameIdx);
                string target = ExtractString(json, "target", nameIdx);
                string reward = ExtractString(json, "reward", nameIdx);
                string taskId = ExtractString(json, "id", nameIdx);
                bool completed = json.Substring(nameIdx, Mathf.Min(200, json.Length - nameIdx)).Contains("\"completed\": true")
                    || json.Substring(nameIdx, Mathf.Min(200, json.Length - nameIdx)).Contains("\"completed\":true");
                bool claimable = json.Substring(nameIdx, Mathf.Min(200, json.Length - nameIdx)).Contains("\"claimable\": true")
                    || json.Substring(nameIdx, Mathf.Min(200, json.Length - nameIdx)).Contains("\"claimable\":true");

                if (name.Length == 0) break;

                var item = CreateTaskItem(name, desc, progress + "/" + target,
                    "+" + reward + " 心意", taskId, completed, claimable);
                item.transform.SetParent(_taskRoot, false);

                pos = nameIdx + 10;
            }
        }

        private async void OnMysteryBox()
        {
            var gm = GameManager.Instance;
            if (gm.Coins < 30)
            {
                if (_loginText != null) _loginText.text = "心意不足（需要30）";
                return;
            }

            try
            {
                string body = "{\"user_id\":" + gm.UserId + "}";
                string json = await gm.Api.PostRaw("/shop/mystery", body);

                string giftName = ParseString(json, "gift_name");
                string rarity = ParseString(json, "rarity");
                int remaining = ParseInt(json, "coins_remaining");

                gm.Coins = remaining;
                UpdateCoinsDisplay();

                if (_loginText != null)
                    _loginText.text = "获得: " + giftName + " (" + rarity + ")";

                if (_chatUI != null)
                    _chatUI.AddAIMessage("[神秘盒子] 获得了" + giftName + "！");
            }
            catch (System.Exception e)
            {
                if (_loginText != null) _loginText.text = "开启失败";
                Debug.LogError(e);
            }
        }

        private async void OnClaimTask(string taskId)
        {
            try
            {
                var gm = GameManager.Instance;
                string body = "{\"user_id\":" + gm.UserId + ",\"task_id\":\"" + taskId + "\"}";
                string json = await gm.Api.PostRaw("/tasks/claim", body);

                int reward = ParseInt(json, "reward");
                gm.Coins = ParseInt(json, "coins");
                UpdateCoinsDisplay();

                if (_loginText != null && reward > 0)
                    _loginText.text = "领取 +" + reward + " 心意！";

                LoadTasks();
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
            }
        }

        private async void OnBuyGift(string giftId)
        {
            try
            {
                var gm = GameManager.Instance;
                string body = "{\"user_id\":" + gm.UserId + ",\"gift_id\":\"" + giftId + "\"}";
                string json = await gm.Api.PostRaw("/gift", body);

                string reply = ParseString(json, "reply");
                int favor = ParseInt(json, "favorability");
                gm.Favorability = favor;

                // 刷新余额
                string shopJson = await gm.Api.GetRaw("/shop?user_id=" + gm.UserId);
                gm.Coins = ParseInt(shopJson, "coins");
                UpdateCoinsDisplay();

                if (_chatUI != null) _chatUI.AddAIMessage(reply);
                if (_statusBar != null) _statusBar.Refresh();

                if (_loginText != null)
                    _loginText.text = "送出成功！好感 +" + ParseString(json, "favor_delta");
            }
            catch (System.Exception e)
            {
                if (_loginText != null) _loginText.text = "心意不足";
                Debug.LogError(e);
            }
        }

        private void ToggleTask()
        {
            if (_taskPanel != null && _shopPanel != null)
            {
                bool showTask = !_taskPanel.activeSelf;
                _taskPanel.SetActive(showTask);
                _shopPanel.SetActive(!showTask);
            }
        }

        private void Close()
        {
            Destroy(gameObject);
        }

        private void UpdateCoinsDisplay()
        {
            if (_coinsText != null)
                _coinsText.text = GameManager.Instance.Coins.ToString() + " 心意";
        }

        // ==================== UI 创建工具 ====================

        private GameObject CreateShopItem(string name, string cost, string hint, string giftId)
        {
            var go = new GameObject("Item_" + name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<Image>().color = new Color(0.28f, 0.28f, 0.35f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 90);

            var nameGo = CreateLabel(go.transform, "Name", name, 14, Color.white);
            var nameRT = nameGo.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(1, 1f);
            nameRT.offsetMin = new Vector2(3, 0);
            nameRT.offsetMax = new Vector2(-3, 0);

            var costGo = CreateLabel(go.transform, "Cost", cost + " 心意", 11, new Color(1f, 0.85f, 0.3f));
            var costRT = costGo.GetComponent<RectTransform>();
            costRT.anchorMin = new Vector2(0, 0.25f);
            costRT.anchorMax = new Vector2(1, 0.5f);
            costRT.offsetMin = new Vector2(3, 0);
            costRT.offsetMax = new Vector2(-3, 0);

            if (hint.Length > 0)
            {
                var hintGo = CreateLabel(go.transform, "Hint", hint, 10, new Color(0.6f, 0.8f, 1f));
                var hintRT = hintGo.GetComponent<RectTransform>();
                hintRT.anchorMin = new Vector2(0, 0);
                hintRT.anchorMax = new Vector2(1, 0.25f);
                hintRT.offsetMin = new Vector2(3, 0);
                hintRT.offsetMax = new Vector2(-3, 0);
            }

            string id = giftId;
            go.GetComponent<Button>().onClick.AddListener(delegate { OnBuyGift(id); });

            return go;
        }

        private GameObject CreateTaskItem(string name, string desc, string progress,
            string reward, string taskId, bool completed, bool claimable)
        {
            var go = new GameObject("Task_" + name, typeof(RectTransform), typeof(Image));
            go.GetComponent<Image>().color = new Color(0.22f, 0.22f, 0.3f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 50);

            Color nameColor = completed ? new Color(0.5f, 0.5f, 0.5f) : Color.white;
            var nameGo = CreateLabel(go.transform, "Name", name + "  " + progress, 14, nameColor);
            var nameRT = nameGo.GetComponent<RectTransform>();
            nameRT.anchorMin = new Vector2(0, 0.5f);
            nameRT.anchorMax = new Vector2(0.6f, 1f);
            nameRT.offsetMin = new Vector2(8, 0);
            nameRT.offsetMax = Vector2.zero;

            var descGo = CreateLabel(go.transform, "Desc", desc, 11, new Color(0.6f, 0.6f, 0.7f));
            var descRT = descGo.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0, 0);
            descRT.anchorMax = new Vector2(0.6f, 0.5f);
            descRT.offsetMin = new Vector2(8, 0);
            descRT.offsetMax = Vector2.zero;

            if (claimable)
            {
                var btn = new GameObject("Claim", typeof(RectTransform), typeof(Image), typeof(Button));
                btn.transform.SetParent(go.transform, false);
                btn.GetComponent<Image>().color = new Color(0.4f, 0.7f, 0.3f);
                var brt = btn.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.65f, 0.15f);
                brt.anchorMax = new Vector2(0.95f, 0.85f);
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;

                CreateLabel(btn.transform, "BtnText", "领取 " + reward, 12, Color.white);

                string tid = taskId;
                btn.GetComponent<Button>().onClick.AddListener(delegate { OnClaimTask(tid); });
            }
            else
            {
                var rewardGo = CreateLabel(go.transform, "Reward", completed ? "已完成" : reward, 12,
                    completed ? new Color(0.5f, 0.5f, 0.5f) : new Color(1f, 0.85f, 0.3f));
                var rrt = rewardGo.GetComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0.65f, 0.2f);
                rrt.anchorMax = new Vector2(0.95f, 0.8f);
                rrt.offsetMin = Vector2.zero;
                rrt.offsetMax = Vector2.zero;
            }

            return go;
        }

        private GameObject CreateLabel(Transform parent, string name, string text, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = fontSize;
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

        // ==================== 工具方法 ====================

        private Text FindText(string n) { return FindInChildren<Text>(n); }
        private Transform FindTransform(string n)
        {
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all) if (t.gameObject.name == n) return t;
            return null;
        }
        private GameObject FindGO(string n)
        {
            var t = FindTransform(n);
            return t != null ? t.gameObject : null;
        }
        private T FindInChildren<T>(string n) where T : Component
        {
            var all = GetComponentsInChildren<T>(true);
            foreach (var c in all) if (c.gameObject.name == n) return c;
            return null;
        }

        private string ParseString(string json, string key)
        {
            string p = "\"" + key + "\":\"";
            int i = json.IndexOf(p);
            if (i < 0) { p = "\"" + key + "\": \""; i = json.IndexOf(p); }
            if (i < 0) return "";
            int s = i + p.Length;
            int e = json.IndexOf("\"", s);
            return e > s ? json.Substring(s, e - s) : "";
        }

        private int ParseInt(string json, string key)
        {
            string p = "\"" + key + "\":";
            int i = json.IndexOf(p);
            if (i < 0) { p = "\"" + key + "\": "; i = json.IndexOf(p); }
            if (i < 0) return 0;
            int s = i + p.Length;
            while (s < json.Length && json[s] == ' ') s++;
            int e = s;
            while (e < json.Length && (json[e] == '-' || (json[e] >= '0' && json[e] <= '9'))) e++;
            if (e > s) { int v; if (int.TryParse(json.Substring(s, e - s), out v)) return v; }
            return 0;
        }

        private string ExtractString(string json, string key, int from)
        {
            string p = "\"" + key + "\":\"";
            int i = json.IndexOf(p, from);
            if (i < 0) { p = "\"" + key + "\": \""; i = json.IndexOf(p, from); }
            if (i < 0 || i - from > 300) return "";
            int s = i + p.Length;
            int e = json.IndexOf("\"", s);
            return e > s ? json.Substring(s, e - s) : "";
        }

        private string ExtractNumber(string json, string key, int from)
        {
            string p = "\"" + key + "\":";
            int i = json.IndexOf(p, from);
            if (i < 0) { p = "\"" + key + "\": "; i = json.IndexOf(p, from); }
            if (i < 0 || i - from > 300) return "0";
            int s = i + p.Length;
            while (s < json.Length && json[s] == ' ') s++;
            int e = s;
            while (e < json.Length && (json[e] >= '0' && json[e] <= '9')) e++;
            return e > s ? json.Substring(s, e - s) : "0";
        }
    }
}
