using UnityEngine;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// 桌面测试模式：禁用VR组件，专注测试小球物理
    /// </summary>
    public class DesktopTestMode : MonoBehaviour
    {
        [Header("桌面测试设置")]
        [SerializeField] private bool enableDesktopMode = true;
        [SerializeField] private bool disableVRComponents = true;
        [SerializeField] private bool disableGameManagers = true;

        private void Awake()
        {
            if (enableDesktopMode)
            {
                SetupDesktopMode();
            }
        }

        private void SetupDesktopMode()
        {
            Debug.Log("[DesktopTestMode] 启用桌面测试模式");

            if (disableVRComponents)
            {
                DisableVRComponents();
            }

            if (disableGameManagers)
            {
                DisableGameManagers();
            }

            SetupBasicCamera();
        }

        /// <summary>
        /// 禁用VR相关组件
        /// </summary>
        private void DisableVRComponents()
        {
            // 禁用边界相关组件
            var boundaryComponents = FindObjectsOfType<MonoBehaviour>();
            foreach (var component in boundaryComponents)
            {
                if (component.GetType().Name.Contains("Boundary"))
                {
                    component.enabled = false;
                    Debug.Log($"[DesktopTestMode] 禁用VR组件: {component.GetType().Name}");
                }
            }

            // 禁用VR特定的播放器组件
            var mainPlayers = FindObjectsOfType<MainPlayer>();
            foreach (var player in mainPlayers)
            {
                player.enabled = false;
                Debug.Log("[DesktopTestMode] 禁用MainPlayer VR组件");
            }
        }

        /// <summary>
        /// 禁用游戏管理器（避免空引用）
        /// </summary>
        private void DisableGameManagers()
        {
            // 这些组件通常依赖VR初始化
            string[] managerNames = {
                "MatchReferee", 
                "WeaponsManagement", 
                "ColocationManager"
            };

            foreach (string managerName in managerNames)
            {
                var manager = GameObject.Find(managerName);
                if (manager != null)
                {
                    var components = manager.GetComponents<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        if (comp.GetType().Name.Contains(managerName))
                        {
                            comp.enabled = false;
                            Debug.Log($"[DesktopTestMode] 禁用管理器: {comp.GetType().Name}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 设置基础相机用于桌面测试
        /// </summary>
        private void SetupBasicCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                // 创建基础相机
                var cameraObj = new GameObject("Desktop Test Camera");
                camera = cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }

            // 设置相机位置用于观察小球
            camera.transform.position = new Vector3(0, 2, -3);
            camera.transform.rotation = Quaternion.Euler(15, 0, 0);
            camera.fieldOfView = 60f;

            Debug.Log("[DesktopTestMode] 设置桌面测试相机");
        }

        private void OnGUI()
        {
            if (enableDesktopMode)
            {
                GUI.Box(new Rect(10, 10, 300, 60), 
                    "🖥️ 桌面测试模式\n" +
                    "VR组件已禁用\n" +
                    "专注测试小球物理系统");
            }
        }
    }
}

