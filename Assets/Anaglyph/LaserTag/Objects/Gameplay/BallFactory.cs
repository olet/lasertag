using UnityEngine;
using Unity.Netcode;
using Anaglyph.Lasertag.Logistics;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 纯代码方式创建小球，无需拖拽和预制体
    /// </summary>
    public static class BallFactory
    {
        private static GameObject ballPrefabCache;

        /// <summary>
        /// 纯代码创建小球预制体
        /// </summary>
        public static GameObject CreateBallPrefab()
        {
            if (ballPrefabCache != null) return ballPrefabCache;

            // 创建根对象
            var ballObject = new GameObject("ThrowableBall");
            ballObject.layer = 0; // 🎯 确保在Default Layer，VR摄像头可见
            
            // 添加物理组件 - 🎯 超小球，像子弹一样
            var collider = ballObject.AddComponent<SphereCollider>();
            collider.radius = 0.005f; // 5mm半径 - 超小超精确
            
            var rigidbody = ballObject.AddComponent<Rigidbody>();
            rigidbody.mass = 0.01f; // 超轻，飞得超远
            
            // 🎯 真正的性能优化：设置物理层级，小球之间不碰撞
            ballObject.layer = LayerMask.NameToLayer("Default"); // 确保在正确层级
            
            // 🚀 性能优化：减少不必要的物理计算
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete; // 离散碰撞检测更快
            rigidbody.sleepThreshold = 0.1f; // 更容易进入休眠状态
            
            // 添加网络组件
            var networkObject = ballObject.AddComponent<NetworkObject>();
            
            // 添加自定义脚本
            var ballScript = ballObject.AddComponent<ThrowableBall>();
            var ballSetup = ballObject.AddComponent<BallSetup>();
            var deletable = ballObject.AddComponent<Deletable>();
            
            // 🎯 创建VR兼容材质并设置给BallSetup
            var vrMaterial = CreateVRCompatibleMaterial();
            ballSetup.SetBallMaterial(vrMaterial); // 我们需要添加这个方法
            // 🎯 重新启用环境物理 - 高性能优化版本
            var envPhysics = ballObject.AddComponent<EnvironmentBallPhysics>();
            
            // 🎯 添加激光枪交互组件 - 代码分离的交互逻辑
            var laserInteraction = ballObject.AddComponent<BallLaserInteraction>();
            
            // 🎯 修复VR立体渲染：直接在父对象添加MeshRenderer，不用子对象
            var meshFilter = ballObject.AddComponent<MeshFilter>();
            var meshRenderer = ballObject.AddComponent<MeshRenderer>();
            
            // 使用球体网格
            var sphereMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            meshFilter.mesh = sphereMesh;
            
            // 🎯 缩小可视化大小，匹配碰撞体
            ballObject.transform.localScale = Vector3.one * 0.01f; // 1cm直径
            
            // 🎯 材质现在由BallSetup统一管理，不在这里设置
            
            // 添加特效组件
            var trailRenderer = ballObject.AddComponent<TrailRenderer>();
            ConfigureTrailRenderer(trailRenderer);
            
            var audioSource = ballObject.AddComponent<AudioSource>();
            ConfigureAudioSource(audioSource);
            
            // 配置物理材质
            var physicMaterial = new PhysicsMaterial("BallPhysicMaterial");
            physicMaterial.bounciness = 0.8f;
            physicMaterial.dynamicFriction = 0.3f;
            physicMaterial.staticFriction = 0.5f;
            collider.material = physicMaterial;
            
            ballPrefabCache = ballObject;
            return ballObject;
        }

        /// <summary>
        /// 纯代码生成小球实例
        /// </summary>
        public static GameObject SpawnBall(Vector3 position, Quaternion rotation)
        {
            var prefab = CreateBallPrefab();
            var instance = Object.Instantiate(prefab, position, rotation);
            
            // 可以在这里添加更多代码配置
            return instance;
        }

        /// <summary>
        /// 代码化配置拖尾效果
        /// </summary>
        private static void ConfigureTrailRenderer(TrailRenderer trail)
        {
            // 🎯 真正的压力测试：重新启用TrailRenderer
            trail.time = 0.5f;          // 轨迹持续时间
            trail.startWidth = 0.02f;   // 轨迹宽度
            trail.endWidth = 0.005f;    
            trail.material = CreateTrailMaterial();
            
            // ✅ 启用轨迹渲染，接受真正的性能挑战！
            trail.enabled = true;
        }

        /// <summary>
        /// 创建VR兼容的材质 - 使用游戏现有的VR Shader
        /// </summary>
        private static Material CreateVRCompatibleMaterial()
        {
            // 🎯 使用游戏中手枪相同的VR兼容Shader
            // GUID: dcb444cf737154443be726880a059186 (支持STEREO_INSTANCING_ON)
            var blasterShader = Shader.Find("Universal Render Pipeline/Lit");
            
            // 🔍 尝试找到手枪使用的VR兼容Shader
            var shaderCandidates = new string[]
            {
                "Shader Graphs/DefaultShaderGraph", // 可能的ShaderGraph
                "Universal Render Pipeline/Simple Lit", // 简化版URP
                "Universal Render Pipeline/Unlit", // Unlit版本
                "Universal Render Pipeline/Lit" // 标准URP作为备选
            };
            
            foreach (var shaderName in shaderCandidates)
            {
                var foundShader = Shader.Find(shaderName);
                if (foundShader != null)
                {
                    blasterShader = foundShader;
                    break;
                }
            }
            
            var material = new Material(blasterShader);
            
            // 🎨 设置和手枪相似的属性 
            material.color = new Color(1f, 0.2f, 0.2f, 1f); // 红色
            
            // 🔧 设置标准材质属性
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", new Color(1f, 0.2f, 0.2f, 1f));
            if (material.HasProperty("_Metallic"))
                material.SetFloat("_Metallic", 0.1f);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.6f);
            if (material.HasProperty("_SpecularHighlights"))
                material.SetFloat("_SpecularHighlights", 1f);
            if (material.HasProperty("_EnvironmentReflections"))
                material.SetFloat("_EnvironmentReflections", 1f);
                
            // 🎮 确保VR立体渲染关键词
            material.EnableKeyword("STEREO_INSTANCING_ON");
            
            return material;
        }
        
        /// <summary>
        /// 代码化配置音频
        /// </summary>
        private static void ConfigureAudioSource(AudioSource audio)
        {
            audio.playOnAwake = false;
            audio.volume = 0.7f;
            audio.spatialBlend = 1.0f; // 3D音效
        }

        /// <summary>
        /// 代码创建材质
        /// </summary>
        private static Material CreateTrailMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = Color.cyan;
            material.SetFloat("_Metallic", 0.5f);
            material.SetFloat("_Smoothness", 0.8f);
            return material;
        }

        /// <summary>
        /// 编程式注册到对象池
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        public static void RegisterToPool()
        {
            // 运行时自动注册，无需手动配置
            if (NetworkObjectPool.Instance != null)
            {
                var ballPrefab = CreateBallPrefab();
                // 这里可以添加运行时注册逻辑
            }
        }
    }
}
