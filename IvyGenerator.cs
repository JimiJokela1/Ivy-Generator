using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Medusa
{
	[ExecuteInEditMode]
	public class IvyGenerator : MonoBehaviour
	{
		[Serializable]
		public class IvyNode
		{
			public Vector3 position;
			public Vector3 forward;
			public Vector3 toCollider;
		}

		[Tooltip("Provides the structure for the combined meshes with lods")]
		public GameObject combinedMeshesPrefab;
		[Tooltip("The leaf that will be spawned every node")]
		public GameObject leafPrefab;
		GameObject ivyParent;
		public Transform startPoint;
		[Tooltip("Shows nodes for previous ivy, no need to set anything here")]
		public List<IvyNode> nodes;
		[Header("Grow settings")]
		[Tooltip("How long is the branch section between nodes and therefore between leaves")]
		public float nodeLenght;
		[Tooltip("How many nodes in total (maximum, growth can be halted before this)")]
		[Range(1, 10000)]
		public int nodeCount;
		[Tooltip("Affects the angle that branch can randomly turn in one node")]
		[Range(0f, 180f)]
		public float randomAngleMax;
		[Tooltip("How strong the branch turns randomly")]
		[Range(0f, 1f)]
		public float randomDirectionStrenght;
		[Tooltip("How strong the branch turns towards closest collider")]
		[Range(-1f, 1f)]
		public float colliderApproachStrength;
		[Tooltip("How strongly gravity affects the ivy growth")]
		[Range(-1f, 1f)]
		public float gravityStrength;
		[Tooltip("Maximum distance that the branch can get from a collider")]
		public float maxDistanceToCollider = 1f;

		[Header("Leaf settings")]
		[Tooltip("How far from the node leaves can randomly be")]
		[Range(0.001f, 0.3f)]
		public float leafRandomPositionChange;
		[Tooltip("Random variance in leaf size, 0 means all leaves are the same size as far as this is concerned")]
		[Range(0f, 3f)]
		public float leafSizeVariance;
		[Tooltip("Variance in leaf size based on distance along branch, 0 means leaves are the same size regardless of where they are along the branch")]
		[Range(0f, 3f)]
		public float leafSizeByPosition;
		[Tooltip("Leaf size scale multiplier, 1 is normal scale")]
		[Range(0.001f, 3f)]
		public float leafScale = 1f;

		public Vector3 leafRandomRotationLimits;
		[Tooltip("Affects the angle at which leaves will orient differently for more horizontal surfaces")]
		public float horizontalLimitFactor;

		[Tooltip("Makes the combined mesh have 32bit vertex index, so an ivy of any node count should fit into a single mesh. 16bit separates the ivy into maximum size meshes")]
		public bool combinedMesh32BitEnabled = true;

		[Header("Branch geometry settings")]
		public int verticesPerNode;
		public float branchThickness;
		public float branchThicknessVariance;
		public Material branchMaterial;

		public void Grow()
		{
			// Create gameobject as parent to store all generated objects
			ivyParent = new GameObject(GetNextIvyName());
			ivyParent.transform.position = startPoint.position;

			// Debug.Log("Started Growing");

			nodes = new List<IvyNode>();

			// First node just goes down
			IvyNode startNode = new IvyNode();
			startNode.position = startPoint.position;
			startNode.forward = Vector3.down;
			startNode.toCollider = FindVectorToClosestCollider(startPoint.position);
			nodes.Add(startNode);

			int i = 1;
			for (i = 1; i < nodeCount; i++)
			{
				Quaternion randomRotation = new Quaternion();

				IvyNode prevNode = nodes[i - 1];
				Vector3 newPos;
				Vector3 toCollider = Vector3.zero;
				int tries = 0;
				while (true)
				{
					// Random rotation calculation (might not be working right in all situations currently)
					float randomRight = UnityEngine.Random.Range(-randomAngleMax, randomAngleMax);
					float randomUp = UnityEngine.Random.Range(-randomAngleMax, randomAngleMax);
					randomRotation.eulerAngles = new Vector3(randomRight, randomUp) * randomDirectionStrenght;

					// Calculate new pos based on sum of factors
					newPos = prevNode.position +
						randomRotation * prevNode.forward.normalized * nodeLenght +
						prevNode.toCollider * colliderApproachStrength +
						Vector3.down * gravityStrength;

					if (!CheckBranchCollision(prevNode.position, newPos))
					{
						toCollider = FindVectorToClosestCollider(newPos);
						if (toCollider != Vector3.zero)
						{
							break;
						}
					}
					tries++;
					if (tries > 1000)
					{
						Debug.Log("Cant find valid new node pos");
						break;
					}
				}
				if (tries > 1000)
				{
					break;
				}

				IvyNode newNode = new IvyNode();
				newNode.position = newPos;
				newNode.forward = newPos - prevNode.position;
				newNode.toCollider = toCollider;

				nodes.Add(newNode);

				Debug.DrawLine(prevNode.position, newNode.position, Color.green);
			}

			CreateGeometry(ivyParent);

			Debug.Log("Stopped Growing at " + i.ToString());
		}

		public void CreateGeometry(GameObject parent)
		{
			// Coroutine doesnt work properly in edit mode
			// StartCoroutine("GenerateGeometryStepped");
			// return;

			// -------------------------------
			// Create leaves

			// Store leaf meshfilters for later combining
			MeshFilter[] meshFiltersLod0 = new MeshFilter[nodeCount];
			MeshFilter[] meshFiltersLod1 = new MeshFilter[nodeCount];

			int actualNodeCount = 0;

			GameObject leafTempParent = new GameObject("TempParent");
			leafTempParent.transform.parent = parent.transform;

			for (int i = 0; i < nodes.Count; i++)
			{
				Vector3 randomPos = new Vector3(UnityEngine.Random.Range(-leafRandomPositionChange, leafRandomPositionChange), 0f,
					UnityEngine.Random.Range(-leafRandomPositionChange, leafRandomPositionChange));
				Vector3 pos = nodes[i].position + randomPos;

				Quaternion leafRotation = new Quaternion();
				Vector3 forward;
				Vector3 upward;
				Vector3 leafRandomRotation;

				Vector3 toColliderUppified = nodes[i].toCollider.normalized;
				if (Vector3.Dot(Vector3.up, nodes[i].toCollider.normalized) < 0f)
				{
					toColliderUppified *= -1f;
				}
				if (Vector3.Dot(Vector3.up, toColliderUppified) > horizontalLimitFactor)
				{
					// collider is horizontal so leaves will follow branch forward mainly
					forward = Vector3.RotateTowards(-nodes[i].toCollider, -nodes[i].forward, Mathf.PI / 4f, 0f);
					upward = Vector3.RotateTowards(nodes[i].toCollider, -nodes[i].forward, Mathf.PI / 4f, 0f);
					leafRotation.SetLookRotation(forward, upward);

					leafRandomRotation = new Vector3(
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.x),
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.y),
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.z));

					leafRotation *= Quaternion.Euler(leafRandomRotation);
				}
				else
				{
					// Rotation follows collider
					// Align to collider and Turn 45 degrees up
					forward = Vector3.RotateTowards(-nodes[i].toCollider, Vector3.up, Mathf.PI / 4f, 0f);
					upward = Vector3.RotateTowards(nodes[i].toCollider, Vector3.up, Mathf.PI / 4f, 0f);
					leafRotation.SetLookRotation(forward, upward);

					leafRandomRotation = new Vector3(
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.x),
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.y),
						UnityEngine.Random.Range(0f, leafRandomRotationLimits.z));

					leafRotation *= Quaternion.Euler(leafRandomRotation);
				}

				GameObject newLeaf = Instantiate(leafPrefab, pos, leafRotation);
				// Leaf size random variance
				float scaleFactor = 1f + UnityEngine.Random.Range(0f, leafSizeVariance);
				// Leaf size by index in ivy
				scaleFactor += ((float) (nodes.Count - i) / nodes.Count * leafSizeByPosition);
				scaleFactor *= leafScale;
				// Apply scale
				newLeaf.transform.localScale *= scaleFactor;

				newLeaf.transform.parent = leafTempParent.transform;

				meshFiltersLod0[i] = newLeaf.transform.GetChild(0).GetChild(0).GetComponentInChildren<MeshFilter>();
				meshFiltersLod1[i] = newLeaf.transform.GetChild(0).GetChild(1).GetComponentInChildren<MeshFilter>();

				actualNodeCount++;

			}

			// -----------------------------
			// Combine leaf meshes

			// Store position and rotation and place ivy to origin while combined
			Quaternion origRot = parent.transform.rotation;
			Vector3 origPos = parent.transform.position;
			parent.transform.rotation = Quaternion.identity;
			parent.transform.position = Vector3.zero;

			// Calculate how many meshes to split the ivy into, limited by 16bit mesh vertex index
			int meshesPerMesh = Mathf.FloorToInt(65536f / (float) meshFiltersLod0[0].sharedMesh.vertexCount);
			int meshesNeeded = Mathf.CeilToInt((float)actualNodeCount / (float)meshesPerMesh);
			if (combinedMesh32BitEnabled)
			{
				meshesPerMesh = actualNodeCount;
				meshesNeeded = 1;
			}

			// Instantiate gameobjects to hold the meshes
			GameObject combinedLods = Instantiate(combinedMeshesPrefab, parent.transform);
			for (int m = 1; m < meshesNeeded; m++)
			{
				Instantiate(combinedLods.transform.GetChild(0).GetChild(0), combinedLods.transform.GetChild(0));
				Instantiate(combinedLods.transform.GetChild(1).GetChild(0), combinedLods.transform.GetChild(1));
			}

			Mesh[] combinedMeshLod0 = new Mesh[meshesNeeded];
			Mesh[] combinedMeshLod1 = new Mesh[meshesNeeded];

			CombineInstance[][] combinersLod0 = new CombineInstance[meshesNeeded][];
			CombineInstance[][] combinersLod1 = new CombineInstance[meshesNeeded][];

			for (int m = 0; m < meshesNeeded; m++)
			{
				combinedMeshLod0[m] = new Mesh();
				combinedMeshLod1[m] = new Mesh();

				if (combinedMesh32BitEnabled)
				{
					combinedMeshLod0[m].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
					combinedMeshLod1[m].indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
				}

				int currentMeshCount = 0;

				if (m == meshesNeeded - 1)
				{
					currentMeshCount = actualNodeCount - m * meshesPerMesh;
				}
				else
				{
					currentMeshCount = meshesPerMesh;
				}

				combinersLod0[m] = new CombineInstance[currentMeshCount];
				combinersLod1[m] = new CombineInstance[currentMeshCount];

				for (int a = 0; a < currentMeshCount; a++)
				{
					combinersLod0[m][a].subMeshIndex = 0;
					combinersLod0[m][a].mesh = meshFiltersLod0[m * meshesPerMesh + a].sharedMesh;
					combinersLod0[m][a].transform = meshFiltersLod0[m * meshesPerMesh + a].transform.localToWorldMatrix;
				}

				for (int a = 0; a < currentMeshCount; a++)
				{
					combinersLod1[m][a].subMeshIndex = 0;
					combinersLod1[m][a].mesh = meshFiltersLod1[m * meshesPerMesh + a].sharedMesh;
					combinersLod1[m][a].transform = meshFiltersLod1[m * meshesPerMesh + a].transform.localToWorldMatrix;
				}

				combinedMeshLod0[m].CombineMeshes(combinersLod0[m]);
				combinedMeshLod1[m].CombineMeshes(combinersLod1[m]);

				combinedLods.transform.GetChild(0).GetChild(m).GetComponent<MeshFilter>().sharedMesh = combinedMeshLod0[m];
				combinedLods.transform.GetChild(1).GetChild(m).GetComponent<MeshFilter>().sharedMesh = combinedMeshLod1[m];

			}
			// Recalculate lod with new mesh
			combinedLods.GetComponent<LODGroup>().RecalculateBounds();
			// Destroy individual leaves
			DestroyImmediate(leafTempParent);

			// Return ivy to original position and rotation
			parent.transform.rotation = origRot;
			parent.transform.position = origPos;

			// --------------------
			// Create branches

			int trianglesPerSection = verticesPerNode * 2;
			int trianglesPerEnd = (verticesPerNode - 2);
			Vector3[] vertices = new Vector3[nodes.Count * verticesPerNode];
			int[] triangles = new int[trianglesPerSection * 3 * (nodes.Count - 1) + trianglesPerEnd * 3 * 2];

			float vertexAngle = 360f / verticesPerNode;

			for (int i = 0; i < nodes.Count; i++)
			{
				// -------------------------------------
				// Vertices for branches
				// Get random thickness
				float randomThickness = 1f + UnityEngine.Random.Range(-0.5f, 0.5f) * branchThicknessVariance;
				// Calculate vector perpendicular to the nodes forward and set its magnitude
				Vector3 perp = GetPerpendicular(nodes[i].forward).normalized * branchThickness * randomThickness;

				// Calculate vertices by rotating perpendicular vector around node forward axis
				for (int v = 0; v < verticesPerNode; v++)
				{
					vertices[i * verticesPerNode + v] = nodes[i].position + Quaternion.AngleAxis(vertexAngle * v, nodes[i].forward) * perp;
				}

				// // Temp spheres to show nodes
				// GameObject branchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				// DestroyImmediate(branchObj.GetComponent<SphereCollider>());
				// branchObj.name = "Branch" + i.ToString();
				// branchObj.transform.position = nodes[i].position;
				// branchObj.transform.localScale = Vector3.one * 0.03f;
				// branchObj.transform.parent = leafParent.transform;
			}

			// triangles
			// starting end of branch
			int triangleIndex = 0;
			for (int i = 0; i < trianglesPerEnd; i++)
			{
				triangles[triangleIndex] = 0;
				triangles[triangleIndex + 1] = i + 1;
				triangles[triangleIndex + 2] = i + 2;
				triangleIndex += 3;
			}

			// sections
			for (int i = 0; i < nodes.Count - 1; i++)
			{
				for (int s = 0; s < verticesPerNode - 1; s++)
				{
					triangles[triangleIndex] = verticesPerNode * i + s + 0;
					triangles[triangleIndex + 1] = verticesPerNode * i + s + 1;
					triangles[triangleIndex + 2] = verticesPerNode * i + s + verticesPerNode;

					triangles[triangleIndex + 3] = verticesPerNode * i + s + 1;
					triangles[triangleIndex + 4] = verticesPerNode * i + s + verticesPerNode + 1;
					triangles[triangleIndex + 5] = verticesPerNode * i + s + verticesPerNode;
					triangleIndex += 6;
				}

				// section that loops around and connects to the top vertices of the section again
				triangles[triangleIndex] = verticesPerNode * i + verticesPerNode - 1 + 0;
				triangles[triangleIndex + 1] = verticesPerNode * i;
				triangles[triangleIndex + 2] = verticesPerNode * i + verticesPerNode - 1 + verticesPerNode;

				triangles[triangleIndex + 3] = verticesPerNode * i;
				triangles[triangleIndex + 4] = verticesPerNode * i + verticesPerNode;
				triangles[triangleIndex + 5] = verticesPerNode * i + verticesPerNode - 1 + verticesPerNode;
				triangleIndex += 6;
			}

			// end end of branch
			for (int i = 0; i < trianglesPerEnd; i++)
			{
				triangles[triangleIndex] = verticesPerNode * (nodes.Count - 1);
				triangles[triangleIndex + 1] = verticesPerNode * (nodes.Count - 1) + i + 1;
				triangles[triangleIndex + 2] = verticesPerNode * (nodes.Count - 1) + i + 2;
			}

			// -----------------------------
			// Create mesh
			Mesh branchMesh = new Mesh();
			branchMesh.Clear();
			branchMesh.vertices = vertices;
			branchMesh.triangles = triangles;
			branchMesh.RecalculateBounds();
			branchMesh.RecalculateNormals();

			GameObject branchObj = new GameObject("Branch");
			branchObj.AddComponent<MeshFilter>().sharedMesh = branchMesh;
			branchObj.AddComponent<MeshRenderer>().material = branchMaterial;
			branchObj.transform.parent = parent.transform;
#if UNITY_EDITOR
			GameObjectUtility.SetStaticEditorFlags(branchObj, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
				StaticEditorFlags.OccluderStatic | StaticEditorFlags.ReflectionProbeStatic);
#endif
			branchObj.GetComponent<MeshRenderer>().reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
			branchObj.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			branchObj.GetComponent<MeshRenderer>().motionVectorGenerationMode = MotionVectorGenerationMode.Camera;

			//branchObj.SetActive(false);

			// Save meshes to assets
#if UNITY_EDITOR
			AssetDatabase.CreateFolder("Assets/Models/GeneratedIvyMeshes", parent.name);
			for (int i = 0; i < combinedMeshLod0.Length && i < combinedMeshLod1.Length; i++)
			{
				AssetDatabase.CreateAsset(combinedMeshLod0[i], "Assets/Models/GeneratedIvyMeshes/" + parent.name + "/IvyLeavesMeshLod0_" + i.ToString() + ".asset");
				AssetDatabase.CreateAsset(combinedMeshLod1[i], "Assets/Models/GeneratedIvyMeshes/" + parent.name + "/IvyLeavesMeshLod1_" + i.ToString() + ".asset");
			}

			AssetDatabase.CreateAsset(branchMesh, "Assets/Models/GeneratedIvyMeshes/" + parent.name + "/IvyBranchMesh" + ".asset");
			AssetDatabase.SaveAssets();
#endif
		}

		string GetNextIvyName()
		{
			return "Ivy_" + Guid.NewGuid().ToString();
		}

		Vector3 GetPerpendicular(Vector3 v)
		{
			// float angle = UnityEngine.Random.Range(0, Mathf.PI * 2f);
			float angle = 0f;

			// Generate a uniformly-distributed unit vector in the XY plane.
			Vector3 inPlane = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

			// Rotate the vector into the plane perpendicular to v and return it.
			return Quaternion.LookRotation(v) * inPlane;
		}

		/// <summary>
		/// Checks if the potential branch collides with anything
		/// </summary>
		/// <param name="origPos"></param>
		/// <param name="newPos"></param>
		/// <returns></returns>
		private bool CheckBranchCollision(Vector3 origPos, Vector3 newPos)
		{
			Vector3 dir = newPos - origPos;
			float dist = dir.magnitude;
			if (Physics.Raycast(origPos, dir, dist))
			{
				return true;
			}
			return false;
		}

		/// <summary>
		/// Finds vector to closest collider by raycasting in loops around main axes
		/// </summary>
		/// <param name="pos"></param>
		/// <returns></returns>
		private Vector3 FindVectorToClosestCollider(Vector3 pos)
		{
			Vector3 dir = Vector3.left;
			float dist = maxDistanceToCollider;
			RaycastHit hit;

			Vector3 best = Vector3.zero;

			// Loop around y
			for (int i = 0; i < 36; i++)
			{
				dir = Quaternion.Euler(0f, i * (360f / 36f), 0f) * Vector3.left;
				if (Physics.Raycast(pos, dir, out hit, dist))
				{
					if (best == Vector3.zero)
					{
						best = hit.point - pos;
					}
					if ((hit.point - pos).magnitude < best.magnitude)
					{
						best = hit.point - pos;
					}
				}
			}

			// Loop around x
			for (int i = 0; i < 36; i++)
			{
				dir = Quaternion.Euler(i * (360f / 36f), 0f, 0f) * Vector3.forward;
				if (Physics.Raycast(pos, dir, out hit, dist))
				{
					if (best == Vector3.zero)
					{
						best = hit.point - pos;
					}
					if ((hit.point - pos).magnitude < best.magnitude)
					{
						best = hit.point - pos;
					}
				}
			}

			// Loop around z
			for (int i = 0; i < 36; i++)
			{
				dir = Quaternion.Euler(0f, 0f, i * (360f / 36f)) * Vector3.left;
				if (Physics.Raycast(pos, dir, out hit, dist))
				{
					if (best == Vector3.zero)
					{
						best = hit.point - pos;
					}
					if ((hit.point - pos).magnitude < best.magnitude)
					{
						best = hit.point - pos;
					}
				}
			}

			return best;
		}

		// IEnumerator GenerateGeometryStepped()
		// {
		// 	for (int i = 0; i < nodes.Count; i++)
		// 	{
		// 		Vector3 randomPos = new Vector3(UnityEngine.Random.Range(-leafRandomPositionChange, leafRandomPositionChange), 0f,
		// 			UnityEngine.Random.Range(-leafRandomPositionChange, leafRandomPositionChange));
		// 		Vector3 pos = nodes[i].position + randomPos;

		// 		Quaternion leafRotation = new Quaternion();
		// 		// Rotation follows collider

		// 		// Align to collider and Turn 45 degrees up
		// 		Vector3 forward = Vector3.RotateTowards(-nodes[i].toCollider, Vector3.up, Mathf.PI / 4f, 0f);
		// 		Vector3 upward = Vector3.RotateTowards(nodes[i].toCollider, Vector3.up, Mathf.PI / 4f, 0f);
		// 		leafRotation.SetLookRotation(forward, upward);

		// 		Vector3 leafRandomRotation = new Vector3(
		// 			UnityEngine.Random.Range(0f, leafRandomRotationLimits.x),
		// 			UnityEngine.Random.Range(0f, leafRandomRotationLimits.y),
		// 			UnityEngine.Random.Range(0f, leafRandomRotationLimits.z));

		// 		leafRotation *= Quaternion.Euler(leafRandomRotation);

		// 		GameObject newLeaf = Instantiate(leafPrefab, pos, leafRotation);
		// 		// Leaf size random variance
		// 		float scaleFactor = 1f + UnityEngine.Random.Range(0f, leafSizeVariance);
		// 		// Leaf size by index in ivy
		// 		scaleFactor += ((float) (nodes.Count - i) / nodes.Count * leafSizeByPosition);
		// 		// Apply scale
		// 		newLeaf.transform.localScale *= scaleFactor;

		// 		newLeaf.transform.parent = leafParent.transform;

		// 		GameObject branchObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		// 		DestroyImmediate(branchObj.GetComponent<SphereCollider>());
		// 		branchObj.name = "Branch" + i.ToString();
		// 		branchObj.transform.position = nodes[i].position;
		// 		branchObj.transform.localScale = Vector3.one * 0.03f;
		// 		branchObj.transform.parent = leafParent.transform;

		// 		yield return new WaitForSecondsRealtime(0.05f);
		// 	}
		// }
	}
}