using UnityEngine;

namespace PocDiceTactics
{
    /// <summary>
    /// UI 버튼과 SceneLoader(싱글톤) 사이를 연결하는 전용 핸들러입니다.
    /// 버튼 인스펙터에서 이 스크립트의 Public 메서드를 연결하세요.
    /// </summary>
    public class UINavigationHandler : MonoBehaviour
    {
        // 1. 다시 시작 버튼용
        public void RequestReloadScene()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.ReloadCurrentScene();
            }
            else
            {
                // 싱글톤이 없는 비상 상황용 로직
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }

        // 2. 메인 메뉴(또는 종료) 버튼용
        public void RequestQuitGame()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.Quit();
            }
            else
            {
                Application.Quit();
            }
        }

        // 3. 로비로 이동 버튼용 (나중에 로비 씬 생기면 사용)
        public void RequestGoToLobby()
        {
            SceneLoader.Instance?.GoToLobby();
        }

        // 4. 일시정지 해제 버튼용
        public void RequestResume()
        {
            // EventBus를 통해 시스템에 알림
            EventBus.Instance?.Publish(new PausePressedEvent());
        }
    }
}