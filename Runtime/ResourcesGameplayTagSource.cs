using Machamy.GameplayTags.Runtime;
using System.Collections.Generic;
using UnityEngine;

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// Resources 폴더에서 게임플레이 태그 데이터베이스를 로드하는 소스
    /// </summary>
    internal class ResourcesGameplayTagSource : IGameplayTagSource
    {
        public string Name => "Resources: GameplayTagDatabase";

        public void RegisterTags(GameplayTagRegistrationContext context)
        {
            GameplayTagDatabase[] databases = Resources.LoadAll<GameplayTagDatabase>("");
            
            if (databases == null || databases.Length == 0)
            {
                Debug.LogWarning($"Resources 폴더에서 GameplayTagDatabase를 찾을 수 없습니다. " +
                    $"Resources 폴더에 GameplayTagDatabase를 생성해주세요.");
                return;
            }

            // Resources.LoadAll은 에디터/테스트 러너 환경에서 같은 에셋의 복제 인스턴스를 여러 번
            // 반환할 수 있습니다. 동일한 정의의 중복 등록은 GameplayTagRegistrationContext가
            // 조용히 무시하므로, 여기서는 걸러내지 않고 그대로 넘깁니다.
            HashSet<string> registeredTagNames = new();

            foreach (var database in databases)
            {
                if (database == null || database.Tags == null)
                    continue;

                foreach (var tagEntry in database.Tags)
                {
                    if (tagEntry == null || string.IsNullOrEmpty(tagEntry.TagName))
                        continue;

                    context.RegisterTag(
                        tagEntry.TagName,
                        tagEntry.Description ?? "",
                        this
                    );
                    registeredTagNames.Add(tagEntry.TagName);
                }
            }

            Debug.Log($"[{Name}] 데이터베이스 에셋 {databases.Length}개를 스캔해 고유 태그 {registeredTagNames.Count}개를 로드했습니다.");
        }
    }
}

