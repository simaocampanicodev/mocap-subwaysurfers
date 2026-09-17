using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Conversão de uma animação gravada num esqueleto para outro esqueleto diferente.
// Usado pelo menu Subway: as gravações Vicon e o boneco não têm os ossos montados
// da mesma maneira (eixos e pose de repouso diferentes).

public class Osso
{
    public string nome;
    public Transform t;
    public Osso pai;
    public string caminho;
    public Quaternion restLocal;
    public Vector3 restLocalPos;
    public Quaternion restWorld;    // em relação à raiz do modelo
    public Vector3 restWorldPos;

    public bool DescendeDe(Osso outro)
    {
        for (Osso p = pai; p != null; p = p.pai)
            if (p == outro) return true;
        return false;
    }
}

public class Esqueleto
{
    public Transform raiz;
    public Osso Hips;
    public List<Osso> Ossos = new List<Osso>();
    public Dictionary<string, Osso> PorNome = new Dictionary<string, Osso>();
    public Quaternion hipsPaiRestWorld = Quaternion.identity;
    public Vector3 hipsPaiRestWorldPos = Vector3.zero;

    public static Esqueleto De(Transform raiz)
    {
        Transform hips = raiz.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => SubwaySetup.Limpo(t.name) == "Hips" && TemDescendente(t, "Neck1"));
        if (hips == null) return null;

        var e = new Esqueleto { raiz = raiz };
        e.Hips = e.Juntar(hips, null);
        e.Adicionar(hips, e.Hips);

        Transform pai = hips.parent;
        if (pai != null)
        {
            e.hipsPaiRestWorld = Quaternion.Inverse(raiz.rotation) * pai.rotation;
            e.hipsPaiRestWorldPos = raiz.InverseTransformPoint(pai.position);
        }
        return e;
    }

    void Adicionar(Transform t, Osso osso)
    {
        foreach (Transform filho in t)
        {
            Osso o = Juntar(filho, osso);
            Adicionar(filho, o);
        }
    }

    Osso Juntar(Transform t, Osso pai)
    {
        var o = new Osso
        {
            nome = SubwaySetup.Limpo(t.name),
            t = t,
            pai = pai,
            caminho = AnimationUtility.CalculateTransformPath(t, raiz),
            restLocal = t.localRotation,
            restLocalPos = t.localPosition,
            restWorld = Quaternion.Inverse(raiz.rotation) * t.rotation,
            restWorldPos = raiz.InverseTransformPoint(t.position),
        };
        Ossos.Add(o);
        if (!PorNome.ContainsKey(o.nome)) PorNome[o.nome] = o;
        return o;
    }

    public Osso PorCaminho(string caminho) => Ossos.FirstOrDefault(o => o.caminho == caminho);

    static bool TemDescendente(Transform t, string nome)
    {
        foreach (Transform filho in t.GetComponentsInChildren<Transform>(true))
            if (filho != t && SubwaySetup.Limpo(filho.name) == nome) return true;
        return false;
    }
}

public class Conversor
{
    readonly Esqueleto fonte, alvo;
    readonly Quaternion W;
    readonly Dictionary<string, Quaternion> delta = new Dictionary<string, Quaternion>();
    readonly float escala;
    readonly Vector3 cimaLocal;

    public Conversor(Esqueleto fonte, Esqueleto alvo)
    {
        this.fonte = fonte;
        this.alvo = alvo;

        // 1. alinhar os dois corpos no espaço (um pode ter a coluna em Y e o outro em Z)
        Quaternion baseF = Base(fonte);
        Quaternion baseA = Base(alvo);
        W = baseA * Quaternion.Inverse(baseF);

        // 2. pôr o repouso do alvo na mesma pose do repouso da gravação (T-pose)
        var P = alvo.Ossos.ToDictionary(o => o, o => o.restWorldPos);
        var Q = alvo.Ossos.ToDictionary(o => o, o => o.restWorld);
        foreach (Osso o in alvo.Ossos)
        {
            if (!fonte.PorNome.TryGetValue(o.nome, out Osso of)) continue;
            Osso filho = FilhoComum(o);
            if (filho == null) continue;
            Vector3 dA = P[filho] - P[o];
            Vector3 dF = W * (fonte.PorNome[filho.nome].restWorldPos - of.restWorldPos);
            if (dA.sqrMagnitude < 1e-8f || dF.sqrMagnitude < 1e-8f) continue;

            Quaternion R = Quaternion.FromToRotation(dA, dF);
            Vector3 centro = P[o];
            foreach (Osso j in alvo.Ossos)
                if (j == o || j.DescendeDe(o))
                {
                    P[j] = centro + R * (P[j] - centro);
                    Q[j] = R * Q[j];
                }
        }

        // 3. diferença entre cada osso da gravação e o mesmo osso do boneco
        foreach (Osso o in alvo.Ossos)
            if (fonte.PorNome.TryGetValue(o.nome, out Osso of))
                delta[o.nome] = Quaternion.Inverse(W * of.restWorld) * Q[o];

        // 4. tamanhos diferentes: a altura da anca acompanha o comprimento da perna
        float pernaF = Comprimento(fonte, "Hips", "LeftFoot");
        float pernaA = Comprimento(alvo, "Hips", "LeftFoot");
        escala = pernaF > 0.0001f ? pernaA / pernaF : 1f;

        Vector3 cima = alvo.PorNome.TryGetValue("Head", out Osso cabeca)
            ? (cabeca.restWorldPos - alvo.Hips.restWorldPos).normalized
            : Vector3.up;
        cimaLocal = (Quaternion.Inverse(alvo.hipsPaiRestWorld) * cima).normalized;
    }

    static float Comprimento(Esqueleto e, string a, string b)
    {
        return e.PorNome.ContainsKey(a) && e.PorNome.ContainsKey(b)
            ? Vector3.Distance(e.PorNome[a].restWorldPos, e.PorNome[b].restWorldPos) : 0f;
    }

    public static string OssoEmFalta(Esqueleto e)
    {
        foreach (string n in new[] { "Head", "LeftUpLeg", "RightUpLeg", "LeftFoot" })
            if (!e.PorNome.ContainsKey(n)) return n;
        return null;
    }

    static Quaternion Base(Esqueleto e)
    {
        Vector3 hips = e.Hips.restWorldPos;
        Vector3 cima = (e.PorNome["Head"].restWorldPos - hips).normalized;
        Vector3 lado = (e.PorNome["RightUpLeg"].restWorldPos - e.PorNome["LeftUpLeg"].restWorldPos).normalized;
        Vector3 frente = Vector3.Cross(lado, cima).normalized;
        return Quaternion.LookRotation(frente, cima);
    }

    Osso FilhoComum(Osso o)
    {
        var fila = new Queue<Osso>(alvo.Ossos.Where(x => x.pai == o));
        while (fila.Count > 0)
        {
            Osso c = fila.Dequeue();
            if (fonte.PorNome.ContainsKey(c.nome)) return c;
            foreach (Osso n in alvo.Ossos.Where(x => x.pai == c)) fila.Enqueue(n);
        }
        return null;
    }

    public void Converter(Dictionary<string, AnimationCurve[]> curvas, float t,
                          Dictionary<string, Quaternion[]> saidaRot, Vector3[] saidaPos, int frame)
    {
        // pose da gravação
        var qFonte = new Dictionary<string, Quaternion>();
        foreach (Osso o in fonte.Ossos)
        {
            Quaternion local = o.restLocal;
            if (curvas.TryGetValue(o.nome, out AnimationCurve[] c) && c[0] != null)
            {
                var q = new Quaternion(c[0].Evaluate(t), c[1].Evaluate(t), c[2].Evaluate(t), c[3].Evaluate(t));
                if (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w > 0.0001f) local = q.normalized;
            }
            Quaternion qPai = o.pai != null && qFonte.ContainsKey(o.pai.nome) ? qFonte[o.pai.nome] : fonte.hipsPaiRestWorld;
            qFonte[o.nome] = qPai * local;
        }

        // mesma pose no boneco
        var qAlvo = new Dictionary<string, Quaternion>();
        foreach (Osso o in alvo.Ossos)
        {
            Quaternion q;
            if (delta.ContainsKey(o.nome) && qFonte.ContainsKey(o.nome))
                q = W * qFonte[o.nome] * delta[o.nome];
            else if (o.pai != null && qAlvo.ContainsKey(o.pai.nome))
                q = qAlvo[o.pai.nome] * o.restLocal;
            else
                q = o.restWorld;
            qAlvo[o.nome] = q;

            Quaternion qPai = o.pai != null && qAlvo.ContainsKey(o.pai.nome) ? qAlvo[o.pai.nome] : alvo.hipsPaiRestWorld;
            Quaternion local = Quaternion.Inverse(qPai) * q;
            if (frame > 0)
            {
                Quaternion ant = saidaRot[o.nome][frame - 1];
                if (local.x * ant.x + local.y * ant.y + local.z * ant.z + local.w * ant.w < 0f)
                    local = new Quaternion(-local.x, -local.y, -local.z, -local.w);
            }
            saidaRot[o.nome][frame] = local;
        }

        // altura da anca
        Vector3 pFonte = fonte.Hips.restWorldPos;
        if (curvas.TryGetValue(fonte.Hips.nome, out AnimationCurve[] ch) && ch[4] != null)
        {
            var localPos = new Vector3(ch[4].Evaluate(t), ch[5].Evaluate(t), ch[6].Evaluate(t));
            pFonte = fonte.hipsPaiRestWorldPos + fonte.hipsPaiRestWorld * localPos;
        }
        Vector3 pAlvo = W * (pFonte * escala);
        saidaPos[frame] = Quaternion.Inverse(alvo.hipsPaiRestWorld) * (pAlvo - alvo.hipsPaiRestWorldPos);
    }

    // O ator andou mesmo para o lado; no jogo quem muda de linha é o código.
    public void TirarDeslocamentoLateral(Vector3[] posicoes)
    {
        Vector3 baseP = alvo.Hips.restLocalPos;
        for (int i = 0; i < posicoes.Length; i++)
            posicoes[i] = baseP + cimaLocal * Vector3.Dot(posicoes[i] - baseP, cimaLocal);
    }
}
