using UnityEngine;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 小球设置工具，用于在Unity编辑器中快速配置小球预制体
    /// </summary>
    [RequireComponent(typeof(ThrowableBall))]
    public class BallSetup : MonoBehaviour
    {
        [Header("快速设置")]
        [SerializeField] private BallType ballType = BallType.Rubber;
        [SerializeField] private bool autoSetupOnAwake = true;
        
        [Header("材质库")]
        [SerializeField] private Material ballMaterial;
        [SerializeField] private PhysicsMaterial physicMaterial;
        
        public enum BallType
        {
            Rubber,     // 橡胶球 - 高反弹
            Tennis,     // 网球 - 中等反弹
            Basketball, // 篮球 - 中高反弹
            PingPong,   // 乒乓球 - 轻量高反弹
            Bowling     // 保龄球 - 重量低反弹
        }

        private void Awake()
        {
            if (autoSetupOnAwake)
                SetupBall();
        }

        [ContextMenu("Setup Ball")]
        public void SetupBall()
        {
            var ball = GetComponent<ThrowableBall>();
            var rigidbody = GetComponent<Rigidbody>();
            var collider = GetComponent<SphereCollider>();
            
            // 🎯 修复：现在Renderer直接在父对象上，不在子物体
            var renderer = GetComponent<Renderer>();
            
            // 根据球类型设置物理属性
            SetupPhysicsProperties(rigidbody, collider);
            
            // 设置视觉
            SetupVisuals(renderer);
            
            Debug.Log($"设置完成: {ballType} 类型的小球");
        }

        private void SetupPhysicsProperties(Rigidbody rb, SphereCollider col)
        {
            if (rb == null || col == null) return;
            
            switch (ballType)
            {
                case BallType.Rubber:
                    rb.mass = 0.15f;
                    col.radius = 0.04f;
                    CreatePhysicMaterial(0.9f, 0.3f, 0.4f);
                    break;
                    
                case BallType.Tennis:
                    rb.mass = 0.06f;
                    col.radius = 0.033f;
                    CreatePhysicMaterial(0.7f, 0.4f, 0.5f);
                    break;
                    
                case BallType.Basketball:
                    rb.mass = 0.6f;
                    col.radius = 0.12f;
                    CreatePhysicMaterial(0.8f, 0.5f, 0.6f);
                    break;
                    
                case BallType.PingPong:
                    rb.mass = 0.003f;
                    col.radius = 0.02f;
                    CreatePhysicMaterial(0.9f, 0.1f, 0.2f);
                    break;
                    
                case BallType.Bowling:
                    rb.mass = 7.0f;
                    col.radius = 0.11f;
                    CreatePhysicMaterial(0.2f, 0.8f, 0.9f);
                    break;
            }
        }

        private void CreatePhysicMaterial(float bounce, float friction, float staticFriction)
        {
            if (physicMaterial == null)
            {
                physicMaterial = new PhysicsMaterial($"{ballType}_Material");
            }
            
            physicMaterial.bounciness = bounce;
            physicMaterial.dynamicFriction = friction;
            physicMaterial.staticFriction = staticFriction;
            physicMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            physicMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            
            var collider = GetComponent<SphereCollider>();
            if (collider != null)
                collider.material = physicMaterial;
        }

        private void SetupVisuals(Renderer renderer)
        {
            if (renderer == null || ballMaterial == null) return;
            
            renderer.material = ballMaterial;
        }
        
        /// <summary>
        /// 🎯 设置小球材质 (由BallFactory调用)
        /// </summary>
        public void SetBallMaterial(Material material)
        {
            ballMaterial = material;
        }

        // 编辑器工具方法
        #if UNITY_EDITOR
        [ContextMenu("Create Ball Materials")]
        private void CreateBallMaterials()
        {
            // 这个方法可以在编辑器中批量创建材质
            UnityEditor.AssetDatabase.CreateFolder("Assets/Anaglyph/LaserTag/Objects/Gameplay", "Materials");
            
            foreach (BallType type in System.Enum.GetValues(typeof(BallType)))
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = $"{type}_Ball_Material";
                
                // 设置不同球类型的颜色
                Color ballColor = type switch
                {
                    BallType.Rubber => Color.red,
                    BallType.Tennis => Color.yellow,
                    BallType.Basketball => new Color(1f, 0.5f, 0f), // 橙色
                    BallType.PingPong => Color.white,
                    BallType.Bowling => Color.black,
                    _ => Color.gray
                };
                
                material.color = ballColor;
                
                string path = $"Assets/Anaglyph/LaserTag/Objects/Gameplay/Materials/{material.name}.mat";
                UnityEditor.AssetDatabase.CreateAsset(material, path);
            }
            
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            
            Debug.Log("球类材质创建完成！");
        }
        #endif
    }
}
