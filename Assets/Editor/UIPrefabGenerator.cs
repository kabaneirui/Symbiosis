using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键生成所有 UI Prefab（菜单：Symbiosis / 生成UI Prefab）
/// 生成后在 Resources/UI/ 目录下可找到
/// </summary>
public class UIPrefabGenerator
{
    private static readonly string SAVE_PATH = "Assets/Resources/UI/";
    private static Color BG_DARK = new Color(0.15f, 0.15f, 0.2f, 1f);
    private static Color BG_PANEL = new Color(0.2f, 0.2f, 0.28f, 0.95f);
    private static Color ACCENT = new Color(0.4f, 0.6f, 1f, 1f);
    private static Color USER_BUBBLE = new Color(0.35f, 0.55f, 0.95f, 1f);
    private static Color AI_BUBBLE = new Color(0.28f, 0.28f, 0.35f, 1f);
    private static Color TEXT_WHITE = Color.white;
    private static Color TEXT_GRAY = new Color(0.7f, 0.7f, 0.75f, 1f);
    private static Color INPUT_BG = new Color(0.25f, 0.25f, 0.32f, 1f);
    private static Color BTN_COLOR = new Color(0.4f, 0.6f, 1f, 1f);

    [MenuItem("Symbiosis/生成 UI Prefab（一键创建）")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/UI"))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");

        CreateLoginPanel();
        CreateChatPanel();
        CreateStatusBar();
        CreateGiftPanel();
        CreateUserBubble();
        CreateAIBubble();
        CreateGiftItem();

        AssetDatabase.Refresh();
        Debug.Log("所有 UI Prefab 已生成到 " + SAVE_PATH);
        EditorUtility.DisplayDialog("完成", "7 个 UI Prefab 已生成到 Resources/UI/ 目录", "好的");
    }

    // ==================== LoginPanel ====================

    static void CreateLoginPanel()
    {
        var root = CreateUIRoot("LoginPanel", 400, 300);
        var bg = AddImage(root, BG_PANEL);
        SetStretch(bg.rectTransform);

        // 标题
        var title = CreateText(root.transform, "Title", "欢迎来到 Symbiosis", 28, TEXT_WHITE);
        SetAnchored(title, 0, 200, 400, 40);
        title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        // 昵称输入框
        var input = CreateInputField(root.transform, "NicknameInput", "请输入你的昵称...");
        SetAnchored(input, 0, 100, 320, 45);

        // 开始按钮
        var btn = CreateButton(root.transform, "StartButton", "开始", BTN_COLOR);
        SetAnchored(btn, 0, 30, 200, 50);

        // 状态文本
        var status = CreateText(root.transform, "StatusText", "", 16, TEXT_GRAY);
        SetAnchored(status, 0, -30, 300, 30);
        status.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        SavePrefab(root, "LoginPanel");
    }

    // ==================== ChatPanel ====================

    static void CreateChatPanel()
    {
        var root = CreateUIRoot("ChatPanel", 0, 0);
        SetStretch(root.GetComponent<RectTransform>());

        // 消息滚动区域
        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
        scrollGo.transform.SetParent(root.transform, false);
        var scrollRT = scrollGo.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0.08f);
        scrollRT.anchorMax = new Vector2(1, 1f);
        scrollRT.offsetMin = new Vector2(10, 0);
        scrollRT.offsetMax = new Vector2(-10, -5);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);

        var scrollRect = scrollGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;

        // Viewport
        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRT = viewport.GetComponent<RectTransform>();
        SetStretch(vpRT);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = vpRT;

        // Content
        var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = new Vector2(0, 0);
        contentRT.offsetMax = new Vector2(0, 0);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = content.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;

        // 正在输入指示器
        var typing = CreateText(root.transform, "TypingIndicator", "对方正在输入...", 14, TEXT_GRAY);
        var typRT = typing.GetComponent<RectTransform>();
        typRT.anchorMin = new Vector2(0, 0.08f);
        typRT.anchorMax = new Vector2(0.5f, 0.12f);
        typRT.offsetMin = new Vector2(15, 0);
        typRT.offsetMax = new Vector2(0, 0);

        // 底部输入区域
        var bottomBar = new GameObject("BottomBar", typeof(RectTransform), typeof(Image));
        bottomBar.transform.SetParent(root.transform, false);
        var bbRT = bottomBar.GetComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0, 0);
        bbRT.anchorMax = new Vector2(1, 0.08f);
        bbRT.offsetMin = Vector2.zero;
        bbRT.offsetMax = Vector2.zero;
        bottomBar.GetComponent<Image>().color = BG_PANEL;

        // 输入框
        var chatInput = CreateInputField(bottomBar.transform, "ChatInput", "输入消息...");
        var ciRT = chatInput.GetComponent<RectTransform>();
        ciRT.anchorMin = new Vector2(0.01f, 0.1f);
        ciRT.anchorMax = new Vector2(0.7f, 0.9f);
        ciRT.offsetMin = Vector2.zero;
        ciRT.offsetMax = Vector2.zero;

        // 发送按钮
        var sendBtn = CreateButton(bottomBar.transform, "SendButton", "发送", BTN_COLOR);
        var sbRT = sendBtn.GetComponent<RectTransform>();
        sbRT.anchorMin = new Vector2(0.71f, 0.1f);
        sbRT.anchorMax = new Vector2(0.85f, 0.9f);
        sbRT.offsetMin = Vector2.zero;
        sbRT.offsetMax = Vector2.zero;

        // 礼物按钮
        var giftBtn = CreateButton(bottomBar.transform, "GiftButton", "礼物", new Color(1f, 0.6f, 0.3f, 1f));
        var gbRT = giftBtn.GetComponent<RectTransform>();
        gbRT.anchorMin = new Vector2(0.86f, 0.1f);
        gbRT.anchorMax = new Vector2(0.99f, 0.9f);
        gbRT.offsetMin = Vector2.zero;
        gbRT.offsetMax = Vector2.zero;

        SavePrefab(root, "ChatPanel");
    }

    // ==================== StatusBar ====================

    static void CreateStatusBar()
    {
        var root = CreateUIRoot("StatusBar", 0, 50);
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0, 1);
        rootRT.anchorMax = new Vector2(1, 1);
        rootRT.pivot = new Vector2(0.5f, 1);
        rootRT.offsetMin = new Vector2(0, -50);
        rootRT.offsetMax = new Vector2(0, 0);

        AddImage(root, new Color(0.12f, 0.12f, 0.18f, 0.95f));

        // 角色名
        var name = CreateText(root.transform, "CharacterName", "小星", 18, TEXT_WHITE);
        SetAnchored(name, -130, 0, 80, 30);

        // 好感度 Slider
        var sliderGo = new GameObject("FavorSlider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(root.transform, false);
        SetAnchored(sliderGo, -20, 10, 160, 12);

        var slider = sliderGo.GetComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = 50;

        var bgImg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgImg.transform.SetParent(sliderGo.transform, false);
        SetStretch(bgImg.GetComponent<RectTransform>());
        bgImg.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.35f, 1f);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGo.transform, false);
        SetStretch(fillArea.GetComponent<RectTransform>());

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        SetStretch(fill.GetComponent<RectTransform>());
        fill.GetComponent<Image>().color = ACCENT;
        slider.fillRect = fill.GetComponent<RectTransform>();

        // 好感数值
        var favorTxt = CreateText(root.transform, "FavorText", "0", 14, TEXT_WHITE);
        SetAnchored(favorTxt, 80, 10, 40, 20);

        // 阶段
        var stageTxt = CreateText(root.transform, "StageText", "陌生", 14, ACCENT);
        SetAnchored(stageTxt, 120, 10, 50, 20);

        // 心情
        var moodTxt = CreateText(root.transform, "MoodText", "平静", 14, TEXT_GRAY);
        SetAnchored(moodTxt, -20, -12, 80, 20);

        SavePrefab(root, "StatusBar");
    }

    // ==================== GiftPanel ====================

    static void CreateGiftPanel()
    {
        var root = CreateUIRoot("GiftPanel", 350, 320);
        AddImage(root, BG_PANEL);

        // 标题
        var title = CreateText(root.transform, "Title", "选择礼物", 22, TEXT_WHITE);
        SetAnchored(title, 0, 130, 200, 35);
        title.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        // 关闭按钮
        var closeBtn = CreateButton(root.transform, "CloseButton", "X", new Color(0.8f, 0.3f, 0.3f, 1f));
        SetAnchored(closeBtn, 140, 130, 40, 35);

        // 网格区域
        var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(root.transform, false);
        var gridRT = gridGo.GetComponent<RectTransform>();
        SetAnchored(gridRT.gameObject, 0, -10, 320, 200);

        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(90, 80);
        grid.spacing = new Vector2(15, 15);
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;

        // 反馈文本
        var feedback = CreateText(root.transform, "FeedbackText", "", 16, new Color(1f, 0.85f, 0.3f, 1f));
        SetAnchored(feedback, 0, -140, 300, 25);
        feedback.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

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
        hlg.padding = new RectOffset(80, 5, 0, 0);

        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(ContentSizeFitter));
        bubble.transform.SetParent(root.transform, false);
        bubble.GetComponent<Image>().color = USER_BUBBLE;
        var bcsf = bubble.GetComponent<ContentSizeFitter>();
        bcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bubble.transform, false);
        var text = textGo.GetComponent<Text>();
        text.fontSize = 16;
        text.color = TEXT_WHITE;
        text.text = "消息内容";
        text.alignment = TextAnchor.UpperLeft;

        var textLE = textGo.AddComponent<LayoutElement>();
        textLE.preferredWidth = 250;

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
        hlg.padding = new RectOffset(5, 80, 0, 0);

        var bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(ContentSizeFitter));
        bubble.transform.SetParent(root.transform, false);
        bubble.GetComponent<Image>().color = AI_BUBBLE;
        var bcsf = bubble.GetComponent<ContentSizeFitter>();
        bcsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        bcsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = bubble.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(bubble.transform, false);
        var text = textGo.GetComponent<Text>();
        text.fontSize = 16;
        text.color = TEXT_WHITE;
        text.text = "AI 回复";
        text.alignment = TextAnchor.UpperLeft;

        var textLE = textGo.AddComponent<LayoutElement>();
        textLE.preferredWidth = 250;

        SavePrefab(root, "AIBubble");
    }

    // ==================== GiftItem ====================

    static void CreateGiftItem()
    {
        var root = new GameObject("GiftItem", typeof(RectTransform), typeof(Image), typeof(Button));
        root.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.38f, 1f);

        var nameGo = CreateText(root.transform, "Name", "礼物", 15, TEXT_WHITE);
        var nameRT = nameGo.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.35f);
        nameRT.anchorMax = new Vector2(1, 0.9f);
        nameRT.offsetMin = new Vector2(5, 0);
        nameRT.offsetMax = new Vector2(-5, 0);
        nameGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        var costGo = CreateText(root.transform, "Cost", "50", 13, TEXT_GRAY);
        var costRT = costGo.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0, 0);
        costRT.anchorMax = new Vector2(1, 0.35f);
        costRT.offsetMin = new Vector2(5, 0);
        costRT.offsetMax = new Vector2(-5, 0);
        costGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        SavePrefab(root, "GiftItem");
    }

    // ==================== 工具方法 ====================

    static GameObject CreateUIRoot(string name, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(width, height);
        return go;
    }

    static Image AddImage(GameObject go, Color color)
    {
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    static void SetStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetAnchored(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static GameObject CreateText(Transform parent, string name, string content, int fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return go;
    }

    static GameObject CreateButton(Transform parent, string name, string label, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;

        var textGo = CreateText(go.transform, "Text", label, 16, TEXT_WHITE);
        SetStretch(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<Text>().alignment = TextAnchor.MiddleCenter;

        return go;
    }

    static GameObject CreateInputField(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = INPUT_BG;

        var textGo = CreateText(go.transform, "Text", "", 16, TEXT_WHITE);
        SetStretch(textGo.GetComponent<RectTransform>());
        textGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2);
        textGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);

        var phGo = CreateText(go.transform, "Placeholder", placeholder, 16, TEXT_GRAY);
        SetStretch(phGo.GetComponent<RectTransform>());
        phGo.GetComponent<RectTransform>().offsetMin = new Vector2(10, 2);
        phGo.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -2);

        var input = go.GetComponent<InputField>();
        input.textComponent = textGo.GetComponent<Text>();
        input.placeholder = phGo.GetComponent<Text>();

        return go;
    }

    static void SavePrefab(GameObject go, string name)
    {
        string path = SAVE_PATH + name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("创建 Prefab: " + path);
    }
}
