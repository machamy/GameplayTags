using Machamy.GameplayTags.Runtime;
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

            int totalTagCount = 0;
            
            foreach (var database in databases)
            {
                if (database == null || database.Tags == null)
                    continue;

                foreach (var tagEntry in database.Tags)
                {
                    if (string.IsNullOrEmpty(tagEntry.TagName))
                        continue;

                    context.RegisterTag(
                        tagEntry.TagName, 
                        tagEntry.Description ?? "", 
                        this
                    );
                    totalTagCount++;
                }
            }

            Debug.Log($"[{Name}] {databases.Length}개의 데이터베이스에서 {totalTagCount}개의 태그를 로드했습니다.");
        }
    }
}

