using UnityEngine;

[RequireComponent(
    typeof(BoxCollider))]
public class RECheckpoint : MonoBehaviour
{
    public int number;

    private void Start()
    {
        RECheckpointManager.RegisterCheckpoint(
            this);
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (number == RECheckpointManager.GetCurrentCheckpoint() + 1)
            RERace.Instance.CheckpointManager.ProgressCheckpoints();
    }
}