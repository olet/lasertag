using Anaglyph.XRTemplate;
using Anaglyph.Lasertag.Logistics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// VR手势投掷小球工具
    /// </summary>
    public class BallThrower : MonoBehaviour
    {
        [Header("投掷设置")]
        [SerializeField] private GameObject ballPrefab;
        [SerializeField] private Transform throwPoint;
        [SerializeField] private float throwForceMultiplier = 30f;  // 3倍力度
        [SerializeField] private float maxThrowForce = 50f;       // 2倍最大力
        [SerializeField] private int maxBallsPerPlayer = 5;
        
        [Header("预览")]
        [SerializeField] private LineRenderer trajectoryLine;
        [SerializeField] private int trajectoryPoints = 30;
        [SerializeField] private float trajectoryTimeStep = 0.1f;
        [SerializeField] private LayerMask environmentLayer = -1;
        
        [Header("VR交互")]
        [SerializeField] private XRDirectInteractor handInteractor;
        
        private HandedHierarchy hand;
        private Vector3 lastPosition;
        private Vector3 currentVelocity;
        private bool isHolding = false;
        private bool isAiming = false;
        private GameObject previewBall;
        private int ballsThrown = 0;
        
        // VR输入动作
        private InputAction gripAction;
        private InputAction triggerAction;

        private void Awake()
        {
            hand = GetComponentInParent<HandedHierarchy>(true);
            
            if (throwPoint == null)
                throwPoint = transform;
                
            if (handInteractor == null)
                handInteractor = GetComponentInParent<XRDirectInteractor>();
                
            // 设置轨迹线
            if (trajectoryLine != null)
            {
                trajectoryLine.positionCount = trajectoryPoints;
                trajectoryLine.enabled = false;
                trajectoryLine.useWorldSpace = true;
            }
            
            // 设置VR输入绑定
            SetupVRInput();
            
            lastPosition = throwPoint.position;
        }
        
        private void SetupVRInput()
        {
            // 根据手势设置输入绑定
            if (hand != null)
            {
                if (hand.Handedness == InteractorHandedness.Left)
                {
                    // 左手控制器
                    gripAction = new InputAction("LeftGrip", binding: "<XRController>{LeftHand}/gripPressed");
                    triggerAction = new InputAction("LeftTrigger", binding: "<XRController>{LeftHand}/triggerPressed");
                }
                else
                {
                    // 右手控制器
                    gripAction = new InputAction("RightGrip", binding: "<XRController>{RightHand}/gripPressed");
                    triggerAction = new InputAction("RightTrigger", binding: "<XRController>{RightHand}/triggerPressed");
                }
            }
            else
            {
                // 默认使用右手
                gripAction = new InputAction("Grip", binding: "<XRController>/gripPressed");
                triggerAction = new InputAction("Trigger", binding: "<XRController>/triggerPressed");
            }
            
            // 绑定回调事件
            gripAction.performed += OnGripPressed;
            gripAction.canceled += OnGripPressed;
            
            triggerAction.performed += OnTriggerPressed;
            triggerAction.canceled += OnTriggerPressed;
        }

        private void Update()
        {
            // 计算手部速度
            CalculateHandVelocity();
            
            if (isHolding && isAiming)
            {
                UpdateTrajectoryPreview();
            }
        }

        private void CalculateHandVelocity()
        {
            Vector3 currentPosition = throwPoint.position;
            currentVelocity = (currentPosition - lastPosition) / Time.deltaTime;
            lastPosition = currentPosition;
        }

        private void OnGripPressed(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                StartHolding();
            }
            else if (context.canceled)
            {
                if (isHolding)
                    ThrowBall();
                    
                StopHolding();
            }
        }

        private void OnTriggerPressed(InputAction.CallbackContext context)
        {
            if (context.performed && isHolding)
            {
                isAiming = true;
                ShowTrajectoryPreview();
            }
            else if (context.canceled)
            {
                isAiming = false;
                HideTrajectoryPreview();
            }
        }

        private void StartHolding()
        {
            if (ballsThrown >= maxBallsPerPlayer)
            {
                Debug.Log($"已达到每玩家小球数量限制: {maxBallsPerPlayer}");
                return;
            }
            
            isHolding = true;
            CreatePreviewBall();
        }

        private void StopHolding()
        {
            isHolding = false;
            isAiming = false;
            
            DestroyPreviewBall();
            HideTrajectoryPreview();
        }

        private void CreatePreviewBall()
        {
            if (ballPrefab == null) return;
            
            previewBall = Instantiate(ballPrefab, throwPoint.position, throwPoint.rotation);
            
            // 禁用预览球的物理和网络组件
            var rb = previewBall.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            
            var netObj = previewBall.GetComponent<NetworkObject>();
            if (netObj != null) netObj.enabled = false;
            
            var collider = previewBall.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            
            // 半透明显示
            var renderers = previewBall.GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.materials)
                {
                    if (material.HasProperty("_Color"))
                    {
                        Color color = material.color;
                        color.a = 0.5f;
                        material.color = color;
                    }
                }
            }
        }

        private void DestroyPreviewBall()
        {
            if (previewBall != null)
            {
                Destroy(previewBall);
                previewBall = null;
            }
        }

        private void UpdatePreviewBallPosition()
        {
            if (previewBall != null)
            {
                previewBall.transform.position = throwPoint.position;
                previewBall.transform.rotation = throwPoint.rotation;
            }
        }

        private void ShowTrajectoryPreview()
        {
            if (trajectoryLine != null)
                trajectoryLine.enabled = true;
        }

        private void HideTrajectoryPreview()
        {
            if (trajectoryLine != null)
                trajectoryLine.enabled = false;
        }

        private void UpdateTrajectoryPreview()
        {
            if (trajectoryLine == null || !isAiming) return;
            
            Vector3 velocity = CalculateThrowVelocity();
            UpdatePreviewBallPosition();
            
            Vector3[] points = CalculateTrajectory(throwPoint.position, velocity);
            trajectoryLine.positionCount = points.Length;
            trajectoryLine.SetPositions(points);
        }

        private Vector3[] CalculateTrajectory(Vector3 startPos, Vector3 velocity)
        {
            Vector3[] points = new Vector3[trajectoryPoints];
            Vector3 currentPos = startPos;
            Vector3 currentVel = velocity;
            
            for (int i = 0; i < trajectoryPoints; i++)
            {
                points[i] = currentPos;
                
                // 检查是否撞到障碍物
                if (i > 0 && Physics.Raycast(points[i-1], currentPos - points[i-1], 
                    out RaycastHit hit, Vector3.Distance(points[i-1], currentPos), environmentLayer))
                {
                    // 在碰撞点结束轨迹
                    System.Array.Resize(ref points, i + 1);
                    points[i] = hit.point;
                    break;
                }
                
                // 应用重力和运动
                currentVel += Physics.gravity * trajectoryTimeStep;
                currentPos += currentVel * trajectoryTimeStep;
            }
            
            return points;
        }

        private void ThrowBall()
        {
            if (!NetworkManager.Singleton.IsConnectedClient)
                return;
                
            Vector3 throwVelocity = CalculateThrowVelocity();
            Vector3 throwTorque = CalculateThrowTorque();
            
            // 使用代码工厂创建小球
            GameObject ballInstance;
            
            if (ballPrefab != null)
            {
                // 方案A: 如果有预制体，使用传统方式
                NetworkObject networkBall = NetworkObjectPool.Instance.GetNetworkObject(
                    ballPrefab, throwPoint.position, throwPoint.rotation);
                networkBall.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
                ballInstance = networkBall.gameObject;
            }
            else
            {
                // 方案B: 使用纯代码工厂创建
                ballInstance = Objects.BallFactory.SpawnBall(throwPoint.position, throwPoint.rotation);
                
                // 手动网络生成
                var networkObject = ballInstance.GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.SpawnWithOwnership(NetworkManager.Singleton.LocalClientId);
                }
            }
            
            // 应用投掷力
            var ballComponent = ballInstance.GetComponent<Objects.ThrowableBall>();
            if (ballComponent != null)
            {
                ballComponent.ThrowBall(throwVelocity, throwTorque);
            }
            
            ballsThrown++;
            
            // 播放投掷音效
            PlayThrowSound();
            
            Debug.Log($"[BallThrower] 投掷第 {ballsThrown} 个小球");
        }

        private Vector3 CalculateThrowVelocity()
        {
            // 基于手部运动计算投掷速度
            Vector3 throwVelocity = currentVelocity * throwForceMultiplier;
            
            // 限制最大投掷力
            if (throwVelocity.magnitude > maxThrowForce)
                throwVelocity = throwVelocity.normalized * maxThrowForce;
                
            // 最小投掷力
            if (throwVelocity.magnitude < 1f)
                throwVelocity = throwPoint.forward * 3f;
            
            return throwVelocity;
        }

        private Vector3 CalculateThrowTorque()
        {
            // 基于手部旋转计算自旋
            return Random.insideUnitSphere * 2f;
        }

        private void PlayThrowSound()
        {
            // TODO: 添加投掷音效
        }

        /// <summary>
        /// 重置球计数器（游戏回合结束时调用）
        /// </summary>
        public void ResetBallCount()
        {
            ballsThrown = 0;
        }

        private void OnEnable()
        {
            lastPosition = throwPoint.position;
            
            // 启用VR输入
            gripAction?.Enable();
            triggerAction?.Enable();
        }

        private void OnDisable()
        {
            StopHolding();
            
            // 禁用VR输入
            gripAction?.Disable();
            triggerAction?.Disable();
        }
        
        private void OnDestroy()
        {
            // 清理输入动作
            gripAction?.Dispose();
            triggerAction?.Dispose();
        }

        // 调试用的可视化
        private void OnDrawGizmos()
        {
            if (throwPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(throwPoint.position, 0.02f);
                Gizmos.DrawRay(throwPoint.position, throwPoint.forward * 0.1f);
            }
        }
    }
}
