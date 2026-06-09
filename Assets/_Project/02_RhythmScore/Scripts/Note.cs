using UnityEngine;

public class Note : MonoBehaviour
{
    public float moveSpeed = 5f;

    public bool isHit = false;

    void Update()
    {
        transform.Translate(
            Vector3.back *
            moveSpeed *
            Time.deltaTime,
            Space.World
        );
    }
    public void Hit()
    {
        isHit = true;
        Destroy(gameObject);
    }
}