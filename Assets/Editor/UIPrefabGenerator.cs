using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 原神风格 UI Prefab 生成器（菜单：Symbiosis / 生成UI Prefab）
/// 半透明毛玻璃卡片 + 渐变色 + 精致排版
/// </summary>
public class UIPrefabGenerator
{
    private static readonly string SAVE_PATH = "Assets/Resources/UI/";

    // 原神风配色
    static Color BG_DARK = new Color(0.08f, 0.08f, 0.14f, 1f);
    static Color CARD = new Color(0.14f, 0.15f, 0.22f, 0.92f);
    static Color CARD_LIGHT = new Color(0.18f, 0.19f, 0.28f, 0.88f);
    static Color ACCENT_GOLD = new Color(0.90f, 0.78f, 0.45f, 1f);
    static Color ACCENT_BLUE = new Color(0.35f, 0.65f, 0.95f, 1f);
    static Color ACCENT_GREEN = new Color(0.40f, 0.78f, 0.55f, 1f);
    static Color TEXT_WHITE = new Color(0.92f, 0.92f, 0.95f, 1f);
    static Color TEXT_DIM = new Color(0.55f, 0.55f, 0.65f, 1f);
    static Color TEXT_WARM = new Color(0.85f, 0.75f, 0.55f, 1f);
    static Color USER_BUBBLE_COL = new Color(0.30f, 0.50f, 0.80f, 0.85f);
    static Color AI_BUBBLE_COL = new Color(0.16f, 0.17f, 0.25f, 0.90f);
    static Color INPUT_BG = new Color(0.12f, 0.12f, 0.20f, 0.95f);
    static Color BTN_PRIMARY = new Color(0.35f, 0.55f, 0.85f, 1f);
    static Color BTN_GIFT = new Color(0.85f, 0.55f, 0.25f, 1f);
    static Color BTN_DANGER = new Color(0.70f, 0.25f, 0.25f, 1f);

    [MenuItem("Symbiosis/生成 UI Prefab（原神风格）")]
    public static void GenerateAll()
    {
        EnsureFolder();
        CreateLoginPanel();
        CreateChatPanel();
        CreateStatusBar();
        CreateGiftPanel();
        CreateUserBubble();
        CreateAIBubble();
        CreateGiftItem();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("完成", "7 个原神风格 UI Prefab 已生成", "好的");
    }

    // ==================== LoginPanel ====================

    static void CreateLoginPanel()
    {
        var root = MakeRoot("LoginPanel");
        AddImg(root, BG_DARK);
        Stretch(root.GetComponent<RectTransform>());

        // 中间卡片
        var card = MakeChild(root, "Card");
        AddImg(card, CARD);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.1f, 0.2f);
        crt.anchorMax = new Vector2(0.9f, 0.8f);
        crt.offsetMin = crt.offsetMax = Vector2.zero;

        // 头像
        var avatar = MakeChild(card, "Avatar");
        var avrt = avatar.GetComponent<RectTransform>();
        avrt.anchorMin = new Vector2(0.35f, 0.6f);
        avrt.anchorMax = new Vector2(0.65f, 0.95f);
        avrt.offsetMin = avrt.offsetMax = Vector2.zero;
        AddImg(avatar, Color.white);

        // 标题
        var title = MakeText(card.transform, "Title", "Symbiosis", 26, ACCENT_GOLD);
        Anchors(title, 0.1f, 0.48f, 0.9f, 0.58f);
        title.GetComponent<Text>().fontStyle = FontStyle.Bold;

        // 副标题
        var sub = MakeText(card.transform, "Subtitle", "你的 AI 伙伴在等你", 14, TEXT_DIM);
        Anchors(sub, 0.1f, 0.40f, 0.9f, 0.48f);

        // 输入框
        var input = MakeInputField(card.transform, "NicknameInput", "输入你的名字...");
        Anchors(input, 0.12f, 0.22f, 0.88f, 0.35f);

        // 开始按钮
        var btn = MakeButton(card.transform, "StartButton", "进入世界", BTN_PRIMARY);
        Anchors(btn, 0.25f, 0.08f, 0.75f, 0.18f);

        // 状态文本
        var status = MakeText(card.transform, "StatusText", "", 12, TEXT_DIM);
        Anchors(status, 0.1f, 0.01f, 0.9f, 0.07f);

        SavePrefab(root, "LoginPanel");
    }

    // ==================== ChatPanel ====================

    static void CreateChatPanel()
    {
        var root = MakeRoot("ChatPanel");
        AddImg(root, BG_DARK);
        Stretch(root.GetComponent<RectTransform>());

        // 消息区域
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(root.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        var srt = scrollGo.GetComponent<RectTransform>();
        srt.anchorMin = new Vector2(0, 0.08f);
        srt.anchorMax = new Vector2(1, 0.92f);
        srt.offsetMin = new Vector2(8, 5);
        srt.offsetMax = new Vector2(-8, -5);

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.horizontal = false;

        var vp = MakeChild(scrollGo, "Viewport");
        AddImg(vp, new Color(1, 1, 1, 0.01f));
        Stretch(vp.GetComponent<RectTransform>());
        vp.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vp.GetComponent<RectTransform>();

        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(vp.transform, false);
        var crt = content.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 1);
        crt.anchorMax = new Vector2(1, 1);
        crt.pivot = new Vector2(0.5f, 1);
        crt.offsetMin = crt.offsetMax = Vector2.zero;
        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 12;
        vlg.padding = new RectOffset(8, 8, 12, 12);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = crt;

        // 打字指示器
        var typing = MakeText(root.transform, "TypingIndicator", "小星正在思考...", 12, TEXT_DIM);
        var tyRT = typing.GetComponent<RectTransform>();
        tyRT.anchorMin = new Vector2(0, 0.92f);
        tyRT.anchorMax = new Vector2(0.5f, 0.95f);
        tyRT.offsetMin = new Vector2(15, 0);
        tyRT.offsetMax = Vector2.zero;
        typing.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        // 底部栏
        var bar = MakeChild(root, "BottomBar");
        AddImg(bar, CARD);
        Stretch(bar.GetComponent<RectTransform>());
        var brt = bar.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = new Vector2(1, 0.08f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;

        // 输入框
        var chatInput = MakeInputField(bar.transform, "ChatInput", "说点什么吧...");
        Anchors(chatInput, 0.01f, 0.12f, 0.68f, 0.88f);

        // 发送
        var send = MakeButton(bar.transform, "SendButton", "发送", BTN_PRIMARY);
        Anchors(send, 0.69f, 0.12f, 0.84f, 0.88f);

        // 礼物/商店
        var gift = MakeButton(bar.transform, "GiftButton", "商店", BTN_GIFT);
        Anchors(gift, 0.85f, 0.12f, 0.99f, 0.88f);

        SavePrefab(root, "ChatPanel");
    }

    // ==================== StatusBar ====================

    static void CreateStatusBar()
    {
        var root = MakeRoot("StatusBar");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.92f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        AddImg(root, new Color(0.10f, 0.10f, 0.18f, 0.95f));

        // 角色名
        var name = MakeText(root.transform, "CharacterName", "小星", 18, ACCENT_GOLD);
        name.GetComponent<Text>().fontStyle = FontStyle.Bold;
        Anchors(name, 0.02f, 0.1f, 0.2f, 0.9f);
        name.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        // 好感度条
        var sliderGo = new GameObject("FavorSlider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(root.transform, false);
        Anchors(sliderGo, 0.22f, 0.55f, 0.55f, 0.75f);
        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 50;

        var bg = MakeChild(sliderGo, "Background");
        AddImg(bg, new Color(0.2f, 0.2f, 0.28f));
        Stretch(bg.GetComponent<RectTransform>());

        var fillArea = MakeChild(sliderGo, "FillArea");
        Stretch(fillArea.GetComponent<RectTransform>());
        var fill = MakeChild(fillArea, "Fill");
        AddImg(fill, ACCENT_BLUE);
        Stretch(fill.GetComponent<RectTransform>());
        slider.fillRect = fill.GetComponent<RectTransform>();

        // 好感数值
        var favorTxt = MakeText(root.transform, "FavorText", "0", 13, TEXT_WHITE);
        Anchors(favorTxt, 0.56f, 0.5f, 0.64f, 0.8f);

        // 阶段
        var stageTxt = MakeText(root.transform, "StageText", "陌生", 13, ACCENT_BLUE);
        Anchors(stageTxt, 0.64f, 0.5f, 0.76f, 0.8f);

        // 心情
        var moodTxt = MakeText(root.transform, "MoodText", "平静", 13, TEXT_DIM);
        Anchors(moodTxt, 0.22f, 0.15f, 0.45f, 0.45f);

        // 心意货币
        var coinsTxt = MakeText(root.transform, "CoinsText", "150 心意", 13, ACCENT_GOLD);
        Anchors(coinsTxt, 0.78f, 0.2f, 0.98f, 0.8f);
        coinsTxt.GetComponent<Text>().alignment = TextAnchor.MiddleRight;

        SavePrefab(root, "StatusBar");
    }

    // ==================== GiftPanel (商店弹窗) ====================

    static void CreateGiftPanel()
    {
        var root = MakeRoot("GiftPanel");
        AddImg(root, CARD);
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(380, 360);

        var title = MakeText(root.transform, "Title", "选择礼物", 22, ACCENT_GOLD);
        title.GetComponent<Text>().fontStyle = FontStyle.Bold;
        Anchors(title, 0.05f, 0.87f, 0.6f, 0.97f);
        title.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var close = MakeButton(root.transform, "CloseButton", "×", BTN_DANGER);
        Anchors(close, 0.88f, 0.88f, 0.97f, 0.97f);

        var grid = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        grid.transform.SetParent(root.transform, false);
        var g = grid.GetComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(100, 90);
        g.spacing = new Vector2(10, 10);
        g.childAlignment = TextAnchor.UpperCenter;
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 3;
        Anchors(grid, 0.03f, 0.15f, 0.97f, 0.85f);

        var feedback = MakeText(root.transform, "FeedbackText", "", 14, ACCENT_GREEN);
        Anchors(feedback, 0.05f, 0.02f, 0.95f, 0.12f);

        SavePrefab(root, "GiftPanel");
    }

    // ==================== UserBubble ====================

    static void CreateUserBubble()
    {
        var root = new GameObject("UserBubble", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var hlg = root.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperRight;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.padding = new RectOffset(100, 8, 0, 0);

        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(ContentSizeFitter));
        bubble.transform.SetParent(root.transform, false);
        bubble.GetComponent<Image>().color = USER_BUBBLE_COL;
        var csf = bubble.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bubble.transform, false);
        var t = textGo.GetComponent<Text>();
        t.fontSize = 15;
        t.color = TEXT_WHITE;
        t.alignment = TextAnchor.UpperLeft;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textGo.AddComponent<LayoutElement>().preferredWidth = 240;

        SavePrefab(root, "UserBubble");
    }

    // ==================== AIBubble ====================

    static void CreateAIBubble()
    {
        var root = new GameObject("AIBubble", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        var hlg = root.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.UpperLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.padding = new RectOffset(8, 100, 0, 0);
        hlg.spacing = 8;

        // 头像
        var avatarGo = new GameObject("Avatar", typeof(RectTransform), typeof(Image));
        avatarGo.transform.SetParent(root.transform, false);
        avatarGo.GetComponent<Image>().color = Color.white;
        var avLE = avatarGo.AddComponent<LayoutElement>();
        avLE.preferredWidth = 36;
        avLE.preferredHeight = 36;

        // 气泡
        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(ContentSizeFitter));
        bubble.transform.SetParent(root.transform, false);
        bubble.GetComponent<Image>().color = AI_BUBBLE_COL;
        var csf = bubble.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bubble.transform, false);
        var t = textGo.GetComponent<Text>();
        t.fontSize = 15;
        t.color = TEXT_WHITE;
        t.alignment = TextAnchor.UpperLeft;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        textGo.AddComponent<LayoutElement>().preferredWidth = 220;

        SavePrefab(root, "AIBubble");
    }

    // ==================== GiftItem ====================

    static void CreateGiftItem()
    {
        var root = new GameObject("GiftItem", typeof(RectTransform), typeof(Image), typeof(Button));
        root.GetComponent<Image>().color = CARD_LIGHT;

        var nameGo = MakeText(root.transform, "Name", "礼物", 14, TEXT_WHITE);
        var nrt = nameGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0.4f);
        nrt.anchorMax = new Vector2(1, 0.85f);
        nrt.offsetMin = new Vector2(5, 0);
        nrt.offsetMax = new Vector2(-5, 0);

        var costGo = MakeText(root.transform, "Cost", "50", 12, ACCENT_GOLD);
        var crt = costGo.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0.05f);
        crt.anchorMax = new Vector2(1, 0.38f);
        crt.offsetMin = new Vector2(5, 0);
        crt.offsetMax = new Vector2(-5, 0);

        SavePrefab(root, "GiftItem");
    }

    // ==================== 工具方法 ====================

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");
    }

    static GameObject MakeRoot(string name)
    {
        return new GameObject(name, typeof(RectTransform));
    }

    static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static Image AddImg(GameObject go, Color c)
    {
        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.color = c;
        return img;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Stretch(RectTransform rt, Color c)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Anchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static GameObject MakeText(Transform parent, string name, string content, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        return go;
    }

    static GameObject MakeButton(Transform parent, string name, string label, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        var txt = MakeText(go.transform, "Text", label, 15, TEXT_WHITE);
        Stretch(txt.GetComponent<RectTransform>());
        txt.GetComponent<Text>().fontStyle = FontStyle.Bold;

        return go;
    }

    static GameObject MakeInputField(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = INPUT_BG;

        var textGo = MakeText(go.transform, "Text", "", 15, TEXT_WHITE);
        Stretch(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(12, 2);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-12, -2);
        textGo.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var phGo = MakeText(go.transform, "Placeholder", placeholder, 15, TEXT_DIM);
        Stretch(phGo.GetComponent<RectTransform>());
        phGo.GetComponent<RectTransform>().offsetMin = new Vector2(12, 2);
        phGo.GetComponent<RectTransform>().offsetMax = new Vector2(-12, -2);
        phGo.GetComponent<Text>().alignment = TextAnchor.MiddleLeft;

        var input = go.GetComponent<InputField>();
        input.textComponent = textGo.GetComponent<Text>();
        input.placeholder = phGo.GetComponent<Text>();

        return go;
    }

    static void SavePrefab(GameObject go, string name)
    {
        PrefabUtility.SaveAsPrefabAsset(go, SAVE_PATH + name + ".prefab");
        Object.DestroyImmediate(go);
        Debug.Log("生成: " + name);
    }
}
