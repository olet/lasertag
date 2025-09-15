using UnityEngine;
using System.Collections.Generic;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 物理交互检测器
    /// 负责检测真实世界物体与小球的6方向接触
    /// </summary>
    public class PhysicalInteractionDetector : MonoBehaviour
    {
        [Header("检测配置")]
        [SerializeField] private bool isDetecting = false;
        [SerializeField] private bool debugVisualization = false;
        
        // 6个检测方向
        private readonly Vector3[] directions = {
            Vector3.up,     Vector3.down,
            Vector3.left,   Vector3.right, 
            Vector3.forward, Vector3.back
        };
        
        // 组件引用
        private InteractableBall ballComponent;
        
        // 调试计数
        private int detectionCount = 0;
        private float lastDebugTime = 0f;
        
        // 🎯 距离对比检测数据
        private Dictionary<Vector3, float> lastDetectionDistances = new Dictionary<Vector3, float>();
        private Dictionary<Vector3, int> validHitCounts = new Dictionary<Vector3, int>(); // 连续有效检测计数
        private const float DISTANCE_REDUCTION_THRESHOLD = 0.10f;  // 距离缩短5cm才算拍打（严格过滤TSDF噪声）
        private const float FINAL_DISTANCE_THRESHOLD = 0.8f;       // 最终距离限制80cm（与检测范围一致）
        private const int REQUIRED_VALID_HITS = 1;                 // 需要连续2次有效检测才触发
        
        // 高级检测数据（预留用于未来功能）
        private struct AdvancedDetectionData
        {
            public Vector3 lastHitPoint;
            public Vector3 currentHitPoint;
            public float lastHitTime;
            public float estimatedVelocity;
            public Vector3 preciseDirection;
        }
        private AdvancedDetectionData advancedData;
        
        /// <summary>
        /// 初始化检测器
        /// </summary>
        public void Initialize(InteractableBall ball)
        {
            ballComponent = ball;
            
            if (ballComponent == null)
            {
                Debug.LogError($"[PhysicalDetector] {name} 缺少 InteractableBall 组件！");
                enabled = false;
            }
        }
        
        /// <summary>
        /// 开始检测
        /// </summary>
        public void StartDetection()
        {
            if (!isDetecting && ballComponent != null)
            {
                isDetecting = true;
                InvokeRepeating(nameof(CheckPhysicalContact), 0f, ballComponent.CheckInterval);
                
                Debug.Log($"[PhysicalDetector] 🎯 {name} 开始超严格拍打检测，间隔:{ballComponent.CheckInterval}s，半径:{ballComponent.DetectionRadius}m，距离缩短阈值:{DISTANCE_REDUCTION_THRESHOLD}m，需要连续{REQUIRED_VALID_HITS}次");
            }
        }
        
        /// <summary>
        /// 停止检测
        /// </summary>
        public void StopDetection()
        {
            if (isDetecting)
            {
                isDetecting = false;
                CancelInvoke(nameof(CheckPhysicalContact));
                
                // 🧹 清理距离记录和计数器
                lastDetectionDistances.Clear();
                validHitCounts.Clear();
                
                Debug.Log($"[PhysicalDetector] {name} 停止物理接触检测");
            }
        }
        
        /// <summary>
        /// 检测6方向物理接触
        /// </summary>
        void CheckPhysicalContact()
        {
            if (!isDetecting || ballComponent == null || !ballComponent.IsMarkedForInteraction)
            {
                StopDetection();
                return;
            }
            
            Vector3 ballPosition = transform.position;
            float detectionRadius = ballComponent.DetectionRadius;
            
            // 🎯 检测6个方向（不排除任何方向，只靠距离判断）
            foreach (Vector3 direction in directions)
            {
                // 🔍 距离对比检测
                bool shouldTriggerHit = CheckDirectionalContactWithDistanceComparison(ballPosition, direction, detectionRadius);
                
                if (shouldTriggerHit)
                {
                    Debug.Log($"[PhysicalDetector] 🎯 检测到从{direction}方向的拍打接触！（距离缩短）");
                    TriggerPhysicalHit(direction);
                    return; // 检测到一次接触就足够了
                }
            }
        }
        
        /// <summary>
        /// 🎯 简化的距离对比检测（两次检测距离缩短就算拍打）
        /// </summary>
        bool CheckDirectionalContactWithDistanceComparison(Vector3 ballPosition, Vector3 direction, float maxDistance)
        {
            Ray detectionRay = new Ray(ballPosition, direction);
            float currentDistance = maxDistance;
            bool hasContact = false;
            
            // 🔍 检测当前距离
            if (EnvironmentMapper.Raycast(detectionRay, maxDistance, out var envHit))
            {
                currentDistance = Vector3.Distance(ballPosition, envHit.point);
                hasContact = true;
            }
            else if (Physics.Raycast(detectionRay, out RaycastHit physicsHit, maxDistance))
            {
                currentDistance = physicsHit.distance;
                hasContact = true;
            }
            
            // 📊 严格的距离对比逻辑（只对比连续检测到的情况）
            if (hasContact && lastDetectionDistances.ContainsKey(direction))
            {
                float lastDistance = lastDetectionDistances[direction];
                
                // 🚫 只有上次也检测到才对比（避免从maxDistance的误判）
                if (lastDistance < maxDistance * 0.9f) // 上次是真实检测值
                {
                    float distanceReduction = lastDistance - currentDistance;
                    
                    // 🎯 严格判断：距离明显缩短 + 在检测范围内 + 最终距离合理（避免远距离噪声）
                    if (distanceReduction > DISTANCE_REDUCTION_THRESHOLD && 
                        currentDistance <= FINAL_DISTANCE_THRESHOLD && 
                        currentDistance >= 0.10f && // 最终距离至少5cm（避免太近的噪声）
                        currentDistance <= 0.8f)   // 最终距离最多60cm（真实拍打距离）
                    {
                        // 增加有效检测计数
                        int hitCount = validHitCounts.ContainsKey(direction) ? validHitCounts[direction] : 0;
                        hitCount++;
                        validHitCounts[direction] = hitCount;
                        
                        Debug.Log($"[ValidHit] {direction}方向有效检测{hitCount}/{REQUIRED_VALID_HITS}: 距离缩短{distanceReduction:F3}m ({lastDistance:F3}→{currentDistance:F3})");
                        
                        // 只有连续多次有效检测才触发
                        if (hitCount >= REQUIRED_VALID_HITS)
                        {
                            Debug.Log($"[ConfirmedHit] 🎯 {direction}方向确认拍打! 连续{hitCount}次有效检测");
                            validHitCounts[direction] = 0; // 重置计数
                            lastDetectionDistances[direction] = currentDistance;
                            return true;
                        }
                    }
                    else
                    {
                        // 条件不满足，重置计数
                        validHitCounts[direction] = 0;
                    }
                }
            }
            
            // 📝 更新距离记录（只在有真实检测时）
            if (hasContact)
            {
                lastDetectionDistances[direction] = currentDistance;
            }
            else
            {
                // 🚫 没检测到时清除记录和计数器，避免从maxDistance误判
                if (lastDetectionDistances.ContainsKey(direction))
                {
                    lastDetectionDistances.Remove(direction);
                }
                if (validHitCounts.ContainsKey(direction))
                {
                    validHitCounts.Remove(direction);
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// 检测特定方向的接触（原版方法，保留备用）
        /// </summary>
        bool CheckDirectionalContact(Vector3 ballPosition, Vector3 direction, float distance)
        {
            // 创建射线
            Ray detectionRay = new Ray(ballPosition, direction);
            
            // 使用 EnvironmentMapper 进行环境射线检测
            bool hasEnvironmentContact = EnvironmentMapper.Raycast(detectionRay, distance, out var envHit);
            
            if (hasEnvironmentContact)
            {
                return true;
            }
            
            // 可选：同时检查Unity物理系统中的物体
            bool hasPhysicsContact = Physics.Raycast(detectionRay, out RaycastHit physicsHit, distance); // 检测所有层
            
            return hasPhysicsContact;
        }
        
        /// <summary>
        /// 触发物理碰撞响应
        /// </summary>
        void TriggerPhysicalHit(Vector3 contactDirection)
        {
            StopDetection(); // 停止检测
            
            // 通知小球组件处理碰撞
            if (ballComponent != null)
            {
                ballComponent.TriggerPhysicalHit(contactDirection);
            }
            
            // 记录高级检测数据（用于未来功能）
            RecordAdvancedData(contactDirection);
        }
        
        /// <summary>
        /// 记录高级检测数据（预留功能）
        /// </summary>
        void RecordAdvancedData(Vector3 contactDirection)
        {
            // 未来可以用来计算速度、精确方向等
            advancedData.currentHitPoint = transform.position + contactDirection * ballComponent.DetectionRadius;
            advancedData.lastHitTime = Time.time;
            
            // TODO: 实现速度计算和精确方向检测
        }
        
        /// <summary>
        /// 获取检测状态（用于调试）
        /// </summary>
        public bool IsDetecting => isDetecting;
        
        /// <summary>
        /// 调试可视化
        /// </summary>
        void OnDrawGizmos()
        {
            if (!debugVisualization || !isDetecting || !Application.isPlaying) return;
            
            if (ballComponent != null)
            {
                Gizmos.color = Color.red;
                Vector3 ballPos = transform.position;
                float radius = ballComponent.DetectionRadius;
                
                // 绘制6个方向的检测射线
                foreach (Vector3 direction in directions)
                {
                    Gizmos.DrawRay(ballPos, direction * radius);
                }
                
                // 绘制检测范围球体
                Gizmos.color = Color.red * 0.3f;
                Gizmos.DrawWireSphere(ballPos, radius);
            }
        }
        
        /// <summary>
        /// 运行时切换调试可视化
        /// </summary>
        [ContextMenu("Toggle Debug Visualization")]
        public void ToggleDebugVisualization()
        {
            debugVisualization = !debugVisualization;
            Debug.Log($"[PhysicalDetector] {name} 调试可视化: {debugVisualization}");
        }
        
        /// <summary>
        /// 清理
        /// </summary>
        void OnDestroy()
        {
            StopDetection();
        }
    }
}
