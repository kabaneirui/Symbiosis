using UnityEngine;
using UnityEngine.EventSystems;

namespace Symbiosis.Services
{
    /// <summary>
    /// UI 管理器 — 动态加载/销毁界面 Prefab（属于 Framework，不热更）
    /// 所有 Prefab 放在 Resources/UI/ 下，按名字加载
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("UI 根节点")]
        public Transform uiRoot;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            var es = FindObjectOfType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                Debug.Log("自动创建 EventSystem + StandaloneInputModule");
            }
            else
            {
                // EventSystem 存在但可能缺少 InputModule
                if (es.GetComponent<StandaloneInputModule>() == null
                    && es.GetComponent<BaseInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                    Debug.Log("为已有 EventSystem 补充 StandaloneInputModule");
                }
            }
        }

        /// <summary>
        /// 从 Resources/UI/ 加载并实例化一个界面 Prefab
        /// </summary>
        public GameObject Open(string prefabName)
        {
            string path = "UI/" + prefabName;
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError("找不到 UI Prefab: " + path + "，请先运行菜单 Symbiosis/生成UI Prefab");
                return null;
            }

            if (uiRoot == null)
            {
                EnsureCanvas();
            }

            Transform parent = uiRoot != null ? uiRoot : transform;
            var instance = Instantiate(prefab, parent);
            instance.name = prefabName;
            Debug.Log("打开界面: " + prefabName);
            return instance;
        }

        private void EnsureCanvas()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                uiRoot = canvas.transform;
                if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            else
            {
                var canvasGo = new GameObject("Canvas",
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                uiRoot = canvasGo.transform;
                Debug.Log("自动创建 Canvas");
            }
        }

        /// <summary>
        /// 关闭（销毁）一个界面
        /// </summary>
        public void Close(string panelName)
        {
            if (uiRoot == null) return;
            var target = uiRoot.Find(panelName);
            if (target != null)
                Destroy(target.gameObject);
        }

        /// <summary>
        /// 查找已打开的界面
        /// </summary>
        public GameObject Find(string panelName)
        {
            if (uiRoot == null) return null;
            var target = uiRoot.Find(panelName);
            return target != null ? target.gameObject : null;
        }
    }
}
