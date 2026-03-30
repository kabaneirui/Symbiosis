using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键生成 5 组表情 Sprite（菜单：Symbiosis / 生成表情资源）
/// 用代码绘制简易表情，后续可替换为美术资源
/// </summary>
public class ExpressionGenerator
{
    private static readonly string SAVE_PATH = "Assets/Resources/Expressions/";
    private static readonly int SIZE = 128;

    [MenuItem("Symbiosis/生成表情资源（一键创建）")]
    public static void GenerateAll()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Expressions"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "Expressions");
        }

        CreateExpression("expr_excited", new Color(1f, 0.85f, 0.2f), DrawExcited);
        CreateExpression("expr_happy", new Color(1f, 0.75f, 0.3f), DrawHappy);
        CreateExpression("expr_calm", new Color(0.7f, 0.8f, 0.9f), DrawCalm);
        CreateExpression("expr_sad", new Color(0.5f, 0.6f, 0.8f), DrawSad);
        CreateExpression("expr_angry", new Color(0.9f, 0.4f, 0.35f), DrawAngry);

        AssetDatabase.Refresh();
        Debug.Log("5 组表情资源已生成到 " + SAVE_PATH);
        EditorUtility.DisplayDialog("完成", "5 组表情 Sprite 已生成到 Resources/Expressions/", "好的");
    }

    delegate void DrawFace(Texture2D tex, int cx, int cy, int r);

    static void CreateExpression(string name, Color bgColor, DrawFace drawFunc)
    {
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false);

        // 填充背景圆
        int cx = SIZE / 2, cy = SIZE / 2, r = SIZE / 2 - 4;
        for (int x = 0; x < SIZE; x++)
        {
            for (int y = 0; y < SIZE; y++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist < r)
                    tex.SetPixel(x, y, bgColor);
                else if (dist < r + 2)
                    tex.SetPixel(x, y, new Color(0.2f, 0.2f, 0.2f, 1f));
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        }

        drawFunc(tex, cx, cy, r);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        string path = SAVE_PATH + name + ".png";
        System.IO.File.WriteAllBytes(path, png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 100;
        importer.SaveAndReimport();
    }

    static void DrawExcited(Texture2D tex, int cx, int cy, int r)
    {
        // 星星眼
        DrawStar(tex, cx - 20, cy + 10, 10, Color.white);
        DrawStar(tex, cx + 20, cy + 10, 10, Color.white);
        // 大笑嘴巴
        DrawArc(tex, cx, cy - 15, 22, 180, 360, 3, new Color(0.2f, 0.1f, 0.1f));
        FillBelow(tex, cx, cy - 15, 22, 180, 360, new Color(0.2f, 0.1f, 0.1f));
    }

    static void DrawHappy(Texture2D tex, int cx, int cy, int r)
    {
        // 开心眼睛（弯弯的）
        DrawArc(tex, cx - 18, cy + 12, 8, 0, 180, 3, new Color(0.2f, 0.15f, 0.1f));
        DrawArc(tex, cx + 18, cy + 12, 8, 0, 180, 3, new Color(0.2f, 0.15f, 0.1f));
        // 微笑
        DrawArc(tex, cx, cy - 12, 16, 200, 340, 2, new Color(0.2f, 0.15f, 0.1f));
    }

    static void DrawCalm(Texture2D tex, int cx, int cy, int r)
    {
        // 普通眼睛
        FillCircle(tex, cx - 18, cy + 10, 5, new Color(0.2f, 0.2f, 0.25f));
        FillCircle(tex, cx + 18, cy + 10, 5, new Color(0.2f, 0.2f, 0.25f));
        // 平嘴
        DrawLine(tex, cx - 12, cy - 12, cx + 12, cy - 12, 2, new Color(0.3f, 0.3f, 0.4f));
    }

    static void DrawSad(Texture2D tex, int cx, int cy, int r)
    {
        // 难过眼睛
        FillCircle(tex, cx - 18, cy + 10, 5, new Color(0.2f, 0.25f, 0.4f));
        FillCircle(tex, cx + 18, cy + 10, 5, new Color(0.2f, 0.25f, 0.4f));
        // 眼泪
        FillCircle(tex, cx - 14, cy + 2, 3, new Color(0.4f, 0.6f, 0.9f));
        // 难过嘴
        DrawArc(tex, cx, cy - 18, 12, 20, 160, 2, new Color(0.2f, 0.25f, 0.4f));
    }

    static void DrawAngry(Texture2D tex, int cx, int cy, int r)
    {
        // 生气眉毛
        DrawLine(tex, cx - 25, cy + 25, cx - 12, cy + 20, 3, new Color(0.3f, 0.1f, 0.1f));
        DrawLine(tex, cx + 25, cy + 25, cx + 12, cy + 20, 3, new Color(0.3f, 0.1f, 0.1f));
        // 生气眼睛
        FillCircle(tex, cx - 18, cy + 12, 5, new Color(0.3f, 0.1f, 0.1f));
        FillCircle(tex, cx + 18, cy + 12, 5, new Color(0.3f, 0.1f, 0.1f));
        // 生气嘴
        DrawArc(tex, cx, cy - 16, 14, 20, 160, 3, new Color(0.3f, 0.1f, 0.1f));
    }

    // ==================== 绘制工具 ====================

    static void FillCircle(Texture2D tex, int cx, int cy, int r, Color color)
    {
        for (int x = cx - r; x <= cx + r; x++)
            for (int y = cy - r; y <= cy + r; y++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r)
                    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                        tex.SetPixel(x, y, color);
    }

    static void DrawLine(Texture2D tex, int x0, int y0, int x1, int y1, int thickness, Color color)
    {
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int steps = Mathf.Max(dx, dy);
        if (steps == 0) return;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = (int)Mathf.Lerp(x0, x1, t);
            int y = (int)Mathf.Lerp(y0, y1, t);
            FillCircle(tex, x, y, thickness / 2, color);
        }
    }

    static void DrawArc(Texture2D tex, int cx, int cy, int r, float startDeg, float endDeg, int thickness, Color color)
    {
        for (float deg = startDeg; deg <= endDeg; deg += 1f)
        {
            float rad = deg * Mathf.Deg2Rad;
            int x = cx + (int)(r * Mathf.Cos(rad));
            int y = cy + (int)(r * Mathf.Sin(rad));
            FillCircle(tex, x, y, thickness / 2 + 1, color);
        }
    }

    static void DrawStar(Texture2D tex, int cx, int cy, int r, Color color)
    {
        for (int i = 0; i < 5; i++)
        {
            float angle1 = (i * 72 - 90) * Mathf.Deg2Rad;
            float angle2 = ((i + 2) * 72 - 90) * Mathf.Deg2Rad;
            int x0 = cx + (int)(r * Mathf.Cos(angle1));
            int y0 = cy + (int)(r * Mathf.Sin(angle1));
            int x1 = cx + (int)(r * Mathf.Cos(angle2));
            int y1 = cy + (int)(r * Mathf.Sin(angle2));
            DrawLine(tex, x0, y0, x1, y1, 2, color);
        }
    }

    static void FillBelow(Texture2D tex, int cx, int cy, int r, float startDeg, float endDeg, Color color)
    {
        for (int x = cx - r; x <= cx + r; x++)
        {
            for (int y = cy - r; y <= cy; y++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist <= r && x >= 0 && x < tex.width && y >= 0 && y < tex.height)
                    tex.SetPixel(x, y, color);
            }
        }
    }
}
