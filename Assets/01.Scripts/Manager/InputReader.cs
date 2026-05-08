using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

 [DefaultExecutionOrder(-140)]
/// <summary>
/// 자동 생성된 InputSystem_Actions 래퍼를 사용하여 강타입(Strong-type) 기반으로 입력 이벤트를 발행합니다.
/// </summary>
public class InputReader : Singleton<InputReader>, InputSystem_Actions.IPlayerActions, InputSystem_Actions.ISystemActions
{
        [Header("Behavior")]
        [SerializeField] private bool _useGameStateInputGate = true;

        private InputSystem_Actions _inputActions;
        private bool _isInputBlocked = false;

        public bool IsInputBlocked => _isInputBlocked;
        public bool IsPointerOverUI { get; private set; }

        protected override void OnBootstrap()
        {
            _inputActions = new InputSystem_Actions();

            // 인터페이스 콜백 등록
            _inputActions.Player.SetCallbacks(this);
            _inputActions.System.SetCallbacks(this);

            _inputActions.Player.Enable();
            _inputActions.System.Enable();

            if (_useGameStateInputGate)
            {
                EventBus.Instance?.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            }
        }

        private void OnDisable()
        {
            if (_useGameStateInputGate)
            {
                EventBus.Instance?.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            }

            _inputActions?.Player.Disable();
            _inputActions?.System.Disable();
        }

        private void Update()
        {
            if (EventSystem.current != null)
            {
                IsPointerOverUI = EventSystem.current.IsPointerOverGameObject();
            }
        }

        #region IPlayerActions Implementation

        public void OnMove(InputAction.CallbackContext context)
        {
            if (_isInputBlocked) return;

            // PoC의 타일 이동을 위해 입력이 시작된 순간(Started)만 감지하여 1칸씩 이동 처리
            if (context.started)
            {
                Vector2 dir = context.ReadValue<Vector2>();

                if (dir.y > 0.5f) PublishIfAllowed(new MoveUpPressedEvent());
                else if (dir.y < -0.5f) PublishIfAllowed(new MoveDownPressedEvent());
                else if (dir.x < -0.5f) PublishIfAllowed(new MoveLeftPressedEvent());
                else if (dir.x > 0.5f) PublishIfAllowed(new MoveRightPressedEvent());
            }
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.started) PublishIfAllowed(new FirePressedEvent());
        }

        public void OnJump(InputAction.CallbackContext context)
        {
            // 제공된 액션맵에서 Space가 Jump로 되어 있으므로, 기존 사격(Space) 조작감을 유지하기 위해 Fire 연동
            if (context.started) PublishIfAllowed(new FirePressedEvent());
        }

        public void OnClick(InputAction.CallbackContext context)
        {
            if (context.started) PublishIfAllowed(new ClickEvent { IsStarted = true });
            else if (context.canceled) PublishIfAllowed(new ClickEvent { IsStarted = false });
        }

        public void OnRightClick(InputAction.CallbackContext context)
        {
            if (context.started) PublishRightClickIfAllowed(true);
            else if (context.canceled) PublishRightClickIfAllowed(false);
        }

        public void OnRotate(InputAction.CallbackContext context)
        {
            if (context.performed) PublishIfAllowed(new RotateEvent());
        }

        public void OnScroll(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                float scrollValue = context.ReadValue<Vector2>().y;
                if (Mathf.Abs(scrollValue) > 0.01f)
                {
                    PublishIfAllowed(new ScrollEvent { Delta = scrollValue });
                }
            }
        }

        // 콜백만 맞춰두고 사용하지 않는 입력들
        public void OnLook(InputAction.CallbackContext context) { }
        public void OnInteract(InputAction.CallbackContext context) { }
        public void OnPoint(InputAction.CallbackContext context) { }

        #endregion

        #region ISystemActions Implementation

        public void OnPause(InputAction.CallbackContext context)
        {
            if (_isInputBlocked) return;
            if (context.performed)
            {
                EventBus.Instance?.Publish(new PausePressedEvent());
            }
        }

        #endregion

        #region Helpers & State Gates

        private void PublishIfAllowed<T>(T evt) where T : struct
        {
            if (_isInputBlocked) return;
            EventBus.Instance?.Publish(evt);
        }

        private void PublishRightClickIfAllowed(bool isStarted)
        {
            if (_isInputBlocked) return;
            EventBus.Instance?.Publish(new RightClickEvent { IsStarted = isStarted });
        }

        private void OnGameStateChanged(GameStateChangedEvent evt)
        {
            if (_inputActions == null) return;

            if (evt.NewState == GameState.Playing) _inputActions.Player.Enable();
            else _inputActions.Player.Disable();
        }

        public void SetInputBlocked(bool blocked)
        {
            _isInputBlocked = blocked;
        }

        public Vector2 GetMousePosition()
        {
            return _inputActions != null ? _inputActions.Player.Point.ReadValue<Vector2>() : Vector2.zero;
        }

        public Vector2 GetMouseDelta()
        {
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        }

        #endregion
}
