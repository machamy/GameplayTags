using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Machamy.GameplayTags.Runtime
{
    /// <summary>
    /// 게임플레이 태그를 관리하는 중앙 관리자
    /// </summary>
    public class GameplayTagManager
    {
        /// <summary>
        /// 태그가 런타임에 다시 로드되었는지 확인합니다.
        /// </summary>
        public static bool HasBeenReloaded => _hasBeenReloaded;

        private static readonly Dictionary<string, GameplayTagSpec> TagSpecsDict = new();
        private static List<GameplayTagSpec> _tagsDefinitions;
        private static GameplayTag[] _tags;
        private static bool _isInitialized;
        private static bool _hasBeenReloaded;
        

        /// <summary>
        /// 등록된 모든 태그를 가져옵니다 
        /// </summary>
        public static ReadOnlySpan<GameplayTag> GetAllTags()
        {
            InitializeIfNeeded();
            return new ReadOnlySpan<GameplayTag>(_tags);
        }

        /// <summary>
        /// ID로 태그 정의를 가져옵니다.
        /// </summary>
        internal static GameplayTagSpec GetDefinitionByID(int tagId)
        {
            InitializeIfNeeded();
            
            if (tagId < 0 || tagId >= _tagsDefinitions.Count)
                return null;
            
            return _tagsDefinitions[tagId];
        }

        /// <summary>
        /// 태그 이름으로 태그를 요청합니다.
        /// </summary>
        public static GameplayTag RequestTag(string name)
        {
            if (string.IsNullOrEmpty(name))
                return GameplayTag.None;

            if (!TryGetSpec(name, out GameplayTagSpec definition))
            {
                return GameplayTagSpec.Invalid.Tag;
            }

            return definition.Tag;
        }

        /// <summary>
        /// 태그 이름으로 태그를 요청합니다 
        /// </summary>
        public static bool TryRequestTag(string name, out GameplayTag tag)
        {
            GameplayTag result = RequestTag(name);
            tag = result;
            return tag.IsValid && !tag.IsNone;
        }

        public static GameplayTag GetTagByID(int tagId)
        {
            GameplayTagSpec spec = GetDefinitionByID(tagId);
            return spec?.Tag ?? GameplayTag.None;
        }
        
        /// <summary>
        /// 태그 정의를 이름으로 가져옵니다.
        /// </summary>
        private static bool TryGetSpec(string name, out GameplayTagSpec spec)
        {
            InitializeIfNeeded();
            return TagSpecsDict.TryGetValue(name, out spec);
        }

        /// <summary>
        /// 태그 시스템을 초기화합니다.
        /// </summary>
        public static void InitializeIfNeeded()
        {
            if (_isInitialized)
                return;

            GameplayTagRegistrationContext context = new();

            // Resources 폴더에서 GameplayTagDatabase 로드
            ResourcesGameplayTagSource resourceSource = new();
            resourceSource.RegisterTags(context);
            

            // 등록 에러 로그 출력
            foreach (GameplayTagRegistrationError error in context.GetRegistrationErrors())
                Debug.LogError($"게임플레이 태그 등록 실패 \"{error.TagName}\": {error.Message} (소스: {error.Source?.Name ?? "Unknown"})");

            // 태그 정의 생성
            _tagsDefinitions = context.GenerateDefinitions();
            
            IEnumerable<GameplayTag> tags = _tagsDefinitions
                .Select(definition => definition.Tag)
                .Skip(1); // 첫 번째 "None" 태그 제외

            _tags = tags.ToArray();
            
            
            foreach (GameplayTagSpec definition in _tagsDefinitions)
                TagSpecsDict[definition.TagName] = definition;

            _isInitialized = true;
            
            Debug.Log($"게임플레이 태그 시스템 초기화 완료: {_tags.Length}개 태그 등록됨");
            
            // 디버그: 등록된 모든 태그 출력
            if (_tagsDefinitions.Count <= 50) // 태그가 50개 이하일 때만 전체 출력
            {
                for(int i = 0; i < _tagsDefinitions.Count; i++)
                {
                    var tagDef = _tagsDefinitions[i];
                    string sourceInfo = tagDef.Source != null ? $" (출처: {tagDef.Source.Name})" : " (자동생성)";
                    Debug.Log($"  - {tagDef.TagName} [ID: {i}]{sourceInfo}");
                }
            }
        }

        /// <summary>
        /// 태그를 다시 로드합니다 (에디터 전용).
        /// </summary>
        public static void ReloadTags()
        {
            _isInitialized = false;
            TagSpecsDict.Clear();

            InitializeIfNeeded();

            _hasBeenReloaded = true;

            if (Application.isPlaying)
                Debug.LogWarning("게임플레이 태그가 런타임에 다시 로드되었습니다." +
                    " 기존 태그를 사용하는 데이터 구조가 예상대로 작동하지 않을 수 있습니다." +
                    " 도메인 리로드가 필요합니다.");
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 시작 시 태그 시스템을 초기화합니다.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            ReloadTags();
        }
#endif

        /// <summary>
        /// 런타임 시작 시 태그 시스템을 초기화합니다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnRuntime()
        {
            InitializeIfNeeded();
        }
    }
}