using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public class GameCsvLogger : MonoBehaviour
{
    [Header("Output")]
    [SerializeField] private bool _enableLogging = true;
    [SerializeField] private string _logDirectoryName = "Logs";
    [SerializeField] private string _filePrefix = "poc_log";

    private readonly ConcurrentQueue<string> _pendingLines = new ConcurrentQueue<string>();
    private readonly AutoResetEvent _flushSignal = new AutoResetEvent(false);

    private Thread _writerThread;
    private volatile bool _isRunning;
    private string _logFilePath;
    private TurnPhase _previousPhase;
    private bool _hasPreviousPhase;

    private void OnEnable()
    {
        if (!_enableLogging)
        {
            return;
        }

        StartWriter();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        if (!_enableLogging)
        {
            return;
        }

        UnsubscribeEvents();
        StopWriter();
    }

    private void StartWriter()
    {
        string executableDirectory = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(executableDirectory))
        {
            Debug.LogError("[GameCsvLogger] Failed to resolve executable directory. Fallback to persistentDataPath.");
            executableDirectory = Application.persistentDataPath;
        }

        string basePath = Path.Combine(executableDirectory, _logDirectoryName);
        Directory.CreateDirectory(basePath);

        string fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _logFilePath = Path.Combine(basePath, fileName);

        _isRunning = true;
        _writerThread = new Thread(WriterLoop)
        {
            IsBackground = true,
            Name = "GameCsvLoggerWriter"
        };
        _writerThread.Start();

        EnqueueLine("TimestampUtc,EventType,Round,Phase,ActorId,ActorName,TargetId,TargetName,GridX,GridY,Metadata");
        Debug.Log($"[GameCsvLogger] Logging started: {_logFilePath}");
    }

    private void StopWriter()
    {
        _isRunning = false;
        _flushSignal.Set();

        if (_writerThread != null)
        {
            _writerThread.Join(1000);
            _writerThread = null;
        }

        while (_pendingLines.TryDequeue(out _))
        {
        }

        Debug.Log("[GameCsvLogger] Logging stopped.");
    }

    private void WriterLoop()
    {
        try
        {
            using (var stream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                while (_isRunning || !_pendingLines.IsEmpty)
                {
                    bool wroteAny = false;
                    while (_pendingLines.TryDequeue(out string line))
                    {
                        writer.WriteLine(line);
                        wroteAny = true;
                    }

                    if (wroteAny)
                    {
                        writer.Flush();
                    }

                    if (_isRunning)
                    {
                        _flushSignal.WaitOne(50);
                    }
                }

                writer.Flush();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameCsvLogger] WriterLoop failed: {ex}");
        }
    }

    private void SubscribeEvents()
    {
        EventBus.Instance.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Subscribe<PhaseChangedEvent>(OnPhaseChanged);
        EventBus.Instance.Subscribe<RoundStartedEvent>(OnRoundStarted);
        EventBus.Instance.Subscribe<RoundClearedEvent>(OnRoundCleared);
        EventBus.Instance.Subscribe<EnemiesSpawnedEvent>(OnEnemiesSpawned);
        EventBus.Instance.Subscribe<PlayerMovedEvent>(OnPlayerMoved);
        EventBus.Instance.Subscribe<EnemyMovedEvent>(OnEnemyMoved);
        EventBus.Instance.Subscribe<ShotFiredEvent>(OnShotFired);
        EventBus.Instance.Subscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
        EventBus.Instance.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Instance.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Instance.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Instance.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void UnsubscribeEvents()
    {
        EventBus.Instance.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        EventBus.Instance.Unsubscribe<PhaseChangedEvent>(OnPhaseChanged);
        EventBus.Instance.Unsubscribe<RoundStartedEvent>(OnRoundStarted);
        EventBus.Instance.Unsubscribe<RoundClearedEvent>(OnRoundCleared);
        EventBus.Instance.Unsubscribe<EnemiesSpawnedEvent>(OnEnemiesSpawned);
        EventBus.Instance.Unsubscribe<PlayerMovedEvent>(OnPlayerMoved);
        EventBus.Instance.Unsubscribe<EnemyMovedEvent>(OnEnemyMoved);
        EventBus.Instance.Unsubscribe<ShotFiredEvent>(OnShotFired);
        EventBus.Instance.Unsubscribe<EnemyTelegraphEvent>(OnEnemyTelegraph);
        EventBus.Instance.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        EventBus.Instance.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        EventBus.Instance.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        EventBus.Instance.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnGameStateChanged(GameStateChangedEvent e)
    {
        if (e.NewState == GameState.Playing)
        {
            LogEvent(GameLogEventType.GameStarted, default, default, -1, -1, null);
            return;
        }

        if (e.NewState == GameState.GameOver)
        {
            LogEvent(GameLogEventType.GameOver, default, default, -1, -1, null);
        }
    }

    private void OnPhaseChanged(PhaseChangedEvent e)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "previous_phase", _hasPreviousPhase ? _previousPhase.ToString() : "Unknown" },
            { "new_phase", e.Phase.ToString() },
            { "round_index", GameLogContext.CurrentRound }
        };

        _previousPhase = e.Phase;
        _hasPreviousPhase = true;

        LogEvent(GameLogEventType.PhaseChanged, default, default, -1, -1, metadata);
    }

    private void OnRoundStarted(RoundStartedEvent e)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "round_index", e.RoundIndex },
            { "phase", TurnManager.Instance != null ? TurnManager.Instance.CurrentPhase.ToString() : "Unknown" }
        };

        LogEvent(GameLogEventType.RoundStarted, default, default, -1, -1, metadata);
    }

    private void OnRoundCleared(RoundClearedEvent e)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "round_index", e.RoundIndex },
            { "phase", TurnManager.Instance != null ? TurnManager.Instance.CurrentPhase.ToString() : "Unknown" }
        };

        LogEvent(GameLogEventType.RoundCleared, default, default, -1, -1, metadata);
    }

    private void OnEnemiesSpawned(EnemiesSpawnedEvent e)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "spawn_count", e.SpawnPositions != null ? e.SpawnPositions.Count : 0 },
            { "spawn_positions", SerializeVector2IntList(e.SpawnPositions) }
        };

        LogEvent(GameLogEventType.EnemySpawned, default, default, -1, -1, metadata);
    }

    private void OnPlayerMoved(PlayerMovedEvent e)
    {
        EntitySnapshot actor = BuildEntitySnapshot(PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null
            ? PlayerManager.Instance.PlayerTransform.gameObject
            : null);
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "top_face", e.TopFace },
            { "facing", SerializeVector2Int(e.Facing) }
        };

        LogEvent(GameLogEventType.PlayerMoved, actor, default, e.NewPosition.x, e.NewPosition.y, metadata);
    }

    private void OnEnemyMoved(EnemyMovedEvent e)
    {
        EntitySnapshot actor = default;
        if (GridManager.Instance != null)
        {
            MonoBehaviour enemy = GridManager.Instance.GetOccupant(e.EnemyPosition);
            actor = BuildEntitySnapshot(enemy != null ? enemy.gameObject : null);
        }

        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "enemy_position", SerializeVector2Int(e.EnemyPosition) }
        };

        LogEvent(GameLogEventType.EnemyMoved, actor, default, e.EnemyPosition.x, e.EnemyPosition.y, metadata);
    }

    private void OnShotFired(ShotFiredEvent e)
    {
        EntitySnapshot actor = BuildEntitySnapshot(PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null
            ? PlayerManager.Instance.PlayerTransform.gameObject
            : null);
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "bullet_type", e.BulletType },
            { "direction", SerializeVector2Int(e.Direction) }
        };

        LogEvent(GameLogEventType.ShotFired, actor, default, e.Origin.x, e.Origin.y, metadata);
    }

    private void OnEnemyTelegraph(EnemyTelegraphEvent e)
    {
        EntitySnapshot actor = default;
        if (GridManager.Instance != null)
        {
            MonoBehaviour enemy = GridManager.Instance.GetOccupant(e.EnemyPosition);
            actor = BuildEntitySnapshot(enemy != null ? enemy.gameObject : null);
        }

        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "is_active", e.IsActive },
            { "target_cell", SerializeVector2Int(e.TargetCell) }
        };

        LogEvent(GameLogEventType.EnemyTelegraph, actor, default, e.EnemyPosition.x, e.EnemyPosition.y, metadata);
    }

    private void OnEnemyDamaged(EnemyDamagedEvent e)
    {
        EntitySnapshot target = default;
        if (GridManager.Instance != null)
        {
            MonoBehaviour enemy = GridManager.Instance.GetOccupant(e.EnemyPosition);
            target = BuildEntitySnapshot(enemy != null ? enemy.gameObject : null);
        }

        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "damage", e.Damage }
        };

        LogEvent(GameLogEventType.DamageDealt, default, target, e.EnemyPosition.x, e.EnemyPosition.y, metadata);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e)
    {
        EntitySnapshot actor = BuildEntitySnapshot(PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null
            ? PlayerManager.Instance.PlayerTransform.gameObject
            : null);
        Vector2Int playerPos = TurnManager.Instance != null ? TurnManager.Instance.PlayerGridPosition : new Vector2Int(-1, -1);

        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "damage", e.Damage },
            { "remaining_hp", e.RemainingHp }
        };

        LogEvent(GameLogEventType.DamageReceived, actor, default, playerPos.x, playerPos.y, metadata);
    }

    private void OnEnemyDied(EnemyDiedEvent e)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>
        {
            { "enemy_position", SerializeVector2Int(e.EnemyPosition) }
        };

        LogEvent(GameLogEventType.EnemyDied, default, default, e.EnemyPosition.x, e.EnemyPosition.y, metadata);
    }

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        EntitySnapshot actor = BuildEntitySnapshot(PlayerManager.Instance != null && PlayerManager.Instance.PlayerTransform != null
            ? PlayerManager.Instance.PlayerTransform.gameObject
            : null);
        LogEvent(GameLogEventType.PlayerDied, actor, default, e.Position.x, e.Position.y, null);
    }

    private void LogEvent(
        GameLogEventType eventType,
        EntitySnapshot actor,
        EntitySnapshot target,
        int gridX,
        int gridY,
        Dictionary<string, object> metadata)
    {
        string timestamp = DateTime.UtcNow.ToString("O");
        int round = GameLogContext.CurrentRound;
        string phase = GameLogContext.CurrentPhase.HasValue ? GameLogContext.CurrentPhase.Value.ToString() : "Unknown";
        string serializedMetadata = SerializeMetadata(metadata);

        string line = string.Join(",",
            EscapeCsv(timestamp),
            EscapeCsv(eventType.ToString()),
            EscapeCsv(round.ToString()),
            EscapeCsv(phase),
            EscapeCsv(actor.EntityId),
            EscapeCsv(actor.EntityName),
            EscapeCsv(target.EntityId),
            EscapeCsv(target.EntityName),
            EscapeCsv(gridX.ToString()),
            EscapeCsv(gridY.ToString()),
            EscapeCsv(serializedMetadata));

        EnqueueLine(line);
    }

    private void EnqueueLine(string line)
    {
        _pendingLines.Enqueue(line);
        _flushSignal.Set();
    }

    private EntitySnapshot BuildEntitySnapshot(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return default;
        }

        PlayerController player = gameObject.GetComponent<PlayerController>();
        if (player != null)
        {
            return new EntitySnapshot("Player", gameObject.name);
        }

        EnemyController enemy = gameObject.GetComponent<EnemyController>();
        if (enemy != null)
        {
            return new EntitySnapshot($"Enemy_{gameObject.GetInstanceID()}", gameObject.name);
        }

        TileView tile = gameObject.GetComponent<TileView>();
        if (tile != null)
        {
            return new EntitySnapshot($"Tile_{tile.Cell.x}_{tile.Cell.y}", gameObject.name);
        }

        return new EntitySnapshot(gameObject.GetInstanceID().ToString(), gameObject.name);
    }

    private static string SerializeMetadata(Dictionary<string, object> metadata)
    {
        if (metadata == null || metadata.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        bool isFirst = true;

        foreach (KeyValuePair<string, object> pair in metadata)
        {
            if (!isFirst)
            {
                builder.Append(';');
            }

            builder.Append(pair.Key);
            builder.Append('=');
            builder.Append(pair.Value);
            isFirst = false;
        }

        return builder.ToString();
    }

    private static string SerializeVector2Int(Vector2Int value)
    {
        return $"({value.x},{value.y})";
    }

    private static string SerializeVector2IntList(List<Vector2Int> values)
    {
        if (values == null || values.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('|');
            }

            Vector2Int value = values[i];
            builder.Append('(')
                .Append(value.x)
                .Append(',')
                .Append(value.y)
                .Append(')');
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string value)
    {
        string safe = value ?? string.Empty;
        if (safe.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }

        return safe;
    }

    private readonly struct EntitySnapshot
    {
        public readonly string EntityId;
        public readonly string EntityName;

        public EntitySnapshot(string entityId, string entityName)
        {
            EntityId = entityId ?? string.Empty;
            EntityName = entityName ?? string.Empty;
        }
    }
}
