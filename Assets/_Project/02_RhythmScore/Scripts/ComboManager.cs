using UnityEngine;

public class ComboManager : MonoBehaviour
{
    public static ComboManager Instance;

    private int combo = 0;

    private void Awake()
    {
        Instance = this;
    }

    public void Hit()
    {
        combo++;

        ComboUI.Instance.UpdateCombo(combo);
    }

    public void Miss()
    {
        combo = 0;

        ComboUI.Instance.UpdateCombo(combo);
    }
}