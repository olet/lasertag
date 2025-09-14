using UnityEngine;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 小球与Quest 3环境的自定义物理碰撞
    /// 直接使用EnvironmentMapper射线投射，无需额外碰撞体
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EnvironmentBallPhysics : MonoBehaviour
    {
        [Header("环境碰撞设置")]
        [SerializeField] private float bounceForce = 0.8f;
        [SerializeField] private float frictionForce = 0.95f;
        [SerializeField] private float minBounceSpeed = 0.5f;
        [SerializeField] private float rayOffset = 0.02f; // 射线偏移，防止穿透
        
        [Header("声音反馈")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bounceSound;
        [SerializeField] private float minSoundInterval = 0.2f;
        
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private Vector3 lastPosition;
        private Vector3 lastVelocity;
        private float lastBounceTime;
        private bool isGrounded;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
                
            lastPosition = transform.position;
        }

        private void FixedUpdate()
        {
            // 🚨 临时性能修复：大幅减少检测频率
            if (Time.fixedTime % 0.1f > 0.02f) // 每100ms只检测一次
                return;
                
            if (EnvironmentMapper.Instance == null) 
            {
                // 环境映射器不可用，使用标准物理
                return;
            }

            // 只有在快速移动时才检测
            if (rb.linearVelocity.magnitude < 0.5f)
                return;

            CheckEnvironmentCollisions();
            ApplyGroundFriction();
            
            lastPosition = transform.position;
            lastVelocity = rb.linearVelocity;
        }

        /// <summary>
        /// 检查与Quest环境的碰撞
        /// </summary>
        private void CheckEnvironmentCollisions()
        {
            Vector3 currentPos = transform.position;
            Vector3 velocity = rb.linearVelocity;
            float radius = sphereCollider.radius;
            
            // 重置接地状态
            isGrounded = false;
            
            // 检查多个方向的环境碰撞
            CheckCollisionInDirection(Vector3.down, radius + rayOffset, "Ground");
            CheckCollisionInDirection(Vector3.up, radius + rayOffset, "Ceiling");
            CheckCollisionInDirection(Vector3.forward, radius + rayOffset, "Wall");
            CheckCollisionInDirection(Vector3.back, radius + rayOffset, "Wall");
            CheckCollisionInDirection(Vector3.left, radius + rayOffset, "Wall");
            CheckCollisionInDirection(Vector3.right, radius + rayOffset, "Wall");
            
            // 检查运动方向上的障碍物
            if (velocity.magnitude > 0.1f)
            {
                Vector3 moveDirection = velocity.normalized;
                float moveDistance = velocity.magnitude * Time.fixedDeltaTime;
                CheckCollisionInDirection(moveDirection, radius + moveDistance, "Movement");
            }
        }

        /// <summary>
        /// 检查特定方向的环境碰撞
        /// </summary>
        private void CheckCollisionInDirection(Vector3 direction, float distance, string collisionType)
        {
            Vector3 rayStart = transform.position;
            Ray ray = new Ray(rayStart, direction);
            
            if (EnvironmentMapper.Raycast(ray, distance, out var hitResult))
            {
                HandleEnvironmentCollision(hitResult, direction, collisionType);
            }
        }

        /// <summary>
        /// 处理环境碰撞
        /// </summary>
        private void HandleEnvironmentCollision(EnvironmentMapper.RayResult hitResult, Vector3 rayDirection, string collisionType)
        {
            Vector3 hitPoint = hitResult.point;
            float hitDistance = hitResult.distance;
            float radius = sphereCollider.radius;
            
            // 计算表面法线（简化版本）
            Vector3 surfaceNormal = -rayDirection;
            
            // 如果太接近表面，进行位置修正
            if (hitDistance < radius + 0.01f)
            {
                Vector3 correction = surfaceNormal * (radius + 0.01f - hitDistance);
                transform.position += correction;
                
                // 处理反弹
                HandleBounce(surfaceNormal, collisionType);
            }
        }

        /// <summary>
        /// 处理反弹逻辑
        /// </summary>
        private void HandleBounce(Vector3 surfaceNormal, string collisionType)
        {
            Vector3 velocity = rb.linearVelocity;
            float speedAlongNormal = Vector3.Dot(velocity, surfaceNormal);
            
            // 只有当速度朝向表面时才反弹
            if (speedAlongNormal < 0)
            {
                // 计算反弹速度
                Vector3 reflectedVelocity = velocity - 2 * speedAlongNormal * surfaceNormal;
                Vector3 newVelocity = Vector3.Lerp(velocity, reflectedVelocity, bounceForce);
                
                // 应用最小反弹速度阈值
                if (Mathf.Abs(speedAlongNormal) > minBounceSpeed)
                {
                    rb.linearVelocity = newVelocity;
                    PlayBounceSound();
                    
                    Debug.Log($"[EnvironmentBallPhysics] {collisionType} 反弹: {speedAlongNormal:F2} m/s");
                }
                else
                {
                    // 低速碰撞，停止在表面上
                    Vector3 tangentialVelocity = velocity - speedAlongNormal * surfaceNormal;
                    rb.linearVelocity = tangentialVelocity * frictionForce;
                }
                
                // 标记接地状态
                if (collisionType == "Ground")
                {
                    isGrounded = true;
                }
            }
        }

        /// <summary>
        /// 应用地面摩擦力
        /// </summary>
        private void ApplyGroundFriction()
        {
            if (isGrounded && rb.linearVelocity.magnitude > 0.1f)
            {
                // 应用滚动摩擦
                Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
                Vector3 frictionedVelocity = horizontalVelocity * frictionForce;
                
                rb.linearVelocity = new Vector3(frictionedVelocity.x, rb.linearVelocity.y, frictionedVelocity.z);
            }
        }

        /// <summary>
        /// 播放反弹音效
        /// </summary>
        private void PlayBounceSound()
        {
            if (bounceSound != null && audioSource != null && 
                Time.time - lastBounceTime > minSoundInterval)
            {
                audioSource.PlayOneShot(bounceSound);
                lastBounceTime = Time.time;
            }
        }

        /// <summary>
        /// 检查小球是否在地面上
        /// </summary>
        public bool IsGrounded()
        {
            return isGrounded;
        }

        /// <summary>
        /// 获取当前环境碰撞信息
        /// </summary>
        public bool GetGroundInfo(out Vector3 groundPoint, out Vector3 groundNormal)
        {
            Vector3 rayStart = transform.position;
            Ray downRay = new Ray(rayStart, Vector3.down);
            float checkDistance = sphereCollider.radius + 0.1f;
            
            if (EnvironmentMapper.Raycast(downRay, checkDistance, out var hitResult))
            {
                groundPoint = hitResult.point;
                groundNormal = Vector3.up; // 简化假设地面朝上
                return true;
            }
            
            groundPoint = Vector3.zero;
            groundNormal = Vector3.up;
            return false;
        }

        private void OnDrawGizmos()
        {
            if (sphereCollider != null)
            {
                Gizmos.color = isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(transform.position, sphereCollider.radius + rayOffset);
            }
        }
    }
}
