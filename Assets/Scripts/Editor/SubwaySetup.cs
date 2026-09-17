using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu "Subway".
// As gravações e o boneco podem ter esqueletos diferentes (o ViconMale tem a coluna
// noutro eixo e os braços em baixo). Este script converte a animação de um esqueleto
// para o outro (retargeting) em vez de copiar as rotações às cegas.
public static class SubwaySetup
{
    const string Pasta = "Assets/Jogo";
    const string PastaClips = Pasta + "/Clips";
    const string CaminhoController = Pasta + "/Jogador.controller";

    struct InfoClip
    {
        public string fbx, estado;
        public float inicio, fim;
        public bool loop;
    }

    // Tempos medidos nas gravações (fora a T-pose de calibração e o tempo parado).
    static readonly InfoClip[] Clips =
    {
        new InfoClip { fbx = "Assets/Animation/idle.fbx",          estado = "idle",     inicio = 1.8f, fim = 5.0f, loop = true },
        new InfoClip { fbx = "Assets/Animation/desvio_esq001.fbx", estado = "esquerda", inicio = 1.6f, fim = 4.6f },
        new InfoClip { fbx = "Assets/Animation/desvio_dir001.fbx", estado = "direita",  inicio = 1.8f, fim = 4.8f },
        new InfoClip { fbx = "Assets/Animation/salto.fbx",         estado = "salto",    inicio = 1.3f, fim = 3.8f },
        new InfoClip { fbx = "Assets/Animation/agachar.fbx",       estado = "agachar",  inicio = 2.8f, fim = 5.2f },
    };

    // ------------------------------------------------------------- menus

    [MenuItem("Subway/Fazer tudo (1 + 2)", priority = 0)]
    public static void FazerTudo()
    {
        Transform personagem = EscolherPersonagem();
        if (personagem == null) return;
        if (PrepararAnimacoes(personagem)) MontarJogo(personagem);
    }

    [MenuItem("Subway/1 - Preparar animações", priority = 20)]
    public static void PrepararAnimacoesMenu()
    {
        Transform personagem = EscolherPersonagem();
        if (personagem != null) PrepararAnimacoes(personagem);
    }

    [MenuItem("Subway/2 - Montar jogo na cena", priority = 21)]
    public static void MontarJogoMenu()
    {
        Transform personagem = EscolherPersonagem();
        if (personagem != null) MontarJogo(personagem);
    }

    [MenuItem("Subway/Ver estado (diagnóstico)", priority = 40)]
    public static void Diagnostico()
    {
        var txt = new System.Text.StringBuilder("[Subway] ESTADO DO JOGO\n");

        int nClips = 0;
        foreach (InfoClip info in Clips)
        {
            bool fbx = AssetDatabase.LoadAssetAtPath<GameObject>(info.fbx) != null;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{PastaClips}/{info.estado}.anim");
            if (clip != null) nClips++;
            txt.AppendLine($"  gravação {info.estado,-9} ficheiro: {(fbx ? "ok" : "EM FALTA")}   clip feito: {(clip != null ? $"ok ({clip.length:0.0}s)" : "NÃO")}");
        }
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CaminhoController);
        txt.AppendLine($"  Animator: {(ctrl != null ? "ok" : "NÃO existe - corre o passo 1")}");

        Jogador jogador = TodosNaCena<Jogador>().FirstOrDefault();
        ObstacleSpawner jogo = TodosNaCena<ObstacleSpawner>().FirstOrDefault();
        txt.AppendLine($"  Spawner na cena: {(jogo != null ? "ok" : "NÃO")}");
        if (jogador == null) txt.AppendLine("  Jogador na cena: NÃO - corre o passo 2");
        else
        {
            Animator an = jogador.GetComponent<Animator>();
            txt.AppendLine($"  Jogador: {jogador.name}");
            txt.AppendLine($"    Animator: {(an == null ? "NÃO" : an.runtimeAnimatorController == null ? "sem controller" : an.runtimeAnimatorController.name)}");
            txt.AppendLine($"    corpo (PlayerBody): {(jogador.GetComponent<PlayerBody>() != null ? "ok" : "NÃO")}");
            txt.AppendLine($"    pista ligada: {(jogador.pista != null ? "ok" : "NÃO")}");
        }
        txt.AppendLine(nClips == Clips.Length && ctrl != null && jogador != null
            ? "  >> está tudo montado: grava a cena e carrega Play"
            : "  >> falta correr 'Subway > Fazer tudo (1 + 2)' (com o boneco selecionado)");
        Debug.Log(txt.ToString());
    }

    // O boneco é o que estiver selecionado na Hierarchy; senão, o ViconMale; senão, o ViconActor.
    static Transform EscolherPersonagem()
    {
        GameObject sel = Selection.activeGameObject;
        if (sel != null && sel.scene.IsValid() && Procurar(sel.transform, "Hips") != null)
            return sel.transform;

        Transform[] candidatos = TodosNaCena<Transform>()
            .Where(t => t.parent == null && Procurar(t, "Hips") != null).ToArray();

        Transform escolhido = candidatos.FirstOrDefault(t => t.name.StartsWith("ViconMale"))
                           ?? candidatos.FirstOrDefault(t => t.name.StartsWith("ViconActor"))
                           ?? candidatos.FirstOrDefault();

        if (escolhido == null)
            EditorUtility.DisplayDialog("Subway", "Não encontrei nenhum boneco com o osso 'Hips' na cena.\n" +
                                                  "Seleciona o boneco na Hierarchy e corre o menu outra vez.", "OK");
        else
            Debug.Log($"[Subway] boneco escolhido: {escolhido.name} (seleciona outro na Hierarchy para mudar)", escolhido);
        return escolhido;
    }

    // ------------------------------------------------------------- 1: animações

    public static bool PrepararAnimacoes(Transform personagem)
    {
        Esqueleto alvo = Esqueleto.De(personagem);
        if (alvo == null)
        {
            Debug.LogError($"[Subway] '{personagem.name}' não tem um esqueleto com os ossos Hips e Neck1. " +
                            "Seleciona o boneco certo na Hierarchy e corre o menu outra vez.", personagem);
            return false;
        }
        string falta = Conversor.OssoEmFalta(alvo);
        if (falta != null)
        {
            Debug.LogError($"[Subway] o boneco '{personagem.name}' não tem o osso '{falta}'.", personagem);
            return false;
        }

        if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets", "Jogo");
        if (!AssetDatabase.IsValidFolder(PastaClips)) AssetDatabase.CreateFolder(Pasta, "Clips");

        var feitos = new Dictionary<string, AnimationClip>();
        foreach (InfoClip info in Clips)
        {
            try
            {
                AnimationClip clip = ConverterClip(info, alvo);
                if (clip != null) feitos[info.estado] = clip;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Subway] falhou a preparar '{info.estado}': {e.Message}\n{e.StackTrace}");
            }
        }

        if (feitos.Count == 0) return false;
        CriarController(feitos);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return true;
    }

    static AnimationClip ConverterClip(InfoClip info, Esqueleto alvo)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(info.fbx);
        AnimationClip original = AssetDatabase.LoadAllAssetsAtPath(info.fbx).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (modelo == null || original == null)
        {
            Debug.LogError($"[Subway] não encontrei a animação em {info.fbx}");
            return null;
        }

        Esqueleto fonte = Esqueleto.De(modelo.transform);
        if (fonte == null || Conversor.OssoEmFalta(fonte) != null)
        {
            Debug.LogError($"[Subway] {info.fbx}: esqueleto da gravação incompleto" +
                           (fonte != null ? $" (falta {Conversor.OssoEmFalta(fonte)})" : " (sem Hips/Neck1)"));
            return null;
        }

        var conv = new Conversor(fonte, alvo);
        var curvasFonte = LerCurvas(original, fonte);

        float fim = info.fim > 0 ? Mathf.Min(info.fim, original.length) : original.length;
        float inicio = Mathf.Clamp(info.inicio, 0f, fim - 0.2f);
        float fps = original.frameRate > 1f ? original.frameRate : 60f;
        int nFrames = Mathf.Max(2, Mathf.RoundToInt((fim - inicio) * fps) + 1);

        // amostrar e converter
        var rotacoes = new Dictionary<string, Quaternion[]>();
        var posicoes = new Vector3[nFrames];
        foreach (Osso o in alvo.Ossos) rotacoes[o.nome] = new Quaternion[nFrames];

        for (int f = 0; f < nFrames; f++)
        {
            float t = inicio + (fim - inicio) * f / (nFrames - 1);
            conv.Converter(curvasFonte, t, rotacoes, posicoes, f);
        }
        conv.TirarDeslocamentoLateral(posicoes);

        // escrever curvas
        var bindings = new List<EditorCurveBinding>();
        var curvas = new List<AnimationCurve>();
        float[] tempos = new float[nFrames];
        for (int f = 0; f < nFrames; f++) tempos[f] = (fim - inicio) * f / (nFrames - 1);

        foreach (Osso o in alvo.Ossos)
        {
            Quaternion[] qs = rotacoes[o.nome];
            for (int c = 0; c < 4; c++)
            {
                float[] v = new float[nFrames];
                for (int f = 0; f < nFrames; f++) v[f] = c == 0 ? qs[f].x : c == 1 ? qs[f].y : c == 2 ? qs[f].z : qs[f].w;
                bindings.Add(new EditorCurveBinding { path = o.caminho, type = typeof(Transform), propertyName = "m_LocalRotation." + "xyzw"[c] });
                curvas.Add(Curva(tempos, v));
            }
        }
        for (int c = 0; c < 3; c++)
        {
            float[] v = new float[nFrames];
            for (int f = 0; f < nFrames; f++) v[f] = posicoes[f][c];
            bindings.Add(new EditorCurveBinding { path = alvo.Hips.caminho, type = typeof(Transform), propertyName = "m_LocalPosition." + "xyz"[c] });
            curvas.Add(Curva(tempos, v));
        }

        var novo = new AnimationClip { name = info.estado, frameRate = fps };
        AnimationUtility.SetEditorCurves(novo, bindings.ToArray(), curvas.ToArray());
        novo.EnsureQuaternionContinuity();
        AnimationClipSettings cfg = AnimationUtility.GetAnimationClipSettings(novo);
        cfg.loopTime = info.loop;
        cfg.loopBlend = info.loop;
        AnimationUtility.SetAnimationClipSettings(novo, cfg);

        string caminho = $"{PastaClips}/{info.estado}.anim";
        var existente = AssetDatabase.LoadAssetAtPath<AnimationClip>(caminho);
        if (existente != null)
        {
            EditorUtility.CopySerialized(novo, existente);
            EditorUtility.SetDirty(existente);
            novo = existente;
        }
        else AssetDatabase.CreateAsset(novo, caminho);

        Debug.Log($"[Subway] {info.estado}: {fim - inicio:0.0}s, {nFrames} frames, {alvo.Ossos.Count} ossos{(info.loop ? " (loop)" : "")}");
        return novo;
    }

    static AnimationCurve Curva(float[] tempos, float[] valores)
    {
        var keys = new Keyframe[tempos.Length];
        for (int i = 0; i < tempos.Length; i++)
        {
            float inc;
            if (i == 0) inc = (valores[1] - valores[0]) / (tempos[1] - tempos[0]);
            else if (i == tempos.Length - 1) inc = (valores[i] - valores[i - 1]) / (tempos[i] - tempos[i - 1]);
            else inc = (valores[i + 1] - valores[i - 1]) / (tempos[i + 1] - tempos[i - 1]);
            keys[i] = new Keyframe(tempos[i], valores[i], inc, inc);
        }
        return new AnimationCurve(keys);
    }

    static Dictionary<string, AnimationCurve[]> LerCurvas(AnimationClip clip, Esqueleto fonte)
    {
        var fora = new Dictionary<string, AnimationCurve[]>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
        {
            if (b.type != typeof(Transform)) continue;
            Osso o = fonte.PorCaminho(b.path);
            if (o == null) continue;

            int idx = -1;
            string p = b.propertyName;
            if (p.StartsWith("m_LocalRotation.")) idx = "xyzw".IndexOf(p[p.Length - 1]);
            else if (p.StartsWith("m_LocalPosition.") && o == fonte.Hips) idx = 4 + "xyz".IndexOf(p[p.Length - 1]);
            if (idx < 0) continue;

            if (!fora.TryGetValue(o.nome, out AnimationCurve[] arr))
            {
                arr = new AnimationCurve[7];
                fora[o.nome] = arr;
            }
            arr[idx] = AnimationUtility.GetEditorCurve(clip, b);
        }
        return fora;
    }

    // ------------------------------------------------------------- Animator

    static void CriarController(Dictionary<string, AnimationClip> clips)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CaminhoController);
        if (ctrl == null) ctrl = AnimatorController.CreateAnimatorControllerAtPath(CaminhoController);

        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states.ToArray()) sm.RemoveState(s.state);

        AnimatorState idle = sm.AddState("idle");
        idle.motion = clips.TryGetValue("idle", out AnimationClip ci) ? ci : null;
        sm.defaultState = idle;

        foreach (var par in clips)
        {
            if (par.Key == "idle") continue;
            AnimatorState estado = sm.AddState(par.Key);
            estado.motion = par.Value;
            AnimatorStateTransition volta = estado.AddTransition(idle);
            volta.hasExitTime = true;
            volta.exitTime = 0.85f;
            volta.hasFixedDuration = true;
            volta.duration = 0.2f;
        }
        EditorUtility.SetDirty(ctrl);
        Debug.Log($"[Subway] Animator: {CaminhoController} ({clips.Count} estados)");
    }

    // ------------------------------------------------------------- 2: cena

    public static void MontarJogo(Transform personagem)
    {
        Scene cena = SceneManager.GetActiveScene();

        ObstacleSpawner jogo = TodosNaCena<ObstacleSpawner>().FirstOrDefault();
        if (jogo == null)
        {
            var obj = new GameObject("Spawner");
            Undo.RegisterCreatedObjectUndo(obj, "Subway");
            jogo = obj.AddComponent<ObstacleSpawner>();
        }
        Transform pista = jogo.transform;

        Undo.RecordObject(personagem, "Subway");
        if (!personagem.gameObject.activeSelf) personagem.gameObject.SetActive(true);
        personagem.position = pista.position;

        Vector3 frente = pista.forward;
        frente.y = 0f;
        if (frente.sqrMagnitude < 0.001f) frente = Vector3.forward;
        personagem.rotation = Quaternion.LookRotation(-frente.normalized);

        // o mocap ao vivo e o Bake mandavam nos mesmos ossos: ficam desligados neste boneco
        foreach (MonoBehaviour mb in personagem.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string tipo = mb == null ? "" : mb.GetType().Name;
            if ((tipo == "SubjectScript" || tipo == "Bake") && mb.enabled)
            {
                Undo.RecordObject(mb, "Subway");
                mb.enabled = false;
                Debug.Log($"[Subway] desliguei o {tipo} no boneco do jogo (senão mandava nos ossos ao mesmo tempo)", mb);
            }
        }

        Animator animator = personagem.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(personagem.gameObject);
        Undo.RecordObject(animator, "Subway");
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CaminhoController);
        animator.avatar = null;
        animator.applyRootMotion = false;

        Rigidbody rb = personagem.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(personagem.gameObject);
        Undo.RecordObject(rb, "Subway");
        rb.isKinematic = true;
        rb.useGravity = false;

        if (personagem.GetComponent<PlayerBody>() == null) Undo.AddComponent<PlayerBody>(personagem.gameObject);

        Jogador jogador = personagem.GetComponent<Jogador>();
        if (jogador == null) jogador = Undo.AddComponent<Jogador>(personagem.gameObject);
        Undo.RecordObject(jogador, "Subway");
        jogador.jogo = jogo;
        jogador.pista = pista;
        jogador.animator = animator;
        jogador.larguraLane = jogo.larguraLane;

        Undo.RecordObject(jogo, "Subway");
        jogo.jogador = jogador;

        // bonecos repetidos e cubos de teste só atrapalham
        foreach (Transform t in TodosNaCena<Transform>().ToArray())
        {
            if (t == null || t == personagem || t.parent != null || !t.gameObject.activeSelf) continue;
            bool repetido = t.name.StartsWith("ViconActor") || t.name.StartsWith("ViconMale");
            bool testeAntigo = t.GetComponent<MonoBehaviour>() != null &&
                               t.GetComponents<MonoBehaviour>().Any(m => m != null && m.GetType().Name == "CollisionDetection");
            if (!repetido && !testeAntigo) continue;
            Undo.RecordObject(t.gameObject, "Subway");
            t.gameObject.SetActive(false);
            Debug.Log($"[Subway] desliguei '{t.name}' (repetido ou de teste)", t);
        }

        CriarTextos(jogo);

        EditorUtility.SetDirty(jogo);
        EditorSceneManager.MarkSceneDirty(cena);
        Selection.activeGameObject = personagem.gameObject;
        Debug.Log("[Subway] Jogo montado com o boneco '" + personagem.name + "'. Grava a cena (Ctrl+S) e carrega Play. " +
                  "Se ele estiver de costas para os obstáculos, roda-o 180 no Y.", personagem);
    }

    // Textos do jogo como objetos da cena (dá para mexer neles no Inspector)
    static void CriarTextos(ObstacleSpawner jogo)
    {
        Canvas canvas = TodosNaCena<Canvas>().FirstOrDefault(c => c.name == "UI do Jogo");
        if (canvas == null)
        {
            var go = new GameObject("UI do Jogo", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(go, "Subway");
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler escala = go.GetComponent<CanvasScaler>();
            escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            escala.referenceResolution = new Vector2(1920, 1080);
        }

        if (jogo.textoPontos == null)
        {
            TMP_Text t = Texto(canvas.transform, "Pontos", 46, TextAlignmentOptions.TopLeft, "Pontos: 0");
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(40, -30);
            rt.sizeDelta = new Vector2(800, 90);
            jogo.textoPontos = t;
        }

        if (jogo.textoMensagem == null)
        {
            TMP_Text t = Texto(canvas.transform, "Mensagem", 34, TextAlignmentOptions.Bottom,
                               "A / D = mudar de linha     W = saltar     S = agachar");
            RectTransform rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(40, 30);
            rt.offsetMax = new Vector2(-40, 130);
            jogo.textoMensagem = t;
        }

        if (jogo.textoPontos != null && jogo.textoPontos.font == null)
            Debug.LogWarning("[Subway] o TextMeshPro ainda não tem fontes: Window > TextMeshPro > Import TMP Essential Resources.");
    }

    static TMP_Text Texto(Transform pai, string nome, float tamanho, TextAlignmentOptions alinhamento, string conteudo)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Subway");
        go.transform.SetParent(pai, false);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text = conteudo;
        t.fontSize = tamanho;
        t.alignment = alinhamento;
        t.color = Color.white;
        t.raycastTarget = false;
        return t;
    }

    // ------------------------------------------------------------- util

    static IEnumerable<T> TodosNaCena<T>() where T : Component
    {
        Scene cena = SceneManager.GetActiveScene();
        if (!cena.IsValid() || !cena.isLoaded) yield break;
        foreach (GameObject raiz in cena.GetRootGameObjects())
            foreach (T c in raiz.GetComponentsInChildren<T>(true))
                yield return c;
    }

    public static string Limpo(string nome) => Regex.Replace(nome, @"(\s*\(\d+\)|[ _.]\d+)$", "");

    static Transform Procurar(Transform raiz, string nome)
    {
        if (Limpo(raiz.name) == nome) return raiz;
        foreach (Transform filho in raiz)
        {
            Transform r = Procurar(filho, nome);
            if (r != null) return r;
        }
        return null;
    }
}
