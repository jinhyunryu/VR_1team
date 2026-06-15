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

        var generatedButtons = new List<Button>();
        for (var i = 0; i < songs.Count; i++)
            generatedButtons.Add(CreateSongButton(songs[i], i));

        RefreshControllerPlaylist(generatedButtons);
    }

    public void Configure(Button template, Transform parent, TitleLobbyCanvasController playlistController)
    {
        templateButton = template;
        listParent = parent;
        controller = playlistController;
    }

    Button CreateSongButton(SongEntry song, int index)
    {
        var instance = Instantiate(templateButton.gameObject, listParent);
        instance.name = $"{generatedButtonPrefix}{index + 1}Button";
        instance.SetActive(true);

        var templateRect = templateButton.transform as RectTransform;
        var rect = instance.transform as RectTransform;
        if (templateRect != null && rect != null)
        {
            rect.anchorMin = templateRect.anchorMin;
            rect.anchorMax = templateRect.anchorMax;
            rect.pivot = templateRect.pivot;
            rect.sizeDelta = templateRect.sizeDelta;
            rect.anchoredPosition = templateRect.anchoredPosition + firstButtonOffset + Vector2.down * verticalSpacing * index;
            rect.localScale = templateRect.localScale;
            rect.localRotation = templateRect.localRotation;
        }

        var item = instance.GetComponent<TitlePlaylistSongItemUI>();
        if (item == null)
            item = instance.AddComponent<TitlePlaylistSongItemUI>();

        item.SetSongData(song.songNumber, song.songName, song.difficulty, song.starCount);

        var button = instance.GetComponent<Button>();
        if (button != null && controller != null)
            SetSelectionListener(button, index + 1);

        return button;
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

    void RefreshControllerPlaylist(List<Button> generatedButtons)
    {
        if (controller == null)
            return;

        var buttons = new List<Button>();
        var highlights = new List<GameObject>();
        var names = new List<string>();

        buttons.Add(templateButton);
        AddControllerItemData(templateButton, highlights, names);

        foreach (var button in generatedButtons)
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
