using UnityEngine;

/// <summary>
/// 매니저 인스턴스 보장과 초기화 순서를 중앙에서 제어합니다.
/// </summary>
[DefaultExecutionOrder(-500)]
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
    [SerializeField] private PocDiceTactics.GridManager _pocGridManagerPrefab;
    [SerializeField] private PocDiceTactics.WaveManager _pocWaveManagerPrefab;
    [SerializeField] private PocDiceTactics.PlayerController _pocPlayerControllerPrefab;
    [SerializeField] private PocDiceTactics.CylinderSystem _pocCylinderSystemPrefab;
    [SerializeField] private PocDiceTactics.TurnManager _pocTurnManagerPrefab;

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

        // PoC scope - no prefab instantiation needed, use Instance
        // PoC managers are instantiated via their prefabs in scene or manually
    }

    private void Start()
    {
        InitializeLogic();
    }

    private void InitializeLogic()
    {
        // Phase 1: CoreData
        if (InputReader.Instance != null) InputReader.Instance.BootstrapIfNeeded();
        if (SceneLoader.Instance != null) SceneLoader.Instance.BootstrapIfNeeded();

        // Phase 2: CoreState
        if (GameManager.Instance != null) GameManager.Instance.BootstrapIfNeeded();
        if (GameFlowManager.Instance != null) GameFlowManager.Instance.BootstrapIfNeeded();

        // Phase 3: World
        if (CameraManager.Instance != null) CameraManager.Instance.BootstrapIfNeeded();
        if (PauseManager.Instance != null) PauseManager.Instance.BootstrapIfNeeded();

        // Phase 4: Presentation
        if (PoolManager.Instance != null) PoolManager.Instance.BootstrapIfNeeded();
        if (UIManager.Instance != null) UIManager.Instance.BootstrapIfNeeded();
        if (SoundManager.Instance != null) SoundManager.Instance.BootstrapIfNeeded();

        // Phase 5: PoC Managers
        if (PocDiceTactics.GridManager.Instance != null) PocDiceTactics.GridManager.Instance.BootstrapIfNeeded();
        if (PocDiceTactics.WaveManager.Instance != null) PocDiceTactics.WaveManager.Instance.BootstrapIfNeeded();
        if (PocDiceTactics.PlayerController.Instance != null) PocDiceTactics.PlayerController.Instance.BootstrapIfNeeded();
        if (PocDiceTactics.CylinderSystem.Instance != null) PocDiceTactics.CylinderSystem.Instance.BootstrapIfNeeded();
        if (PocDiceTactics.TurnManager.Instance != null) PocDiceTactics.TurnManager.Instance.BootstrapIfNeeded();

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
}