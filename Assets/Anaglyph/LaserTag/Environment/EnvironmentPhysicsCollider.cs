using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Anaglyph.XRTemplate;

namespace Anaglyph.Lasertag.Environment
{
    /// <summary>
    /// 将Quest 3环境深度数据转换为Unity物理碰撞体
    /// 让小球能与真实房间发生物理碰撞
    /// </summary>
    public class EnvironmentPhysicsCollider : MonoBehaviour
    {
        [Header("碰撞体生成设置")]
        [SerializeField] private float updateInterval = 2f; // 多久更新一次碰撞体
        [SerializeField] private float gridSize = 0.5f; // 网格密度
        [SerializeField] private float rayDistance = 8f; // 射线检测距离
        [SerializeField] private LayerMask environmentLayer = 1 << 10; // 环境碰撞体层
        
        [Header("性能优化")]
        [SerializeField] private int maxCollidersPerFrame = 20; // 每帧最多创建碰撞体数量
        [SerializeField] private bool useSimplifiedColliders = true; // 使用简化碰撞体
        [SerializeField] private float minColliderSize = 0.2f; // 最小碰撞体尺寸
        
        [Header("调试")]
        [SerializeField] private bool visualizeColliders = true;
        [SerializeField] private Material debugMaterial;

        private Transform playerHead;
        private List<GameObject> environmentColliders = new List<GameObject>();
        private HashSet<Vector3> processedPositions = new HashSet<Vector3>();
        private Queue<Vector3> pendingColliderCreation = new Queue<Vector3>();
        
        private Coroutine updateCoroutine;

        private void Start()
        {
            // 找到玩家头部位置作为扫描中心
            playerHead = Camera.main?.transform;
            if (playerHead == null)
                playerHead = transform;

            // 开始环境扫描和碰撞体生成
            updateCoroutine = StartCoroutine(EnvironmentScanLoop());
        }

        private void OnDestroy()
        {
            if (updateCoroutine != null)
                StopCoroutine(updateCoroutine);
                
            ClearAllColliders();
        }

        /// <summary>
        /// 持续扫描环境并生成碰撞体
        /// </summary>
        private IEnumerator EnvironmentScanLoop()
        {
            while (enabled)
            {
                yield return new WaitForSeconds(updateInterval);
                
                if (EnvironmentMapper.Instance != null)
                {
                    ScanEnvironmentAroundPlayer();
                }
            }
        }

        /// <summary>
        /// 扫描玩家周围的环境
        /// </summary>
        private void ScanEnvironmentAroundPlayer()
        {
            Vector3 playerPos = playerHead.position;
            
            // 在玩家周围进行网格扫描
            for (float x = -rayDistance; x <= rayDistance; x += gridSize)
            {
                for (float y = -2f; y <= 3f; y += gridSize) // 地面到天花板
                {
                    for (float z = -rayDistance; z <= rayDistance; z += gridSize)
                    {
                        Vector3 scanPoint = playerPos + new Vector3(x, y, z);
                        
                        // 跳过已处理的位置
                        Vector3 gridPos = SnapToGrid(scanPoint);
                        if (processedPositions.Contains(gridPos))
                            continue;
                            
                        // 向扫描点发射射线
                        CheckEnvironmentAtPoint(scanPoint, gridPos);
                    }
                }
            }
        }

        /// <summary>
        /// 检查特定点是否有环境表面
        /// </summary>
        private void CheckEnvironmentAtPoint(Vector3 worldPoint, Vector3 gridPos)
        {
            // 使用EnvironmentMapper进行射线检测
            Vector3 rayStart = worldPoint + Vector3.up * 0.1f;
            Ray downRay = new Ray(rayStart, Vector3.down);
            
            if (EnvironmentMapper.Raycast(downRay, 0.5f, out var hitResult))
            {
                // 发现环境表面，创建碰撞体
                pendingColliderCreation.Enqueue(hitResult.point);
                processedPositions.Add(gridPos);
            }
            
            // 同时检查其他方向的表面
            CheckSurfaceInDirection(worldPoint, Vector3.forward, gridPos);
            CheckSurfaceInDirection(worldPoint, Vector3.back, gridPos);
            CheckSurfaceInDirection(worldPoint, Vector3.left, gridPos);
            CheckSurfaceInDirection(worldPoint, Vector3.right, gridPos);
        }

        private void CheckSurfaceInDirection(Vector3 point, Vector3 direction, Vector3 gridPos)
        {
            Ray ray = new Ray(point, direction);
            if (EnvironmentMapper.Raycast(ray, gridSize * 2f, out var hitResult))
            {
                pendingColliderCreation.Enqueue(hitResult.point);
                processedPositions.Add(gridPos);
            }
        }

        private void Update()
        {
            // 每帧创建少量碰撞体，避免卡顿
            int created = 0;
            while (pendingColliderCreation.Count > 0 && created < maxCollidersPerFrame)
            {
                Vector3 colliderPos = pendingColliderCreation.Dequeue();
                CreateEnvironmentCollider(colliderPos);
                created++;
            }
        }

        /// <summary>
        /// 在环境表面创建物理碰撞体
        /// </summary>
        private void CreateEnvironmentCollider(Vector3 position)
        {
            GameObject colliderObj = new GameObject($"EnvCollider_{environmentColliders.Count}");
            colliderObj.transform.position = position;
            colliderObj.layer = (int)Mathf.Log(environmentLayer.value, 2);

            if (useSimplifiedColliders)
            {
                // 创建简单的盒子碰撞体
                var boxCollider = colliderObj.AddComponent<BoxCollider>();
                boxCollider.size = Vector3.one * minColliderSize;
            }
            else
            {
                // 创建更精确的网格碰撞体（性能较差）
                CreateMeshCollider(colliderObj, position);
            }

            // 调试可视化
            if (visualizeColliders)
            {
                AddDebugVisualization(colliderObj);
            }

            environmentColliders.Add(colliderObj);
        }

        private void CreateMeshCollider(GameObject colliderObj, Vector3 centerPos)
        {
            // 生成基于环境扫描的网格
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            
            // 简化：创建一个小平面
            float size = minColliderSize;
            vertices.AddRange(new Vector3[]
            {
                Vector3.zero + Vector3.left * size + Vector3.forward * size,
                Vector3.zero + Vector3.right * size + Vector3.forward * size,
                Vector3.zero + Vector3.right * size + Vector3.back * size,
                Vector3.zero + Vector3.left * size + Vector3.back * size
            });
            
            triangles.AddRange(new int[] { 0, 1, 2, 0, 2, 3 });

            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();

            var meshCollider = colliderObj.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = mesh;
        }

        private void AddDebugVisualization(GameObject colliderObj)
        {
            if (debugMaterial == null)
            {
                // 创建默认调试材质
                debugMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                debugMaterial.color = new Color(0, 1, 0, 0.3f); // 半透明绿色
                debugMaterial.SetFloat("_Surface", 1); // 透明模式
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(colliderObj.transform);
            cube.transform.localPosition = Vector3.zero;
            cube.transform.localScale = Vector3.one * minColliderSize;
            
            // 移除Cube的碰撞体，只用于可视化
            Destroy(cube.GetComponent<BoxCollider>());
            
            var renderer = cube.GetComponent<Renderer>();
            renderer.material = debugMaterial;
        }

        private Vector3 SnapToGrid(Vector3 position)
        {
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                Mathf.Round(position.y / gridSize) * gridSize,
                Mathf.Round(position.z / gridSize) * gridSize
            );
        }

        /// <summary>
        /// 清理所有环境碰撞体
        /// </summary>
        public void ClearAllColliders()
        {
            foreach (var collider in environmentColliders)
            {
                if (collider != null)
                    Destroy(collider);
            }
            
            environmentColliders.Clear();
            processedPositions.Clear();
            pendingColliderCreation.Clear();
        }

        /// <summary>
        /// 强制重新扫描环境
        /// </summary>
        public void RescanEnvironment()
        {
            ClearAllColliders();
            if (enabled && gameObject.activeInHierarchy)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = StartCoroutine(EnvironmentScanLoop());
            }
        }

        private void OnDrawGizmos()
        {
            if (playerHead != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(playerHead.position, Vector3.one * rayDistance * 2);
            }
        }
    }
}
