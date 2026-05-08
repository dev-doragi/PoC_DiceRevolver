using UnityEngine;

namespace PocDiceTactics
{
    public enum DiceMode
    {
        Standard,
        Gatling,
        APPumping,
        RussianRoulette,
        BlackjackCylinder
    }

    public class GameModeManager : Singleton<GameModeManager>
    {
        [SerializeField] private DiceMode _currentMode = DiceMode.Standard;

        public DiceMode CurrentMode => _currentMode;

        protected override void Awake()
        {
            _isDontDestroyOnLoad = false;
            base.Awake();
        }

        protected override void OnBootstrap()
        {
        }
    }
}
