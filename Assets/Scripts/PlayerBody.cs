using System.Collections.Generic;
using UnityEngine;

public class PlayerBody : MonoBehaviour
{
    [System.Serializable]
    public class Segmento
    {
        public string osso;
        public string ossoFim;
        public float raio;

        public Segmento(string osso, string ossoFim, float raio)
        {
            this.osso = osso;
            this.ossoFim = ossoFim;
            this.raio = raio;
        }
    }

    [Range(0.3f, 2f)] public float multiplicadorRaio = 1f;

    public List<Segmento> segmentos = new List<Segmento>
    {
        new Segmento("Hips", "Spine", 0.11f),
        new Segmento("Spine", "Spine1", 0.12f),
        new Segmento("Spine1", "Spine2", 0.12f),
        new Segmento("Spine2", "Spine3", 0.12f),
        new Segmento("Spine3", "Neck", 0.11f),
        new Segmento("Neck", "Head", 0.05f),
        new Segmento("Head", "HeadEnd", 0.10f),
        new Segmento("LeftShoulder", "LeftArm", 0.045f),
        new Segmento("LeftArm", "LeftForeArm", 0.045f),
        new Segmento("LeftForeArm", "LeftHand", 0.04f),
        new Segmento("LeftHand", "LeftHandMiddle1", 0.04f),
        new Segmento("RightShoulder", "RightArm", 0.045f),
        new Segmento("RightArm", "RightForeArm", 0.045f),
        new Segmento("RightForeArm", "RightHand", 0.04f),
        new Segmento("RightHand", "RightHandMiddle1", 0.04f),
        new Segmento("Hips", "LeftUpLeg", 0.08f),
        new Segmento("LeftUpLeg", "LeftLeg", 0.07f),
        new Segmento("LeftLeg", "LeftFoot", 0.055f),
        new Segmento("LeftFoot", "LeftToeBase", 0.045f),
        new Segmento("Hips", "RightUpLeg", 0.08f),
        new Segmento("RightUpLeg", "RightLeg", 0.07f),
        new Segmento("RightLeg", "RightFoot", 0.055f),
        new Segmento("RightFoot", "RightToeBase", 0.045f),
    };

    readonly List<CapsuleCollider> criadas = new List<CapsuleCollider>();

    void Awake()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        foreach (Segmento seg in segmentos)
        {
            Transform a = Procurar(transform, seg.osso);
            Transform b = Procurar(transform, seg.ossoFim);
            if (a == null || b == null)
            {
                Debug.LogWarning($"[MocapWall] osso não encontrado: {(a == null ? seg.osso : seg.ossoFim)}", this);
                continue;
            }

            Vector3 local = a.InverseTransformPoint(b.position);
            if (local.sqrMagnitude < 1e-8f) continue;

            GameObject go = new GameObject(seg.osso);
            go.transform.SetParent(a, false);
            go.transform.localPosition = local * 0.5f;
            Vector3 dir = local.normalized;
            go.transform.localRotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up);

            float escala = Mathf.Max(0.0001f, Mathf.Abs(a.lossyScale.x));
            float raio = seg.raio * multiplicadorRaio / escala;

            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.direction = 2;
            cap.radius = raio;
            cap.height = local.magnitude + 2f * raio;
            go.AddComponent<BodyPartHitbox>();
            criadas.Add(cap);
        }
    }

    static Transform Procurar(Transform raiz, string nome)
    {
        if (raiz.name == nome) return raiz;
        foreach (Transform filho in raiz)
        {
            Transform r = Procurar(filho, nome);
            if (r != null) return r;
        }
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        foreach (CapsuleCollider c in criadas)
        {
            if (c == null) continue;
            float meio = Mathf.Max(0f, c.height * 0.5f - c.radius);
            float raio = c.radius * Mathf.Abs(c.transform.lossyScale.x);
            Gizmos.DrawWireSphere(c.transform.TransformPoint(new Vector3(0, 0, -meio)), raio);
            Gizmos.DrawWireSphere(c.transform.TransformPoint(new Vector3(0, 0, meio)), raio);
        }
    }
}
