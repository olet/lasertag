using UnityEngine;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 可交互小球组件
    /// 负责单个球的交互状态管理和视觉反馈
    /// </summary>
    [RequireComponent(typeof(EnvironmentBallPhysics))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class InteractableBall : MonoBehaviour
    {
        [Header("交互状态")]
        [SerializeField] private bool isMarkedForInteraction = false;
        [SerializeField] private BallMarkingMethod currentMarkingMethod = BallMarkingMethod.None;
        [SerializeField] private float markingTime = 0f;
        
        [Header("检测设置")]
        [SerializeField] private float detectionRadius = 0.2f;      // 20cm检测半径（精确拍打范围）
        [SerializeField] private float checkInterval = 0.20f;       // 每0.25秒检测一次（确保TSDF数据更新）
        
        [Header("反弹力设置")]
        [SerializeField] private float hitForceMin = 0.5f;          // 最小反弹力
        [SerializeField] private float hitForceMax = 1.5f;            // 最大反弹力
        
        // 组件引用
        private PhysicalInteractionDetector detector;
        private EnvironmentBallPhysics ballPhysics;
        private Rigidbody rb;
        private Renderer ballRenderer;
        private Material originalMaterial;
        
        // 钉住状态数据
        private Vector3 stuckDirection = Vector3.zero;  // 钉住的方向（需要排除检测）
        
        // 属性访问器
        public bool IsMarkedForInteraction => isMarkedForInteraction;
        public BallMarkingMethod CurrentMarkingMethod => currentMarkingMethod;
        public float MarkingTime => markingTime;
        public float DetectionRadius => detectionRadius;
        public float CheckInterval => checkInterval;
        public Vector3 StuckDirection => stuckDirection;
        
        void Awake()
        {
            // 获取组件引用
            ballPhysics = GetComponent<EnvironmentBallPhysics>();
            rb = GetComponent<Rigidbody>();
            ballRenderer = GetComponent<Renderer>();
            
            // 备份原始材质
            if (ballRenderer != null)
            {
                originalMaterial = ballRenderer.material;
            }
            
            // 添加物理检测器组件
            detector = GetComponent<PhysicalInteractionDetector>();
            if (detector == null)
            {
                detector = gameObject.AddComponent<PhysicalInteractionDetector>();
            }
        }
        
        void Start()
        {
            // 确保检测器正确配置
            if (detector != null)
            {
                detector.Initialize(this);
            }
        }
        
        /// <summary>
        /// 标记为可交互
        /// </summary>
        public void MarkForInteraction(BallMarkingMethod method)
        {
            if (!CanBeMarked())
            {
                Debug.LogWarning($"[InteractableBall] 球 {name} 不能被标记：不在钉住状态");
                return;
            }
            
            if (!isMarkedForInteraction || currentMarkingMethod != method)
            {
                isMarkedForInteraction = true;
                currentMarkingMethod = method;
                markingTime = Time.time;
                
                // 开始物理检测
                if (detector != null)
                {
                    detector.StartDetection();
                }
                
                // 应用视觉反馈
                ApplyMarkingVisualFeedback();
                
                Debug.Log($"[InteractableBall] 球 {name} 被{method.GetDisplayName()}标记为可交互");
            }
        }
        
        /// <summary>
        /// 取消交互标记
        /// </summary>
        public void UnmarkInteraction()
        {
            if (isMarkedForInteraction)
            {
                isMarkedForInteraction = false;
                currentMarkingMethod = BallMarkingMethod.None;
                
                // 停止物理检测
                if (detector != null)
                {
                    detector.StopDetection();
                }
                
                // 恢复原始外观
                RemoveMarkingVisualFeedback();
                
                Debug.Log($"[InteractableBall] 球 {name} 取消交互标记");
            }
        }
        
        /// <summary>
        /// 检查球是否可以被标记
        /// </summary>
        public bool CanBeMarked()
        {
            return ballPhysics != null && ballPhysics.IsGrounded();
        }
        
        /// <summary>
        /// 触发物理碰撞响应
        /// </summary>
        public void TriggerPhysicalHit(Vector3 contactDirection)
        {
            // 取消标记
            UnmarkInteraction();
            
            // 重新激活球的物理
            if (ballPhysics != null)
            {
                ballPhysics.ResetStuckState();
            }
            
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                
                // 施加反向力
                Vector3 hitForce = (-contactDirection).normalized * Random.Range(hitForceMin, hitForceMax);
                rb.AddForce(hitForce, ForceMode.Impulse);
                
                Debug.Log($"[InteractableBall] 球 {name} 被物理拍飞！接触方向:{contactDirection:F2}, 反弹力:{hitForce:F2}");
            }
            
            // 特殊视觉反馈：被拍中的球变为青色
            if (ballRenderer != null)
            {
                ballRenderer.material.color = Color.cyan;
            }
        }
        
        /// <summary>
        /// 应用标记视觉反馈
        /// </summary>
        private void ApplyMarkingVisualFeedback()
        {
            if (ballRenderer == null) return;
            
            Color markingColor = currentMarkingMethod.GetMarkingColor();
            ballRenderer.material.SetColor("_EmissionColor", markingColor * 0.3f);
            
            // 确保材质支持发光
            ballRenderer.material.EnableKeyword("_EMISSION");
        }
        
        /// <summary>
        /// 移除标记视觉反馈
        /// </summary>
        private void RemoveMarkingVisualFeedback()
        {
            if (ballRenderer == null) return;
            
            ballRenderer.material.SetColor("_EmissionColor", Color.black);
            ballRenderer.material.DisableKeyword("_EMISSION");
        }
        
        /// <summary>
        /// 🚫 自动管理已禁用：不再自动标记钉住的球
        /// </summary>
        void Update()
        {
            // 🔍 调试：输出状态信息
            bool canBeMarked = CanBeMarked();
            bool isGrounded = ballPhysics != null ? ballPhysics.IsGrounded() : false;
            
            if (Time.frameCount % 60 == 0) // 每秒输出一次调试信息
            {
                Debug.Log($"[InteractableBall] 球 {name} 状态检查: ballPhysics={ballPhysics != null}, isGrounded={isGrounded}, canBeMarked={canBeMarked}, isMarked={isMarkedForInteraction}");
            }
            
            // 🚫 自动标记功能已禁用
            /* 
            // 🎯 测试模式：自动标记所有钉住的球
            if (canBeMarked && !isMarkedForInteraction)
            {
                // 🔍 检测钉住的方向
                DetectStuckDirection();
                
                // 自动标记为手动标记（紫色发光）
                MarkForInteraction(BallMarkingMethod.Manual);
                Debug.Log($"[AutoMark] 球 {name} 钉住后自动标记为可拍打！钉住方向: {stuckDirection:F2}");
            }
            else if (isMarkedForInteraction && !canBeMarked)
            {
                // 球开始移动，取消标记
                UnmarkInteraction();
            }
            */
        }
        
        /// <summary>
        /// 检测球钉住的方向（用于排除检测）
        /// </summary>
        private void DetectStuckDirection()
        {
            Vector3[] directions = {
                Vector3.up, Vector3.down, Vector3.left, 
                Vector3.right, Vector3.forward, Vector3.back
            };
            
            float minDistance = float.MaxValue;
            Vector3 closestDirection = Vector3.zero;
            
            foreach (Vector3 direction in directions)
            {
                Ray detectionRay = new Ray(transform.position, direction);
                
                // 检测这个方向最近的碰撞
                if (EnvironmentMapper.Raycast(detectionRay, 0.1f, out var envHit))
                {
                    float distance = Vector3.Distance(transform.position, envHit.point);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestDirection = direction;
                    }
                }
            }
            
            stuckDirection = closestDirection;
            Debug.Log($"[StuckDirection] 球 {name} 钉住方向检测: {stuckDirection:F2}，距离: {minDistance:F3}m");
        }
        
        /// <summary>
        /// 清理时确保停止检测
        /// </summary>
        void OnDestroy()
        {
            if (detector != null)
            {
                detector.StopDetection();
            }
        }
    }
}
