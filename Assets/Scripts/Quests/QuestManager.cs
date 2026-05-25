using System;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Header("Quest")]
    [SerializeField] private string questName = "Kill 10 enemies";
    [SerializeField, Min(1)] private int requiredKills = 10;

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private int _killCount;

    public bool HasActiveQuest => true;
    public bool IsCompleted => _killCount >= requiredKills;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        requiredKills = Mathf.Max(1, requiredKills);
        _killCount = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterEnemyKilled()
    {
        if (IsCompleted)
            return;

        _killCount = Mathf.Min(_killCount + 1, requiredKills);
    }

    public string GetQuestDisplayText()
    {
        string status = _killCount + " / " + requiredKills;
        string completeSuffix = IsCompleted ? " (Complete)" : string.Empty;

        return "> " + questName + "\n> Progress: " + status + completeSuffix;
    }
}