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
            var balls = FindObjectsOfType<ThrowableBall>();
            var ballCount = balls.Length;
            var fps = (int)(1.0f / Time.deltaTime);
            
            // 🎯 统计环境检测状态
            int groundedBalls = 0;
            int sleepingBalls = 0;
            foreach (var ball in balls)
            {
                var envPhysics = ball.GetComponent<Objects.EnvironmentBallPhysics>();
                if (envPhysics != null && envPhysics.IsGrounded())
                    groundedBalls++;
                    
                var rb = ball.GetComponent<Rigidbody>();
                if (rb != null && rb.IsSleeping())
                    sleepingBalls++;
            }
            
            bool environmentAvailable = Anaglyph.XRTemplate.EnvironmentMapper.Instance != null;
            
            // 🎯 统计钉住状态
            int stuckBalls = 0;
            foreach (var ball in balls)
            {
                var envPhysics = ball.GetComponent<Objects.EnvironmentBallPhysics>();
                if (envPhysics != null)
                {
                    // 通过反射检查isStuck字段
                    var stuckField = envPhysics.GetType().GetField("isStuck", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (stuckField != null && (bool)stuckField.GetValue(envPhysics))
                        stuckBalls++;
                }
            }
            
            GUI.Label(new Rect(10, 10, 650, 200), 
                $"🚀 激光枪风格碰撞检测测试 - 每帧检测 🚀\n" +
                $"按 X/A键 生成小球 (最多{maxBalls}个)\n" +
                $"按 C 清理所有小球\n" +
                $"当前小球数量: {ballCount}/{maxBalls}\n" +
                $"FPS: {fps}\n" +
                $"🌍 Quest环境映射器: {(environmentAvailable ? "✅ 可用" : "❌ 不可用")}\n" +
                $"🎯 钉在表面的小球: {groundedBalls}\n" +
                $"📌 已钉住休眠的小球: {stuckBalls}\n" +
                $"😴 物理休眠小球: {sleepingBalls}\n" +
                $"💡 原理: 复用激光枪每帧射线检测\n" +
                $"📏 小球尺寸: 2cm (更容易检测)");
        }
    }
}
