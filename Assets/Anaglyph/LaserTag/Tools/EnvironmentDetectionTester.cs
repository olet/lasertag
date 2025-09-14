using UnityEngine;
using Anaglyph.XRTemplate;
using System.Collections.Generic;

namespace Anaglyph.Lasertag.Tools
{
    /// <summary>
    /// 测试Quest 3环境检测系统
    /// 验证EnvironmentMapper是否能检测到真实房间的表面
    /// </summary>
    public class EnvironmentDetectionTester : MonoBehaviour
    {
        [Header("测试设置")]
        [SerializeField] private bool enableVisualDebug = true;
        [SerializeField] private float testRadius = 3f;
        [SerializeField] private float testInterval = 1f;
        [SerializeField] private LayerMask testLayer = 1 << 11;
        
        [Header("可视化")]
        [SerializeField] private Material hitMaterial;
        [SerializeField] private Material missMaterial;
        
        private List<GameObject> debugObjects = new List<GameObject>();
        private Transform playerHead;
        private float lastTestTime;

        private void Start()
        {
            playerHead = Camera.main?.transform;
            if (playerHead == null)
                playerHead = transform;
                
            CreateDebugMaterials();
        }

        private void Update()
        {
            if (Time.time - lastTestTime > testInterval)
            {
                TestEnvironmentDetection();
                lastTestTime = Time.time;
            }
        }

        /// <summary>
        /// 测试环境检测
        /// </summary>
        private void TestEnvironmentDetection()
        {
            if (EnvironmentMapper.Instance == null)
            {
                Debug.LogWarning("[EnvironmentTester] EnvironmentMapper不可用");
                return;
            }

            ClearDebugObjects();
            
            Vector3 playerPos = playerHead.position;
            int hitCount = 0;
            int totalTests = 0;
            
            // 测试地面检测
            hitCount += TestGroundDetection(playerPos);
            totalTests += 8;
            
            // 测试墙壁检测  
            hitCount += TestWallDetection(playerPos);
            totalTests += 16;
            
            // 测试天花板检测
            hitCount += TestCeilingDetection(playerPos);
            totalTests += 4;
            
            float hitRate = (float)hitCount / totalTests * 100f;
            Debug.Log($"[EnvironmentTester] 环境检测率: {hitRate:F1}% ({hitCount}/{totalTests})");
            
            // 测试小球投掷点
            TestBallDropPoints(playerPos);
        }

        /// <summary>
        /// 测试地面检测
        /// </summary>
        private int TestGroundDetection(Vector3 center)
        {
            int hits = 0;
            float[] angles = { 0, 45, 90, 135, 180, 225, 270, 315 };
            
            foreach (float angle in angles)
            {
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad), 
                    0, 
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                ) * testRadius * 0.5f;
                
                Vector3 testPoint = center + offset + Vector3.up * 0.5f;
                Ray downRay = new Ray(testPoint, Vector3.down);
                
                if (EnvironmentMapper.Raycast(downRay, 2f, out var hitResult))
                {
                    CreateDebugMarker(hitResult.point, true, "Ground");
                    hits++;
                }
                else
                {
                    CreateDebugMarker(testPoint + Vector3.down * 2f, false, "Ground");
                }
            }
            
            return hits;
        }

        /// <summary>
        /// 测试墙壁检测
        /// </summary>
        private int TestWallDetection(Vector3 center)
        {
            int hits = 0;
            Vector3[] directions = { 
                Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                (Vector3.forward + Vector3.left).normalized,
                (Vector3.forward + Vector3.right).normalized,
                (Vector3.back + Vector3.left).normalized,
                (Vector3.back + Vector3.right).normalized
            };
            
            foreach (Vector3 direction in directions)
            {
                for (float height = 0f; height <= 2f; height += 1f)
                {
                    Vector3 testPoint = center + Vector3.up * height;
                    Ray wallRay = new Ray(testPoint, direction);
                    
                    if (EnvironmentMapper.Raycast(wallRay, testRadius, out var hitResult))
                    {
                        CreateDebugMarker(hitResult.point, true, "Wall");
                        hits++;
                    }
                    else
                    {
                        CreateDebugMarker(testPoint + direction * testRadius, false, "Wall");
                    }
                }
            }
            
            return hits;
        }

        /// <summary>
        /// 测试天花板检测
        /// </summary>
        private int TestCeilingDetection(Vector3 center)
        {
            int hits = 0;
            Vector3[] offsets = { 
                Vector3.zero, Vector3.forward, Vector3.back, Vector3.left, Vector3.right 
            };
            
            foreach (Vector3 offset in offsets)
            {
                Vector3 testPoint = center + offset * 0.5f + Vector3.up * 2f;
                Ray upRay = new Ray(testPoint, Vector3.up);
                
                if (EnvironmentMapper.Raycast(upRay, 1.5f, out var hitResult))
                {
                    CreateDebugMarker(hitResult.point, true, "Ceiling");
                    hits++;
                }
                else
                {
                    CreateDebugMarker(testPoint + Vector3.up * 1.5f, false, "Ceiling");
                }
            }
            
            return hits;
        }

        /// <summary>
        /// 测试小球投掷点
        /// </summary>
        private void TestBallDropPoints(Vector3 center)
        {
            // 在玩家前方测试几个小球投掷点
            for (int i = 0; i < 3; i++)
            {
                Vector3 dropPoint = center + playerHead.forward * (1f + i * 0.5f) + Vector3.up * (1f + i * 0.3f);
                Ray dropRay = new Ray(dropPoint, Vector3.down);
                
                if (EnvironmentMapper.Raycast(dropRay, 3f, out var hitResult))
                {
                    CreateDebugMarker(hitResult.point, true, $"Drop{i}", Color.yellow);
                    Debug.Log($"[EnvironmentTester] 投掷点 {i}: 地面距离 {hitResult.distance:F2}m");
                }
            }
        }

        /// <summary>
        /// 创建调试标记
        /// </summary>
        private void CreateDebugMarker(Vector3 position, bool hit, string label, Color? customColor = null)
        {
            if (!enableVisualDebug) return;
            
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * 0.05f;
            marker.name = $"EnvTest_{label}_{(hit ? "Hit" : "Miss")}";
            
            // 移除碰撞体
            Destroy(marker.GetComponent<SphereCollider>());
            
            // 设置材质
            var renderer = marker.GetComponent<Renderer>();
            if (customColor.HasValue)
            {
                renderer.material.color = customColor.Value;
            }
            else
            {
                renderer.material = hit ? hitMaterial : missMaterial;
            }
            
            debugObjects.Add(marker);
            
            // 自动清理
            Destroy(marker, testInterval * 2f);
        }

        private void CreateDebugMaterials()
        {
            if (hitMaterial == null)
            {
                hitMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                hitMaterial.color = Color.green;
            }
            
            if (missMaterial == null)
            {
                missMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                missMaterial.color = Color.red;
            }
        }

        private void ClearDebugObjects()
        {
            foreach (var obj in debugObjects)
            {
                if (obj != null)
                    Destroy(obj);
            }
            debugObjects.Clear();
        }

        private void OnDestroy()
        {
            ClearDebugObjects();
        }

        private void OnGUI()
        {
            if (EnvironmentMapper.Instance == null)
            {
                GUI.Label(new Rect(10, 100, 400, 20), "❌ EnvironmentMapper 不可用");
                return;
            }
            
            GUI.Label(new Rect(10, 100, 400, 80), 
                "🌍 Quest 3 环境检测测试器\n" +
                "绿色球 = 检测到表面\n" +
                "红色球 = 未检测到\n" +
                "黄色球 = 小球投掷测试点");
        }
    }
}
