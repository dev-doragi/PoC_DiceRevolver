using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerManager : Singleton<PlayerManager>
{
    [SerializeField] private PlayerController _playerPrefab;

    private PlayerController _playerController;

    public Transform PlayerTransform { get; private set; }
    public PlayerMovementSystem Movement { get; private set; }
    public PlayerCombatSystem Combat { get; private set; }
    public PlayerStatusSystem Status { get; private set; }

    protected override void Awake()
    {
        _isDontDestroyOnLoad = true;
        base.Awake();
    }

    public void RegisterPlayer(PlayerController controller)
    {
        if (controller == null)
        {
            Debug.LogError("[PlayerManager] controller is null");
            return;
        }

        _playerController = controller;
        PlayerTransform = controller.transform;

        Movement = controller.GetComponent<PlayerMovementSystem>();
        Combat = controller.GetComponent<PlayerCombatSystem>();
        Status = controller.GetComponent<PlayerStatusSystem>();

        if (Movement == null)
        {
            Debug.LogError("[PlayerManager] PlayerMovementSystem is missing");
            throw new System.InvalidOperationException("[PlayerManager] PlayerMovementSystem is missing");
        }

        if (Combat == null)
        {
            Debug.LogError("[PlayerManager] PlayerCombatSystem is missing");
            throw new System.InvalidOperationException("[PlayerManager] PlayerCombatSystem is missing");
        }

        if (Status == null)
        {
            Debug.LogError("[PlayerManager] PlayerStatusSystem is missing");
            throw new System.InvalidOperationException("[PlayerManager] PlayerStatusSystem is missing");
        }

        // 씬 재시작 시 새 Player 컴포넌트에 대해 반드시 재초기화
        Movement.Initialize(this);
        Combat.Initialize(this);
        Status.Initialize(this);
    }

    protected override void OnBootstrap()
    {
        if (_playerController == null)
        {
            _playerController = FindAnyObjectByType<PlayerController>();
        }

        if (_playerController == null)
        {
            if (_playerPrefab == null)
            {
                Debug.LogError("[PlayerManager] _playerPrefab is null");
                throw new System.InvalidOperationException("[PlayerManager] _playerPrefab is null");
            }

            _playerController = Instantiate(_playerPrefab);
        }

        RegisterPlayer(_playerController);
    }

    public void InitializeCombatContext(TurnManager turnManager, GridManager gridManager, CylinderSystem cylinderSystem, Vector2Int startCell)
    {
        if (turnManager == null)
        {
            Debug.LogError("[PlayerManager] turnManager is null");
            throw new System.InvalidOperationException("[PlayerManager] turnManager is null");
        }

        if (gridManager == null)
        {
            Debug.LogError("[PlayerManager] gridManager is null");
            throw new System.InvalidOperationException("[PlayerManager] gridManager is null");
        }

        if (cylinderSystem == null)
        {
            Debug.LogError("[PlayerManager] cylinderSystem is null");
            throw new System.InvalidOperationException("[PlayerManager] cylinderSystem is null");
        }

        Movement.InitializeCombatContext(turnManager, gridManager, startCell);
        Combat.InitializeCombatContext(turnManager, gridManager, cylinderSystem);
        Status.InitializeCombatContext(turnManager);
    }
}
