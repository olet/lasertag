using Anaglyph.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 可投掷的物理小球，支持墙壁反弹和地面滚动
    /// </summary>
    public class ThrowableBall : Deletable
    {
        [Header("物理设置")]
        [SerializeField] private float bounceForce = 0.8f;
        [SerializeField] private float maxLifetime = 60f; // 🎯 1分钟生命周期，真正的压力测试
        [SerializeField] private float minVelocityThreshold = 0.1f;
        
        [Header("音效")]
        [SerializeField] private AudioClip bounceSound;
        [SerializeField] private AudioClip rollSound;
        [SerializeField] private AudioSource audioSource;
        
        [Header("视觉效果")]
        [SerializeField] private TrailRenderer trail;
        [SerializeField] private ParticleSystem bounceEffect;
        
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private NetworkVariable<float> spawnTime = new NetworkVariable<float>();
        private bool isRolling = false;
        private float lastBounceTime = 0f;
        private const float BOUNCE_COOLDOWN = 0.1f;
        
        // 🚀 性能优化：降低Update频率
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.1f; // 每100ms更新一次，而不是每帧

        public UnityEvent<Vector3> OnBounce = new UnityEvent<Vector3>();
        public UnityEvent OnDestroyed = new UnityEvent();

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
                
            // 确保物理组件存在
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.mass = 0.2f; // 轻量级小球
            }
            
            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.05f; // 5cm直径
            }
            
            // 🚨 禁用环境物理组件 - 性能问题
            // var envPhysics = GetComponent<EnvironmentBallPhysics>();
            // if (envPhysics == null)
            // {
            //     envPhysics = gameObject.AddComponent<EnvironmentBallPhysics>();
            // }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                spawnTime.Value = Time.time;
            }
            
            // 设置物理材质
            SetupPhysicsMaterial();
            
            // 启用拖尾效果
            if (trail != null)
                trail.enabled = true;
        }

        private void SetupPhysicsMaterial()
        {
            // 创建物理材质用于反弹和摩擦
            PhysicsMaterial ballMaterial = new PhysicsMaterial("BallMaterial");
            ballMaterial.bounciness = bounceForce;
            ballMaterial.dynamicFriction = 0.3f;
            ballMaterial.staticFriction = 0.5f;
            ballMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            ballMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            
            sphereCollider.material = ballMaterial;
        }

        private void Update()
        {
            if (!IsOwner) return;

            // 🚀 性能优化：降低Update频率，特别是对于100个小球
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL)
                return;
            lastUpdateTime = Time.time;

            // 生命周期管理
            if (Time.time - spawnTime.Value > maxLifetime)
            {
                DestroyBall();
                return;
            }

            // 检查是否在滚动
            CheckRollingState();
            
            // 🎯 物理优化：让慢速小球更容易休眠，但不过于激进
            if (rb.linearVelocity.magnitude < minVelocityThreshold)
            {
                rb.linearVelocity *= 0.9f; // 逐渐减速
                rb.angularVelocity *= 0.9f; 
                
                // 很低速度时让刚体休眠
                if (rb.linearVelocity.magnitude < 0.02f)
                {
                    rb.Sleep();
                }
            }
        }

        private void CheckRollingState()
        {
            bool wasRolling = isRolling;
            isRolling = rb.linearVelocity.magnitude < 5f && rb.linearVelocity.magnitude > minVelocityThreshold;
            
            // 滚动音效
            if (isRolling && !wasRolling && rollSound != null)
            {
                PlaySoundRpc(1); // 1 = roll sound
            }
            else if (!isRolling && wasRolling)
            {
                StopRollingSoundRpc();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsOwner) return;
            
            // 防止频繁反弹音效
            if (Time.time - lastBounceTime < BOUNCE_COOLDOWN) return;
            lastBounceTime = Time.time;

            Vector3 bouncePoint = collision.contacts[0].point;
            Vector3 bounceNormal = collision.contacts[0].normal;
            
            // 检查是否撞到环境（墙壁、地面等）
            if (IsEnvironmentCollision(collision))
            {
                HandleEnvironmentBounce(bouncePoint, bounceNormal, collision);
            }
            
            OnBounce.Invoke(bouncePoint);
        }

        private bool IsEnvironmentCollision(Collision collision)
        {
            // 检查是否是环境碰撞（非玩家、非其他游戏物体）
            GameObject hitObject = collision.gameObject;
            
            // 🚀 性能优化：排除玩家和其他小球，减少小球间碰撞计算
            if (hitObject.CompareTag("Player") || 
                hitObject.GetComponent<ThrowableBall>() != null)
                return false;
                
            return true;
        }

        private void HandleEnvironmentBounce(Vector3 point, Vector3 normal, Collision collision)
        {
            // 计算反弹强度基于碰撞速度
            float impactStrength = rb.linearVelocity.magnitude;
            
            // 播放反弹音效
            if (bounceSound != null && impactStrength > 1f)
            {
                PlaySoundRpc(0); // 0 = bounce sound
            }
            
            // 粒子效果
            if (bounceEffect != null && impactStrength > 2f)
            {
                SpawnBounceEffectRpc(point, normal);
            }

            // 检查Quest环境碰撞
            HandleQuestEnvironmentCollision(point, normal);
        }

        private void HandleQuestEnvironmentCollision(Vector3 point, Vector3 normal)
        {
            // 与Quest 3环境深度数据的碰撞处理
            // 这里可以添加特殊的环境反应，比如在真实墙壁上的特效
        }

        /// <summary>
        /// 给小球施加投掷力
        /// </summary>
        public void ThrowBall(Vector3 force, Vector3 torque = default)
        {
            if (!IsOwner) return;
            
            rb.AddForce(force, ForceMode.VelocityChange);
            
            if (torque != Vector3.zero)
                rb.AddTorque(torque, ForceMode.VelocityChange);
                
            ApplyThrowForceRpc(force, torque);
        }

        [Rpc(SendTo.Everyone)]
        private void ApplyThrowForceRpc(Vector3 force, Vector3 torque)
        {
            if (IsOwner) return; // 已经在本地应用了
            
            rb.AddForce(force, ForceMode.VelocityChange);
            if (torque != Vector3.zero)
                rb.AddTorque(torque, ForceMode.VelocityChange);
        }

        [Rpc(SendTo.Everyone)]
        private void PlaySoundRpc(int soundType)
        {
            if (audioSource == null) return;
            
            AudioClip clipToPlay = soundType switch
            {
                0 => bounceSound,
                1 => rollSound,
                _ => null
            };
            
            if (clipToPlay != null)
            {
                audioSource.PlayOneShot(clipToPlay);
            }
        }

        [Rpc(SendTo.Everyone)]
        private void StopRollingSoundRpc()
        {
            if (audioSource != null && audioSource.clip == rollSound)
            {
                audioSource.Stop();
            }
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnBounceEffectRpc(Vector3 position, Vector3 normal)
        {
            if (bounceEffect != null)
            {
                var effect = Instantiate(bounceEffect, position, Quaternion.LookRotation(normal));
                effect.Play();
                Destroy(effect.gameObject, 2f);
            }
        }

        public new void Delete()
        {
            if (IsOwner)
                DestroyBall();
        }

        private void DestroyBall()
        {
            OnDestroyed.Invoke();
            
            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn();
        }

        // VR抓取接口支持
        public void OnSelectEntered()
        {
            // 当被VR手柄抓取时停止物理
            rb.isKinematic = true;
            if (trail != null)
                trail.enabled = false;
        }

        public void OnSelectExited()
        {
            // 释放时恢复物理
            rb.isKinematic = false;
            if (trail != null)
                trail.enabled = true;
        }
    }
}
