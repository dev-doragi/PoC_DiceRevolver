using UnityEngine;

/// <summary>
/// 매니저 인스턴스 보장과 초기화 순서를 중앙에서 제어합니다.
/// </summary>
[DefaultExecutionOrder(-200)]
public class Bootstrapper : MonoBehaviour
{
    [Header("Strict Validation")]
    [SerializeField] private bool _strictMode = true;

    [Header("Global Managers (DDOL)")]
    [SerializeField] private InputReader _inputReaderPrefab;
    [SerializeField] private GameManager _gameManagerPrefab;
    [SerializeField] private GameFlowManager _gameFlowManagerPrefab;
    [SerializeField] private SceneLoader _sceneLoaderPrefab;
    [SerializeField] private PauseManager _pauseManagerPrefab;
    [SerializeField] private SoundManager _soundManagerPrefab;


    [Header("Scene Specific Managers (Non-DDOL)")]
    [SerializeField] private CameraManager _cameraManagerPrefab;
    [SerializeField] private UIManager _uiManagerPrefab;
    [SerializeField] private PoolManager _poolManagerPrefab;

    [Header("PoC Managers (Non-DDOL)")]
    [SerializeField] private GameModeManager _uiGameModeManagerPrefab;
    [SerializeField] private DiceModeManager _pocGameModeManagerPrefab;
    [SerializeField] private PlayerManager _playerManagerPrefab;
    [SerializeField] private GridManager _pocGridManagerPrefab;
    [SerializeField] private WaveManager _pocWaveManagerPrefab;
    [SerializeField] private CylinderSystem _pocCylinderSystemPrefab;
    [SerializeField] private TurnManager _pocTurnManagerPrefab;

    private void Awake()
    {
        ValidateRequiredPrefabs();

        EnsureInstance(_inputReaderPrefab);
        EnsureInstance(_gameManagerPrefab);
        EnsureInstance(_gameFlowManagerPrefab);
        EnsureInstance(_sceneLoaderPrefab);
        EnsureInstance(_pauseManagerPrefab);
        EnsureInstance(_soundManagerPrefab);

        // Scene scope
        EnsureInstance(_cameraManagerPrefab);
        EnsureInstance(_uiManagerPrefab);
        EnsureInstance(_poolManagerPrefab);

        EnsureModeManagerInstances();
        EnsureInstance(_playerManagerPrefab);

        // PoC scope - no prefab instantiation needed, use Instance
        // PoC managers are instantiated via their prefabs in scene or manually
    }

    private void Start()
    {
        InitializeLogic();
    }

    private void InitializeLogic()
    {
        BootstrapOrThrow(InputReader.Instance, nameof(InputReader));
        BootstrapOrThrow(SceneLoader.Instance, nameof(SceneLoader));

        BootstrapOrThrow(GameManager.Instance, nameof(GameManager));
        BootstrapOrThrow(GameFlowManager.Instance, nameof(GameFlowManager));

        BootstrapOrThrow(CameraManager.Instance, nameof(CameraManager));
        BootstrapOrThrow(PauseManager.Instance, nameof(PauseManager));

        BootstrapOrThrow(PoolManager.Instance, nameof(PoolManager));
        BootstrapOrThrow(UIManager.Instance, nameof(UIManager));
        BootstrapOrThrow(SoundManager.Instance, nameof(SoundManager));

        GameModeManager uiGameModeManager = FindAnyObjectByType<GameModeManager>();
        if (uiGameModeManager != null)
        {
            BootstrapOrThrow(uiGameModeManager, nameof(GameModeManager));
        }

        DiceModeManager pocGameModeManager = FindAnyObjectByType<DiceModeManager>();
        if (pocGameModeManager != null)
        {
            BootstrapOrThrow(pocGameModeManager, nameof(DiceModeManager));
        }

        BootstrapOrThrow(PlayerManager.Instance, nameof(PlayerManager));
        BootstrapOrThrow(GridManager.Instance, nameof(GridManager));
        BootstrapOrThrow(WaveManager.Instance, nameof(WaveManager));
        BootstrapOrThrow(TurnManager.Instance, nameof(TurnManager));
        BootstrapOrThrow(CylinderSystem.Instance, nameof(CylinderSystem));

        Debug.Log("<color=green>[Bootstrapper]</color> manager initialization completed.");
    }

    private void ValidateRequiredPrefabs()
    {
        if (!_strictMode)
        {
            return;
        }

        ValidateRequiredPrefab(_inputReaderPrefab, nameof(_inputReaderPrefab));
        ValidateRequiredPrefab(_gameManagerPrefab, nameof(_gameManagerPrefab));
        ValidateRequiredPrefab(_gameFlowManagerPrefab, nameof(_gameFlowManagerPrefab));
        ValidateRequiredPrefab(_sceneLoaderPrefab, nameof(_sceneLoaderPrefab));
        ValidateRequiredPrefab(_pauseManagerPrefab, nameof(_pauseManagerPrefab));
        ValidateRequiredPrefab(_soundManagerPrefab, nameof(_soundManagerPrefab));
        ValidateRequiredPrefab(_poolManagerPrefab, nameof(_poolManagerPrefab));
    }

    private void ValidateRequiredPrefab(Object prefab, string fieldName)
    {
        if (prefab == null)
        {
            Debug.LogError($"[Bootstrapper] Required prefab is missing: {fieldName}", this);
        }
    }

    private void EnsureInstance<T>(T prefab) where T : MonoBehaviour
    {
        if (prefab == null) return;
        if (FindAnyObjectByType<T>() != null) return;
        Instantiate(prefab);
    }

    private void EnsureModeManagerInstances()
    {
        EnsureInstanceWithFallback(_uiGameModeManagerPrefab, "GameModeManager_UI");
        EnsureInstanceWithFallback(_pocGameModeManagerPrefab, "GameModeManager_PoC");
    }

    private void BootstrapOrThrow<T>(T manager, string managerName) where T : MonoBehaviour
    {
        if (manager == null)
        {
            Debug.LogError($"[Bootstrapper] {managerName} instance is null");
            throw new System.InvalidOperationException($"[Bootstrapper] {managerName} instance is null");
        }

        if (manager is not ISingletonBootstrap bootstrap)
        {
            Debug.LogError($"[Bootstrapper] {managerName} does not implement ISingletonBootstrap");
            throw new System.InvalidOperationException($"[Bootstrapper] {managerName} does not implement ISingletonBootstrap");
        }

        bootstrap.BootstrapIfNeeded();

        if (!bootstrap.IsBootstrapped)
        {
            Debug.LogError($"[Bootstrapper] {managerName} bootstrap failed");
            throw new System.InvalidOperationException($"[Bootstrapper] {managerName} bootstrap failed");
        }
    }

    private void EnsureInstanceWithFallback<T>(T prefab, string fallbackObjectName) where T : MonoBehaviour
    {
        if (FindAnyObjectByType<T>() != null)
        {
            return;
        }

        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        GameObject fallbackObject = new GameObject(fallbackObjectName);
        fallbackObject.AddComponent<T>();
    }
}