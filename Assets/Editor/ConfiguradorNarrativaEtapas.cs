using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Events;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Configura e integra o fluxo narrativo e de gameplay completo na SampleScene:
/// 1. DiarioDeBordo.cs no CanvasNarrativa com 3 páginas e botões com suporte VR;
/// 2. PainelObjetivo.cs em GerenciadorObjetivos com Canvas vermelho de objetivo dinâmico a 1,6m do jogador;
/// 3. TvNoticias.cs na TV da parede com manchetes em corrotina;
/// 4. RadarNave.cs como ponte lógica entre o gerador e o alarme;
/// 5. GeradorCombustivel e BotaoGerador ligados ao Radar e PainelObjetivo;
/// 6. Sistema de Bateria (Socket, Bateria Danificada e Reserva) integrado na espaçonave.
/// </summary>
public static class ConfiguradorNarrativaEtapas
{
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("MathBlaster/Configurar Narrativa e Objetivos Completos (SampleScene)")]
    public static void ConfigurarTodosOsItens()
    {
        Debug.Log(">>> [ConfiguradorNarrativa] Iniciando configuração completa na SampleScene...");
        var scene = EditorSceneManager.OpenScene(SampleScenePath);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // 1. Configurar PainelObjetivo desacoplado primeiro para que outros componentes possam referenciá-lo
        PainelObjetivo painelObjetivo = ConfigurarPainelObjetivo(font);

        // 2. Configurar TvNoticias na TV da parede
        TvNoticias tvNoticias = ConfigurarTvNoticias(font);

        // 3. Configurar RadarNave
        RadarNave radarNave = ConfigurarRadarNave(font, painelObjetivo);

        // 4. Configurar DiarioDeBordo no CanvasNarrativa
        ConfigurarDiarioDeBordo(font, tvNoticias, painelObjetivo);

        // 5. Configurar GeradorCombustivel para acionar o RadarNave e esconder objetivo
        ConfigurarGeradorCombustivel(radarNave);

        // 6. Configurar Sistema de Bateria na Cabine e Prateleira
        ConfigurarBateria(font, painelObjetivo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(">>> [ConfiguradorNarrativa] Configuração da narrativa e gameplay da SampleScene concluída com sucesso!");
    }

    private static PainelObjetivo ConfigurarPainelObjetivo(TMP_FontAsset font)
    {
        // Remove painéis duplicados antigos se existirem
        GameObject oldPanel = GameObject.Find("PainelObjetivo");
        if (oldPanel != null)
        {
            Object.DestroyImmediate(oldPanel);
        }

        // Câmera do jogador (Main Camera dentro do XR Origin)
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null) mainCam = camObj.GetComponent<Camera>();
        }

        // 1. Cria o objeto gerenciador (NÃO contém o Canvas que será desativado)
        GameObject mgrObj = GameObject.Find("GerenciadorObjetivos");
        if (mgrObj == null)
        {
            mgrObj = new GameObject("GerenciadorObjetivos");
        }

        // 2. Cria o Canvas vermelho de objetivo
        GameObject canvasObj = GameObject.Find("CanvasObjetivo");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("CanvasObjetivo");
        }

        // Posicionamento inicial padrão (será reposicionado dinamicamente em Mostrar)
        canvasObj.transform.position = new Vector3(-3.57f, 1.55f, -7.0f);
        canvasObj.transform.rotation = Quaternion.identity;

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        if (mainCam != null) canvas.worldCamera = mainCam;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(750f, 260f);
        rt.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);

        // Fundo vermelho escuro translúcido de emergência / objetivo
        Transform bgTrans = canvasObj.transform.Find("Fundo");
        GameObject bgObj = bgTrans != null ? bgTrans.gameObject : new GameObject("Fundo");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        if (bgImg == null) bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.14f, 0.02f, 0.03f, 0.95f);
        bgImg.raycastTarget = false;
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Borda vermelha de alerta
        Transform borderTrans = canvasObj.transform.Find("Borda");
        GameObject borderObj = borderTrans != null ? borderTrans.gameObject : new GameObject("Borda");
        borderObj.transform.SetParent(canvasObj.transform, false);
        Image borderImg = borderObj.GetComponent<Image>();
        if (borderImg == null) borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.95f, 0.20f, 0.20f, 0.90f);
        borderImg.raycastTarget = false;
        RectTransform bRt = borderObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.03f, 0.78f);
        bRt.anchorMax = new Vector2(0.97f, 0.80f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        // Título "OBJETIVO"
        Transform titleTrans = canvasObj.transform.Find("Titulo");
        GameObject titleObj = titleTrans != null ? titleTrans.gameObject : new GameObject("Titulo");
        titleObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI titleTxt = titleObj.GetComponent<TextMeshProUGUI>();
        if (titleTxt == null) titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        if (font != null) titleTxt.font = font;
        titleTxt.fontSize = 24f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.35f, 0.35f);
        titleTxt.text = "OBJETIVO";
        titleTxt.raycastTarget = false;
        RectTransform tRt = titleObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.81f);
        tRt.anchorMax = new Vector2(1f, 0.98f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        // Texto do Objetivo (descrição)
        Transform textTrans = canvasObj.transform.Find("TextoDescricao");
        GameObject textObj = textTrans != null ? textTrans.gameObject : new GameObject("TextoDescricao");
        textObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI bodyTxt = textObj.GetComponent<TextMeshProUGUI>();
        if (bodyTxt == null) bodyTxt = textObj.AddComponent<TextMeshProUGUI>();
        if (font != null) bodyTxt.font = font;
        bodyTxt.fontSize = 22f;
        bodyTxt.alignment = TextAlignmentOptions.Center;
        bodyTxt.textWrappingMode = TextWrappingModes.Normal;
        bodyTxt.color = Color.white;
        bodyTxt.text = "Aguardando início da missão...";
        bodyTxt.raycastTarget = false;
        RectTransform txtRt = textObj.GetComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0.06f, 0.08f);
        txtRt.anchorMax = new Vector2(0.94f, 0.76f);
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        // 3. Configura o componente PainelObjetivo no objeto gerenciador
        PainelObjetivo painel = mgrObj.GetComponent<PainelObjetivo>();
        if (painel == null) painel = mgrObj.AddComponent<PainelObjetivo>();

        SerializedObject so = new SerializedObject(painel);
        so.FindProperty("canvasObjetivo").objectReferenceValue = canvasObj;
        so.FindProperty("textoDescricao").objectReferenceValue = bodyTxt;
        so.FindProperty("cameraJogador").objectReferenceValue = mainCam != null ? mainCam.transform : null;
        so.ApplyModifiedProperties();

        // Inicia desativado até o diário terminar
        canvasObj.SetActive(false);
        EditorUtility.SetDirty(canvasObj);
        EditorUtility.SetDirty(mgrObj);
        return painel;
    }

    private static TvNoticias ConfigurarTvNoticias(TMP_FontAsset font)
    {
        GameObject tvObj = GameObject.Find("TvNoticias_Parede");
        if (tvObj == null)
        {
            tvObj = new GameObject("TvNoticias_Parede");
            tvObj.transform.position = new Vector3(-5.74f, 1.85f, -2.5f);
            tvObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject moldura = GameObject.CreatePrimitive(PrimitiveType.Cube);
            moldura.name = "MolduraTV";
            moldura.transform.SetParent(tvObj.transform, false);
            moldura.transform.localScale = new Vector3(1.5f, 0.9f, 0.05f);
            var mr = moldura.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material dark = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                dark.color = new Color(0.08f, 0.10f, 0.12f);
                mr.sharedMaterial = dark;
            }
        }

        Transform canvasTrans = tvObj.transform.Find("CanvasTV");
        GameObject canvasObj = canvasTrans != null ? canvasTrans.gameObject : new GameObject("CanvasTV");
        canvasObj.transform.SetParent(tvObj.transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        canvasObj.transform.localRotation = Quaternion.identity;

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(850f, 500f);
        rt.localScale = new Vector3(0.0016f, 0.0016f, 0.0016f);

        Transform screenTrans = canvasObj.transform.Find("TelaFundo");
        GameObject screenObj = screenTrans != null ? screenTrans.gameObject : new GameObject("TelaFundo");
        screenObj.transform.SetParent(canvasObj.transform, false);
        Image screenImg = screenObj.GetComponent<Image>();
        if (screenImg == null) screenImg = screenObj.AddComponent<Image>();
        screenImg.color = new Color(0.02f, 0.04f, 0.09f, 0.98f);
        screenImg.raycastTarget = false;
        RectTransform sRt = screenObj.GetComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = Vector2.zero;
        sRt.offsetMax = Vector2.zero;

        Transform bannerTrans = canvasObj.transform.Find("BannerNoticias");
        GameObject bannerObj = bannerTrans != null ? bannerTrans.gameObject : new GameObject("BannerNoticias");
        bannerObj.transform.SetParent(canvasObj.transform, false);
        Image bImg = bannerObj.GetComponent<Image>();
        if (bImg == null) bImg = bannerObj.AddComponent<Image>();
        bImg.color = new Color(0.85f, 0.15f, 0.15f, 0.90f);
        bImg.raycastTarget = false;
        RectTransform bRt = bannerObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.04f, 0.82f);
        bRt.anchorMax = new Vector2(0.96f, 0.96f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Transform bTxtTrans = bannerObj.transform.Find("TextoBanner");
        GameObject bTxtObj = bTxtTrans != null ? bTxtTrans.gameObject : new GameObject("TextoBanner");
        bTxtObj.transform.SetParent(bannerObj.transform, false);
        TextMeshProUGUI bTxt = bTxtObj.GetComponent<TextMeshProUGUI>();
        if (bTxt == null) bTxt = bTxtObj.AddComponent<TextMeshProUGUI>();
        if (font != null) bTxt.font = font;
        bTxt.fontSize = 20f;
        bTxt.fontStyle = FontStyles.Bold;
        bTxt.alignment = TextAlignmentOptions.Center;
        bTxt.color = Color.white;
        bTxt.text = "● REDE GALÁCTICA DE NOTÍCIAS | AO VIVO";
        bTxt.raycastTarget = false;
        RectTransform btRt = bTxtObj.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.offsetMin = Vector2.zero;
        btRt.offsetMax = Vector2.zero;

        Transform newsTrans = canvasObj.transform.Find("TextoManchete");
        GameObject newsObj = newsTrans != null ? newsTrans.gameObject : new GameObject("TextoManchete");
        newsObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI newsTxt = newsObj.GetComponent<TextMeshProUGUI>();
        if (newsTxt == null) newsTxt = newsObj.AddComponent<TextMeshProUGUI>();
        if (font != null) newsTxt.font = font;
        newsTxt.fontSize = 24f;
        newsTxt.alignment = TextAlignmentOptions.Center;
        newsTxt.textWrappingMode = TextWrappingModes.Normal;
        newsTxt.color = new Color(0.4f, 0.85f, 1.0f);
        newsTxt.text = "Aguardando transmissão...";
        newsTxt.raycastTarget = false;
        RectTransform nRt = newsObj.GetComponent<RectTransform>();
        nRt.anchorMin = new Vector2(0.08f, 0.08f);
        nRt.anchorMax = new Vector2(0.92f, 0.78f);
        nRt.offsetMin = Vector2.zero;
        nRt.offsetMax = Vector2.zero;

        AudioSource audio = tvObj.GetComponent<AudioSource>();
        if (audio == null)
        {
            audio = tvObj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.85f;
            audio.volume = 0.65f;
        }

        TvNoticias tv = tvObj.GetComponent<TvNoticias>();
        if (tv == null) tv = tvObj.AddComponent<TvNoticias>();

        SerializedObject so = new SerializedObject(tv);
        so.FindProperty("texto").objectReferenceValue = newsTxt;
        so.FindProperty("som").objectReferenceValue = audio;
        so.FindProperty("segundosPorManchete").floatValue = 5.0f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(tvObj);
        return tv;
    }

    private static RadarNave ConfigurarRadarNave(TMP_FontAsset font, PainelObjetivo painelObjetivo)
    {
        GameObject radarObj = GameObject.Find("RadarNave");
        if (radarObj == null)
        {
            radarObj = new GameObject("RadarNave");
            radarObj.transform.position = new Vector3(-3.5f, 1.35f, 1.5f);
            radarObj.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        }

        Transform canvasTrans = radarObj.transform.Find("CanvasRadar");
        GameObject canvasObj = canvasTrans != null ? canvasTrans.gameObject : new GameObject("CanvasRadar");
        canvasObj.transform.SetParent(radarObj.transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 90;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(500f, 320f);
        rt.localScale = new Vector3(0.0018f, 0.0018f, 0.0018f);

        Transform bgTrans = canvasObj.transform.Find("Fundo");
        GameObject bgObj = bgTrans != null ? bgTrans.gameObject : new GameObject("Fundo");
        bgObj.transform.SetParent(canvasObj.transform, false);
        Image bgImg = bgObj.GetComponent<Image>();
        if (bgImg == null) bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.02f, 0.06f, 0.08f, 0.95f);
        bgImg.raycastTarget = false;
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        Transform borderTrans = canvasObj.transform.Find("BordaVerde");
        GameObject borderObj = borderTrans != null ? borderTrans.gameObject : new GameObject("BordaVerde");
        borderObj.transform.SetParent(canvasObj.transform, false);
        Image bImg = borderObj.GetComponent<Image>();
        if (bImg == null) bImg = borderObj.AddComponent<Image>();
        bImg.color = new Color(0.1f, 0.85f, 0.45f, 0.85f);
        bImg.raycastTarget = false;
        RectTransform bRt = borderObj.GetComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0.05f, 0.80f);
        bRt.anchorMax = new Vector2(0.95f, 0.82f);
        bRt.offsetMin = Vector2.zero;
        bRt.offsetMax = Vector2.zero;

        Transform titTrans = canvasObj.transform.Find("Titulo");
        GameObject titObj = titTrans != null ? titTrans.gameObject : new GameObject("Titulo");
        titObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI titTxt = titObj.GetComponent<TextMeshProUGUI>();
        if (titTxt == null) titTxt = titObj.AddComponent<TextMeshProUGUI>();
        if (font != null) titTxt.font = font;
        titTxt.fontSize = 22f;
        titTxt.fontStyle = FontStyles.Bold;
        titTxt.alignment = TextAlignmentOptions.Center;
        titTxt.color = new Color(0.15f, 0.90f, 0.50f);
        titTxt.text = "RADAR DE VARREDURA ORBITAL";
        titTxt.raycastTarget = false;
        RectTransform tRt = titObj.GetComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 0.83f);
        tRt.anchorMax = new Vector2(1f, 0.98f);
        tRt.offsetMin = Vector2.zero;
        tRt.offsetMax = Vector2.zero;

        Transform avisoTrans = canvasObj.transform.Find("AvisoRadar");
        GameObject avisoObj = avisoTrans != null ? avisoTrans.gameObject : new GameObject("AvisoRadar");
        avisoObj.transform.SetParent(canvasObj.transform, false);
        TextMeshProUGUI avisoTxt = avisoObj.GetComponent<TextMeshProUGUI>();
        if (avisoTxt == null) avisoTxt = avisoObj.AddComponent<TextMeshProUGUI>();
        if (font != null) avisoTxt.font = font;
        avisoTxt.fontSize = 20f;
        avisoTxt.alignment = TextAlignmentOptions.Center;
        avisoTxt.textWrappingMode = TextWrappingModes.Normal;
        avisoTxt.color = Color.white;
        avisoTxt.text = "Radar: em espera";
        avisoTxt.raycastTarget = false;
        RectTransform aRt = avisoObj.GetComponent<RectTransform>();
        aRt.anchorMin = new Vector2(0.06f, 0.10f);
        aRt.anchorMax = new Vector2(0.94f, 0.78f);
        aRt.offsetMin = Vector2.zero;
        aRt.offsetMax = Vector2.zero;

        AudioSource audio = radarObj.GetComponent<AudioSource>();
        if (audio == null)
        {
            audio = radarObj.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0.8f;
        }

        RadarNave radar = radarObj.GetComponent<RadarNave>();
        if (radar == null) radar = radarObj.AddComponent<RadarNave>();

        SerializedObject so = new SerializedObject(radar);
        so.FindProperty("somLigando").objectReferenceValue = audio;
        so.FindProperty("avisoRadar").objectReferenceValue = avisoTxt;
        so.FindProperty("atrasoDeteccao").floatValue = 4.0f;

        SerializedProperty telasProp = so.FindProperty("telas");
        telasProp.arraySize = 1;
        telasProp.GetArrayElementAtIndex(0).objectReferenceValue = canvasObj;
        so.ApplyModifiedProperties();

        // Conexão aoDetectarDetritos:
        // 1. Inicia alarme no GerenteAlarme
        // 2. PainelObjetivo.Mostrar("Corra até o painel de controle e aperte o botão da mesa")
        GerenteAlarme gerenteAlarme = Object.FindAnyObjectByType<GerenteAlarme>();
        radar.aoDetectarDetritos = new UnityEngine.Events.UnityEvent();

        if (gerenteAlarme != null)
        {
            UnityEventTools.AddPersistentListener(radar.aoDetectarDetritos, gerenteAlarme.TriggerAlarm);
        }

        if (painelObjetivo != null)
        {
            UnityEventTools.AddStringPersistentListener(
                radar.aoDetectarDetritos,
                painelObjetivo.Mostrar,
                "Corra até o painel de controle e aperte o botão da mesa"
            );
        }

        EditorUtility.SetDirty(radarObj);
        return radar;
    }

    private static void ConfigurarDiarioDeBordo(TMP_FontAsset font, TvNoticias tvNoticias, PainelObjetivo painelObjetivo)
    {
        GameObject canvasNarrativa = GameObject.Find("CanvasNarrativa");
        if (canvasNarrativa == null)
        {
            Debug.LogError("[ConfiguradorEtapas] CanvasNarrativa não encontrado na SampleScene!");
            return;
        }

        Canvas canvas = canvasNarrativa.GetComponent<Canvas>();
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            var camObj = GameObject.FindWithTag("MainCamera");
            if (camObj != null) mainCam = camObj.GetComponent<Camera>();
        }
        if (canvas != null && mainCam != null)
        {
            canvas.worldCamera = mainCam;
        }

        var regularRaycaster = canvasNarrativa.GetComponent<GraphicRaycaster>();
        if (regularRaycaster != null)
        {
            Object.DestroyImmediate(regularRaycaster);
        }

        var trackedRaycaster = canvasNarrativa.GetComponent<TrackedDeviceGraphicRaycaster>();
        if (trackedRaycaster == null)
        {
            trackedRaycaster = canvasNarrativa.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        Transform fundoTrans = canvasNarrativa.transform.Find("FundoHolograma");
        if (fundoTrans != null)
        {
            Image fundoImg = fundoTrans.GetComponent<Image>();
            if (fundoImg != null) fundoImg.raycastTarget = false;
        }

        Transform linhaTrans = canvasNarrativa.transform.Find("LinhaHolograma");
        if (linhaTrans != null)
        {
            Image linhaImg = linhaTrans.GetComponent<Image>();
            if (linhaImg != null) linhaImg.raycastTarget = false;
        }

        TMP_Text textoPrincipal = canvasNarrativa.transform.Find("TextoNarrativa")?.GetComponent<TMP_Text>();
        if (textoPrincipal != null)
        {
            textoPrincipal.raycastTarget = false;
        }

        Transform btnVoltarTrans = canvasNarrativa.transform.Find("BtnVoltar");
        Transform btnAvancarTrans = canvasNarrativa.transform.Find("BtnAvancar");

        GameObject btnVoltarObj = btnVoltarTrans != null ? btnVoltarTrans.gameObject : null;
        GameObject btnAvancarObj = btnAvancarTrans != null ? btnAvancarTrans.gameObject : null;

        Transform btnExplorarTrans = canvasNarrativa.transform.Find("BtnExplorar");
        GameObject btnExplorarObj = null;

        if (btnExplorarTrans == null)
        {
            if (btnAvancarObj != null)
            {
                btnExplorarObj = Object.Instantiate(btnAvancarObj, canvasNarrativa.transform);
                btnExplorarObj.name = "BtnExplorar";
                btnExplorarObj.transform.localPosition = btnAvancarObj.transform.localPosition;
                btnExplorarObj.transform.localRotation = btnAvancarObj.transform.localRotation;
                btnExplorarObj.transform.localScale = btnAvancarObj.transform.localScale;

                TMP_Text txtExplorar = btnExplorarObj.GetComponentInChildren<TMP_Text>();
                if (txtExplorar != null) txtExplorar.text = "Explorar Nave";

                Image imgExplorar = btnExplorarObj.GetComponent<Image>();
                if (imgExplorar != null) imgExplorar.color = new Color(0.12f, 0.65f, 0.40f, 0.92f);
            }
        }
        else
        {
            btnExplorarObj = btnExplorarTrans.gameObject;
        }

        Transform contadorTrans = canvasNarrativa.transform.Find("TextoContador");
        GameObject contadorObj = contadorTrans != null ? contadorTrans.gameObject : new GameObject("TextoContador");
        contadorObj.transform.SetParent(canvasNarrativa.transform, false);
        TextMeshProUGUI contadorTxt = contadorObj.GetComponent<TextMeshProUGUI>();
        if (contadorTxt == null) contadorTxt = contadorObj.AddComponent<TextMeshProUGUI>();
        if (font != null) contadorTxt.font = font;
        contadorTxt.fontSize = 18f;
        contadorTxt.fontStyle = FontStyles.Bold;
        contadorTxt.alignment = TextAlignmentOptions.Center;
        contadorTxt.color = new Color(0.6f, 0.75f, 0.9f);
        contadorTxt.text = "[1/3]";
        contadorTxt.raycastTarget = false;
        RectTransform cRt = contadorObj.GetComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0.5f, 0f);
        cRt.anchorMax = new Vector2(0.5f, 0f);
        cRt.pivot = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = new Vector2(0f, 32f);
        cRt.sizeDelta = new Vector2(180f, 35f);

        DiarioDeBordo diario = canvasNarrativa.GetComponent<DiarioDeBordo>();
        if (diario == null) diario = canvasNarrativa.AddComponent<DiarioDeBordo>();

        SerializedObject so = new SerializedObject(diario);
        so.FindProperty("texto").objectReferenceValue = textoPrincipal;
        so.FindProperty("contador").objectReferenceValue = contadorTxt;
        so.FindProperty("botaoVoltar").objectReferenceValue = btnVoltarObj;
        so.FindProperty("botaoAvancar").objectReferenceValue = btnAvancarObj;
        so.FindProperty("botaoExplorar").objectReferenceValue = btnExplorarObj;
        so.ApplyModifiedProperties();

        ConfigurarBotaoVR(btnAvancarObj, diario.Avancar, diario.AvancarXR);
        ConfigurarBotaoVR(btnVoltarObj, diario.Voltar, diario.VoltarXR);
        ConfigurarBotaoVR(btnExplorarObj, diario.Explorar, diario.ExplorarXR);

        // Conecta aoTerminarDiario:
        // 1. TvNoticias.Transmitir()
        // 2. PainelObjetivo.Mostrar("Abasteça o gerador com o galão de combustível")
        diario.aoTerminarDiario = new UnityEngine.Events.UnityEvent();

        if (tvNoticias != null)
        {
            UnityEventTools.AddPersistentListener(diario.aoTerminarDiario, tvNoticias.Transmitir);
        }

        if (painelObjetivo != null)
        {
            UnityEventTools.AddStringPersistentListener(
                diario.aoTerminarDiario,
                painelObjetivo.Mostrar,
                "Abasteça o gerador com o galão de combustível"
            );
        }

        EditorUtility.SetDirty(canvasNarrativa);
    }

    private static void ConfigurarBotaoVR(
        GameObject btnObj,
        UnityEngine.Events.UnityAction acaoOnClick,
        UnityEngine.Events.UnityAction<UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs> acaoXR)
    {
        if (btnObj == null) return;

        Image img = btnObj.GetComponent<Image>();
        if (img != null) img.raycastTarget = true;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(btn.onClick, acaoOnClick);
        }

        BoxCollider col = btnObj.GetComponent<BoxCollider>();
        if (col == null) col = btnObj.AddComponent<BoxCollider>();
        col.isTrigger = true;
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt != null) col.size = new Vector3(rt.rect.width, rt.rect.height, 20f);

        var simple = btnObj.GetComponent<XRSimpleInteractable>();
        if (simple == null) simple = btnObj.AddComponent<XRSimpleInteractable>();
        if (simple != null)
        {
            simple.selectEntered.RemoveAllListeners();
            UnityEventTools.AddPersistentListener(simple.selectEntered, acaoXR);
        }

        EditorUtility.SetDirty(btnObj);
    }

    private static void ConfigurarGeradorCombustivel(RadarNave radarNave)
    {
        GeradorCombustivel gerador = Object.FindAnyObjectByType<GeradorCombustivel>();
        if (gerador != null)
        {
            SerializedObject so = new SerializedObject(gerador);
            so.FindProperty("radarNave").objectReferenceValue = radarNave;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(gerador.gameObject);
        }
    }

    private static void ConfigurarBateria(TMP_FontAsset font, PainelObjetivo painelObjetivo)
    {
        GameObject socketObj = GameObject.Find("SocketBateria_Cabine");
        if (socketObj == null)
        {
            socketObj = new GameObject("SocketBateria_Cabine");
            socketObj.transform.position = new Vector3(-3.90f, 0.80f, 8.10f);
            socketObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }

        GameObject batDanificadaObj = GameObject.Find("Bateria_Danificada");
        if (batDanificadaObj == null)
        {
            batDanificadaObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            batDanificadaObj.name = "Bateria_Danificada";
            batDanificadaObj.transform.position = socketObj.transform.position;
            batDanificadaObj.transform.rotation = socketObj.transform.rotation;
            batDanificadaObj.transform.localScale = new Vector3(0.18f, 0.22f, 0.18f);

            var mr = batDanificadaObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material matDan = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                matDan.color = new Color(0.35f, 0.15f, 0.10f);
                mr.sharedMaterial = matDan;
            }
        }

        ItemBateria batDanificada = batDanificadaObj.GetComponent<ItemBateria>();
        if (batDanificada == null) batDanificada = batDanificadaObj.AddComponent<ItemBateria>();
        batDanificada.SetDamaged(true, "Bateria Danificada");
        batDanificada.InitializeComponents();

        GameObject batReservaObj = GameObject.Find("Bateria_Reserva");
        if (batReservaObj == null)
        {
            batReservaObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            batReservaObj.name = "Bateria_Reserva";
            batReservaObj.transform.position = new Vector3(-4.85f, 0.95f, -3.20f);
            batReservaObj.transform.rotation = Quaternion.identity;
            batReservaObj.transform.localScale = new Vector3(0.18f, 0.22f, 0.18f);

            var mr = batReservaObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material matRes = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                matRes.color = new Color(0.15f, 0.75f, 0.90f);
                mr.sharedMaterial = matRes;
            }
        }

        ItemBateria batReserva = batReservaObj.GetComponent<ItemBateria>();
        if (batReserva == null) batReserva = batReservaObj.AddComponent<ItemBateria>();
        batReserva.SetDamaged(false, "Bateria Reserva");
        batReserva.InitializeComponents();

        GameObject painelBatObj = GameObject.Find("PainelBateria_Cabine");
        if (painelBatObj == null)
        {
            painelBatObj = new GameObject("PainelBateria_Cabine");
            painelBatObj.transform.position = new Vector3(-3.90f, 1.25f, 8.10f);
            painelBatObj.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        PainelBateria painelBat = painelBatObj.GetComponent<PainelBateria>();
        if (painelBat == null) painelBat = painelBatObj.AddComponent<PainelBateria>();

        SerializedObject so = new SerializedObject(painelBat);
        so.FindProperty("socketTransform").objectReferenceValue = socketObj.transform;
        so.FindProperty("damagedBattery").objectReferenceValue = batDanificada;
        so.FindProperty("socketSnapDistance").floatValue = 0.55f;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(socketObj);
        EditorUtility.SetDirty(batDanificadaObj);
        EditorUtility.SetDirty(batReservaObj);
        EditorUtility.SetDirty(painelBatObj);
    }
}
