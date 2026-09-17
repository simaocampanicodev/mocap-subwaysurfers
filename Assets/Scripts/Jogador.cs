using UnityEngine;
using UnityEngine.InputSystem;

public class Jogador : MonoBehaviour
{
    public ObstacleSpawner jogo;
    public Transform pista;
    public Animator animator;

    public float larguraLane = 1.2f;
    public float suavidade = 0.25f;
    public float transicao = 0.15f;

    [Header("Ajuda no salto (a gravação sozinha salta pouco)")]
    public float alturaExtraSalto = 0.25f;
    public float inicioSalto = 0.5f;
    public float duracaoSalto = 1.1f;

    public string estadoIdle = "idle";
    public string estadoEsquerda = "esquerda";
    public string estadoDireita = "direita";
    public string estadoSalto = "salto";
    public string estadoAgachar = "agachar";

    int lane = 1;
    float x, xAlvo, velX;
    float alturaLocal, zLocal;
    float saltouEm = -99f;

    void Start()
    {
        if (pista == null || animator == null)
        {
            Debug.LogError("[Subway] O Jogador precisa da Pista e do Animator. Usa o menu Subway > Montar jogo.", this);
            enabled = false;
            return;
        }

        Vector3 local = pista.InverseTransformPoint(transform.position);
        alturaLocal = local.y;
        zLocal = local.z;
        x = xAlvo = 0f;
        lane = 1;
    }

    void Update()
    {
        Keyboard t = Keyboard.current;
        bool parado = jogo != null && jogo.perdeu;

        if (t != null && !parado)
        {
            if (t.aKey.wasPressedThisFrame || t.leftArrowKey.wasPressedThisFrame) Mudar(-1);
            if (t.dKey.wasPressedThisFrame || t.rightArrowKey.wasPressedThisFrame) Mudar(1);
            if (t.wKey.wasPressedThisFrame || t.upArrowKey.wasPressedThisFrame || t.spaceKey.wasPressedThisFrame) Saltar();
            if (t.sKey.wasPressedThisFrame || t.downArrowKey.wasPressedThisFrame) Tocar(estadoAgachar);
        }

        if (t != null && parado && t.rKey.wasPressedThisFrame && jogo != null) jogo.Recomecar();

        x = Mathf.SmoothDamp(x, xAlvo, ref velX, suavidade);
        transform.position = pista.TransformPoint(new Vector3(x, alturaLocal + AlturaDoSalto(), zLocal));
    }

    void Saltar()
    {
        saltouEm = Time.time;
        Tocar(estadoSalto);
    }

    // sobe o corpo todo por cima da animação, para dar altura suficiente ao salto
    float AlturaDoSalto()
    {
        float u = Time.time - saltouEm - inicioSalto;
        if (u <= 0f || u >= duracaoSalto || alturaExtraSalto <= 0f) return 0f;
        return alturaExtraSalto * Mathf.Sin(Mathf.PI * u / duracaoSalto);
    }

    void Mudar(int lado)
    {
        int nova = Mathf.Clamp(lane + lado, 0, 2);
        if (nova == lane) return;

        lane = nova;
        xAlvo = (lane - 1) * larguraLane;
        Tocar(lado < 0 ? estadoEsquerda : estadoDireita);
    }

    void Tocar(string estado)
    {
        if (animator != null) animator.CrossFadeInFixedTime(estado, transicao, 0, 0f);
    }

    public void Reiniciar()
    {
        lane = 1;
        x = xAlvo = 0f;
        velX = 0f;
        saltouEm = -99f;
        if (animator != null)
        {
            animator.speed = 1f;
            Tocar(estadoIdle);
        }
    }
}
