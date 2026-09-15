using UnityEngine;

[RequireComponent(typeof(SkinnedMeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Bake : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedMesh;
    private MeshCollider meshCollider;
    private Mesh bakedMesh;

    void Start()
    {
        skinnedMesh = GetComponent<SkinnedMeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();

        bakedMesh = new Mesh();
    }

    void FixedUpdate()
    {
        skinnedMesh.BakeMesh(bakedMesh);

        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = bakedMesh;
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision with: " + collision.gameObject.name);
    }
}