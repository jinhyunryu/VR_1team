using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TatleUIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerClickHandler,
    ISubmitHandler
{
    public enum ButtonClickSound
    {
        ButtonClick,
        TouchToStart,
        None
    }

    [SerializeField] TatleUIAudioMaster audioMaster;
    [SerializeField] ButtonClickSound clickSound = ButtonClickSound.ButtonClick;
    [SerializeField] Transform visualTarget;
    [SerializeField, Range(0.8f, 1f)] float pressedScale = 0.94f;
    [SerializeField, Range(1f, 1.12f)] float hoverScale = 1.035f;
    [SerializeField, Min(1f)] float followSpeed = 18f;

    Button button;
    Vector3 originalScale = Vector3.one;
    bool hovering;
    bool pressing;
    bool clickSoundPlayedDuringPress;
    bool subscribedToButtonClick;
    float submitPressUntil;

    public void SetAudioMaster(TatleUIAudioMaster master)
    {
        audioMaster = master;
    }

    public void SetClickSound(ButtonClickSound sound)
    {
        clickSound = sound;
    }

    void Awake()
    {
        ResolveReferences();
        CaptureOriginalScale();
        SubscribeButtonClick();
    }

    void OnEnable()
    {
        ResolveReferences();
        CaptureOriginalScale();
        SubscribeButtonClick();
    }

    void OnDisable()
    {
        UnsubscribeButtonClick();
        hovering = false;
        pressing = false;
        clickSoundPlayedDuringPress = false;

        if (visualTarget != null)
            visualTarget.localScale = originalScale;
    }

    void OnDestroy()
    {
        UnsubscribeButtonClick();
    }

    void Update()
    {
        if (visualTarget == null)
            return;

        if (!hovering && !pressing && Time.unscaledTime >= submitPressUntil)
        {
            if ((visualTarget.localScale - originalScale).sqrMagnitude < 0.000001f)
            {
                visualTarget.localScale = originalScale;
                return;
            }
        }

        var desiredScale = GetDesiredScale();
        visualTarget.localScale = Vector3.Lerp(
            visualTarget.localScale,
            desiredScale,
            1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsUsable())
            return;

        if (!hovering)
            ResolveAudioMaster().PlayButtonHover();

        hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        pressing = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsUsable())
            return;

        pressing = true;
        clickSoundPlayedDuringPress = true;
        PlayClickSound();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressing = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsUsable())
            return;

        submitPressUntil = Time.unscaledTime + 0.12f;
    }

    void ResolveReferences()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (visualTarget == null)
            visualTarget = transform;
    }

    void CaptureOriginalScale()
    {
        if (visualTarget != null)
            originalScale = visualTarget.localScale;
    }

    void SubscribeButtonClick()
    {
        if (button == null || subscribedToButtonClick)
            return;

        button.onClick.AddListener(HandleButtonClicked);
        subscribedToButtonClick = true;
    }

    void UnsubscribeButtonClick()
    {
        if (button == null || !subscribedToButtonClick)
            return;

        button.onClick.RemoveListener(HandleButtonClicked);
        subscribedToButtonClick = false;
    }

    void HandleButtonClicked()
    {
        if (clickSoundPlayedDuringPress)
        {
            clickSoundPlayedDuringPress = false;
            return;
        }

        PlayClickSound();
    }

    Vector3 GetDesiredScale()
    {
        if (!IsUsable())
            return originalScale;

        if (pressing || Time.unscaledTime < submitPressUntil)
            return originalScale * pressedScale;

        if (hovering)
            return originalScale * hoverScale;

        return originalScale;
    }

    bool IsUsable()
    {
        return isActiveAndEnabled && button != null && button.interactable;
    }

    TatleUIAudioMaster ResolveAudioMaster()
    {
        if (audioMaster == null)
            audioMaster = TatleUIAudioMaster.EnsureInstance();

        return audioMaster;
    }

    void PlayClickSound()
    {
        switch (clickSound)
        {
            case ButtonClickSound.TouchToStart:
                ResolveAudioMaster().PlayTouchToStartClick();
                break;
            case ButtonClickSound.None:
                break;
            default:
                ResolveAudioMaster().PlayButtonClick();
                break;
        }
    }
}
