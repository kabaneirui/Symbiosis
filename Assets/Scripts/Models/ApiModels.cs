using System;

namespace Symbiosis.Models
{
    // ========== 请求模型 ==========

    [Serializable]
    public class UserInitRequest
    {
        public string nickname;
    }

    [Serializable]
    public class ChatRequest
    {
        public int user_id;
        public string message;
    }

    [Serializable]
    public class GiftRequest
    {
        public int user_id;
        public string gift_id;
    }

    // ========== 响应模型 ==========

    [Serializable]
    public class UserInitResponse
    {
        public int user_id;
        public string character_name;
        public string message;
    }

    [Serializable]
    public class ChatResponse
    {
        public string reply;
        public float mood;
        public int favorability;
        public string favor_stage;
        public string expression;
    }

    [Serializable]
    public class GiftResponse
    {
        public string reply;
        public int favorability;
        public int favor_delta;
        public float mood;
        public float mood_delta;
        public string favor_stage;
        public string expression;
    }

    [Serializable]
    public class GiftItem
    {
        public string id;
        public string name;
        public int cost;
        public string rarity;
    }

    [Serializable]
    public class GiftListResponse
    {
        public GiftItem[] gifts;
    }

    [Serializable]
    public class PersonalityData
    {
        public float kindness;
        public float tsundere;
        public float humor;
        public float rational;
    }

    [Serializable]
    public class StateResponse
    {
        public int favorability;
        public string favor_stage;
        public float mood;
        public string mood_label;
        public PersonalityData personality;
        public string expression;
    }

    // ========== 商店模型 ==========

    [Serializable]
    public class ShopItem
    {
        public string id;
        public string name;
        public int cost;
        public string rarity;
        public string hint;
    }

    [Serializable]
    public class DailyItem
    {
        public string id;
        public string name;
        public int cost;
        public int original_cost;
        public string discount;
        public string rarity;
    }

    [Serializable]
    public class TaskItem
    {
        public string id;
        public string name;
        public string desc;
        public int progress;
        public int target;
        public int reward;
        public bool completed;
        public bool claimable;
    }

    [Serializable]
    public class MysteryBoxRequest
    {
        public int user_id;
    }

    [Serializable]
    public class TaskClaimRequest
    {
        public int user_id;
        public string task_id;
    }
}
