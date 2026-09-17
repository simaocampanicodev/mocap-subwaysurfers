using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObstacleSpawner : MonoBehaviour
{
    public static ObstacleSpawner Instancia;

    public Jogador jogador;

    [Header("Pista")]
    public float larguraLane = 1.2f;
    public float velocidade = 6f;
    public float distanciaSpawn = 26f;
    public float distanciaFim = -4f;

    [Header("Obstáculos")]
    public float alturaSaltar = 0.15f;    // altura da barra baixa
    public float alturaAgachar = 1.1f;    // onde começa a barra de cima

    [Header("Ritmo")]
    public float esperaInicial = 2f;
    public float intervalo = 2.2f;
    [Range(0f, 1f)] public float hipoteseSegundoObstaculo = 0.3f;

    [Header("Textos")]
    public TMP_Text textoPontos;
    public TMP_Text textoMensagem;

    [Header("Testes")]
    public bool invencivel = false;

    public int pontos;
    public bool perdeu;

    const string Controlos = "A / D = mudar de linha     W = saltar     S = agachar";

    readonly List<Obstaculo> ativos = new List<Obstaculo>();
    float proxima;
    string ultimaParte;
    int pontosMostrados = -1;
    bool perdeuMostrado;

    void Awake() => Instancia = this;

    void Start()
    {
        if (textoPontos == null || textoMensagem == null) CriarUI();
        proxima = Time.time + esperaInicial;
    }

    void Update()
    {
        if (pontos != pontosMostrados || perdeu != perdeuMostrado)
        {
            pontosMostrados = pontos;
            perdeuMostrado = perdeu;
            AtualizarUI();
        }

        if (perdeu) return;

        if (Time.time >= proxima)
        {
            CriarLinha();
            proxima = Time.time + intervalo;
        }

        float passo = velocidade * Time.deltaTime;
        for (int i = ativos.Count - 1; i >= 0; i--)
        {
            Obstaculo o = ativos[i];
            if (o == null) { ativos.RemoveAt(i); continue; }

            o.transform.localPosition += Vector3.back * passo;
            float z = o.transform.localPosition.z;

            if (!o.contado && z < -1f)
            {
                o.contado = true;
                pontos++;
            }
            if (z < distanciaFim)
            {
                ativos.RemoveAt(i);
                Destroy(o.gameObject);
            }
        }
    }

    void CriarLinha()
    {
        int livre = Random.Range(0, 3);
        int lane = Random.Range(0, 3);
        while (lane == livre) lane = Random.Range(0, 3);

        Criar(lane, TipoAoAcaso());

        if (Random.value < hipoteseSegundoObstaculo)
        {
            int outra = 3 - livre - lane;
            Criar(outra, TipoAoAcaso());
        }
    }

    static string TipoAoAcaso()
    {
        float v = Random.value;
        if (v < 0.34f) return "saltar";
        if (v < 0.67f) return "agachar";
        return "bloco";
    }

    void Criar(int lane, string tipo)
    {
        GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubo.name = tipo;
        cubo.transform.SetParent(transform, false);

        Vector3 tamanho;
        Color cor;
        float alturaBase;
        if (tipo == "saltar")
        {
            tamanho = new Vector3(larguraLane * 0.8f, alturaSaltar, 0.4f);
            cor = Color.yellow;
            alturaBase = alturaSaltar / 2f;
        }
        else if (tipo == "agachar")
        {
            float alto = Mathf.Max(0.2f, 2.2f - alturaAgachar);
            tamanho = new Vector3(larguraLane * 0.8f, alto, 0.4f);
            cor = Color.cyan;
            alturaBase = alturaAgachar + alto / 2f;
        }
        else
        {
            tamanho = new Vector3(larguraLane * 0.8f, 2f, 0.6f);
            cor = Color.red;
            alturaBase = 1f;
        }

        cubo.transform.localScale = tamanho;
        cubo.transform.localPosition = new Vector3((lane - 1) * larguraLane, alturaBase, distanciaSpawn);
        cubo.GetComponent<Renderer>().material.color = cor;

        Rigidbody rb = cubo.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Obstaculo o = cubo.AddComponent<Obstaculo>();
        o.tipo = tipo;
        ativos.Add(o);
    }

    public void Bateu(Obstaculo obstaculo, string parte)
    {
        if (perdeu || obstaculo.bateu) return;
        obstaculo.bateu = true;

        if (invencivel)
        {
            Debug.Log($"[Subway] {parte} bateu no {obstaculo.tipo}", obstaculo);
            return;
        }

        perdeu = true;
        ultimaParte = $"{parte} no {obstaculo.tipo}";
    }

    public void Recomecar()
    {
        foreach (Obstaculo o in ativos)
            if (o != null) Destroy(o.gameObject);
        ativos.Clear();

        pontos = 0;
        perdeu = false;
        ultimaParte = null;
        proxima = Time.time + esperaInicial;
        if (jogador != null) jogador.Reiniciar();
    }

    void AtualizarUI()
    {
        if (textoPontos != null) textoPontos.text = "Pontos: " + pontos;
        if (textoMensagem == null) return;
        textoMensagem.text = perdeu ? $"PERDESTE ({ultimaParte})     R para recomeçar" : Controlos;
        textoMensagem.color = perdeu ? Color.red : Color.white;
    }

    void CriarUI()
    {
        var canvasGo = new GameObject("UI do Jogo", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler escala = canvasGo.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);

        if (textoPontos == null)
        {
            textoPontos = CriarTexto(canvasGo.transform, "Pontos", 46, TextAlignmentOptions.TopLeft);
            RectTransform rt = textoPontos.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(40, -30);
            rt.sizeDelta = new Vector2(800, 90);
        }

        if (textoMensagem == null)
        {
            textoMensagem = CriarTexto(canvasGo.transform, "Mensagem", 34, TextAlignmentOptions.Bottom);
            RectTransform rt = textoMensagem.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(40, 30);
            rt.offsetMax = new Vector2(-40, 130);
        }

        if (textoPontos.font == null)
            Debug.LogError("[Subway] Falta importar o TextMeshPro: menu Window > TextMeshPro > Import TMP Essential Resources.", this);
    }

    static TMP_Text CriarTexto(Transform pai, string nome, float tamanho, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        TMP_Text texto = go.AddComponent<TextMeshProUGUI>();
        texto.fontSize = tamanho;
        texto.alignment = alinhamento;
        texto.color = Color.white;
        texto.raycastTarget = false;
        return texto;
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;
        for (int lane = 0; lane < 3; lane++)
        {
            float x = (lane - 1) * larguraLane;
            Gizmos.DrawLine(new Vector3(x, 0, distanciaFim), new Vector3(x, 0, distanciaSpawn));
        }
    }
}
