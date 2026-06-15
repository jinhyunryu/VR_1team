using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TitlePlaylistSongItemUI : MonoBehaviour
{
    const int DefaultMaxStarCount = 5;

    [Header("Song Data")]
    [SerializeField, Min(1)] int songNumber = 1;
    [SerializeField] string songName = "Ocean Beat";
    [SerializeField] string difficulty = "EASY";
    [SerializeField, Range(0, DefaultMaxStarCount)] int starCount = 1;

    [Header("References")]
    [SerializeField] TMP_Text songNumberText;
    [SerializeField] TMP_Text songNameText;
    [SerializeField] TMP_Text difficultyText;
    [SerializeField] GameObject selectedHighlight;
    [SerializeField] GameObject starTemplate;
    [SerializeField] Transform starParent;

    [Header("Star Layout")]
    [SerializeField, Range(1, DefaultMaxStarCount)] int maxStarCount = DefaultMaxStarCount;
    [SerializeField] float starSpacing = 28f;
    [SerializeField] bool createMissingStars = true;
    [SerializeField] bool autoBindChildren = true;

    Button cachedButton;

    public int SongNumber => songNumber;
    public string SongName => songName;
    public string Difficulty => difficulty;
    public int StarCount => starCount;
    public Button Button => cachedButton != null ? cachedButton : cachedButton = GetComponent<Button>();
    public GameObject SelectedHighlight => selectedHighlight;

    void Reset()
    {
        ResolveReferences();
        ApplyToUI();
    }

    void Awake()
    {
        ResolveReferences();
        ApplyToUI();
    }

    void OnEnable()
    {
        ResolveReferences();
        ApplyToUI();
    }

    void OnValidate()
    {
        songNumber = Mathf.Max(1, songNumber);
        maxStarCount = Mathf.Clamp(maxStarCount, 1, DefaultMaxStarCount);
        starCount = Mathf.Clamp(starCount, 0, maxStarCount);

        if (autoBindChildren)
            ResolveReferences();

        ApplyToUI();
    }

    public void SetSongData(int number, string name, string difficultyLabel, int stars)
    {
        songNumber = Mathf.Max(1, number);
        songName = name;
        difficulty = difficultyLabel;
        starCount = Mathf.Clamp(stars, 0, maxStarCount);

        ResolveReferences();
        ApplyToUI();
    }

    public void ApplyToUI()
    {
        if (songNumberText != null)
            songNumberText.text = songNumber.ToString();

        if (songNameText != null)
            songNameText.text = songName;

        if (difficultyText != null)
            difficultyText.text = difficulty;

        ApplyStars();
    }

    void ResolveReferences()
    {
        cachedButton = GetComponent<Button>();

        if (songNumberText == null)
            songNumberText = FindTextChild("SongNumber");

        if (songNameText == null)
            songNameText = FindTextChild("SongName");

        if (difficultyText == null)
            difficultyText = FindTextChild("Difficulty");

        if (selectedHighlight == null)
            selectedHighlight = FindChild("SelectedHighlight")?.gameObject;

        if (starTemplate == null)
            starTemplate = FindChild("LevelStar1")?.gameObject;

        if (starParent == null && starTemplate != null)
            starParent = starTemplate.transform.parent;
    }

    void ApplyStars()
    {
        var stars = GetStarObjects();
        EnsureStarObjects(stars);

        for (var i = 0; i < stars.Count; i++)
            stars[i].SetActive(i < starCount);
    }

    void EnsureStarObjects(List<GameObject> stars)
    {
        if (!createMissingStars || starTemplate == null || starParent == null)
            return;

        while (stars.Count < maxStarCount)
        {
            var nextIndex = stars.Count + 1;
            var star = Instantiate(starTemplate, starParent);
            star.name = $"LevelStar{nextIndex}";

            var templateRect = starTemplate.transform as RectTransform;
            var starRect = star.transform as RectTransform;
            if (templateRect != null && starRect != null)
            {
                starRect.anchorMin = templateRect.anchorMin;
                starRect.anchorMax = templateRect.anchorMax;
                starRect.pivot = templateRect.pivot;
                starRect.sizeDelta = templateRect.sizeDelta;
                starRect.anchoredPosition = templateRect.anchoredPosition + Vector2.right * starSpacing * (nextIndex - 1);
                starRect.localScale = templateRect.localScale;
                starRect.localRotation = templateRect.localRotation;
            }

            stars.Add(star);
        }
    }

    List<GameObject> GetStarObjects()
    {
        var stars = new List<GameObject>();
        var transforms = GetComponentsInChildren<Transform>(true);

        foreach (var child in transforms)
        {
            if (!child.name.StartsWith("LevelStar", System.StringComparison.Ordinal))
                continue;

            stars.Add(child.gameObject);
        }

        stars.Sort((left, right) => GetTrailingNumber(left.name).CompareTo(GetTrailingNumber(right.name)));
        return stars;
    }

    TMP_Text FindTextChild(string childName)
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text.name == childName)
                return text;
        }

        return null;
    }

    Transform FindChild(string childName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static int GetTrailingNumber(string value)
    {
        var number = 0;
        var multiplier = 1;

        for (var i = value.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(value[i]))
                break;

            number += (value[i] - '0') * multiplier;
            multiplier *= 10;
        }

        return number;
    }
}
