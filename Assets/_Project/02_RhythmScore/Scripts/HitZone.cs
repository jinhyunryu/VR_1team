using UnityEngine;

public class HitZone : MonoBehaviour
{
    private void OnTriggerExit(Collider other)
    {
        Debug.Log("나감 : " + other.name);

        Note note = other.GetComponent<Note>();

        if (note == null)
        {
            Debug.Log("Note 없음");
            return;
        }

        Debug.Log("isHit = " + note.isHit);

        if (note.isHit)
        {
            Debug.Log("이미 맞춘 노트");
            return;
        }

        Debug.Log("MISS 발생");

        ComboManager.Instance.Miss();

        Destroy(other.gameObject);
    }
}