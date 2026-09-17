using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public enum ObstacleType { None, Train, Jump, Duck }

    [Header("Prefabs (vazio = cubos)")]
    public GameObject[] trainPrefabs;
    public GameObject[] jumpPrefabs;
    public GameObject[] duckPrefabs;

    [Header("Lanes")]
    [Min(2)] public int laneCount = 3;
    public float laneWidth = 1f;
    public float groundY = 0f;

    [Header("Velocidade")]
    public float startSpeed = 6f;
    public float maxSpeed = 14f;
    [Min(1)] public float secondsToMaxSpeed = 120f;
    public bool playOnStart = true;

    [Header("Tempo entre linhas (segundos)")]
    public float minReactionTime = 1.2f;
    public float extraRandomTime = 0.8f;
    [Range(0, 1)] public float breatherChance = 0.12f;
    public float breatherTime = 1.5f;

    [Header("Distâncias")]
    public float spawnDistance = 60f;
    public float startEmptyDistance = 30f;
    public float despawnBehind = 5f;

    [Header("Obstáculos")]
    [Range(0, 1)] public float minObstacleChance = 0.35f;
    [Range(0, 1)] public float maxObstacleChance = 0.75f;
    [Range(0, 1)] public float trainChance = 0.5f;
    [Range(0, 1)] public float safeLaneObstacleChance = 0.3f;
    [Range(0, 1)] public float safeLaneMoveChance = 0.4f;
    public float minTrainLength = 6f;
    public float maxTrainLength = 16f;
    public float gapAfterTrain = 3f;

    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; private set; }

    private readonly List<(Transform obj, float length)> obstacles = new List<(Transform, float)>();
    private float[] laneFreeAt;   // distância a partir da qual cada lane fica sem comboio
    private int safeLane;         // lane que nunca recebe comboio
    private float lastRowDistance;
    private float nextRowDistance;
    private float distance;
    private float time;

    void Start()
    {
        ResetRun();
        IsRunning = playOnStart;
    }

    public void StartRun() => IsRunning = true;
    public void StopRun() => IsRunning = false;

    public void ResetRun()
    {
        foreach (var o in obstacles)
            if (o.obj != null) Destroy(o.obj.gameObject);
        obstacles.Clear();

        laneFreeAt = new float[laneCount];
        safeLane = laneCount / 2;
        lastRowDistance = 0f;
        nextRowDistance = startEmptyDistance;
        distance = 0f;
        time = 0f;
        CurrentSpeed = startSpeed;
    }

    void Update()
    {
        if (!IsRunning) return;

        time += Time.deltaTime;
        CurrentSpeed = SpeedAt(time);
        float step = CurrentSpeed * Time.deltaTime;
        distance += step;

        MoveObstacles(step);

        while (nextRowDistance <= distance + spawnDistance)
        {
            SpawnRow(nextRowDistance);
            nextRowDistance += NextGap();
        }
    }

    float Difficulty(float t) => Mathf.Clamp01(t / secondsToMaxSpeed);
    float SpeedAt(float t) => Mathf.Lerp(startSpeed, maxSpeed, Difficulty(t));
    float LaneX(int lane) => (lane - (laneCount - 1) / 2f) * laneWidth;

    void MoveObstacles(float step)
    {
        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            var (obj, length) = obstacles[i];
            if (obj == null) { obstacles.RemoveAt(i); continue; }

            obj.localPosition += Vector3.back * step;
            if (obj.localPosition.z + length < -despawnBehind)
            {
                Destroy(obj.gameObject);
                obstacles.RemoveAt(i);
            }
        }
    }

    float NextGap()
    {
        float seconds = minReactionTime + Random.value * extraRandomTime;
        if (Random.value < breatherChance) seconds += breatherTime;

        float arrival = time + spawnDistance / CurrentSpeed;
        return Mathf.Max(1f, seconds * SpeedAt(arrival));
    }

    void SpawnRow(float rowDistance)
    {
        ObstacleType[] row = PickRow(rowDistance);
        float z = rowDistance - distance;

        for (int lane = 0; lane < laneCount; lane++)
        {
            if (row[lane] == ObstacleType.None) continue;

            float length = Spawn(row[lane], lane, z);
            if (row[lane] == ObstacleType.Train)
                laneFreeAt[lane] = rowDistance + length + gapAfterTrain;
        }
        lastRowDistance = rowDistance;
    }
    
    // garante um caminho sem comboio
    ObstacleType[] PickRow(float rowDistance)
    {
        if (Random.value < safeLaneMoveChance)
        {
            int side = Random.value < 0.5f ? -1 : 1;
            int lane = safeLane + side;
            if (lane < 0 || lane >= laneCount) lane = safeLane - side;
            if (laneFreeAt[lane] <= lastRowDistance) safeLane = lane;
        }

        float obstacleChance = Mathf.Lerp(minObstacleChance, maxObstacleChance, Difficulty(time));
        var row = new ObstacleType[laneCount];

        for (int lane = 0; lane < laneCount; lane++)
        {
            if (lane == safeLane)
                row[lane] = Random.value < safeLaneObstacleChance ? JumpOrDuck() : ObstacleType.None;
            else if (laneFreeAt[lane] > rowDistance || Random.value > obstacleChance)
                row[lane] = ObstacleType.None;
            else
                row[lane] = Random.value < trainChance ? ObstacleType.Train : JumpOrDuck();
        }
        return row;
    }

    static ObstacleType JumpOrDuck() => Random.value < 0.5f ? ObstacleType.Jump : ObstacleType.Duck;

    float Spawn(ObstacleType type, int lane, float z)
    {
        GameObject prefab = RandomPrefab(type);
        GameObject obj = prefab != null ? Instantiate(prefab, transform) : CreateCube(type);
        obj.name = type.ToString();
        obj.transform.localPosition += new Vector3(LaneX(lane), groundY, 0f);

        float length = PlaceFront(obj.transform, z);
        MakeTrigger(obj);
        obstacles.Add((obj.transform, length));
        return length;
    }

    float PlaceFront(Transform obj, float z)
    {
        Bounds b = GetBounds(obj.gameObject);
        float front = transform.InverseTransformPoint(b.min).z;
        float back = transform.InverseTransformPoint(b.max).z;
        if (front > back) (front, back) = (back, front);

        obj.localPosition += Vector3.forward * (z - front);
        return back - front;
    }

    static Bounds GetBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one);

        Bounds b = renderers[0].bounds;
        foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // detetar colisões
    static void MakeTrigger(GameObject obj)
    {
        foreach (Collider col in obj.GetComponentsInChildren<Collider>())
        {
            if (col is MeshCollider mesh) mesh.convex = true;
            col.isTrigger = true;
        }

        if (!obj.TryGetComponent(out Rigidbody rb)) rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    GameObject RandomPrefab(ObstacleType type)
    {
        GameObject[] list = type == ObstacleType.Train ? trainPrefabs
                          : type == ObstacleType.Jump ? jumpPrefabs
                          : duckPrefabs;
        return list != null && list.Length > 0 ? list[Random.Range(0, list.Length)] : null;
    }

    GameObject CreateCube(ObstacleType type)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.SetParent(transform, false);

        Vector3 size;
        Color color;
        float lift = 0f;
        switch (type)
        {
            case ObstacleType.Train:
                size = new Vector3(0.85f, 3f, Random.Range(minTrainLength, maxTrainLength));
                color = Color.red;
                break;
            case ObstacleType.Jump:
                size = new Vector3(0.85f, 0.4f, 0.4f);
                color = Color.yellow;
                break;
            default:
                size = new Vector3(0.85f, 1.2f, 0.4f);
                color = Color.blue;
                lift = 1.3f;
                break;
        }

        size.x *= laneWidth;
        cube.transform.localScale = size;
        cube.transform.localPosition = Vector3.up * (size.y / 2f + lift);
        cube.GetComponent<Renderer>().material.color = color;
        return cube;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        for (int lane = 0; lane < laneCount; lane++)
        {
            float x = LaneX(lane);
            Gizmos.DrawLine(new Vector3(x, groundY, -despawnBehind), new Vector3(x, groundY, spawnDistance));
        }
    }
}
