
namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그 소스 인터페이스
    /// </summary>
    public interface IGameplayTagSource
    {
        string Name { get; }
        void RegisterTags(GameplayTagRegistrationContext context);
    }
}

