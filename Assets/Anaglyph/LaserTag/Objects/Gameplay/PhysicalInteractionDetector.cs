using UnityEngine;
using System.Collections.Generic;
using Anaglyph.XRTemplate;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 物理交互检测器
    /// 负责使用Quest 3 TSDF环境数据检测真实世界物体与小球的6方向接触
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
        private const float FINAL_DISTANCE_THRESHOLD = 0.2f;       // 最终距离限制20cm（与检测范围一致）
        private const int REQUIRED_VALID_HITS = 1;                 // 需要连续2次有效检测才触发
        
        // 可视化调试数据
        private Dictionary<Vector3, bool> directionHasContact = new Dictionary<Vector3, bool>();
        private Dictionary<Vector3, float> directionDistances = new Dictionary<Vector3, float>();
        private Dictionary<Vector3, Vector3> directionHitPoints = new Dictionary<Vector3, Vector3>();
        
        // 运行时射线可视化
        [SerializeField] private bool showRuntimeRays = true;
        private Dictionary<Vector3, LineRenderer> directionLineRenderers = new Dictionary<Vector3, LineRenderer>();
        
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
                
                // 创建运行时射线可视化
                if (showRuntimeRays)
                {
                    CreateRuntimeRayVisualizers();
                }
                
                Debug.Log($"[PhysicalDetector] 🎯 {name} 开始20cm精确拍打检测，间隔:{ballComponent.CheckInterval}s，半径:{ballComponent.DetectionRadius}m，距离缩短阈值:{DISTANCE_REDUCTION_THRESHOLD}m");
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
                
                // 🎨 清理可视化数据
                directionHasContact.Clear();
                directionDistances.Clear();
                directionHitPoints.Clear();
                
                // 🎨 清理运行时射线
                DestroyRuntimeRayVisualizers();
                
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
            Vector3 hitPoint = ballPosition + direction * maxDistance;
            
            // 🔍 检测当前距离（仅使用Quest 3环境检测）
            if (EnvironmentMapper.Raycast(detectionRay, maxDistance, out var envHit))
            {
                currentDistance = Vector3.Distance(ballPosition, envHit.point);
                hitPoint = envHit.point;
                hasContact = true;
            }
            
            // 🎨 更新可视化数据
            directionHasContact[direction] = hasContact;
            directionDistances[direction] = currentDistance;
            directionHitPoints[direction] = hitPoint;
            
            // 🎮 更新运行时射线可视化
            if (showRuntimeRays)
            {
                UpdateRuntimeRayVisualization(direction, ballPosition, hitPoint, hasContact, currentDistance);
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
                        currentDistance >= 0.01f && // 最终距离至少1cm（避免太近的噪声）
                        currentDistance <= 0.2f)   // 最终距离最多20cm（真实拍打距离）
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
        /// 检测特定方向的接触（原版方法，保留备用）- 仅Quest 3环境检测
        /// </summary>
        bool CheckDirectionalContact(Vector3 ballPosition, Vector3 direction, float distance)
        {
            // 创建射线
            Ray detectionRay = new Ray(ballPosition, direction);
            
            // 仅使用 EnvironmentMapper 进行Quest 3环境射线检测
            bool hasEnvironmentContact = EnvironmentMapper.Raycast(detectionRay, distance, out var envHit);
            
            return hasEnvironmentContact;
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
        /// 🎮 创建运行时射线可视化器
        /// </summary>
        void CreateRuntimeRayVisualizers()
        {
            foreach (Vector3 direction in directions)
            {
                // 为每个方向创建一个LineRenderer子对象
                GameObject rayObject = new GameObject($"Ray_{direction.ToString()}");
                rayObject.transform.SetParent(transform);
                rayObject.transform.localPosition = Vector3.zero;
                
                LineRenderer lineRenderer = rayObject.AddComponent<LineRenderer>();
                
                // 配置LineRenderer
                lineRenderer.material = CreateRayMaterial();
                lineRenderer.startWidth = 0.005f;  // 5mm宽度
                lineRenderer.endWidth = 0.003f;    // 渐细
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
                lineRenderer.sortingOrder = 10;
                
                // 初始设置为不可见
                lineRenderer.enabled = false;
                
                directionLineRenderers[direction] = lineRenderer;
            }
            
            Debug.Log($"[PhysicalDetector] 创建了{directionLineRenderers.Count}个运行时射线可视化器");
        }
        
        /// <summary>
        /// 🎨 创建射线材质
        /// </summary>
        Material CreateRayMaterial()
        {
            // 使用Unity内置的Default-Line材质或创建简单的Unlit材质
            Material rayMaterial = new Material(Shader.Find("Sprites/Default"));
            rayMaterial.color = Color.white;
            return rayMaterial;
        }
        
        /// <summary>
        /// 🎮 更新运行时射线可视化
        /// </summary>
        void UpdateRuntimeRayVisualization(Vector3 direction, Vector3 startPos, Vector3 endPos, bool hasContact, float distance)
        {
            if (!directionLineRenderers.ContainsKey(direction))
                return;
                
            LineRenderer lineRenderer = directionLineRenderers[direction];
            
            if (hasContact)
            {
                // 有碰撞：显示射线
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, endPos);
                
                // 根据检测状态设置颜色
                Color rayColor = Color.green; // 默认绿色
                
                if (lastDetectionDistances.ContainsKey(direction))
                {
                    float lastDistance = lastDetectionDistances[direction];
                    float distanceReduction = lastDistance - distance;
                    
                    if (distanceReduction > DISTANCE_REDUCTION_THRESHOLD)
                    {
                        rayColor = Color.cyan; // 满足拍打条件：青色
                    }
                    else if (distanceReduction > 0)
                    {
                        rayColor = Color.yellow; // 距离缩短：黄色
                    }
                }
                
                lineRenderer.material.color = rayColor;
            }
            else
            {
                // 没有碰撞：隐藏射线或显示到最大距离
                lineRenderer.enabled = false;
            }
        }
        
        /// <summary>
        /// 🎮 销毁运行时射线可视化器
        /// </summary>
        void DestroyRuntimeRayVisualizers()
        {
            foreach (var kvp in directionLineRenderers)
            {
                if (kvp.Value != null && kvp.Value.gameObject != null)
                {
                    DestroyImmediate(kvp.Value.gameObject);
                }
            }
            directionLineRenderers.Clear();
            
            Debug.Log("[PhysicalDetector] 清理了所有运行时射线可视化器");
        }
        
        /// <summary>
        /// 🎨 增强的调试可视化
        /// </summary>
        void OnDrawGizmos()
        {
            if (!isDetecting || ballComponent == null)
                return;
                
            Vector3 ballPosition = transform.position;
            float detectionRadius = ballComponent.DetectionRadius;
            
            // 🎯 为每个方向绘制射线
            foreach (Vector3 direction in directions)
            {
                // 🎨 根据检测状态选择颜色
                Color rayColor = Color.white;
                
                if (directionHasContact.ContainsKey(direction))
                {
                    bool hasContact = directionHasContact[direction];
                    float distance = directionDistances.ContainsKey(direction) ? directionDistances[direction] : detectionRadius;
                    Vector3 hitPoint = directionHitPoints.ContainsKey(direction) ? directionHitPoints[direction] : ballPosition + direction * detectionRadius;
                    
                    // 🎨 颜色编码
                    if (hasContact)
                    {
                        // 检测到物体：绿色
                        rayColor = Color.green;
                        
                        // 如果有历史数据，检查是否满足拍打条件
                        if (lastDetectionDistances.ContainsKey(direction))
                        {
                            float lastDistance = lastDetectionDistances[direction];
                            float distanceReduction = lastDistance - distance;
                            
                            if (distanceReduction > DISTANCE_REDUCTION_THRESHOLD)
                            {
                                // 满足拍打条件：亮蓝色
                                rayColor = Color.cyan;
                            }
                            else if (distanceReduction > 0)
                            {
                                // 距离在缩短但不够阈值：黄色
                                rayColor = Color.yellow;
                            }
                        }
                        
                        // 🔍 绘制射线到碰撞点
                        Gizmos.color = rayColor;
                        Gizmos.DrawLine(ballPosition, hitPoint);
                        
                        // 🎯 在碰撞点画一个小球
                        Gizmos.color = rayColor;
                        Gizmos.DrawSphere(hitPoint, 0.02f);
                        
                        // 📏 显示距离信息
                        Vector3 textPos = ballPosition + direction * (distance * 0.5f);
                        #if UNITY_EDITOR
                        UnityEditor.Handles.Label(textPos, $"{distance:F2}m");
                        #endif
                    }
                    else
                    {
                        // 没检测到物体：红色虚线
                        rayColor = Color.red;
                        Gizmos.color = rayColor;
                        
                        // 🔍 绘制到最大检测距离的虚线
                        Vector3 endPoint = ballPosition + direction * detectionRadius;
                        DrawDashedLine(ballPosition, endPoint, 0.1f);
                    }
                }
                else
                {
                    // 还没有检测数据：灰色
                    rayColor = Color.gray;
                    Gizmos.color = rayColor;
                    Vector3 endPoint = ballPosition + direction * detectionRadius;
                    Gizmos.DrawLine(ballPosition, endPoint);
                }
                
                // 🎯 在射线起点画方向标识
                Vector3 directionIndicator = ballPosition + direction * 0.05f;
                Gizmos.color = rayColor;
                Gizmos.DrawCube(directionIndicator, Vector3.one * 0.02f);
            }
            
            // 🎯 在小球中心画一个指示器
            Gizmos.color = isDetecting ? Color.magenta : Color.gray;
            Gizmos.DrawWireSphere(ballPosition, 0.03f);
            
            // 🔵 绘制检测范围球体（半透明）
            if (debugVisualization)
            {
                Gizmos.color = Color.blue * 0.2f;
                Gizmos.DrawWireSphere(ballPosition, detectionRadius);
            }
        }
        
        /// <summary>
        /// 绘制虚线
        /// </summary>
        void DrawDashedLine(Vector3 start, Vector3 end, float dashSize)
        {
            Vector3 direction = (end - start).normalized;
            float totalDistance = Vector3.Distance(start, end);
            
            for (float distance = 0; distance < totalDistance; distance += dashSize * 2)
            {
                Vector3 dashStart = start + direction * distance;
                Vector3 dashEnd = start + direction * Mathf.Min(distance + dashSize, totalDistance);
                Gizmos.DrawLine(dashStart, dashEnd);
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
        /// 运行时切换VR射线可视化
        /// </summary>
        [ContextMenu("Toggle VR Ray Visualization")]
        public void ToggleVRRayVisualization()
        {
            showRuntimeRays = !showRuntimeRays;
            
            if (showRuntimeRays && isDetecting)
            {
                CreateRuntimeRayVisualizers();
            }
            else if (!showRuntimeRays)
            {
                DestroyRuntimeRayVisualizers();
            }
            
            Debug.Log($"[PhysicalDetector] {name} VR射线可视化: {showRuntimeRays}");
        }
        
        /// <summary>
        /// 清理
        /// </summary>
        void OnDestroy()
        {
            StopDetection();
            DestroyRuntimeRayVisualizers();
        }
    }
}
