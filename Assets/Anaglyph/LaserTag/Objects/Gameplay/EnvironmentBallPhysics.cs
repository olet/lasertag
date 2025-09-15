using UnityEngine;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 🚀 超简单小球碰撞 - 学激光枪逻辑
    /// 1. 球抛出去
    /// 2. 球运动方向有个小射线
    /// 3. 每帧检测射线碰到东西就停住
    /// 4. 🟢 垂直俯冲 = 落地(绿色)  🔴 水平撞击 = 撞墙(红色)
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnvironmentBallPhysics : MonoBehaviour
    {
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private Renderer ballRenderer;
        private bool isStuck = false;
        
        // 🔍 公共访问方法用于性能统计
        public bool IsStuckForStats => isStuck;
        
        // 🔍 性能调试统计
        private static int totalBalls = 0;
        private static int activeBallsThisSecond = 0;
        private static float lastStatsTime = 0f;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            ballRenderer = GetComponent<Renderer>();
            totalBalls++;
        }
        
        private void OnDestroy()
        {
            totalBalls--;
        }

        private void Update()
        {
            // 一旦停住就不再检测
            if (isStuck) return;
            
            // 🔍 统计活跃的球数量
            activeBallsThisSecond++;
            
            // 🔍 每秒输出性能统计 (只在第一个球执行时输出)
            if (Time.time - lastStatsTime > 1f)
            {
                // 计算真正的活跃球数和钉住球数
                int movingBalls = 0, stuckBalls = 0;
                var allBalls = FindObjectsOfType<EnvironmentBallPhysics>();
                foreach (var ball in allBalls)
                {
                    if (ball.IsStuckForStats) stuckBalls++;
                    else movingBalls++;
                }
                
                Debug.Log($"[PERFORMANCE] Total: {totalBalls}, Moving: {movingBalls}, Stuck: {stuckBalls}, Updates/sec: {activeBallsThisSecond}, FPS: {1f/Time.deltaTime:F1}");
                lastStatsTime = Time.time;
                activeBallsThisSecond = 0;
            }
            
            // 🚀 学激光枪：在运动方向发射小射线
            Vector3 velocity = rb.linearVelocity;
            float speed = velocity.magnitude;
            
            // 有速度才检测
            if (speed > 0.1f)
            {
                Vector3 direction = velocity.normalized;
                float checkDistance = speed * Time.deltaTime + sphereCollider.radius;
                
                // 🎯 方法1：Physics射线检测
                if (Physics.Raycast(transform.position, direction, out RaycastHit hit, checkDistance))
                {
                    StickToSurface(hit.point, hit.normal, hit.collider.name, direction);
                    return;
                }
                
                // 🎯 方法2：Quest环境检测
                if (EnvironmentMapper.Instance != null)
                {
                    Ray ray = new Ray(transform.position, direction);
                    if (EnvironmentMapper.Raycast(ray, checkDistance, out var envHit))
                    {
                        Vector3 hitPoint = ray.GetPoint(envHit.distance);
                        StickToSurface(hitPoint, -direction, "Quest环境", direction);
                        return;
                    }
                }
            }
            else if (speed < 0.1f && speed > 0.001f) // 🚀 低速球强制钉住
            {
                // 球在低速滚动，检测脚下是否有地面
                if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit groundHit, 0.1f))
                {
                    StickToSurface(groundHit.point, groundHit.normal, groundHit.collider.name + "(低速钉住)", Vector3.down);
                    return;
                }
                
                // 检测Quest环境地面
                if (EnvironmentMapper.Instance != null)
                {
                    Ray downRay = new Ray(transform.position, Vector3.down);
                    if (EnvironmentMapper.Raycast(downRay, 0.1f, out var groundEnvHit))
                    {
                        Vector3 hitPoint = downRay.GetPoint(groundEnvHit.distance);
                        StickToSurface(hitPoint, Vector3.up, "Quest地面(低速钉住)", Vector3.down);
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// 🎯 碰到就停住 - 就像激光击中墙壁
        /// </summary>
        private void StickToSurface(Vector3 hitPoint, Vector3 normal, string surfaceName, Vector3 flyDirection)
        {
            // 🧠 聪明的几何判断：比较运动碰撞点 vs 垂直下落碰撞点
            bool isHorizontalSurface = IsHorizontalSurface(hitPoint, transform.position);
            
            // 🎯 位置处理：区分Physics vs Quest环境
            Vector3 finalPosition = hitPoint;
            
            // ✅ Physics碰撞：游戏世界，无需补偿
            // ❌ Quest环境碰撞：TSDF有偏移，需要补偿
            if (surfaceName == "Quest环境")
            {
                // 🔧 TSDF补偿：沿真正的飞行方向继续推进，穿透到真实表面
                finalPosition = hitPoint + flyDirection * 0.03f; // 3cm沿飞行方向继续推进
                
                Debug.Log($"[TSDF补偿] 沿飞行方向{flyDirection:F2}推进3cm到{finalPosition:F2}");
            }
            
            transform.position = finalPosition;
            transform.up = normal;          // 法向量对齐 (TODO: 后续反弹功能可能需要修正)
            
            // 完全停止
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 🎯 关闭重力，真正钉住！
            rb.useGravity = false;
            
            // 🚀 CPU性能优化：设为Kinematic，停止物理计算
            rb.isKinematic = true;
            
            // 🔍 测试：确认Kinematic物体是否能被激光检测
            Debug.Log($"[PHYSICS TEST] Ball now kinematic: {rb.isKinematic}, Collider active: {sphereCollider.enabled}");
            
            // 🎨 根据表面类型改变颜色
            if (isHorizontalSurface)
            {
                SetBallColor(Color.green);  // 🟢 水平表面 = 绿色
                Debug.Log($"[球落地] 水平表面 {surfaceName}，标记为绿色！");
            }
            else
            {
                SetBallColor(Color.red);    // 🔴 垂直表面 = 红色  
                Debug.Log($"[球撞墙] 垂直表面 {surfaceName}，标记为红色！");
            }
            
            // 标记为已停住
            isStuck = true;
            Debug.Log($"[BALL PHYSICS] Ball stuck! Surface: {surfaceName}, isStuck: {isStuck}");
            
            // 🎨 保持小球可见
            if (ballRenderer != null)
                ballRenderer.enabled = true;
        }
        
        /// <summary>
        /// 检查是否已停住
        /// </summary>
        public bool IsGrounded()
        {
            return isStuck;
        }
        
        /// <summary>
        /// 🚀 重置停住状态 - 供激光交互使用
        /// </summary>
        public void ResetStuckState()
        {
            isStuck = false;
            
            // 🚀 恢复物理计算
            rb.isKinematic = false;
            rb.useGravity = true;
                
            Debug.Log("[BALL PHYSICS] Reset stuck state, ball can detect collisions again");
        }
        
        /// <summary>
        /// 🧠 聪明的几何表面判断 - 比较运动碰撞点 vs 垂直下落碰撞点
        /// </summary>
        private bool IsHorizontalSurface(Vector3 movementHitPoint, Vector3 ballPosition)
        {
            // 🎯 从球位置垂直向下发射射线  
            Vector3 verticalHitPoint;
            bool foundVerticalHit = GetVerticalHitPoint(ballPosition, out verticalHitPoint);
            
            if (!foundVerticalHit)
            {
                // 垂直向下没检测到，可能是悬崖边缘，默认认为是撞墙
                Debug.Log("[表面判断] 垂直向下无碰撞，默认为垂直表面");
                return false;
            }
            
            // 🎯 比较两个碰撞点的Y轴高度差（忽略水平位置差异）
            float heightDifference = Mathf.Abs(movementHitPoint.y - verticalHitPoint.y);
            
            // 🎯 高度判断：高度相近 = 真正的水平表面
            bool isHorizontal = heightDifference < 0.02f; // 2cm高度差容错
            
            Debug.Log($"[表面判断] 运动点:{movementHitPoint:F2} 垂直点:{verticalHitPoint:F2} 高度差:{heightDifference:F2}m → {(isHorizontal ? "水平" : "垂直")}表面");
            
            return isHorizontal;
        }
        
        /// <summary>
        /// 🎯 获取垂直向下的碰撞点
        /// </summary>
        private bool GetVerticalHitPoint(Vector3 startPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            
            // 🎯 垂直向下射线，检测范围10m (足够覆盖房间高度)
            Ray verticalRay = new Ray(startPos, Vector3.down);
            float maxDistance = 10f;
            
            // 🎯 优先用Physics射线检测游戏物体
            if (Physics.Raycast(verticalRay, out RaycastHit physicsHit, maxDistance))
            {
                hitPoint = physicsHit.point;
                return true;
            }
            
            // 🎯 再用EnvironmentMapper检测Quest环境
            if (EnvironmentMapper.Instance != null && 
                EnvironmentMapper.Raycast(verticalRay, maxDistance, out var envHit))
            {
                hitPoint = verticalRay.GetPoint(envHit.distance);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 🎨 改变球的颜色
        /// </summary>
        private void SetBallColor(Color color)
        {
            if (ballRenderer != null && ballRenderer.material != null)
            {
                ballRenderer.material.color = color;
                
                // 如果材质有_BaseColor属性 (URP标准)
                if (ballRenderer.material.HasProperty("_BaseColor"))
                {
                    ballRenderer.material.SetColor("_BaseColor", color);
                }
            }
        }
    }
}