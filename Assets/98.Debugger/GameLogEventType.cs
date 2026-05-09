public enum GameLogEventType
{
    GameStarted,
    GameOver,
    PhaseChanged,
    RoundStarted,
    RoundCleared,
    EnemySpawned,
    PlayerMoved,
    EnemyMoved,
    ShotFired,
    EnemyTelegraph,
    DamageDealt,
    DamageReceived,
    EnemyDied,
    PlayerDied
}

public static class GameLogContext
{
    public static int CurrentRound
    {
        get
        {
            if (TurnManager.Instance == null)
            {
                return 0;
            }

            return TurnManager.Instance.RoundIndex;
        }
    }

    public static TurnPhase? CurrentPhase
    {
        get
        {
            if (TurnManager.Instance == null)
            {
                return null;
            }

            return TurnManager.Instance.CurrentPhase;
        }
    }
}
