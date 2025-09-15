using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Anaglyph.Lasertag.Objects
{
    /// <summary>
    /// 多球标记系统管理器
    /// 负责管理多个小球的标记、取消标记和交互协调
    /// </summary>
    public class MultiBallMarkingSystem : MonoBehaviour
    {
        [Header("标记设置")]
        [SerializeField] private float gazeMarkingDistance = 5f;
        [SerializeField] private float pointingMarkingDistance = 5f;
        [SerializeField] private LayerMask ballLayerMask = 1 << 0;
        
        [Header("输入检测")]
        [SerializeField] private bool enableHeadGazeMarking = true;
        [SerializeField] private bool enableControllerPointMarking = true;
        [SerializeField] private KeyCode manualMarkingKey = KeyCode.M;
        [SerializeField] private KeyCode clearAllMarkingsKey = KeyCode.C;
        
        [Header("性能管理")]
        [SerializeField] private int maxMarkedBalls = 10;  // 最多同时标记球数
        [SerializeField] private float cleanupInterval = 2f; // 清理检查间隔
        
        [Header("调试")]
        [SerializeField] private bool showDebugUI = true;
        
        // 管理数据
        private HashSet<InteractableBall> markedBalls = new HashSet<InteractableBall>();
        private InteractableBall gazeTarget = null;      // 当前注视目标
        private InteractableBall pointingTarget = null;  // 当前指向目标
        
        // 性能统计
        private int totalDetectionCount = 0;
        private float lastCleanupTime = 0f;
        
        // 属性访问器
        public int MarkedBallsCount => markedBalls.Count;
        public int MaxMarkedBalls => maxMarkedBalls;
        public bool HasMarkedBalls => markedBalls.Count > 0;
        
        void Start()
        {
            Debug.Log($"[MultiBallMarking] 多球标记系统启动，最大标记数: {maxMarkedBalls}");
            lastCleanupTime = Time.time;
        }
        
        void Update()
        {
            // 输入检测
            if (enableHeadGazeMarking)
                CheckHeadGazeMarking();
                
            if (enableControllerPointMarking)
                CheckControllerPointMarking();
                
            if (Input.GetKeyDown(manualMarkingKey))
                ToggleManualMarking();
                
            if (Input.GetKeyDown(clearAllMarkingsKey))
                ClearAllMarkings();
            
            // 定期清理
            if (Time.time - lastCleanupTime > cleanupInterval)
            {
                PerformCleanup();
                lastCleanupTime = Time.time;
            }
        }
        
        /// <summary>
        /// 检查头部注视标记
        /// </summary>
        void CheckHeadGazeMarking()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;
            
            Ray gazeRay = new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            CheckMarkingRay(gazeRay, BallMarkingMethod.HeadGaze, gazeMarkingDistance, ref gazeTarget);
        }
        
        /// <summary>
        /// 检查控制器指向标记
        /// </summary>
        void CheckControllerPointMarking()
        {
            var leftPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
            var leftRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.LTouch);
            
            Ray pointingRay = new Ray(leftPos, leftRot * Vector3.forward);
            CheckMarkingRay(pointingRay, BallMarkingMethod.ControllerPoint, pointingMarkingDistance, ref pointingTarget);
        }
        
        /// <summary>
        /// 通用射线标记检测
        /// </summary>
        void CheckMarkingRay(Ray ray, BallMarkingMethod method, float maxDistance, ref InteractableBall currentTarget)
        {
            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, ballLayerMask))
            {
                var ball = hit.collider.GetComponent<InteractableBall>();
                
                if (ball != null && ball.CanBeMarked())
                {
                    if (currentTarget != ball)
                    {
                        // 移除旧目标的标记（如果是同一方法）
                        if (currentTarget != null && currentTarget.CurrentMarkingMethod == method)
                        {
                            UnmarkBall(currentTarget);
                        }
                        
                        // 标记新目标
                        currentTarget = ball;
                        MarkBall(ball, method);
                    }
                }
            }
            else
            {
                // 没有射线击中，移除当前目标标记
                if (currentTarget != null && currentTarget.CurrentMarkingMethod == method)
                {
                    UnmarkBall(currentTarget);
                    currentTarget = null;
                }
            }
        }
        
        /// <summary>
        /// 标记球
        /// </summary>
        public bool MarkBall(InteractableBall ball, BallMarkingMethod method)
        {
            if (ball == null) return false;
            
            if (!ball.CanBeMarked())
            {
                Debug.LogWarning($"[MultiBallMarking] 球 {ball.name} 不能被标记：不在钉住状态");
                return false;
            }
            
            if (markedBalls.Count >= maxMarkedBalls && !markedBalls.Contains(ball))
            {
                Debug.LogWarning($"[MultiBallMarking] 已达到最大标记数量 {maxMarkedBalls}，无法标记更多球");
                return false;
            }
            
            if (!markedBalls.Contains(ball))
            {
                markedBalls.Add(ball);
                ball.MarkForInteraction(method);
                totalDetectionCount++;
                
                Debug.Log($"[MultiBallMarking] 球 {ball.name} 被{method.GetDisplayName()}标记，当前标记数: {markedBalls.Count}");
                return true;
            }
            else if (ball.CurrentMarkingMethod != method)
            {
                // 更新标记方式
                ball.MarkForInteraction(method);
                Debug.Log($"[MultiBallMarking] 球 {ball.name} 标记方式更新为{method.GetDisplayName()}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 取消标记球
        /// </summary>
        public bool UnmarkBall(InteractableBall ball)
        {
            if (ball == null) return false;
            
            if (markedBalls.Contains(ball))
            {
                markedBalls.Remove(ball);
                ball.UnmarkInteraction();
                
                // 清理引用
                if (gazeTarget == ball) gazeTarget = null;
                if (pointingTarget == ball) pointingTarget = null;
                
                Debug.Log($"[MultiBallMarking] 球 {ball.name} 取消标记，当前标记数: {markedBalls.Count}");
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 手动切换标记
        /// </summary>
        void ToggleManualMarking()
        {
            var nearestBall = FindNearestStuckBall();
            if (nearestBall != null)
            {
                if (markedBalls.Contains(nearestBall))
                {
                    UnmarkBall(nearestBall);
                }
                else
                {
                    MarkBall(nearestBall, BallMarkingMethod.Manual);
                }
            }
            else
            {
                Debug.LogWarning("[MultiBallMarking] 附近没有可标记的钉住球");
            }
        }
        
        /// <summary>
        /// 清除所有标记
        /// </summary>
        public void ClearAllMarkings()
        {
            var ballsToUnmark = new List<InteractableBall>(markedBalls);
            foreach (var ball in ballsToUnmark)
            {
                UnmarkBall(ball);
            }
            
            gazeTarget = null;
            pointingTarget = null;
            
            Debug.Log("[MultiBallMarking] 清除所有标记");
        }
        
        /// <summary>
        /// 寻找最近的钉住球
        /// </summary>
        InteractableBall FindNearestStuckBall()
        {
            var allBalls = FindObjectsOfType<InteractableBall>();
            InteractableBall nearest = null;
            float minDistance = float.MaxValue;
            
            Vector3 referencePosition = Camera.main?.transform.position ?? transform.position;
            
            foreach (var ball in allBalls)
            {
                if (ball.CanBeMarked())
                {
                    float distance = Vector3.Distance(referencePosition, ball.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearest = ball;
                    }
                }
            }
            
            return nearest;
        }
        
        /// <summary>
        /// 定期清理无效的标记
        /// </summary>
        void PerformCleanup()
        {
            var ballsToRemove = new List<InteractableBall>();
            
            foreach (var ball in markedBalls)
            {
                if (ball == null || !ball.CanBeMarked())
                {
                    ballsToRemove.Add(ball);
                }
            }
            
            foreach (var ball in ballsToRemove)
            {
                UnmarkBall(ball);
            }
            
            if (ballsToRemove.Count > 0)
            {
                Debug.Log($"[MultiBallMarking] 清理了 {ballsToRemove.Count} 个无效标记");
            }
        }
        
        /// <summary>
        /// 获取所有标记的球
        /// </summary>
        public List<InteractableBall> GetMarkedBalls()
        {
            return markedBalls.Where(ball => ball != null).ToList();
        }
        
        /// <summary>
        /// 检查球是否已标记
        /// </summary>
        public bool IsBallMarked(InteractableBall ball)
        {
            return ball != null && markedBalls.Contains(ball);
        }
        
        /// <summary>
        /// 调试UI显示
        /// </summary>
        void OnGUI()
        {
            if (!showDebugUI || !Application.isEditor) return;
            
            GUILayout.BeginArea(new Rect(10, 100, 350, 250));
            GUILayout.Box("🎯 多球标记系统");
            
            GUILayout.Label($"标记的球: {markedBalls.Count} / {maxMarkedBalls}");
            GUILayout.Label($"总检测次数: {totalDetectionCount}");
            
            if (gazeTarget != null)
                GUILayout.Label($"👀 注视目标: {gazeTarget.name}");
                
            if (pointingTarget != null)
                GUILayout.Label($"🎮 指向目标: {pointingTarget.name}");
            
            GUILayout.Space(10);
            GUILayout.Label("📋 标记的球列表:");
            foreach (var ball in markedBalls.Take(5)) // 只显示前5个
            {
                if (ball != null)
                {
                    GUILayout.Label($"• {ball.name} ({ball.CurrentMarkingMethod.GetDisplayName()})");
                }
            }
            
            if (markedBalls.Count > 5)
            {
                GUILayout.Label($"... 和其他 {markedBalls.Count - 5} 个球");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("🎮 操作提示:");
            GUILayout.Label($"{manualMarkingKey}键: 手动标记最近的球");
            GUILayout.Label($"{clearAllMarkingsKey}键: 清除所有标记");
            
            GUILayout.EndArea();
        }
    }
}
