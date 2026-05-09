using UnityEngine;

public class GimmickPanelController : Singleton<GimmickPanelController>
{
    [SerializeField] private Transform _contentParent;
    [SerializeField] private GameObject _entryPrefab;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false;
            base.Awake();

            if (_contentParent == null || _entryPrefab == null)
            {
                Debug.LogError("[GimmickPanelController] UI Reference Missing!");
                return;
            }

            ClearEntries();
            BuildEntries();
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

        protected override void OnBootstrap()
        {
        }

        private void ClearEntries()
        {
            for (int i = _contentParent.childCount - 1; i >= 0; i--)
            {
                Destroy(_contentParent.GetChild(i).gameObject);
            }
        }

        private void BuildEntries()
        {
            BulletUIData[] bulletDataList = Resources.LoadAll<BulletUIData>("Bullets");
            GameModeManager modeManager = FindAnyObjectByType<GameModeManager>();
            GameMode mode = modeManager != null ? modeManager.CurrentMode : GameMode.Normal;
            for (int i = 0; i < bulletDataList.Length; i++)
            {
                BulletUIData data = bulletDataList[i];
                if (!ShouldInclude(data, mode))
                {
                    continue;
                }

                GameObject go = Instantiate(_entryPrefab, _contentParent);
                GimmickEntry entry = go.GetComponent<GimmickEntry>();
                if (entry == null)
                {
                    Debug.LogError("[GimmickPanelController] GimmickEntry component missing!");
                    continue;
                }

                entry.Setup(data);
            }
        }

        private void OnGameModeChanged(GameModeChangedEvent _)
        {
            if (_contentParent == null || _entryPrefab == null)
            {
                Debug.LogError("[GimmickPanelController] UI Reference Missing!");
                return;
            }

            ClearEntries();
            BuildEntries();
        }

        private bool ShouldInclude(BulletUIData data, GameMode mode)
        {
            if (data == null)
            {
                return false;
            }

            switch (mode)
            {
                case GameMode.Hard:
                    return data.DisplayDamage >= 2;
                case GameMode.GimmickTest:
                    return data.BulletType >= 3;
                case GameMode.Normal:
                case GameMode.Sandbox:
                default:
                    return true;
            }
        }
}
