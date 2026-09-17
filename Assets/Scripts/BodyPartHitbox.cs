using UnityEngine;

public class BodyPartHitbox : MonoBehaviour
{
    void OnTriggerEnter(Collider outro)
    {
        Obstaculo obstaculo = outro.GetComponentInParent<Obstaculo>();
        if (obstaculo != null && ObstacleSpawner.Instancia != null)
            ObstacleSpawner.Instancia.Bateu(obstaculo, name);
    }
}
