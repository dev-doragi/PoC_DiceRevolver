using UnityEngine;

public enum DiceMode
{
    Standard,
    Gatling,
    APPumping,
    RussianRoulette,
    BlackjackCylinder
}

public class DiceModeManager : Singleton<DiceModeManager>
{
    [SerializeField] private DiceMode _currentMode = DiceMode.Standard;

    public DiceMode CurrentMode => _currentMode;

    protected override void Awake()
    {
        _isDontDestroyOnLoad = true;
        base.Awake();
    }

    protected override void OnBootstrap()
    {
        EventBus.Instance?.Publish(new DiceModeChangedEvent(_currentMode));
    }

    private void OnEnable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Subscribe<ReloadRequestedEvent>(OnReloadRequested);
    }

    private void OnDisable()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Unsubscribe<ReloadRequestedEvent>(OnReloadRequested);
    }

    public void SetMode(DiceMode mode)
    {
        _currentMode = mode;
        EventBus.Instance?.Publish(new DiceModeChangedEvent(mode));
        EventBus.Instance?.Publish(new ReloadRequestedEvent(mode));
    }

    private void OnReloadRequested(ReloadRequestedEvent evt)
    {
        if (evt.NewMode is DiceMode diceMode)
        {
            _currentMode = diceMode;
            EventBus.Instance?.Publish(new DiceModeChangedEvent(diceMode));
        }
    }
}
