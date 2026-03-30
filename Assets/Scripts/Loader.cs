using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 热更新启动加载器（属于 Framework 程序集，不热更）
/// 职责：下载最新 HotUpdate.dll → 加载 → 启动热更入口
/// </summary>
public class Loader : MonoBehaviour
{
    [Header("服务器配置")]
    public string serverUrl = "http://127.0.0.1:8000";

    [Header("UI（可选）")]
    public Text statusText;

    private string HotUpdateDllUrl
    {
        get { return serverUrl + "/hotupdate/HotUpdate.dll"; }
    }

    private string LocalDllPath
    {
        get { return Path.Combine(Application.persistentDataPath, "HotUpdate.dll"); }
    }

    private void Start()
    {
        StartCoroutine(LoadHotUpdate());
    }

    private IEnumerator LoadHotUpdate()
    {
        SetStatus("检查更新...");

        // 尝试从服务器下载最新 DLL
        bool downloaded = false;
        var request = UnityWebRequest.Get(HotUpdateDllUrl);
        request.timeout = 10;
        yield return request.SendWebRequest();

        if (request.isNetworkError || request.isHttpError)
        {
            Debug.LogWarning("热更新下载失败，使用本地缓存: " + request.error);
            SetStatus("使用本地版本...");
        }
        else
        {
            byte[] dllBytes = request.downloadHandler.data;
            File.WriteAllBytes(LocalDllPath, dllBytes);
            downloaded = true;
            Debug.Log("热更新 DLL 下载成功: " + dllBytes.Length + " bytes");
            SetStatus("更新完成");
        }

        // 加载 DLL
        byte[] assemblyData = null;

#if UNITY_EDITOR
        // 编辑器下直接从 HybridCLR 编译输出目录加载
        string editorDllPath = Path.Combine(
            Application.dataPath,
            "../HybridCLRData/HotUpdateDlls/StandaloneOSX/HotUpdate.dll"
        );
        if (File.Exists(editorDllPath))
        {
            assemblyData = File.ReadAllBytes(editorDllPath);
            Debug.Log("编辑器模式：加载 HybridCLR 编译的 DLL");
        }
        else
        {
            // 编辑器下如果没有编译过 HybridCLR DLL，使用 Library 中的
            Debug.Log("编辑器模式：HotUpdate 程序集由 Unity 直接编译加载");
            StartHotUpdateEntry();
            yield break;
        }
#else
        // 真机环境：从本地缓存加载
        if (File.Exists(LocalDllPath))
        {
            assemblyData = File.ReadAllBytes(LocalDllPath);
        }
        else
        {
            // 首次安装，从 StreamingAssets 加载内置版本
            string builtinPath = Path.Combine(Application.streamingAssetsPath, "HotUpdate.dll");
            var builtinRequest = UnityWebRequest.Get(builtinPath);
            yield return builtinRequest.SendWebRequest();
            if (!builtinRequest.isNetworkError && !builtinRequest.isHttpError)
            {
                assemblyData = builtinRequest.downloadHandler.data;
            }
        }
#endif

        if (assemblyData != null)
        {
            Assembly.Load(assemblyData);
            Debug.Log("HotUpdate 程序集加载成功");
        }

        SetStatus("启动中...");
        StartHotUpdateEntry();
    }

    private void StartHotUpdateEntry()
    {
        // 通过 AddComponent 启动热更代码中的入口脚本
        // LoginUI 是热更程序集中的入口
        var entryType = FindType("Symbiosis.UI.LoginUI");
        if (entryType != null)
        {
            // 查找场景中预设的挂载点，或创建一个
            var entryGo = GameObject.Find("HotUpdateEntry");
            if (entryGo == null)
            {
                entryGo = new GameObject("HotUpdateEntry");
            }
            entryGo.AddComponent(entryType);
            Debug.Log("热更入口启动: " + entryType.FullName);
        }
        else
        {
            Debug.LogError("找不到热更入口类型 Symbiosis.UI.LoginUI");
        }

        SetStatus("");
    }

    private Type FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType(fullName);
            if (type != null) return type;
        }
        return null;
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }
}
