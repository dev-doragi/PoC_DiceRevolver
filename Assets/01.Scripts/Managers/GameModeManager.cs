using UnityEngine;
using PocDiceTactics;

namespace PocDiceTactics.ModeSelection
{
    [DefaultExecutionOrder(-100)]
    public class GameModeManager : Singleton<GameModeManager>
    {
        public GameMode CurrentMode { get; private set; } = GameMode.Normal;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false;
            base.Awake();
        }

        protected override void OnBootstrap()
        {
        }

        private void OnEnable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            }
        }

        private void OnDisable()
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            }
        }

        private void OnGameModeChanged(GameModeChangedEvent evt)
        {
            CurrentMode = evt.NewMode;
            ApplyMode(CurrentMode);
        }

        private void ApplyMode(GameMode mode)
        {
            BulletUIData[] loadedData = Resources.LoadAll<BulletUIData>("05.Data/Bullets");
            if (loadedData == null || loadedData.Length == 0)
            {
                Debug.LogError($"[GameModeManager] Mode switch failed due to data load error: {mode}");
                return;
            }

            switch (mode)
            {
                case GameMode.Normal:
                case GameMode.Hard:
                case GameMode.Sandbox:
                case GameMode.GimmickTest:
                default:
                    break;
            }
        }
    }
}
