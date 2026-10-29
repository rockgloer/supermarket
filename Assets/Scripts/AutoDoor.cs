using UnityEngine;

// Раздвижная дверь: открывается, когда кто-то из списка watch подходит близко.
// Анимация — клипы Door_Open / Door_Close в Animator (параметр open). Если Animator не назначен — створки двигаются скриптом.
public class AutoDoor : MonoBehaviour
{
    public Transform leftPanel, rightPanel;
    public Transform[] watch;
    public Animator animator;
    public float openOffset = 1.15f;
    public float triggerDistance = 2.8f;
    public float speed = 2.5f;

    Vector3 leftClosed, rightClosed;
    float open01;
    bool wasOpen;

    void Start()
    {
        leftClosed = leftPanel.localPosition;
        rightClosed = rightPanel.localPosition;
    }

    void Update()
    {
        bool want = false;
        if (watch != null)
        {
            foreach (var t in watch)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 d = t.position - transform.position;
                d.y = 0f;
                if (d.magnitude < triggerDistance) { want = true; break; }
            }
        }

        if (want != wasOpen)
        {
            wasOpen = want;
            SoundManager.Door(transform.position + Vector3.up);
            if (animator != null && animator.runtimeAnimatorController != null) animator.SetBool("open", want);
        }

        if (animator != null && animator.runtimeAnimatorController != null) return;
        open01 = Mathf.MoveTowards(open01, want ? 1f : 0f, speed * Time.deltaTime);
        leftPanel.localPosition = leftClosed + Vector3.left * openOffset * open01;
        rightPanel.localPosition = rightClosed + Vector3.right * openOffset * open01;
    }
}
