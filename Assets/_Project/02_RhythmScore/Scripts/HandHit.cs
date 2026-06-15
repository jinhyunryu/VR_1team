using UnityEngine;

public class HandHit : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("HandHit 충돌 : " + other.name);

        Note note = other.GetComponent<Note>();

        if (note == null)
            return;

        note.isHit = true;

        Debug.Log("노트 맞춤!");

        ComboManager.Instance.Hit();

        Destroy(other.gameObject);
    }
}