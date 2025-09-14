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
            // 🎨 判断是垂直俯冲还是水平撞击
            bool isVerticalDrop = IsVerticalDrop(rb.linearVelocity);
            
            // 位置修正到表面
            transform.position = hitPoint + normal * sphereCollider.radius;
            
            // 完全停止
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 🎯 关闭重力，真正钉住！
            rb.useGravity = false;
            
            // 🎨 根据撞击类型改变颜色
            if (isVerticalDrop)
            {
                SetBallColor(Color.green);  // 🟢 落地 = 绿色
                Debug.Log($"[球落地] 垂直俯冲撞到 {surfaceName}，标记为绿色！");
            }
            else
            {
                SetBallColor(Color.red);    // 🔴 撞墙 = 红色  
                Debug.Log($"[球撞墙] 水平撞击 {surfaceName}，标记为红色！");
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
        /// 🎯 判断是否为垂直俯冲 - 基于速度向量分析
        /// </summary>
        private bool IsVerticalDrop(Vector3 velocity)
        {
            // 必须有明显向下的速度
            if (velocity.y >= -1f) return false;
            
            // Y轴速度必须占主导地位 (垂直方向比水平方向更强)
            float verticalSpeed = Mathf.Abs(velocity.y);
            float horizontalSpeed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            
            // 垂直速度比水平速度大，就认为是俯冲落地
            return verticalSpeed > horizontalSpeed * 1.5f; // 1.5倍的容错
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