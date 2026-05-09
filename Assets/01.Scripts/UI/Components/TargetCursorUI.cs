using UnityEngine;

public class TargetCursorUI : MonoBehaviour
{
    [SerializeField] private float _followSpeed = 20f;
    [SerializeField] private CanvasGroup _canvasGroup;

        private Camera _mainCamera;

        private void Awake()
        {
            _mainCamera = Camera.main;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (InputReader.Instance == null)
            {
                Debug.LogError("[TargetCursorUI] InputReader.Instance is null");
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            GridManager grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogError("[TargetCursorUI] GridManager.Instance is null");
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    Debug.LogError("[TargetCursorUI] Main camera is null");
                    if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                    return;
                }
            }

            Vector2 mouseScreen = InputReader.Instance.GetMousePosition();
            Vector3 mouseWorld = _mainCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -_mainCamera.transform.position.z));
            Vector2Int cell = grid.WorldToCell(mouseWorld);
            if (!grid.IsInside(cell))
            {
                if (_canvasGroup != null) _canvasGroup.alpha = 0f;
                return;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            Vector3 targetWorld = grid.CellToWorld(cell);
            transform.position = Vector3.Lerp(transform.position, targetWorld, Time.deltaTime * _followSpeed);
        }
}
