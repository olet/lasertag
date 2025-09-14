using UnityEngine;
using UnityEngine.InputSystem;
using Anaglyph.Lasertag.Objects;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// 简化版VR小球生成器 - 只需按X/A键生成小球
    /// </summary>
    public class SimpleVRBallSpawner : MonoBehaviour
    {
        [Header("简单设置")]
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private float forwardForce = 5f;
        [SerializeField] private float upwardForce = 2f;
        
        private InputAction spawnAction;
        
        private void Awake()
        {
            if (spawnPoint == null)
                spawnPoint = transform;
                
            // VR控制器X/A键
            spawnAction = new InputAction("SpawnBall", binding: "<XRController>/primaryButton");
            spawnAction.performed += OnSpawnPressed;
        }
        
        private void OnEnable()
        {
            spawnAction?.Enable();
        }
        
        private void OnDisable()
        {
            spawnAction?.Disable();
        }
        
        private void OnDestroy()
        {
            spawnAction?.Dispose();
        }
        
        private void OnSpawnPressed(InputAction.CallbackContext context)
        {
            SpawnBall();
        }
        
        private void SpawnBall()
        {
            // 使用BallFactory生成小球
            var ball = BallFactory.SpawnBall(spawnPoint.position, spawnPoint.rotation);
            
            // 添加向前的力
            var rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = spawnPoint.forward * forwardForce + Vector3.up * upwardForce;
                rb.AddForce(force, ForceMode.VelocityChange);
                
                // 添加随机旋转
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            }
            
            Debug.Log("[SimpleVRBallSpawner] 生成小球并投掷");
        }
    }
}
