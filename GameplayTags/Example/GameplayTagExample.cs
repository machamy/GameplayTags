
using UnityEngine;
using Machamy.GameplayTags.Runtime;

// 어셈블리 레벨에서 태그 등록 예제
[assembly: GameplayTagDef("Example.Player.State.Idle", "플레이어가 대기 중인 상태")]
[assembly: GameplayTagDef("Example.Player.State.Moving", "플레이어가 이동 중인 상태")]
[assembly: GameplayTagDef("Example.Player.State.Attacking", "플레이어가 공격 중인 상태")]
[assembly: GameplayTagDef("Example.Enemy.Type.Normal", "일반 적")]
[assembly: GameplayTagDef("Example.Enemy.Type.Boss", "보스 적")]
[assembly: GameplayTagDef("Example.Item.Consumable.HealthPotion", "체력 회복 포션")]
[assembly: GameplayTagDef("Example.Item.Equipment.Weapon", "무기")]

namespace GameplayTags.Example
{
    /// <summary>
    /// GameplayTag 시스템 사용 예제
    /// </summary>
    public class GameplayTagExample : MonoBehaviour
    {
        [Header("태그 테스트")]
        [SerializeField] 
        private GameplayTag testTag;
        
        public GameplayTagContainer testTagContainer = new GameplayTagContainer();
        

        private void Start()
        {
            
            // 태그 요청
            var playerIdleTag = GameplayTagManager.RequestTag("Player.State.Idle");
            var playerMovingTag = GameplayTagManager.RequestTag("Player.State.Moving");
            var playerStateTag = GameplayTagManager.RequestTag("Player.State");
            
            Debug.Log($"플레이어 Idle 태그: {playerIdleTag.TagName}");
            Debug.Log($"플레이어 Moving 태그: {playerMovingTag.TagName}");
            
            // 계층 구조 테스트
            Debug.Log($"Player.State는 Player.State.Idle의 부모인가? {playerStateTag.IsParentOf(playerIdleTag)}");
            Debug.Log($"Player.State.Idle은 Player.State의 자식인가? {playerIdleTag.IsChildOf(playerStateTag)}");
            
            
            // 등록된 모든 태그 출력
            Debug.Log("=== 등록된 모든 태그 ===");
            var allTags = GameplayTagManager.GetAllTags();
            foreach (var tag in allTags)
            {
                Debug.Log($"- {tag.TagName}");
            }
        }

        private void Update()
        {
            // 인스펙터에서 설정한 태그 사용
            if (testTag.IsValid && !testTag.IsNone)
            {
                // testTag를 사용한 로직
            }
        }
    }
}

