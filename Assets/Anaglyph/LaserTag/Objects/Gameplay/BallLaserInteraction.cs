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
            var collider = GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.isTrigger = false; // 确保不是Trigger，这样激光弹能检测到
                Debug.Log($"[BALL] Collider setup: radius={collider.radius}, isTrigger={collider.isTrigger}, layer={gameObject.layer}");
                Debug.Log($"[BALL] Position: {transform.position}");
            }
            else
            {
                Debug.LogError("[BALL] No SphereCollider found!");
            }
            
            // 🔥 初始状态禁用Update()，节省性能
            enabled = false;
        }
        
        private void Update()
        {
            // 🚀 性能优化：只有在颜色动画时才执行
            if (!isReactivated || colorChangeTimer <= 0f)
            {
                enabled = false; // 🔥 禁用组件，停止Update()调用！
                return;
            }
            
            // 🎨 颜色变化动画
            colorChangeTimer -= Time.deltaTime;
            float progress = colorChangeTimer / colorChangeDuration;
            
            // 从重新激活颜色渐变回原始颜色
            Color currentColor = Color.Lerp(originalColor, reactivatedColor, progress);
            SetBallColor(currentColor);
            
            if (colorChangeTimer <= 0f)
            {
                isReactivated = false;
                SetBallColor(originalColor);
                enabled = false; // 🔥 动画结束，禁用组件！
            }
        }
        
        /// <summary>
        /// 🎯 被激光击中时调用
        /// </summary>
        public void OnLaserHit(Vector3 hitPoint, ulong shooterClientId, Vector3 laserDirection)
        {
            Debug.Log($"[BALL] Laser hit detected! Checking if ball can be reactivated...");
            Debug.Log($"[BALL] IsGrounded: {ballPhysics.IsGrounded()}, isReactivated: {isReactivated}");
            
            // 🎯 只有在小球已经停住时才能被重新激活
            if (!ballPhysics.IsGrounded())
            {
                Debug.Log("[BALL] Ball still moving, ignoring laser hit");
                return;
            }
            
            Debug.Log("[BALL] Ball is grounded, proceeding with reactivation...");
            
            // 🚀 恢复重力和物理模拟，沿激光反方向弹射
            RestorePhysics(laserDirection);
            
            // 🎨 视觉和音效反馈
            PlayInteractionFeedback(hitPoint);
            
            Debug.Log($"[BALL] Ball hit by player {shooterClientId}, restarting physics!");
        }
        
        /// <summary>
        /// 🚀 恢复小球的物理特性，沿激光反方向弹射
        /// </summary>
        private void RestorePhysics(Vector3 laserDirection)
        {
            // ✅ 恢复物理模拟
            rb.isKinematic = false;
            rb.useGravity = true;
            
            // ✅ 让小球可以再次检测碰撞
            if (ballPhysics != null)
            {
                ballPhysics.ResetStuckState();
            }
            
            // 🔥 沿着激光反方向弹射 + 随机扰动
            float sparkForce = Random.Range(0.2f, 0.4f); // 随机弹射力度 - 每次都不同
            Vector3 baseDirection = -laserDirection; // 正确的激光反方向！
            
            // 🎲 添加随机扰动，让弹射更自然
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                Random.Range(-0.1f, 0.3f), // 稍微偏向上方
                Random.Range(-0.3f, 0.3f)
            );
            
            Vector3 finalDirection = (baseDirection + randomOffset).normalized;
            rb.AddForce(finalDirection * sparkForce, ForceMode.Impulse);
            
            Debug.Log($"[激光交互] 小球沿激光反方向弹射! 激光方向:{laserDirection:F2}, 弹射方向:{finalDirection:F2}");
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
            
            // 🔥 重新启用组件以执行颜色动画
            enabled = true;
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
