# Changelog

## [1.0.0] - 2026-10-10

Downwelling과 unity-3d-coop-adventure-framework(`com.machamy.gameplaytags`)에서 개발된 내용을 원본 저장소에 반영했다.

### 변경 (호환성 주의)
- 저장소를 UPM 패키지 구조로 바꿨다. `package.json` 추가, `Test/` → `Tests/Editor`·`Tests/Runtime`, `Example/` → `Samples~/Example`(Package Manager에서 가져온다).
- 저장소에 포함돼 있던 생성물(`Generated/AllGameplayTags.g.cs`, `Resources/GameplayTags/*` 데이터베이스)을 제거했다. 소비 프로젝트가 생성한다.
- `GameplayTagDatabase` → `GameplayTagDatabaseSO`로 이름을 바꿨다.
- `LICENSE.md`로 파일 이름을 바꿨다(내용과 BandoWare 고지는 같다).

### 추가
- `GameplayTagSet`과 드로어, `GameplayTagParentAttribute`, `GameplayTagCompactAttribute`, `GameplayTagContainerEvent`, `EditorState`, `NetworkGameplayTagManager`, 에디터 UI 유틸리티.
- EditMode 테스트(코어, 컨테이너, 직렬화, 태그 세트, 등록 컨텍스트, 패키지 어셈블리 태그 정의)와 Netcode 통합 테스트.

### 수정
- None 태그 처리(`NoneTagId`, `GameplayTagSpec.None`), 런타임 디버거 이벤트 중복 등록.
- 중복 태그 등록 병합 규칙: 같은 정의는 무시하고, 한쪽 설명이 비어 있으면 채워진 쪽을 쓰고, 자동 생성 부모는 실제 정의로 교체한다. 설명이 서로 다를 때만 양쪽 소스를 담아 오류를 낸다(`fix/duplicate-tag-registration` 브랜치 내용 포함).
