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
        [SerializeField] private float forwardForce = 20f;  // 4倍前进力
        [SerializeField] private float upwardForce = 8f;   // 4倍向上力
        
        private InputAction rightHandAction;  // A键 = 右手
        private InputAction leftHandAction;   // X键 = 左手
        private Transform rightHandTransform;
        private Transform leftHandTransform;
        
        private void Awake()
        {
            // 找到左右手
            var rightHand = GameObject.Find("Right Hand");
            var leftHand = GameObject.Find("Left Hand");
            
            if (rightHand != null) rightHandTransform = rightHand.transform;
            if (leftHand != null) leftHandTransform = leftHand.transform;
                
            // A键 = 右手发射
            rightHandAction = new InputAction("RightHandBall", binding: "<XRController>{RightHand}/primaryButton");
            rightHandAction.performed += OnRightHandPressed;
            
            // X键 = 左手发射  
            leftHandAction = new InputAction("LeftHandBall", binding: "<XRController>{LeftHand}/primaryButton");
            leftHandAction.performed += OnLeftHandPressed;
        }
        
        private void OnEnable()
        {
            rightHandAction?.Enable();
            leftHandAction?.Enable();
        }
        
        private void OnDisable()
        {
            rightHandAction?.Disable();
            leftHandAction?.Disable();
        }
        
        private void OnDestroy()
        {
            rightHandAction?.Dispose();
            leftHandAction?.Dispose();
        }
        
        private void OnRightHandPressed(InputAction.CallbackContext context)
        {
            SpawnBallFromHand(rightHandTransform);
        }
        
        private void OnLeftHandPressed(InputAction.CallbackContext context)
        {
            SpawnBallFromHand(leftHandTransform);
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
            
            Debug.Log($"[SimpleVRBallSpawner] 从{handTransform.name}生成小球并投掷");
        }
    }
}
