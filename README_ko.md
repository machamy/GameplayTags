# Gameplay Tags for Unity

---

## 개요

이 패키지는 **Unreal Engine의 Gameplay Tag 시스템**을 Unity에 구현한 것입니다.

Gameplay Tag는 **계층 구조를 가진 문자열 기반 식별자 시스템**으로, 게임플레이 관련 상태, 속성, 조건을 유연하고 효율적으로 표현할 수 있습니다.

### 주요 특징

* **계층 구조 문자열 기반 ID 시스템** - `"Damage.Fire.DoT"` 같은 형태로 태그를 구성
* **빠른 비교 성능** - `==` 연산은 O(1), 계층 구조 판별은 O(레벨)
* **ScriptableObject 기반 구조** - 대부분의 플랫폼에서 사용 가능
* **네트워크 지원** - Unity Netcode for GameObjects (NGO)를 위한 네트워크 직렬화 지원
  * 게임 중 동적으로 생성된 태그는 동기화되지 않을 수 있음
  * 클라이언트 간 동일한 게임 버전 필요
* **에디터 통합** - 직관적인 에디터 UI 제공
* **런타임 디버깅** - 등록된 태그 목록을 확인할 수 있는 런타임 UI

### 주요 사용 사례

* 능력(Ability), 상태(State), 효과(Effect), 상호작용(Interaction) 표시
* 하드코딩된 참조 없이 조건 체크
* 깔끔하고 확장 가능한 게임플레이 로직 구축

---

## 시작하기

태그를 사용하기 전에 먼저 **태그를 생성**해야 합니다. 프로젝트에 태그를 등록하는 방법은 세 가지입니다:

### 1. ScriptableObject를 통한 태그 생성

Unity 에디터에서 `Assets/Create/Gameplay Tags/Tag Database`를 통해 태그 데이터베이스를 생성할 수 있습니다.

생성된 데이터베이스 에셋을 열면 태그를 추가, 수정, 삭제할 수 있는 인터페이스가 표시됩니다.

![Editor Placeholder](Documentation~/Images/Placeholder.png)
*[태그 에디터 스크린샷 위치]*

### 2. 에디터 UI를 통한 태그 생성

Unity 에디터 상단 메뉴바에서 **Gameplay Tags** 버튼을 클릭하여 태그 에디터 창을 열 수 있습니다.

이 창에서 태그를 직접 생성하고 관리할 수 있으며, 런타임에 등록된 모든 태그를 확인할 수 있습니다.

### 3. 어셈블리 속성을 통한 태그 등록

코드에서 직접 태그를 선언할 수도 있습니다:

```csharp
[assembly: GameplayTagDef("Damage.Fatal")]
[assembly: GameplayTagDef("Damage.Fire")]
[assembly: GameplayTagDef("Damage.Fire.DoT")]
[assembly: GameplayTagDef("CrowdControl.Stunned")]
```

이 방식은 **코드가 특정 태그의 존재를 요구할 때** 특히 유용합니다. 태그가 의존하는 코드와 함께 항상 등록되도록 보장합니다.

---

## API 문서

### `GameplayTag`

게임플레이 태그를 나타내는 경량 구조체입니다.

#### 핵심 속성

* **`string TagName`** - 태그의 전체 이름 (예: `"Damage.Fire"`)
* **`bool IsValid`** - 태그가 유효한지 (시스템에 등록되었는지) 여부
* **`bool IsNone`** - 특수 `None` 태그인지 여부
* **`GameplayTag None`** - 비어있거나 유효하지 않은 태그를 나타내는 특수 값

#### 계층 구조 메서드

* **`bool IsParentOf(GameplayTag other)`** - 이 태그가 다른 태그의 부모인지 확인
  * 예: `"Damage"`는 `"Damage.Fire"`의 부모
* **`bool IsChildOf(GameplayTag other)`** - 이 태그가 다른 태그의 자식인지 확인
  * 예: `"Damage.Fire.DoT"`는 `"Damage.Fire"`의 자식

#### 성능 특성

* **태그 비교 (`==`)** - O(1) - 내부 ID로 즉시 비교
* **계층 구조 체크** - O(레벨) - 부모-자식 관계 확인 시 계층 깊이에 비례

---

### `GameplayTagManager`

모든 등록된 태그를 관리하는 정적 클래스입니다.

* **`RequestTag(string name)`** - 이름으로 `GameplayTag` 가져오기 (없으면 `GameplayTag.None` 반환)
* **`TryRequestTag(string name, out GameplayTag tag)`** - 태그 요청 시도 (성공/실패 반환)
* **`GetAllTags()`** - 등록된 모든 태그 반환
* **`HasBeenReloaded`** *(속성)* - 런타임에 태그가 다시 로드되었는지 표시

---

### `GameplayTagContainer`

태그 집합을 관리하는 메인 API입니다. 태그를 추가, 제거, 쿼리, 조합할 수 있는 메서드를 제공합니다.

#### 태그 관리

* **`AddTag(GameplayTag tag)`** - 컨테이너에 태그 추가 (카운트 증가)
* **`AddTagUnique(GameplayTag tag)`** - 태그가 없을 때만 추가 (카운트가 0일 때만)
* **`RemoveTagOnce(GameplayTag tag)`** - 태그를 한 번 제거 (카운트 감소)
* **`RemoveTagAll(GameplayTag tag)`** - 태그를 모두 제거 (카운트를 0으로)
* **`ClearTags()`** - 모든 태그 제거

#### 태그 쿼리

* **`HasTag(GameplayTag tag)`** - 컨테이너가 특정 태그를 포함하는지 확인 (카운트 > 0)
* **`CountTag(GameplayTag tag)`** - 태그의 개수 반환
* **`HasTagIncludeChildren(GameplayTag tag)`** - 자식 태그를 포함하여 태그 존재 확인
* **`CountTagIncludeChildren(GameplayTag tag)`** - 자식 태그를 포함한 태그 개수 반환
* **`HasAnyTags(IGameplayTagContainer other)`** - 다른 컨테이너의 태그 중 하나라도 있는지 확인
* **`HasAllTags(IGameplayTagContainer other)`** - 다른 컨테이너의 모든 태그를 가지고 있는지 확인

#### 속성

* **`int UniqueTagCount`** - 고유 태그 개수 (카운트 > 0인 태그 개수)
* **`int TotalTagCount`** - 총 태그 개수 (모든 카운트 합산)

---

### 네트워크 지원 (Netcode for GameObjects)

`GameplayTag`와 `GameplayTagContainer`는 Unity Netcode for GameObjects (NGO)와 함께 사용할 수 있도록 `INetworkSerializable`을 구현합니다.

```csharp
using Unity.Netcode;
using Machamy.GameplayTags.Runtime;

public struct MyNetworkData : INetworkSerializable
{
    public GameplayTag statusTag;
    public GameplayTagContainer activeTags;
    
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref statusTag);
        serializer.SerializeValue(ref activeTags);
    }
}
```

#### 주의사항

* 게임 중 동적으로 생성된 태그는 클라이언트 간 동기화되지 않을 수 있습니다
* 모든 클라이언트가 동일한 태그 데이터베이스를 가진 동일한 게임 버전을 사용해야 합니다
* 네트워크 직렬화는 태그의 내부 ID를 사용하므로 빠르고 효율적입니다

---

## 사용 예제

### 기본 태그 사용

```csharp
using Machamy.GameplayTags.Runtime;

// 태그 요청
GameplayTag fireTag = GameplayTagManager.RequestTag("Damage.Fire");
GameplayTag dotTag = GameplayTagManager.RequestTag("Damage.Fire.DoT");

// 계층 구조 체크
if (dotTag.IsChildOf(fireTag))
{
    Debug.Log("DoT는 Fire 데미지의 하위 타입입니다.");
}

// 태그 비교 (O(1))
if (fireTag == dotTag)
{
    // 같지 않음
}
```

### 컨테이너 사용

```csharp
using Machamy.GameplayTags.Runtime;

public class CharacterStatus : MonoBehaviour
{
    [SerializeField]
    private GameplayTagContainer statusTags = new GameplayTagContainer();
    
    public void ApplyBurn()
    {
        GameplayTag burnTag = GameplayTagManager.RequestTag("Status.Burn");
        statusTags.AddTag(burnTag);
    }
    
    public bool IsBurning()
    {
        GameplayTag burnTag = GameplayTagManager.RequestTag("Status.Burn");
        return statusTags.HasTag(burnTag);
    }
    
    public void RemoveBurn()
    {
        GameplayTag burnTag = GameplayTagManager.RequestTag("Status.Burn");
        statusTags.RemoveTagAll(burnTag);
    }
}
```

### 계층 구조 활용

```csharp
// "Damage" 태그 또는 그 하위 태그를 가진 경우 체크
GameplayTag damageTag = GameplayTagManager.RequestTag("Damage");

if (container.HasTagIncludeChildren(damageTag))
{
    // "Damage", "Damage.Fire", "Damage.Ice" 등 모두 해당
    Debug.Log("어떤 형태로든 데미지를 받고 있습니다!");
}
```

---

## 크레딧

이 프로젝트는 [BandoWare/GameplayTags](https://github.com/BandoWare/GameplayTags)에서 영감을 받아 개발되었습니다.

---

## 라이선스

이 프로젝트는 Creative Commons Attribution 4.0 International (CC BY 4.0) 라이선스에 따라 배포됩니다. 자세한 내용은 [LICENSE](License.md) 파일을 참조하세요.

