using System.Collections.Generic;
using UnityEngine;

public class RECheckpointManager
{
    public delegate void CheckPointCompleted();

    public delegate void CheckPointsCompleted();

    private static List<RECheckpoint> _checkpoints;
    private static int _currentCheckpoint;

    public event CheckPointsCompleted Completed;
    public event CheckPointCompleted PartCompleted;

    public Color ActiveColor;
    public Color InactiveColor;

    public RECheckpointManager()
    {
        _checkpoints = new List<RECheckpoint>();
        _currentCheckpoint = -1;
    }

    public void StartRace()
    {
        _currentCheckpoint = 0;
        ChangeColor(
            _currentCheckpoint,
            true);
    }

    public int NumberOfCheckpoints()
    {
        return _checkpoints.Count;
    }

    public static void RegisterCheckpoint(
        RECheckpoint checkpoint)
    {
        _checkpoints.Add(
            checkpoint);
        _checkpoints.Sort(
            (a, b) => a.number.CompareTo(
                b.number));
    }

    public static int GetCurrentCheckpoint()
    {
        return _currentCheckpoint;
    }

    private void ChangeColor(
        int checkpoint,
        bool active)
    {
        var changedColor = active ? ActiveColor : InactiveColor;

        _checkpoints[
            checkpoint
        ].GetComponent<MeshRenderer>().material.color = changedColor;
    }

    public void ProgressCheckpoints()
    {
        ChangeColor(
            _currentCheckpoint,
            false);
        if (_currentCheckpoint < _checkpoints.Count - 1)
        {
            _currentCheckpoint++;
            if (PartCompleted != null)
            {
                PartCompleted();
            }
        }
        else
        {
            if (Completed != null)
                Completed();
            
            _currentCheckpoint = 0;
        }

        ChangeColor(
            _currentCheckpoint,
            true);
    }

    public void Reset()
    {
        for(var i = 0; i<_checkpoints.Count; i++)
        {
            ChangeColor(
                i,
                false);
        }
        _currentCheckpoint = -1;
    }

    public static Vector3 GetCheckpointPosition(int index)
    {
        return _checkpoints[index].transform.position;
    }

    public static Quaternion GetCheckpointOrientation(int index)
    {
        return _checkpoints[index].transform.rotation;
    }
}