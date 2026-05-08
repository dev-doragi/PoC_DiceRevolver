# Copilot Instructions

## 프로젝트 지침
- 사용자는 Unity C# 코드에서 클래스/메서드는 PascalCase, private 필드는 _camelCase 네이밍 컨벤션을 엄격히 선호한다.
- 사용자는 Unity C# 작업에서 실행 중심의 엄격한 방식을 선호한다.
  - 환각 금지
  - 요청된 스크립트/메서드만 수정
  - 누락 참조는 임의로 생성하지 말 것
  - `T.Instance` 및 `EventBus.Instance.Publish` / `Subscribe` 사용
  - `GameObject.Find()`, `FindObjectOfType()`, 레거시 Input 시스템 사용 금지
  - null은 `Debug.LogError` 후 중단
  - UI와 Prefab은 모듈식 초기화 기준으로 처리