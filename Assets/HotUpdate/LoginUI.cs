using UnityEngine;
using UnityEngine.UI;
using Symbiosis.Services;

namespace Symbiosis.UI
{
    /// <summary>
    /// 登录界面 — 热更入口，动态加载其他界面
    /// </summary>
    public class LoginUI : MonoBehaviour
    {
        private InputField _nicknameInput;
        private Button _startButton;
        private Text _statusText;
        private GameObject _loginPanel;

        private void Start()
        {
            // 动态加载登录面板
            _loginPanel = UIManager.Instance.Open("LoginPanel");
            if (_loginPanel == null) return;

            _nicknameInput = _loginPanel.GetComponentInChildren<InputField>();
            _statusText = FindChildText(_loginPanel, "StatusText");

            _startButton = _loginPanel.GetComponentInChildren<Button>();
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);
        }

        private async void OnStartClicked()
        {
            string nickname = _nicknameInput != null ? _nicknameInput.text.Trim() : "";
            if (string.IsNullOrEmpty(nickname))
                nickname = "用户";

            _startButton.interactable = false;
            SetStatus("正在连接...");

            try
            {
                var gm = GameManager.Instance;
                var response = await gm.Api.InitUser(nickname);

                gm.UserId = response.user_id;
                gm.CharacterName = response.character_name;

                var state = await gm.Api.GetState(gm.UserId);
                gm.Favorability = state.favorability;
                gm.FavorStage = state.favor_stage;
                gm.Mood = state.mood;
                gm.MoodLabel = state.mood_label;
                gm.Expression = state.expression;
                gm.Personality = state.personality;

                // 获取商店数据（触发每日登录奖励）
                string shopJson = await gm.Api.GetRaw("/shop?user_id=" + gm.UserId);
                gm.Coins = ParseInt(shopJson, "coins");
                string loginMsg = ParseStr(shopJson, "login_message");

                // 关闭登录面板，打开聊天界面
                UIManager.Instance.Close("LoginPanel");
                OpenChatUI(response.message, loginMsg);
            }
            catch (System.Exception e)
            {
                SetStatus("连接失败: " + e.Message);
                Debug.LogError(e);
                _startButton.interactable = true;
            }
        }

        private void OpenChatUI(string welcomeMessage, string loginReward)
        {
            var chatGo = UIManager.Instance.Open("ChatPanel");
            if (chatGo == null) return;

            var chatUI = chatGo.GetComponent<ChatUI>();
            if (chatUI == null)
                chatUI = chatGo.AddComponent<ChatUI>();

            chatUI.Init();

            // 系统消息
            if (!string.IsNullOrEmpty(loginReward))
                chatUI.AddSystemMessage(loginReward);
            chatUI.AddSystemMessage("当前心意: " + GameManager.Instance.Coins);
            chatUI.AddAIMessage(welcomeMessage);

            // 打开状态栏
            var statusGo = UIManager.Instance.Open("StatusBar");
            if (statusGo != null)
            {
                var statusUI = statusGo.GetComponent<StatusBarUI>();
                if (statusUI == null)
                    statusUI = statusGo.AddComponent<StatusBarUI>();
                statusUI.Refresh();
                chatUI.statusBar = statusUI;
            }
        }

        private void SetStatus(string text)
        {
            if (_statusText != null)
                _statusText.text = text;
        }

        private Text FindChildText(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            return t != null ? t.GetComponent<Text>() : null;
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
            while (e < json.Length && (json[e] >= '0' && json[e] <= '9')) e++;
            int v = 0;
            if (e > s) int.TryParse(json.Substring(s, e - s), out v);
            return v;
        }

        private string ParseStr(string json, string key)
        {
            string p = "\"" + key + "\":\"";
            int i = json.IndexOf(p);
            if (i < 0) { p = "\"" + key + "\": \""; i = json.IndexOf(p); }
            if (i < 0) return "";
            int s = i + p.Length;
            int e = json.IndexOf("\"", s);
            return e > s ? json.Substring(s, e - s) : "";
        }
    }
}
