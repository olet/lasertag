using UnityEngine;
using UnityEngine.InputSystem;
using Anaglyph.Lasertag.Objects;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// 键盘测试代码化小球系统，使用新Input System
    /// </summary>
    public class KeyboardBallTester : MonoBehaviour
    {
        [Header("测试配置")]
        public Transform spawnPoint;
        [SerializeField] private int maxBalls = 100; // 🎯 真正的压力测试！
        
        private int currentBallCount = 0;
        
        private InputAction spawnAction;
        private InputAction clearAction;
        
        private void Awake()
        {
            // 使用新Input System创建动作
            spawnAction = new InputAction("SpawnBall", binding: "<Keyboard>/space");
            clearAction = new InputAction("ClearBalls", binding: "<Keyboard>/c");
            
            spawnAction.performed += OnSpawnPressed;
            clearAction.performed += OnClearPressed;
        }
        
        private void OnEnable()
        {
            spawnAction?.Enable();
            clearAction?.Enable();
        }
        
        private void OnDisable()
        {
            spawnAction?.Disable();
            clearAction?.Disable();
        }
        
        private void OnDestroy()
        {
            spawnAction?.Dispose();
            clearAction?.Dispose();
        }
        
        private void OnSpawnPressed(InputAction.CallbackContext context)
        {
            TestSpawnBall();
        }
        
        private void OnClearPressed(InputAction.CallbackContext context)
        {
            ClearAllBalls();
        }
        
        private void TestSpawnBall()
        {
            // 🎯 真正的压力测试：允许最多100个小球同时存在
            var existingBalls = FindObjectsOfType<ThrowableBall>();
            if (existingBalls.Length >= maxBalls)
            {
                Debug.Log($"[KeyboardTester] 已达到{maxBalls}个小球上限，无法生成更多");
                return;
            }
            
            Vector3 spawnPos = spawnPoint ? spawnPoint.position : transform.position + Vector3.up;
            
            var ball = BallFactory.SpawnBall(spawnPos, Quaternion.identity);
            
            // 添加随机初始力
            var rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomForce = new Vector3(
                    Random.Range(-5f, 5f),
                    Random.Range(3f, 8f),
                    Random.Range(-5f, 5f)
                );
                rb.AddForce(randomForce, ForceMode.VelocityChange);
            }
            
            Debug.Log($"[KeyboardTester] 生成小球 #{existingBalls.Length + 1}: {ball.name}");
        }
        
        private void ClearAllBalls()
        {
            var balls = FindObjectsOfType<ThrowableBall>();
            foreach (var ball in balls)
            {
                if (ball != null)
                {
                    Destroy(ball.gameObject);
                }
            }
            Debug.Log($"[KeyboardTester] 清理了 {balls.Length} 个小球");
        }
        
        private void OnGUI()
        {
            var ballCount = FindObjectsOfType<ThrowableBall>().Length;
            var fps = (int)(1.0f / Time.deltaTime);
            
            GUI.Label(new Rect(10, 10, 400, 100), 
                $"🎯 真正的压力测试模式 🎯\n" +
                $"按 Space 生成小球 (最多{maxBalls}个)\n" +
                $"按 C 清理所有小球\n" +
                $"当前小球数量: {ballCount}/{maxBalls}\n" +
                $"FPS: {fps}");
        }
    }
}
