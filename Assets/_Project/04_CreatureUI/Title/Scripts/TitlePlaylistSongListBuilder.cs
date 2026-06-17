using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class TitlePlaylistSongListBuilder : MonoBehaviour
{
    [System.Serializable]
    public class SongEntry
    {
        [Min(1)] public int songNumber = 1;
        public string songName = "New Song";
        public string difficulty = "EASY";
        [Range(0, 5)] public int starCount = 1;
    }

    [Header("Template")]
    [SerializeField] Button templateButton;
    [SerializeField] Transform listParent;
    [SerializeField] TitleLobbyCanvasController controller;

    [Header("Generated Buttons")]
    [SerializeField] List<SongEntry> songs = new List<SongEntry>();
    [SerializeField] string generatedButtonPrefix = "PlaylistSongAdded";
    [SerializeField] Vector2 firstButtonOffset = new Vector2(0f, -92f);
    [SerializeField] float verticalSpacing = 92f;
    [SerializeField] bool rebuildOnValidate = true;

    bool rebuildQueued;

    void Reset()
    {
        ResolveReferences();
    }

    void Awake()
    {
        if (Application.isPlaying)
            RebuildList();
    }

    void OnValidate()
    {
        ClampSongEntries();
        ResolveReferences();

        if (rebuildOnValidate)
            QueueRebuild();
    }

    [ContextMenu("Rebuild Song Buttons")]
    public void RebuildList()
    {
        ResolveReferences();

        if (templateButton == null || songs.Count == 0)
            return;

        if (listParent == null)
            listParent = templateButton.transform.parent;

        RemoveGeneratedButtons();

        // 단일 소스: songs[] = 전체 곡 목록. 곡 0 = 템플릿 버튼 재사용, 나머지 = 생성.
        //   → 버튼 순서 = songs[] 순서 = SelectPlaylistSong 인덱스(0-based)로 일치(off-by-one 제거).
        var allButtons = new List<Button> { templateButton };
        templateButton.gameObject.SetActive(true);
        ConfigureSongButton(templateButton, songs[0], 0);

        for (var i = 1; i < songs.Count; i++)
            allButtons.Add(CreateSongButton(songs[i], i));

        RefreshControllerPlaylist(allButtons);
    }

    public void Configure(Button template, Transform parent, TitleLobbyCanvasController playlistController)
    {
        templateButton = template;
        listParent = parent;
        controller = playlistController;
    }

    // index = songs[] 인덱스(0-based, 곡 0 은 템플릿이라 여기 들어오는 index 는 1 이상).
    Button CreateSongButton(SongEntry song, int index)
    {
        var instance = Instantiate(templateButton.gameObject, listParent);
        instance.name = $"{generatedButtonPrefix}{index}Button";
        instance.SetActive(true);

        var templateRect = templateButton.transform as RectTransform;
        var rect = instance.transform as RectTransform;
        if (templateRect != null && rect != null)
        {
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.pivot = templateRect.pivot;
            rect.sizeDelta = templateRect.sizeDelta;
            // 곡 1(첫 생성)이 템플릿 바로 아래(firstButtonOffset), 이후 verticalSpacing 씩.
            rect.anchoredPosition = templateRect.anchoredPosition + firstButtonOffset + Vector2.down * verticalSpacing * (index - 1);
            rect.localScale = templateRect.localScale;
            rect.localRotation = templateRect.localRotation;
        }

        var button = instance.GetComponent<Button>();
        ConfigureSongButton(button, song, index);
        return button;
    }

    // 버튼 1개에 곡 데이터 + 선택 리스너(0-based) 적용. 템플릿/생성 공통.
    void ConfigureSongButton(Button button, SongEntry song, int index)
    {
        if (button == null)
            return;

        var item = button.GetComponent<TitlePlaylistSongItemUI>();
        if (item == null)
            item = button.gameObject.AddComponent<TitlePlaylistSongItemUI>();

        item.SetSongData(song.songNumber, song.songName, song.difficulty, song.starCount);

        if (controller != null)
            SetSelectionListener(button, index); // 0-based — playlistSongButtons 인덱스와 일치
    }

    void RemoveGeneratedButtons()
    {
        if (listParent == null)
            return;

        var generated = new List<GameObject>();
        for (var i = 0; i < listParent.childCount; i++)
        {
            var child = listParent.GetChild(i);
            if (child.name.StartsWith(generatedButtonPrefix, System.StringComparison.Ordinal))
                generated.Add(child.gameObject);
        }

        foreach (var item in generated)
            DestroyGenerated(item);
    }

    // allButtons = [템플릿(곡0), 생성(곡1..)] — 이미 순서대로 들어옴(중복 추가 X).
    void RefreshControllerPlaylist(List<Button> allButtons)
    {
        if (controller == null)
            return;

        var buttons = new List<Button>();
        var highlights = new List<GameObject>();
        var names = new List<string>();

        foreach (var button in allButtons)
        {
            if (button == null)
                continue;

            buttons.Add(button);
            AddControllerItemData(button, highlights, names);
        }

        controller.playlistSongButtons = buttons.ToArray();
        controller.playlistSongSelectedHighlights = highlights.ToArray();
        controller.playlistSongNames = names.ToArray();
        controller.RefreshCurrentSongBanner();
    }

    static void AddControllerItemData(Button button, List<GameObject> highlights, List<string> names)
    {
        var item = button.GetComponent<TitlePlaylistSongItemUI>();
        if (item != null)
        {
            highlights.Add(item.SelectedHighlight);
            names.Add(item.SongName);
            return;
        }

        highlights.Add(FindChild(button.transform, "SelectedHighlight")?.gameObject);
        names.Add(ReadSongNameFromButton(button));
    }

    static string ReadSongNameFromButton(Button button)
    {
        if (button == null)
            return string.Empty;

        var texts = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (var text in texts)
        {
            if (text.name == "SongName" && !string.IsNullOrWhiteSpace(text.text))
                return text.text;
        }

        return button.name;
    }

    void ResolveReferences()
    {
        if (listParent == null)
            listParent = transform;

        if (templateButton == null)
            templateButton = FindChild(listParent, "PlaylistSong1Button")?.GetComponent<Button>();

        if (controller == null)
            controller = FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
    }

    void ClampSongEntries()
    {
        foreach (var song in songs)
        {
            if (song == null)
                continue;

            song.songNumber = Mathf.Max(1, song.songNumber);
            song.starCount = Mathf.Clamp(song.starCount, 0, 5);
        }
    }

    void QueueRebuild()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || rebuildQueued)
            return;

        rebuildQueued = true;
        EditorApplication.delayCall += RebuildAfterValidation;
#endif
    }

#if UNITY_EDITOR
    void RebuildAfterValidation()
    {
        rebuildQueued = false;

        if (this == null)
            return;

        RebuildList();
        EditorUtility.SetDirty(this);

        if (gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
            return null;

        var transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (var child in transforms)
        {
            if (child.name == childName)
                return child;
        }

        return null;
    }

    static void DestroyGenerated(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    static void SetSelectionListener(Button button, int songIndex)
    {
        if (button == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            for (var i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);

            var controller = FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
            if (controller != null)
                UnityEventTools.AddIntPersistentListener(button.onClick, controller.SelectPlaylistSong, songIndex);

            EditorUtility.SetDirty(button);
            return;
        }
#endif

        var runtimeController = FindAnyObjectByType<TitleLobbyCanvasController>(FindObjectsInactive.Include);
        if (runtimeController == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => runtimeController.SelectPlaylistSong(songIndex));
    }
}
