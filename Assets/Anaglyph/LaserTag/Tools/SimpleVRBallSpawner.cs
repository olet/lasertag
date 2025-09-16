using UnityEngine;
using UnityEngine.InputSystem;
using Anaglyph.Lasertag.Objects;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// 简化版VR小球生成器 - A/X键生成普通小球，B/Y键生成自动标记交互小球
    /// </summary>
    public class SimpleVRBallSpawner : MonoBehaviour
    {
        [Header("简单设置")]
        [SerializeField] private float forwardForce = 20f;  // 4倍前进力
        [SerializeField] private float upwardForce = 8f;   // 4倍向上力
        
        private InputAction rightHandAction;        // A键 = 右手普通球
        private InputAction leftHandAction;         // X键 = 左手普通球
        private InputAction rightHandInteractAction; // B键 = 右手交互球
        private InputAction leftHandInteractAction;  // Y键 = 左手交互球
        private Transform rightHandTransform;
        private Transform leftHandTransform;
        
        private void Awake()
        {
            // 找到左右手
            var rightHand = GameObject.Find("Right Hand");
            var leftHand = GameObject.Find("Left Hand");
            
            if (rightHand != null) rightHandTransform = rightHand.transform;
            if (leftHand != null) leftHandTransform = leftHand.transform;
                
            // A键 = 右手普通球
            rightHandAction = new InputAction("RightHandBall", binding: "<XRController>{RightHand}/primaryButton");
            rightHandAction.performed += OnRightHandPressed;
            
            // X键 = 左手普通球
            leftHandAction = new InputAction("LeftHandBall", binding: "<XRController>{LeftHand}/primaryButton");
            leftHandAction.performed += OnLeftHandPressed;
            
            // B键 = 右手交互球
            rightHandInteractAction = new InputAction("RightHandInteractBall", binding: "<XRController>{RightHand}/secondaryButton");
            rightHandInteractAction.performed += OnRightHandInteractPressed;
            
            // Y键 = 左手交互球
            leftHandInteractAction = new InputAction("LeftHandInteractBall", binding: "<XRController>{LeftHand}/secondaryButton");
            leftHandInteractAction.performed += OnLeftHandInteractPressed;
        }
        
        private void OnEnable()
        {
            rightHandAction?.Enable();
            leftHandAction?.Enable();
            rightHandInteractAction?.Enable();
            leftHandInteractAction?.Enable();
        }
        
        private void OnDisable()
        {
            rightHandAction?.Disable();
            leftHandAction?.Disable();
            rightHandInteractAction?.Disable();
            leftHandInteractAction?.Disable();
        }
        
        private void OnDestroy()
        {
            rightHandAction?.Dispose();
            leftHandAction?.Dispose();
            rightHandInteractAction?.Dispose();
            leftHandInteractAction?.Dispose();
        }
        
        private void OnRightHandPressed(InputAction.CallbackContext context)
        {
            SpawnBallFromHand(rightHandTransform);
        }
        
        private void OnLeftHandPressed(InputAction.CallbackContext context)
        {
            SpawnBallFromHand(leftHandTransform);
        }
        
        private void OnRightHandInteractPressed(InputAction.CallbackContext context)
        {
            SpawnInteractBallFromHand(rightHandTransform);
        }
        
        private void OnLeftHandInteractPressed(InputAction.CallbackContext context)
        {
            SpawnInteractBallFromHand(leftHandTransform);
        }
        
        private void SpawnBallFromHand(Transform handTransform)
        {
            if (handTransform == null)
            {
                Debug.LogWarning("[SimpleVRBallSpawner] 手部Transform未找到！");
                return;
            }
            
            // 使用BallFactory生成小球
            var ball = BallFactory.SpawnBall(handTransform.position, handTransform.rotation);
            
            // 添加向前的力
            var rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = handTransform.forward * forwardForce + Vector3.up * upwardForce;
                rb.AddForce(force, ForceMode.VelocityChange);
                
                // 添加随机旋转
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            }
            
            Debug.Log($"[SimpleVRBallSpawner] 从{handTransform.name}生成普通小球并投掷");
        }
        
        private void SpawnInteractBallFromHand(Transform handTransform)
        {
            if (handTransform == null)
            {
                Debug.LogWarning("[SimpleVRBallSpawner] 手部Transform未找到！");
                return;
            }
            
            // 使用BallFactory生成小球
            var ball = BallFactory.SpawnBall(handTransform.position, handTransform.rotation);
            
            // 添加向前的力
            var rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = handTransform.forward * forwardForce + Vector3.up * upwardForce;
                rb.AddForce(force, ForceMode.VelocityChange);
                
                // 添加随机旋转
                rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.VelocityChange);
            }
            
            // 🎯 自动标记为可交互的小球（等小球钉住后）
            var interactableBall = ball.GetComponent<InteractableBall>();
            if (interactableBall != null)
            {
                // 延迟2秒等小球钉住后自动标记
                StartCoroutine(AutoMarkBallForInteraction(interactableBall));
            }
            
            Debug.Log($"[SimpleVRBallSpawner] 从{handTransform.name}生成自动交互小球并投掷");
        }
        
        private System.Collections.IEnumerator AutoMarkBallForInteraction(InteractableBall ball)
        {
            // 等待2秒让小球钉住
            yield return new WaitForSeconds(2f);
            
            // 检查是否钉住
            if (ball != null && ball.CanBeMarked())
            {
                ball.MarkForInteraction(BallMarkingMethod.Manual);
                Debug.Log($"[SimpleVRBallSpawner] 小球 {ball.name} 自动标记为可交互！");
            }
        }
    }
}
