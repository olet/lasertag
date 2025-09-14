using UnityEngine;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 🚀 超简单小球碰撞 - 学激光枪逻辑
    /// 1. 球抛出去
    /// 2. 球运动方向有个小射线
    /// 3. 每帧检测射线碰到东西就停住
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnvironmentBallPhysics : MonoBehaviour
    {
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private bool isStuck = false;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
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
            // 位置修正到表面
            transform.position = hitPoint + normal * sphereCollider.radius;
            
            // 完全停止
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // 标记为已停住
            isStuck = true;
            
            Debug.Log($"[球碰撞] 撞到 {surfaceName}，停住！");
        }
        
        /// <summary>
        /// 检查是否已停住
        /// </summary>
        public bool IsGrounded()
        {
            return isStuck;
        }
    }
}