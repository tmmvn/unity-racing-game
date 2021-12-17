using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RERace : MonoBehaviour
{
    public int laps;
    public int raceCountdown;

    public Color activeColor;
    public Color inactiveColor;

    public RectTransform raceDonePanel;
    public Text raceTime;

    private static int _currentLap;
    private float _currentRaceTime;
    private int _checkPointsDone;

    public RECheckpointManager CheckpointManager;

    public GameObject[] driveEffectPrefabs;
    public GameObject[] slipEffectPrefabs;

    private Queue<GameObject>[] _driveEffectPool;
    private Queue<GameObject>[] _slipEffectPool;
    private Queue<GameObject>[] _driveEffectReturnPool;
    private Queue<GameObject>[] _slipEffectReturnPool;
    public static RERace Instance { get; private set; }

    public AudioClip launch;
    public AudioClip countdown;

    public bool dark;
    public bool sprint;

    public static bool IsRacing()
    {
        return _currentLap > 0;
    }

    private void Awake()
    {
        Instance = this;
        _currentLap = -1;
        CheckpointManager = new RECheckpointManager();

        _driveEffectPool = new Queue<GameObject>[driveEffectPrefabs.Length];
        for (var i = 0; i < _driveEffectPool.Length; i++)
        {
            _driveEffectPool[i] = new Queue<GameObject>();
            for (var wheel = 1; wheel <= 4; wheel++)
            {
                var driveEffect = Instantiate(
                    driveEffectPrefabs[i],
                    Vector3.zero,
                    Quaternion.identity
                );
                driveEffect.SetActive(false);
                _driveEffectPool[i].Enqueue(driveEffect);
            }
        }

        _driveEffectReturnPool = new Queue<GameObject>[driveEffectPrefabs.Length];
        for (var i = 0; i < _driveEffectReturnPool.Length; i++)
        {
            _driveEffectReturnPool[i] = new Queue<GameObject>();
        }

        _slipEffectPool = new Queue<GameObject>[slipEffectPrefabs.Length];
        for (var i = 0; i < _slipEffectPool.Length; i++)
        {
            _slipEffectPool[i] = new Queue<GameObject>();
            for (var wheel = 1; wheel <= 4; wheel++)
            {
                var slipEffect = Instantiate(
                    slipEffectPrefabs[i],
                    Vector3.zero,
                    Quaternion.identity
                );
                slipEffect.SetActive(false);
                _slipEffectPool[i].Enqueue(slipEffect);
            }
        }
        
        _slipEffectReturnPool = new Queue<GameObject>[slipEffectPrefabs.Length];
        for (var i = 0; i < _slipEffectReturnPool.Length; i++)
        {
            _slipEffectReturnPool[i] = new Queue<GameObject>();
        }
    }

    public void ReturnDriveEffect(int terrainType, GameObject driveEffect)
    {
        var effectSystem = driveEffect.GetComponent<ParticleSystem>();
        if (effectSystem != null)
        {
            effectSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _driveEffectReturnPool[terrainType].Enqueue(driveEffect);
        }
        else
        {
            driveEffect.transform.SetParent(null);
            driveEffect.transform.localScale = Vector3.one;
            driveEffect.SetActive(false);
            _driveEffectPool[terrainType].Enqueue(driveEffect);
        }
    }

    public void ReturnSlipEffect(int terrainType, GameObject slipEffect)
    {
        var effectSystem = slipEffect.GetComponent<ParticleSystem>();
        if (effectSystem != null)
        {
            effectSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _slipEffectReturnPool[terrainType].Enqueue(slipEffect);
        }
        else
        {
            slipEffect.transform.SetParent(null);
            slipEffect.transform.localScale = Vector3.one;
            slipEffect.SetActive(false);
            _driveEffectPool[terrainType].Enqueue(slipEffect);
        }
    }

    public GameObject GetDriveEffect(int terrainType)
    {
        GameObject effect;

        if (_driveEffectReturnPool[terrainType].Count > 0)
        {
            effect = _driveEffectReturnPool[terrainType].Dequeue();
            var effectSystem = effect.GetComponent<ParticleSystem>();
            if(effectSystem != null)
                effectSystem.Play(true);
        }
        else
        {
            effect = _driveEffectPool[terrainType].Dequeue();
            effect.SetActive(true);    
        }

        return effect;
    }

    public GameObject GetSlipEffect(int terrainType)
    {
        GameObject effect;

        if (_slipEffectReturnPool[terrainType].Count > 0)
        {
            effect = _slipEffectReturnPool[terrainType].Dequeue();
            var effectSystem = effect.GetComponent<ParticleSystem>();
            if(effectSystem != null)
                effectSystem.Play(true);
        }
        else
        {
            effect = _slipEffectPool[terrainType].Dequeue();
            effect.SetActive(true);    
        }

        return effect;
    }

    private void Start()
    {
        GetComponent<FadeToBlack>().FadeOut();
        CheckpointManager.ActiveColor = activeColor;
        CheckpointManager.InactiveColor = inactiveColor;
        CheckpointManager.PartCompleted += CheckpointDone;
        CheckpointManager.Completed += CheckpointsDone;
        CheckpointManager.Reset();
        StartCoroutine(
            "RaceCountDown");
    }

    private IEnumerator RaceCountDown()
    {
        var audioEffect = GetComponent<AudioSource>();
        audioEffect.clip = countdown;
        audioEffect.Play();
        for (var i = 0; i < raceCountdown; i++)
        {
            var message = new ticker.TickerMessage
            {
                TimeShown = 1.0f, Message = "Race begins in... " + (raceCountdown - i)
            };
            ticker.Instance.PushMessage(
                message);
        }

        yield return new WaitForSeconds(
            raceCountdown);
        StartRace();
    }

    private void CheckpointDone()
    {
        var t = TimeSpan.FromSeconds(_currentRaceTime);

        var timestamp = string.Format("{0:D2}:{1:D2}:{2:D3}",
            t.Minutes,
            t.Seconds,
            t.Milliseconds);
        var message = new ticker.TickerMessage
        {
            TimeShown = 2.0f, Message = "Checkpoint: " + timestamp
        };
        ticker.Instance.PushMessage(
            message);
        _checkPointsDone++;
        if (!sprint)
            return;

        if (_checkPointsDone >= CheckpointManager.NumberOfCheckpoints())
        {
            RaceDone();
        }
    }

    private void CheckpointsDone()
    {
        if (_currentLap < laps && !sprint)
        {
            _currentLap++;
            var t = TimeSpan.FromSeconds(_currentRaceTime);

            var timestamp = string.Format("{0:D2}:{1:D2}:{2:D3}",
                t.Minutes,
                t.Seconds,
                t.Milliseconds);
            var message = new ticker.TickerMessage
            {
                TimeShown = 2.0f, Message = "Lap time: " + timestamp
            };
            ticker.Instance.PushMessage(
                message);
            return;
        }

        RaceDone();
    }

    private void StartRace()
    {
        var audioEffect = GetComponent<AudioSource>();
        audioEffect.clip = launch;
        audioEffect.Play();
        CheckpointManager.StartRace();
        _currentRaceTime = 0.0f;
        _currentLap = 1;
        GameObject.FindGameObjectWithTag(
            "Player").GetComponentInChildren<REEngine>().SetGearOn();
    }

    private void LateUpdate()
    {
        if (!IsRacing()) return;

        if (Input.GetButton("Y"))
        {
            Respawn();
        }

        var i = -1;
        foreach (var queue in _driveEffectReturnPool)
        {
            i++;
            if(queue.Count == 0)
                continue;

            var effect = queue.Peek();
            var effectSystem = effect.GetComponent<ParticleSystem>();

            if(effectSystem.IsAlive())
                continue;

            effect = queue.Dequeue();
            effect.transform.SetParent(null);
            effect.transform.localScale = Vector3.one;
            effect.SetActive(false);
            _driveEffectPool[i].Enqueue(effect);
        }

        i = -1;
        foreach (var queue in _slipEffectReturnPool)
        {
            i++;
            if(queue.Count == 0)
                continue;

            var effect = queue.Peek();
            var effectSystem = effect.GetComponent<ParticleSystem>();
            if(effectSystem.IsAlive())
                continue;

            effect = queue.Dequeue();
            effect.transform.SetParent(null);
            effect.transform.localScale = Vector3.one;
            effect.SetActive(false);
            _slipEffectPool[i].Enqueue(effect);
        }

        if (_currentLap < 0)
            return;

        _currentRaceTime += Time.deltaTime;
        if (_currentRaceTime < TimeSpan.MaxValue.TotalSeconds)
        {
            return;
        }

        _currentRaceTime = (float) TimeSpan.MaxValue.TotalSeconds;
    }

    private void Respawn()
    {
        var currentCheckpoint = RECheckpointManager.GetCurrentCheckpoint() - 1;

        Vector3 position;
        Quaternion rotation;

        if (currentCheckpoint >= 0)
        {
            position = RECheckpointManager.GetCheckpointPosition(currentCheckpoint);
            rotation = RECheckpointManager.GetCheckpointOrientation(currentCheckpoint);
        }
        else
        {
            position = GameObject.Find("Start").transform.position;
            rotation = GameObject.Find("Start").transform.rotation;
        }

        position.y -= 1.0f;
        var player = GameObject.FindGameObjectWithTag("Player");
        player.transform.position = position;
        player.transform.rotation = rotation;
        player.GetComponent<Rigidbody>().velocity = Vector3.zero;
        player.GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
    }

    private void RaceDone()
    {
        _currentLap = -1;
        var t = TimeSpan.FromSeconds(_currentRaceTime);

        var timestamp = string.Format("{0:D2}:{1:D2}:{2:D3}",
            t.Minutes,
            t.Seconds,
            t.Milliseconds);
        var text = "Race time: " + timestamp;
        var message = new ticker.TickerMessage {TimeShown = 2.0f, Message = text};
        raceTime.text = text;
        ticker.Instance.PushMessage(
            message);
        if (_checkPointsDone == CheckpointManager.NumberOfCheckpoints() * laps)
            MaintainLeaderBoard(t.Minutes * 60 + t.Seconds + t.Milliseconds / 1000);
        CompleteRaceDone();
    }

    private static void MaintainLeaderBoard(long highScore)
    {
        Debug.Log("Maintaining leaderboards.");
        Social.ReportScore(highScore, "yourLeaderboardIDinQuotes", HighScoreCheck);
    }

    private static void HighScoreCheck(bool result)
    {
        if (!result)
            Debug.Log("score submission failed");
    }

    private void CompleteRaceDone()
    {
        raceDonePanel.gameObject.SetActive(true);
    }

    public void ReturnFromRace()
    {
        GetComponent<FadeToBlack>().FadeIn();
        StartCoroutine(
            DoLoad());
    }

    private static IEnumerator DoLoad()
    {
        yield return new WaitForSeconds(0.5f);
        var loadOp = SceneManager.LoadSceneAsync(
            "menu",
            LoadSceneMode.Single);
        loadOp.allowSceneActivation = false;
        while (loadOp.progress < 0.9f)
        {
            yield return null;
        }

        loadOp.allowSceneActivation = true;
    }
}