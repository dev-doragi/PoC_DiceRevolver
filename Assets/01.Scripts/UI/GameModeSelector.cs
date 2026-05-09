using TMPro;
using UnityEngine;

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

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<DiceModeChangedEvent>(OnDiceModeChanged);
        }
    }

    private void OnDisable()
    {
        if (_dropdown == null)
        {
            return;
        }

        _dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<DiceModeChangedEvent>(OnDiceModeChanged);
        }
    }

    private void SyncDropdownWithCurrentMode()
    {
        if (DiceModeManager.Instance == null)
        {
            Debug.LogError("[GameModeSelector] DiceModeManager.Instance is null");
            return;
        }

        int modeIndex = (int)DiceModeManager.Instance.CurrentMode;
        _dropdown.SetValueWithoutNotify(modeIndex);
    }

    private void OnDiceModeChanged(DiceModeChangedEvent evt)
    {
        _dropdown.SetValueWithoutNotify((int)evt.NewMode);
    }

    private void OnDropdownValueChanged(int index)
    {
        DiceMode selectedMode = (DiceMode)index;
        if (DiceModeManager.Instance == null)
        {
            Debug.LogError("[GameModeSelector] DiceModeManager.Instance is null");
            return;
        }

        DiceModeManager.Instance.SetMode(selectedMode);
    }
}
