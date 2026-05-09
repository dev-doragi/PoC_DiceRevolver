using UnityEngine;

/// <summary>
/// ???ÅÌÉú/?ÖÎ†•/?§Îîî???êÎ¶Ñ?êÏÑú ?¨Ïö©?òÎäî Í≥µÌÜµ ?¥Î≤§???ïÏùò?ÖÎãà??
/// </summary>
public struct StageLoadedEvent
{
    public int StageIndex;
}

public struct WaveStartedEvent
{
    public int StageIndex;
    public int WaveIndex;
}

public struct WaveEndedEvent
{
    public int StageIndex;
    public int WaveIndex;
    public bool IsWin;
    public bool IsFinalWave;
}

public struct StageClearedEvent
{
    public int StageIndex;
    public bool IsFinalStage;
}

public struct StageFailedEvent
{
    public int StageIndex;
}

public struct GameStateChangedEvent
{
    public GameState NewState;
}

public struct InGameStateChangedEvent
{
    public InGameState NewState;
}

public struct PlaySFXEvent
{
    public AudioClip Clip;
    public float Volume;
}

public struct ClickEvent
{
    public bool IsStarted;
}

public struct RightClickEvent
{
    public bool IsStarted;
}

public struct RotateEvent { }

public struct ScrollEvent
{
    public float Delta;
}

public struct PausePressedEvent { }

public struct WaveWaitInterruptedEvent { }

public struct WaveWaitTimerTickEvent
{
    public float RemainingTime;
}

public struct TutorialCompletedEvent
{
    public int RewardStageIndex;
}

public struct CameraManipulationEvent { }

// PoC Events
public struct RoundStartedEvent
{
    public int RoundIndex;
}

public struct PlayerTurnStartedEvent { }

public struct PlayerMovedEvent
{
    public Vector2Int NewPosition;
    public Vector2Int Facing;
    public int TopFace;
}

public struct EnemyTurnStartedEvent { }

public struct EnemyMovedEvent
{
    public Vector2Int EnemyPosition;
}

public struct EnemyAttackedEvent
{
    public Vector2Int EnemyPosition;
}

public struct EnemyDamagedEvent
{
    public Vector2Int EnemyPosition;
    public int Damage;
}

public struct EnemyDiedEvent
{
    public Vector2Int EnemyPosition;
}

public struct RoundClearedEvent
{
    public int RoundIndex;
}

public struct CylinderRotatedEvent
{
    public int NewFirePointer;
    public int NewLoadPointer;
}

public struct CylinderLoadedEvent
{
    public int ChamberIndex;
    public int BulletType;
}

public struct CylinderFiredEvent
{
    public int ChamberIndex;
    public int BulletType;
}

public struct ShotFiredEvent
{
    public Vector2Int Origin;
    public Vector2Int Direction;
    public int BulletType;
    public GridManager.LaserLogicResult LogicResult;
    public System.Collections.Generic.List<Vector3> PathPoints;
}

public struct RicochetTrajectoryPreviewEvent
{
    public bool IsActive;                   
    public Vector2Int Origin;
    public Vector2Int Direction;
    public GridManager.LaserLogicResult LogicResult;
    public System.Collections.Generic.List<Vector3> PathPoints;
}

public struct PlayerAPChangedEvent
{
    public int CurrentAP;
}

public struct CylinderDryFiredEvent
{
    public int ChamberIndex;
}

public struct TileOverheatedEvent
{
    public Vector2Int Cell;
}

public struct TileCooledEvent
{
    public Vector2Int Cell;
}

public struct EnemiesSpawnedEvent
{
    public System.Collections.Generic.List<Vector2Int> SpawnPositions;
}

public struct PlayerDamagedEvent
{
    public int Damage;
    public int RemainingHp;
}

public struct GameOverEvent { }

public struct PlayerDiedEvent
{
    public Vector2Int Position;
}

public struct TileHoverEvent
{
    public Vector2Int Cell;
    public int PredictedTopFace;
}

public struct MoveGhostEvent
{
    public Vector2Int TargetCell;
    public int PredictedTopFace;
    public bool IsConfirmRequired;
}

public struct PhaseChangedEvent
{
    public TurnPhase Phase;
}

public struct EnemyTelegraphEvent
{
    public Vector2Int EnemyPosition;
    public Vector2Int TargetCell;
    public bool IsActive;
}

public struct CylinderStateChangedEvent
{
    public int?[] Chambers;
    public int FirePointer;
    public int LoadPointer;
}

// PoC ?ÖÎ†• ?¥Î≤§??
public struct MoveUpPressedEvent { }
public struct MoveDownPressedEvent { }
public struct MoveLeftPressedEvent { }
public struct MoveRightPressedEvent { }
public struct FirePressedEvent { }


public struct PlayerTurnEndedEvent { }
public struct OnVisualsCompletedEvent { }
public struct StartEnemyTurnEvent { }
public struct EnemyTurnCompletedEvent { }
public struct AllEnemiesDefeatedEvent { }

