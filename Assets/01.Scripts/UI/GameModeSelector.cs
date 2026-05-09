using TMPro;
using UnityEngine;
using PocDiceTactics.ModeSelection;

namespace PocDiceTactics
{
    public class GameModeSelector : MonoBehaviour
    {
        private TMP_Dropdown _dropdown;

        private void Awake()
        {
            _dropdown = GetComponent<TMP_Dropdown>();
            if (_dropdown == null)
            {
                Debug.LogError("[GameModeSelector] TMP_Dropdown component is missing.");
            }
        }

        private void OnEnable()
        {
            if (_dropdown == null)
            {
                return;
            }

            _dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
            SyncDropdownWithCurrentMode();
        }

        private void OnDisable()
        {
            if (_dropdown == null)
            {
                return;
            }

            _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        }

        private void SyncDropdownWithCurrentMode()
        {
            PocDiceTactics.ModeSelection.GameModeManager modeManager = FindAnyObjectByType<PocDiceTactics.ModeSelection.GameModeManager>();
            if (modeManager == null)
            {
                return;
            }

            int modeIndex = (int)modeManager.CurrentMode;
            _dropdown.SetValueWithoutNotify(modeIndex);
        }

        private void OnDropdownValueChanged(int index)
        {
            GameMode selectedMode = (GameMode)index;
            EventBus.Instance?.Publish(new GameModeChangedEvent { NewMode = selectedMode });
        }
    }
}
