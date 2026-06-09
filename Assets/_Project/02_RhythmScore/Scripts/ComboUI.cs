using TMPro;
using UnityEngine;

public class ComboUI : MonoBehaviour
{
    public static ComboUI Instance;

    public TMP_Text comboText;

    private void Awake()
    {
        Instance = this;
    }

    public void UpdateCombo(int combo)
    {
        comboText.text =
            "COMBO " + combo;
    }
}