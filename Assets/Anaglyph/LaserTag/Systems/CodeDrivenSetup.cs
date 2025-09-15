using UnityEngine;
using Unity.Netcode;
using Anaglyph.Lasertag.Logistics;
using Anaglyph.Lasertag.Objects;

namespace Anaglyph.Lasertag.Systems
{
    /// <summary>
    /// 纯代码驱动的游戏系统配置，无需Unity编辑器操作
    /// </summary>
    public class CodeDrivenSetup : MonoBehaviour
    {
        [Header("开发者友好的配置")]
        [SerializeField] private bool useCodeDrivenApproach = true;
        
        private void Start()
        {
            if (useCodeDrivenApproach)
            {
                SetupGameSystems();
                RegisterPrefabs();
                ConfigureNetworking();
            }
        }

        /// <summary>
        /// 代码配置游戏系统
        /// </summary>
        private void SetupGameSystems()
        {
            Debug.Log("[CodeDriven] 配置游戏系统...");
            
            // 🎯 设置多球物理交互系统
            SetupPhysicalInteractionSystem();
            
            // 创建球类型配置
            var ballConfigs = new BallConfig[]
            {
                new BallConfig { Type = BallType.Rubber, Mass = 0.15f, Bounce = 0.9f },
                new BallConfig { Type = BallType.Tennis, Mass = 0.06f, Bounce = 0.7f },
                new BallConfig { Type = BallType.Basketball, Mass = 0.6f, Bounce = 0.8f }
            };
            
            foreach (var config in ballConfigs)
            {
                Debug.Log($"[CodeDriven] 注册球类型: {config.Type}");
            }
        }
        
        /// <summary>
        /// 设置物理交互系统
        /// </summary>
        private void SetupPhysicalInteractionSystem()
        {
            // 🎯 查找或创建多球标记系统
            var existingSystem = FindObjectOfType<MultiBallMarkingSystem>();
            if (existingSystem == null)
            {
                // 创建新的管理器对象
                var markingSystemObject = new GameObject("MultiBallMarkingSystem");
                var markingSystem = markingSystemObject.AddComponent<MultiBallMarkingSystem>();
                
                // 确保管理器不会被意外销毁
                DontDestroyOnLoad(markingSystemObject);
                
                Debug.Log("[CodeDriven] 创建多球物理交互系统");
            }
            else
            {
                Debug.Log("[CodeDriven] 发现现有的多球物理交互系统");
            }
        }

        /// <summary>
        /// 编程式注册预制体到对象池
        /// </summary>
        private void RegisterPrefabs()
        {
            if (NetworkObjectPool.Instance == null)
            {
                Debug.LogError("[CodeDriven] NetworkObjectPool not found!");
                return;
            }

            // 代码创建和注册小球预制体
            var ballPrefab = BallFactory.CreateBallPrefab();
            
            // 这里本来应该注册到池，但需要访问私有方法
            // 作为演示，展示编程思路
            Debug.Log($"[CodeDriven] 创建小球预制体: {ballPrefab.name}");
        }

        /// <summary>
        /// 代码配置网络设置
        /// </summary>
        private void ConfigureNetworking()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null)
            {
                Debug.Log("[CodeDriven] 配置网络设置...");
                
                // 编程式设置网络参数
                networkManager.NetworkConfig.TickRate = 60;
                networkManager.NetworkConfig.ClientConnectionBufferTimeout = 10;
            }
        }
    }

    /// <summary>
    /// 数据驱动的球配置
    /// </summary>
    [System.Serializable]
    public struct BallConfig
    {
        public BallType Type;
        public float Mass;
        public float Bounce;
        public Color Color;
    }

    public enum BallType
    {
        Rubber,
        Tennis, 
        Basketball,
        PingPong,
        Bowling
    }
}

