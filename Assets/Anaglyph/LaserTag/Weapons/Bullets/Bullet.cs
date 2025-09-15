using Anaglyph.Netcode;
using Anaglyph.XRTemplate;
using Anaglyph.Lasertag.Objects;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Anaglyph.Lasertag
{
	public class Bullet : NetworkBehaviour
	{
		private const float MaxTravelDist = 50;

		[SerializeField] private float metersPerSecond;
		[SerializeField] private AnimationCurve damageOverDistance = AnimationCurve.Constant(0, MaxTravelDist, 50f);

		[SerializeField] private int despawnDelay = 1;

		[SerializeField] private AudioClip fireSFX;
		[SerializeField] private AudioClip collideSFX;

		private NetworkVariable<NetworkPose> spawnPoseSync = new();
		public Pose SpawnPose => spawnPoseSync.Value;

		private Ray fireRay;
		private bool isAlive;
		private float spawnedTime;
		private float envHitDist;
		private float travelDist;

		public event Action OnFire = delegate { };
		public event Action OnCollide = delegate { };

		private void Awake()
		{
			spawnPoseSync.OnValueChanged += OnSpawnPosChange;
		}

		public override void OnNetworkSpawn()
		{
			envHitDist = MaxTravelDist;
			isAlive = true;
			spawnedTime = Time.time;

			Debug.Log($"[LASER] Fire! IsOwner={IsOwner}, pos={transform.position}");

			if (IsOwner)
			{
				spawnPoseSync.Value = new NetworkPose(transform);
			}
			else
			{
				SetPose(SpawnPose);
			}

			OnFire.Invoke();
			AudioSource.PlayClipAtPoint(fireSFX, transform.position);

			fireRay = new(transform.position, transform.forward);
			if (EnvironmentMapper.Raycast(fireRay, MaxTravelDist, out var envCast))
				if (IsOwner)
					envHitDist = envCast.distance;
				else
					EnvironmentRaycastRpc(envCast.distance);
		}

		[Rpc(SendTo.Owner)]
		private void EnvironmentRaycastRpc(float dist)
		{
			if (dist > EnvironmentMapper.Instance.MaxEyeDist)
				envHitDist = Mathf.Min(envHitDist, dist);
		}

		private void OnSpawnPosChange(NetworkPose p, NetworkPose v) => SetPose(v);

		private void SetPose(Pose pose)
		{
			transform.SetPositionAndRotation(pose.position, pose.rotation);
		}

		private void Update()
		{
			if (isAlive)
			{
				float lifeTime = Time.time - spawnedTime;
				Vector3 prevPos = transform.position;
				travelDist = metersPerSecond * lifeTime;

				transform.position = fireRay.GetPoint(travelDist);

				if (IsOwner)
				{
					bool didHitEnv = travelDist > envHitDist;

					if (didHitEnv)
						transform.position = fireRay.GetPoint(envHitDist);

					bool didHitPhys = Physics.Linecast(prevPos, transform.position, out var physHit,
						Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
					Debug.Log($"[LASER] Raycast info: LayerMask={Physics.DefaultRaycastLayers}, distance={Vector3.Distance(prevPos, transform.position):F3}m");

					if (didHitPhys)
					{
						HitRpc(physHit.point, physHit.normal);

						var col = physHit.collider;
						Debug.Log($"[LASER] Hit object: {col.name}, Layer: {col.gameObject.layer}");

						if (col.CompareTag(Networking.PlayerAvatar.Tag))
						{
							var av = col.GetComponentInParent<Networking.PlayerAvatar>();
							float damage = damageOverDistance.Evaluate(travelDist);
							av.DamageRpc(damage, OwnerClientId);
						}

						// 🎯 新增功能：碰撞后发射短射线检测小球
						CheckForNearbyBalls(physHit.point, fireRay.direction);
					}
					else if (didHitEnv)
					{
						Vector3 envHitPoint = fireRay.GetPoint(envHitDist);
						HitRpc(envHitPoint, -transform.forward);
						
						// 🎯 新增功能：环境碰撞后也检测小球
						CheckForNearbyBalls(envHitPoint, fireRay.direction);
					}
				}
			}
		}
		
		/// <summary>
		/// 🎯 新增功能：在碰撞点附近检测小球
		/// </summary>
		private void CheckForNearbyBalls(Vector3 hitPoint, Vector3 laserDirection)
		{
			Debug.Log($"[LASER] Checking for balls near hit point: {hitPoint:F2}");
			
			// 方法1：球形检测 - 在碰撞点周围检测小球
			float checkRadius = 0.1f; // 10cm半径 - 扩大检测范围
			Collider[] nearbyColliders = Physics.OverlapSphere(hitPoint, checkRadius);
			
			Debug.Log($"[LASER] Found {nearbyColliders.Length} colliders in sphere");
			foreach (var col in nearbyColliders)
			{
				Debug.Log($"[LASER] Checking collider: {col.name}, has BallLaserInteraction: {col.GetComponent<BallLaserInteraction>() != null}");
				var ballInteraction = col.GetComponent<BallLaserInteraction>();
				if (ballInteraction != null)
				{
					var ballRb = col.GetComponent<Rigidbody>();
					Debug.Log($"[LASER] Found ball in sphere! Hit: {col.name}, isKinematic: {ballRb?.isKinematic}");
					ballInteraction.OnLaserHit(hitPoint, OwnerClientId, laserDirection);
					return; // 找到一个就够了
				}
			}
			
			// 方法2：沿着发射方向的短射线检测
			float checkDistance = 0.12f; // 12cm检测距离 - 也相应扩大
			Vector3 checkStart = hitPoint - laserDirection * 0.04f; // 往后退4cm开始
			Vector3 checkEnd = hitPoint + laserDirection * checkDistance;
			
			if (Physics.Linecast(checkStart, checkEnd, out var ballHit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
			{
				var ballInteraction = ballHit.collider.GetComponent<BallLaserInteraction>();
				if (ballInteraction != null)
				{
					var ballRb = ballHit.collider.GetComponent<Rigidbody>();
					Debug.Log($"[LASER] Found ball on extended ray! Hit: {ballHit.collider.name}, isKinematic: {ballRb?.isKinematic}");
					ballInteraction.OnLaserHit(ballHit.point, OwnerClientId, laserDirection);
					return;
				}
			}
			
			Debug.Log("[LASER] No balls found nearby with both methods");
		}

		[Rpc(SendTo.Everyone)]
		private void HitRpc(Vector3 pos, Vector3 norm)
		{
			transform.position = pos;
			transform.up = norm;
			isAlive = false;

			OnCollide.Invoke();
			AudioSource.PlayClipAtPoint(collideSFX, transform.position);

			if (IsOwner)
			{
				StartCoroutine(D());
				IEnumerator D() {
					yield return new WaitForSeconds(despawnDelay);
					NetworkObject.Despawn();
				}
			}
		}
	}
}