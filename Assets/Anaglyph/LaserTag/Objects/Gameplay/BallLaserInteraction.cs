using UnityEngine;
using Anaglyph.Lasertag.Objects;
using Anaglyph.Lasertag;
using System.Collections.Generic;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 🎯 小球与激光枪的交互组件 - 代码分离的交互逻辑
    /// 当激光枪击中小球时，小球恢复重力并重新开始物理模拟
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(EnvironmentBallPhysics))]
    [RequireComponent(typeof(SphereCollider))]
    public class BallLaserInteraction : MonoBehaviour
    {
        private Rigidbody rb;
        private EnvironmentBallPhysics ballPhysics;
        private Renderer ballRenderer;
        
        [Header("交互设置")]
        [SerializeField] private AudioClip hitSFX; // 被击中的音效
        [SerializeField] private Color reactivatedColor = Color.yellow; // 重新激活时的颜色
        [SerializeField] private float colorChangeDuration = 1f; // 颜色变化持续时间
        
        private bool isReactivated = false;
        private Color originalColor;
        private float colorChangeTimer = 0f;
        
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            ballPhysics = GetComponent<EnvironmentBallPhysics>();
            ballRenderer = GetComponent<Renderer>();
            
            // 记录原始颜色
            if (ballRenderer != null && ballRenderer.material != null)
            {
                originalColor = ballRenderer.material.color;
            }
            
            // 🎯 确保小球可以被激光枪的Physics.Linecast检测到
            // 小球必须有非Trigger的碰撞体才能被激光弹的射线检测
            var collider = GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.isTrigger = false; // 确保不是Trigger，这样激光弹能检测到
                Debug.Log("[激光交互] 小球碰撞体已设置为非Trigger，可被激光枪检测");
            }
        }
        
        private void Update()
        {
            // 🎨 颜色变化动画
            if (isReactivated && colorChangeTimer > 0f)
            {
                colorChangeTimer -= Time.deltaTime;
                float progress = colorChangeTimer / colorChangeDuration;
                
                // 从重新激活颜色渐变回原始颜色
                Color currentColor = Color.Lerp(originalColor, reactivatedColor, progress);
                SetBallColor(currentColor);
                
                if (colorChangeTimer <= 0f)
                {
                    isReactivated = false;
                    SetBallColor(originalColor);
                }
            }
            
            // 🎯 检测附近的激光弹击中
            if (!isReactivated && ballPhysics.IsGrounded())
            {
                CheckForNearbyBulletHits();
            }
        }
        
        private HashSet<GameObject> processedBullets = new HashSet<GameObject>();
        
        /// <summary>
        /// 🎯 检测附近是否有激光弹
        /// </summary>
        private void CheckForNearbyBulletHits()
        {
            // 在小球周围搜索激光弹 - 使用很小的范围，只检测真正击中的
            Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, 0.1f); // 10cm范围
            
            foreach (var collider in nearbyColliders)
            {
                // 检查是否是激光弹
                var bullet = collider.GetComponent<Bullet>();
                if (bullet != null && !processedBullets.Contains(bullet.gameObject))
                {
                    // 🎯 发现新的激光弹在非常近的距离内，说明击中了！
                    processedBullets.Add(bullet.gameObject);
                    OnLaserHit(transform.position, bullet.OwnerClientId);
                    
                    Debug.Log($"[激光交互] 检测到激光弹击中小球！距离: {Vector3.Distance(transform.position, bullet.transform.position):F3}m");
                    break; // 只处理第一个击中的激光弹
                }
            }
            
            // 🧹 清理已经被销毁的激光弹引用
            processedBullets.RemoveWhere(bullet => bullet == null);
        }
        
        /// <summary>
        /// 🎯 被激光击中时调用
        /// </summary>
        public void OnLaserHit(Vector3 hitPoint, ulong shooterClientId)
        {
            // 🎯 只有在小球已经停住时才能被重新激活
            if (!ballPhysics.IsGrounded())
            {
                Debug.Log("[激光交互] 小球还在运动中，不响应激光击中");
                return;
            }
            
            // 🚀 恢复重力和物理模拟
            RestorePhysics();
            
            // 🎨 视觉和音效反馈
            PlayInteractionFeedback(hitPoint);
            
            Debug.Log($"[激光交互] 小球被玩家{shooterClientId}的激光击中，重新开始物理模拟！");
        }
        
        /// <summary>
        /// 🚀 恢复小球的物理特性
        /// </summary>
        private void RestorePhysics()
        {
            // ✅ 恢复重力
            rb.useGravity = true;
            
            // ✅ 让小球可以再次检测碰撞
            // 重置EnvironmentBallPhysics的内部状态
            if (ballPhysics != null)
            {
                // 通过反射或者添加公共方法来重置状态
                ballPhysics.ResetStuckState();
            }
            
            // ✅ 给小球一个小的随机冲量，避免直接垂直下落
            Vector3 randomImpulse = new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0f, 0.2f),
                Random.Range(-0.5f, 0.5f)
            );
            rb.AddForce(randomImpulse, ForceMode.Impulse);
            
            Debug.Log("[激光交互] 已恢复重力和物理模拟");
        }
        
        /// <summary>
        /// 🎨 播放交互反馈效果
        /// </summary>
        private void PlayInteractionFeedback(Vector3 hitPoint)
        {
            // 🔊 音效反馈
            if (hitSFX != null)
            {
                AudioSource.PlayClipAtPoint(hitSFX, hitPoint);
            }
            
            // 🎨 颜色变化反馈
            isReactivated = true;
            colorChangeTimer = colorChangeDuration;
            SetBallColor(reactivatedColor);
        }
        
        /// <summary>
        /// 🎨 设置小球颜色
        /// </summary>
        private void SetBallColor(Color color)
        {
            if (ballRenderer != null && ballRenderer.material != null)
            {
                ballRenderer.material.color = color;
                
                // URP材质支持
                if (ballRenderer.material.HasProperty("_BaseColor"))
                {
                    ballRenderer.material.SetColor("_BaseColor", color);
                }
            }
        }
        
        /// <summary>
        /// 🎯 公共方法：检查小球是否可以被激光交互
        /// </summary>
        public bool CanBeReactivated()
        {
            return ballPhysics != null && ballPhysics.IsGrounded() && !isReactivated;
        }
    }
}
