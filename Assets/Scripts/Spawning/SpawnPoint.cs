using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [Tooltip("Spawn group name. All points sharing a name belong to the same group. e.g. 'GroupA', 'GroupB'")]
    public string spawnTag = "Default";

    [Tooltip("Temporarily disables this point without removing it from the scene.")]
    public bool disabled = false;

    void OnDrawGizmos()
    {
        Gizmos.color = disabled
            ? new Color(0.5f, 0.5f, 0.5f, 0.5f)
            : new Color(0.2f, 0.9f, 0.2f, 0.8f);

        Gizmos.DrawSphere(transform.position, 0.4f);

        Vector3 tip = transform.position + transform.forward * 1.5f;
        Gizmos.DrawLine(transform.position, tip);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.4f + transform.right * 0.3f);
        Gizmos.DrawLine(tip, tip - transform.forward * 0.4f - transform.right * 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
