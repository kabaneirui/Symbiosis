using UnityEngine;
using Symbiosis.Network;
using Symbiosis.Models;

namespace Symbiosis.Services
{
    /// <summary>
    /// 全局游戏管理器 — 持有 ApiClient 和用户状态，场景切换不销毁
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("服务器配置")]
        public string serverUrl = "http://127.0.0.1:8000";

        public ApiClient Api { get; private set; }

        // 当前用户/角色状态（登录后填充）
        public int UserId { get; set; }
        public string CharacterName { get; set; }
        public int Favorability { get; set; }
        public string FavorStage { get; set; }
        public float Mood { get; set; }
        public string MoodLabel { get; set; }
        public string Expression { get; set; }
        public PersonalityData Personality { get; set; }
        public int Coins { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Api = new ApiClient(serverUrl);
        }

        /// <summary>
        /// 用聊天/送礼返回的数据刷新本地状态缓存
        /// </summary>
        public void UpdateState(int favorability, string favorStage, float mood, string expression)
        {
            Favorability = favorability;
            FavorStage = favorStage;
            Mood = mood;
            Expression = expression;
        }

        /// <summary>
        /// 从服务器拉取完整状态
        /// </summary>
        public async void RefreshState()
        {
            if (UserId <= 0) return;
            try
            {
                var state = await Api.GetState(UserId);
                Favorability = state.favorability;
                FavorStage = state.favor_stage;
                Mood = state.mood;
                MoodLabel = state.mood_label;
                Expression = state.expression;
                Personality = state.personality;
            }
            catch (System.Exception e)
            {
                Debug.LogError("刷新状态失败: " + e.Message);
            }
        }
    }
}
