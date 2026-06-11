using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TatleStartPulseEffect : MonoBehaviour
{
    [SerializeField] Graphic targetGraphic;
    [SerializeField, Min(0.1f)] float cycleDuration = 1.65f;
    [SerializeField, Range(0f, 1f)] float minAlpha = 0.45f;
    [SerializeField, Range(0f, 1f)] float maxAlpha = 1f;
    [SerializeField, Range(1f, 1.2f)] float sharpScale = 1.04f;
    [SerializeField] Color dimTint = new Color(0.72f, 0.95f, 1f, 1f);
    [SerializeField] Color brightTint = new Color(1.08f, 1.18f, 1.28f, 1f);

    [Header("Wave Glow")]
    [SerializeField] bool createWaveGlow = true;
    [SerializeField, Range(1f, 1.6f)] float glowMaxScale = 1.28f;
    [SerializeField, Range(0f, 1f)] float glowAlpha = 0.34f;
    [SerializeField] Color glowTint = new Color(0.42f, 0.92f, 1.25f, 1f);

    const string GlowObjectName = "Tatle_Start_1_WaveGlow";

    Graphic glowGraphic;
    RectTransform glowRectTransform;
    RectTransform targetRectTransform;
    Color originalColor;
    Vector3 originalScale;
    bool initialized;

    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        Initialize();
    }

    void Update()
    {
        if (!initialized)
            Initialize();

        if (targetGraphic == null)
            return;

        var cycle = Mathf.Repeat(Time.unscaledTime / cycleDuration, 1f);
        var sharpen = Mathf.Sin(cycle * Mathf.PI);
        sharpen = Mathf.SmoothStep(0f, 1f, sharpen);

        var tint = Color.Lerp(dimTint, brightTint, sharpen);
        tint.a = Mathf.Lerp(minAlpha, maxAlpha, sharpen);
        targetGraphic.color = MultiplyColor(originalColor, tint);

        if (targetRectTransform != null)
            targetRectTransform.localScale = originalScale * Mathf.Lerp(1f, sharpScale, sharpen);

        UpdateWaveGlow(cycle);
    }

    void OnDisable()
    {
        if (targetGraphic != null)
            targetGraphic.color = originalColor;

        if (targetRectTransform != null)
            targetRectTransform.localScale = originalScale;

        if (glowGraphic != null)
            glowGraphic.color = Color.clear;
    }

    void Initialize()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();

        if (targetGraphic == null)
            return;

        targetRectTransform = targetGraphic.rectTransform;
        originalColor = targetGraphic.color;
        originalScale = targetRectTransform.localScale;
        EnsureWaveGlow();
        initialized = true;
    }

    void EnsureWaveGlow()
    {
        if (!createWaveGlow || glowGraphic != null || targetGraphic == null)
            return;

        var existingGlow = transform.Find(GlowObjectName);
        if (existingGlow != null)
            glowGraphic = existingGlow.GetComponent<Graphic>();

        if (glowGraphic == null)
            glowGraphic = CreateGlowGraphic();

        if (glowGraphic == null)
            return;

        glowGraphic.raycastTarget = false;
        glowGraphic.color = Color.clear;
        glowRectTransform = glowGraphic.rectTransform;
        StretchToTarget(glowRectTransform);
    }

    Graphic CreateGlowGraphic()
    {
        var glowObject = new GameObject(GlowObjectName, typeof(RectTransform));
        glowObject.layer = gameObject.layer;
        glowObject.transform.SetParent(transform, false);

        if (targetGraphic is RawImage rawImage)
        {
            var glowRawImage = glowObject.AddComponent<RawImage>();
            glowRawImage.texture = rawImage.texture;
            glowRawImage.uvRect = rawImage.uvRect;
            glowRawImage.material = rawImage.material;
            return glowRawImage;
        }

        if (targetGraphic is Image image)
        {
            var glowImage = glowObject.AddComponent<Image>();
            glowImage.sprite = image.sprite;
            glowImage.type = image.type;
            glowImage.preserveAspect = image.preserveAspect;
            glowImage.fillCenter = image.fillCenter;
            glowImage.fillMethod = image.fillMethod;
            glowImage.fillAmount = image.fillAmount;
            glowImage.fillClockwise = image.fillClockwise;
            glowImage.fillOrigin = image.fillOrigin;
            glowImage.material = image.material;
            return glowImage;
        }

        return null;
    }

    void UpdateWaveGlow(float cycle)
    {
        if (glowGraphic == null || glowRectTransform == null)
            return;

        var waveAlpha = Mathf.Sin(cycle * Mathf.PI) * glowAlpha;
        glowGraphic.color = new Color(glowTint.r, glowTint.g, glowTint.b, glowTint.a * waveAlpha);
        glowRectTransform.localScale = Vector3.one * Mathf.Lerp(1f, glowMaxScale, cycle);
    }

    static void StretchToTarget(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.localScale = Vector3.one;
        rectTransform.localRotation = Quaternion.identity;
        rectTransform.localPosition = Vector3.zero;
    }

    static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a * tint.a);
    }
}
