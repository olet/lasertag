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
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            ballRenderer = GetComponent<Renderer>();
        }

        private void Update()
        {
            // 一旦停住就不再检测
            if (isStuck) return;
            
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
                    StickToSurface(hit.point, hit.normal, hit.collider.name);
                    return;
                }
                
                // 🎯 方法2：Quest环境检测
                if (EnvironmentMapper.Instance != null)
                {
                    Ray ray = new Ray(transform.position, direction);
                    if (EnvironmentMapper.Raycast(ray, checkDistance, out var envHit))
                    {
                        Vector3 hitPoint = ray.GetPoint(envHit.distance);
                        StickToSurface(hitPoint, -direction, "Quest环境");
                        return;
                    }
                }
            }
        }
        
        /// <summary>
        /// 🎯 碰到就停住 - 就像激光击中墙壁
        /// </summary>
        private void StickToSurface(Vector3 hitPoint, Vector3 normal, string surfaceName)
        {
            // 🧠 聪明的几何判断：比较运动碰撞点 vs 垂直下落碰撞点
            bool isHorizontalSurface = IsHorizontalSurface(hitPoint, transform.position);
            
            // 位置修正到表面
            transform.position = hitPoint + normal * sphereCollider.radius;
            
            // 完全停止
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 🎯 关闭重力，真正钉住！
            rb.useGravity = false;
            
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
        }
        
        /// <summary>
        /// 检查是否已停住
        /// </summary>
        public bool IsGrounded()
        {
            return isStuck;
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
            
            // 🎯 比较两个碰撞点的距离
            float distance = Vector3.Distance(movementHitPoint, verticalHitPoint);
            
            // 🎯 距离判断：近 = 水平表面，远 = 垂直表面
            bool isHorizontal = distance < 0.2f; // 20cm容错
            
            Debug.Log($"[表面判断] 运动点:{movementHitPoint:F2} 垂直点:{verticalHitPoint:F2} 距离:{distance:F2}m → {(isHorizontal ? "水平" : "垂直")}表面");
            
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