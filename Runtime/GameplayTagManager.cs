using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Profiling;

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
        private static readonly ProfilerMarker Marker_GetAllTags = new("GameplayTagManager.GetAllTags");
        private static readonly ProfilerMarker Marker_GetDefinitionByID = new("GameplayTagManager.GetDefinitionByID");
        private static readonly ProfilerMarker Marker_RequestTag = new("GameplayTagManager.RequestTag");
        private static readonly ProfilerMarker Marker_TryRequestTag = new("GameplayTagManager.TryRequestTag");
        private static readonly ProfilerMarker Marker_RequestTagsFromParent = new("GameplayTagManager.RequestTagsFromParent");
        private static readonly ProfilerMarker Marker_RequestTagsFromParents = new("GameplayTagManager.RequestTagsFromParents");
        private static readonly ProfilerMarker Marker_GetTagByID = new("GameplayTagManager.GetTagByID");
        private static readonly ProfilerMarker Marker_CreateUnresolvedTag = new("GameplayTagManager.CreateUnresolvedTag");
        private static readonly ProfilerMarker Marker_RegisterDynamicTagWithIdStrict = new("GameplayTagManager.RegisterDynamicTagWithIdStrict");
        private static readonly ProfilerMarker Marker_RegisterDynamicTagChecked = new("GameplayTagManager.RegisterDynamicTagChecked");
        private static readonly ProfilerMarker Marker_RegisterDynamicTagIncludeParentTags = new("GameplayTagManager.RegisterDynamicTagIncludeParentTags");
        private static readonly ProfilerMarker Marker_UnregisterDynamicTag = new("GameplayTagManager.UnregisterDynamicTag");
        private static readonly ProfilerMarker Marker_UnregisterDynamicTagById = new("GameplayTagManager.UnregisterDynamicTagById");
        private static readonly ProfilerMarker Marker_ClearAllDynamicTags = new("GameplayTagManager.ClearAllDynamicTags");
        private static readonly ProfilerMarker Marker_RegisterTagInternal = new("GameplayTagManager.RegisterTagInternal");
        private static readonly ProfilerMarker Marker_GetParentTagName = new("GameplayTagManager.GetParentTagName");
        private static readonly ProfilerMarker Marker_TryGetSpec = new("GameplayTagManager.TryGetSpec");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded = new("GameplayTagManager.InitializeIfNeeded");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_CreateContext = new("InitializeIfNeeded.CreateContext");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_RegisterSources = new("InitializeIfNeeded.RegisterSources");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_LogErrors = new("InitializeIfNeeded.LogErrors");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_GenerateDefinitions = new("InitializeIfNeeded.GenerateDefinitions");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_BuildTagList = new("InitializeIfNeeded.BuildTagList");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_BuildDictionary = new("InitializeIfNeeded.BuildDictionary");
        private static readonly ProfilerMarker Marker_InitializeIfNeeded_CalculateHash = new("InitializeIfNeeded.CalculateHash");
        private static readonly ProfilerMarker Marker_ReloadTags = new("GameplayTagManager.ReloadTags");
        private static readonly ProfilerMarker Marker_CalculateTagsHash = new("GameplayTagManager.CalculateTagsHash");
        private static readonly ProfilerMarker Marker_InitializeOnRuntime = new("GameplayTagManager.InitializeOnRuntime");
        private static readonly ProfilerMarker Marker_IsValidTagName = new("GameplayTagManager.IsValidTagName");

        /// <summary>
        /// 태그가 런타임에 다시 로드되었는지 확인합니다.
        /// </summary>
        public static bool HasBeenReloaded => _hasBeenReloaded;

        /// <summary>
        /// 현재 로드된 태그들의 해시값을 가져옵니다 (에디터 전용).
        /// </summary>
        public static int CurrentTagsHash => _currentTagsHash;

        private static readonly Dictionary<string, GameplayTagSpec> TagSpecsDict = new();
        private static List<GameplayTagSpec> _tagsDefinitions;
        private static List<GameplayTag> _tags;
        private static bool _isInitialized;
        private static bool _isInitializing; // 초기화 중 플래그
        private static bool _hasLoggedDeferredInitializationWarning;
        private static bool _hasBeenReloaded;
        private static int _currentTagsHash;
        
        private static List<GameplayTagSpec> _dynamicTagDefinitions = new();
        
        private static readonly Stack<int> _freeIndices = new();
        
        
        internal static event Action<GameplayTagSpec> OnDynamicTagRegistered;
        
        internal static IReadOnlyList<GameplayTagSpec> DynamicTagDefinitions => _dynamicTagDefinitions;
        public static bool Initialized => _isInitialized;


        /// <summary>
        /// 등록된 모든 태그를 가져옵니다 
        /// </summary>
        public static IReadOnlyList<GameplayTag> GetAllTags()
        {
            using (Marker_GetAllTags.Auto())
            {
                InitializeIfNeeded();
                return _tags;
            }
        }

        /// <summary>
        /// ID로 태그 정의를 가져옵니다.
        /// </summary>
        internal static GameplayTagSpec GetDefinitionByID(int tagId)
        {
            using (Marker_GetDefinitionByID.Auto())
            {
                InitializeIfNeeded();

                if (tagId < 0 || tagId >= _tagsDefinitions.Count)
                    return null;

                return _tagsDefinitions[tagId];
            }
        }
        

        /// <summary>
        /// 태그 이름으로 태그를 요청합니다.
        /// </summary>
        public static GameplayTag RequestTag(string name)
        {
            using (Marker_RequestTag.Auto())
            {
                if (string.IsNullOrEmpty(name))
                    return GameplayTag.None;

                // 초기화 중일 때는 Invalid 반환 (무한 재귀 방지)
                if (_isInitializing)
                    return GameplayTag.GetInvalid(name);

                if (!TryGetSpec(name, out GameplayTagSpec definition))
                {
                    return GameplayTag.GetInvalid(name);
                }

                return definition.Tag;
            }
        }

        /// <summary>
        /// 태그 이름으로 태그를 요청합니다 
        /// </summary>
        public static bool TryRequestTag(string name, out GameplayTag tag)
        {
            using (Marker_TryRequestTag.Auto())
            {
                GameplayTag result = RequestTag(name);
                tag = result;
                return tag.IsValid && !tag.IsNone;
            }
        }

        public static bool RequestTagsFromParent(string parentTagName, out List<GameplayTag> outTags)
        {
            using (Marker_RequestTagsFromParent.Auto())
            {
                outTags = new List<GameplayTag>();
                GameplayTag parentTag = RequestTag(parentTagName);
                if (!parentTag.IsValid)
                    return false;

                foreach (var tagSpec in TagSpecsDict.Values)
                {
                    if (tagSpec.IsChildOf(TagSpecsDict[parentTagName]))
                    {
                        outTags.Add(tagSpec.Tag);
                    }
                }

                return outTags.Count > 0;
            }
        }
        
        public static bool RequestTagsFromParents(string[] parentTagNames, ref List<GameplayTag> outTags)
        {
            using (Marker_RequestTagsFromParents.Auto())
            {
                outTags = new List<GameplayTag>();
                HashSet<int> addedTagIndices = HashSetPool<int>.Get();

                foreach (string parentTagName in parentTagNames)
                {
                    GameplayTag parentTag = RequestTag(parentTagName);
                    if (!parentTag.IsValid)
                        continue;

                    foreach (var tagSpec in TagSpecsDict.Values)
                    {
                        if (tagSpec.IsChildOf(TagSpecsDict[parentTagName]) && !addedTagIndices.Contains(tagSpec.RuntimeIndex))
                        {
                            outTags.Add(tagSpec.Tag);
                            addedTagIndices.Add(tagSpec.RuntimeIndex);
                        }
                    }
                }
                HashSetPool<int>.Release(addedTagIndices);

                return outTags.Count > 0;
            }
        }
        
        public static GameplayTag GetTagByID(int tagId)
        {
            using (Marker_GetTagByID.Auto())
            {
                GameplayTagSpec spec = GetDefinitionByID(tagId);
                return spec?.Tag ?? GameplayTag.None;
            }
        }
        
        public static GameplayTag CreateUnresolvedTag(string name)
        {
            using (Marker_CreateUnresolvedTag.Auto())
            {
                return GameplayTag.FromTagName(name);
            }
        }
        
        
        /// <summary>
        /// 동적 태그를 등록합니다. 부모 태그가 존재해야만 성공하며, ID를 강제 할당합니다.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="forceId"></param>
        /// <param name="invokeEvent"></param>
        /// <returns></returns>
        public static bool RegisterDynamicTagWithIdStrict(string name, int forceId, bool invokeEvent = true)
        {
            using (Marker_RegisterDynamicTagWithIdStrict.Auto())
            {
                InitializeIfNeeded();

                if (string.IsNullOrEmpty(name)) return false;
                if (TagSpecsDict.ContainsKey(name))
                {
                    if (TagSpecsDict[name].RuntimeIndex == forceId)
                        return true; // 이미 존재하면 성공 처리
                    else
                    {
                        Debug.LogError($"[GameplayTag] 동적 태그 등록 실패: 태그 '{name}'가 이미 존재하지만 ID가 다릅니다. ({TagSpecsDict[name].RuntimeIndex} != {forceId})");
                        return false;
                    }
                }

                // 부모 태그 존재 여부 확인
                string parentName = GetParentTagName(name);
                if (!string.IsNullOrEmpty(parentName) && !TagSpecsDict.ContainsKey(parentName))
                {
                    Debug.LogError($"[GameplayTag] 동적 태그 등록 실패: 부모 태그 '{parentName}'가 존재하지 않습니다. ({name})");
                    return false;
                }

                // ID 강제 할당
                var newSpec = RegisterTagInternal(name, forceId);
                _dynamicTagDefinitions.Add(newSpec);
                if (invokeEvent)
                    OnDynamicTagRegistered?.Invoke(newSpec);
                return true;
            }
        }
        
        /// <summary>
        /// 동적 태그를 등록합니다. 부모 태그가 존재해야만 성공합니다.
        /// </summary>
        public static bool RegisterDynamicTagChecked(string name, bool invokeEvent = true)
        {
            using (Marker_RegisterDynamicTagChecked.Auto())
            {
                InitializeIfNeeded();

                if (string.IsNullOrEmpty(name)) return false;
                if (TagSpecsDict.ContainsKey(name)) return true; // 이미 존재하면 성공 처리

                // 부모 태그 존재 여부 확인
                string parentName = GetParentTagName(name);
                if (!string.IsNullOrEmpty(parentName) && !TagSpecsDict.ContainsKey(parentName))
                {
                    Debug.LogError($"[GameplayTag] 동적 태그 등록 실패: 부모 태그 '{parentName}'가 존재하지 않습니다. ({name})");
                    return false;
                }

                // ID는 현재 리스트의 끝 번호 자동 할당
                var newSpec = RegisterTagInternal(name);
                _dynamicTagDefinitions.Add(newSpec);
                if (invokeEvent)
                    OnDynamicTagRegistered?.Invoke(newSpec);
                return true;
            }
        }

        /// <summary>
        /// 동적 태그를 등록합니다. 부모 태그가 없으면 자동으로 생성합니다.
        /// </summary>
        public static bool RegisterDynamicTagIncludeParentTags(string name, bool invokeEvent = true)
        {
            using (Marker_RegisterDynamicTagIncludeParentTags.Auto())
            {
                InitializeIfNeeded();

                if (string.IsNullOrEmpty(name)) return false;
                if (TagSpecsDict.ContainsKey(name)) return true;

                string[] parts = name.Trim().Split('.');
                string currentTagName = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i == 0) currentTagName = parts[0];
                    else currentTagName += "." + parts[i];

                    if (!TagSpecsDict.ContainsKey(currentTagName))
                    {
                        var newSpec = RegisterTagInternal(currentTagName);
                        _dynamicTagDefinitions.Add(newSpec);
                        if (invokeEvent)
                            OnDynamicTagRegistered?.Invoke(newSpec);
                    }
                }

                return true;
            }
        }
        
        private static bool UnregisterDynamicTag(string name)
        {
            using (Marker_UnregisterDynamicTag.Auto())
            {
                InitializeIfNeeded();

                if (string.IsNullOrEmpty(name)) return false;
                if (!TagSpecsDict.ContainsKey(name)) return false;

                GameplayTagSpec spec = TagSpecsDict[name];

                TagSpecsDict.Remove(name);
                _dynamicTagDefinitions.Remove(spec);

                if (spec.RuntimeIndex >= 0 && spec.RuntimeIndex < _tagsDefinitions.Count)
                {
                    _tagsDefinitions[spec.RuntimeIndex] = null;
                    _tags[spec.RuntimeIndex - 1] = GameplayTag.None;

                    _freeIndices.Push(spec.RuntimeIndex);
                }

                Debug.Log($"[GameplayTag] 동적 태그 비활성화됨: {name} (ID: {spec.RuntimeIndex} 슬롯 유지)");
                return true;
            }
        }

        public static bool UnregisterDynamicTagById(int tagId)
        {
            using (Marker_UnregisterDynamicTagById.Auto())
            {
                InitializeIfNeeded();

                GameplayTagSpec spec = GetDefinitionByID(tagId);
                if (spec == null) return false;

                return UnregisterDynamicTag(spec.TagName);
            }
        }
        
        public static void ClearAllDynamicTags()
        {
            using (Marker_ClearAllDynamicTags.Auto())
            {
                InitializeIfNeeded();

                foreach (var spec in _dynamicTagDefinitions.ToList())
                {
                    UnregisterDynamicTag(spec.TagName);
                }

                _dynamicTagDefinitions.Clear();
            }
        }
        
        
        /// <summary>
        /// 내부 실제 등록 로직
        /// </summary>
        private static GameplayTagSpec RegisterTagInternal(string name, int forceId = -1)
        {
            using (Marker_RegisterTagInternal.Auto())
            {
                int newId;

                if (forceId != -1)
                {
                    // [서버 동기화] 강제 ID가 들어온 경우
                    newId = forceId;

                    // 리스트가 충분히 크지 않으면 늘려줌 (중간에 구멍 채우기)
                    while (_tagsDefinitions.Count <= newId)
                    {
                        _tagsDefinitions.Add(null);
                        _tags.Add(GameplayTag.None);
                    }
                    _tagsDefinitions.Add(null);
                    _tags.Add(GameplayTag.None);
                }
                else
                {
                    // [로컬 생성] 빈 ID가 있으면 재활용, 없으면 새로 발급
                    if (_freeIndices.Count > 0)
                    {
                        newId = _freeIndices.Pop();
                    }
                    else
                    {
                        newId = _tagsDefinitions.Count;
                        _tagsDefinitions.Add(null); // 공간 확보
                        _tags.Add(GameplayTag.None);
                    }
                }

                GameplayTagSpec newSpec = new GameplayTagSpec(
                    tagName: name,
                    description: "Dynamically Registered Tag",
                    runtimeIndex: newId,
                    source: null
                );

                string parentName = GetParentTagName(name);
                if (!string.IsNullOrEmpty(parentName))
                {
                    if (TagSpecsDict.TryGetValue(parentName, out GameplayTagSpec parentSpec))
                    {
                        // 부모가 이미 존재하면 즉시 연결
                        newSpec.SetParent(parentSpec);
                    }
                    else
                    {
                        Debug.LogError($"[GameplayTag] 치명적 오류: 태그 '{name}' 등록 중 부모 '{parentName}'을 찾을 수 없습니다. 계층 구조가 깨집니다.");
                    }
                }

                // 해당 슬롯에 데이터 할당
                _tagsDefinitions[newId] = newSpec;
                _tags[newId - 1] = newSpec.Tag;
                TagSpecsDict[name] = newSpec;

                Debug.Log($"[GameplayTag] 태그 등록됨: {name} (ID: {newId})");
                return newSpec;
            }
        }

        private static string GetParentTagName(string tagName)
        {
            using (Marker_GetParentTagName.Auto())
            {
                int lastDotIndex = tagName.LastIndexOf('.');
                if (lastDotIndex == -1) return null;
                return tagName.Substring(0, lastDotIndex);
            }
        }
        
        /// <summary>
        /// 태그 정의를 이름으로 가져옵니다.
        /// </summary>
        private static bool TryGetSpec(string name, out GameplayTagSpec spec)
        {
            using (Marker_TryGetSpec.Auto())
            {
                spec = null;
#if UNITY_EDITOR
                if (!TryInitializeIfNeeded())
                    return false;
#else
                InitializeIfNeeded();
#endif
                return TagSpecsDict.TryGetValue(name, out spec);
            }
        }

        /// <summary>
        /// 초기화중에 UnityError가 발생할 수 있는경우에도 안전하게 초기화를 시도합니다. (예: MonoBehaviour 생성자, 필드 초기화자)
        /// </summary>
        /// <returns></returns>
        private static bool TryInitializeIfNeeded()
        {
            
            bool IsUnityLifecycleInitializationException(UnityException exception)
            {
                return exception.Message.Contains("MonoBehaviour constructor", StringComparison.OrdinalIgnoreCase) ||
                       exception.Message.Contains("instance field initializer", StringComparison.OrdinalIgnoreCase);
            }
            
            try
            {
                InitializeIfNeeded();
                return _isInitialized;
            }
            catch (UnityException exception) when (IsUnityLifecycleInitializationException(exception))
            {
                if (!_hasLoggedDeferredInitializationWarning)
                {
                    Debug.LogWarning("[GameplayTag] Unity가 MonoBehaviour 생성자 또는 필드 초기화자에서 태그 데이터베이스 로드를 허용하지 않아 태그 초기화를 지연합니다. " +
                                     "요청된 태그는 현재 Invalid로 반환되며, 태그 시스템 초기화 이후 다시 resolve될 수 있습니다.");
                    _hasLoggedDeferredInitializationWarning = true;
                }

                return false;
            }
        }



        /// <summary>
        /// 태그 시스템을 초기화합니다.
        /// </summary>
        public static void InitializeIfNeeded()
        {
            using (Marker_InitializeIfNeeded.Auto())
            {
                if (_isInitialized || _isInitializing)
                    return;

                _isInitializing = true;
#if UNITY_EDITOR
                if (_hasLoggedDeferredInitializationWarning)
                {
                    
                }
                else
                {
                    Debug.Log("게임플레이 태그 시스템 초기화 시작...");
                }
#else
                Debug.Log("게임플레이 태그 시스템 초기화 시작...");
#endif
                try
                {
                    using (Marker_InitializeIfNeeded_CreateContext.Auto())
                    {
                        _freeIndices.Clear();
                    }

                    GameplayTagRegistrationContext context;
                    using (Marker_InitializeIfNeeded_RegisterSources.Auto())
                    {
                        context = new GameplayTagRegistrationContext();

                        ResourcesGameplayTagSource resourceSource = new();
                        resourceSource.RegisterTags(context);
                    }
                    


                    using (Marker_InitializeIfNeeded_LogErrors.Auto())
                    {
                        foreach (GameplayTagRegistrationError error in context.GetRegistrationErrors())
                            Debug.LogError($"게임플레이 태그 등록 실패 \"{error.TagName}\": {error.Message} (소스: {error.Source?.Name ?? "Unknown"})");
                    }

                    using (Marker_InitializeIfNeeded_GenerateDefinitions.Auto())
                    {
                        _tagsDefinitions = context.GenerateDefinitions();
                    }

                    using (Marker_InitializeIfNeeded_BuildTagList.Auto())
                    {
                        IEnumerable<GameplayTag> tags = _tagsDefinitions
                            .Select(definition => definition.Tag)
                            .Skip(1); // 첫 번째 "None" 태그 제외

                        _tags = tags.ToList();
                    }

                    using (Marker_InitializeIfNeeded_BuildDictionary.Auto())
                    {
                        TagSpecsDict.Clear();
                        foreach (GameplayTagSpec definition in _tagsDefinitions)
                            TagSpecsDict[definition.TagName] = definition;
                    }

                    using (Marker_InitializeIfNeeded_CalculateHash.Auto())
                    {
                        // 태그 해시값 계산 (정렬된 태그 이름 기반)
                        _currentTagsHash = CalculateTagsHash();
                    }

                    Debug.Log($"게임플레이 태그 시스템 초기화 완료: {_tags.Count}개 태그 등록됨 (해시: {_currentTagsHash})");
                    _isInitialized = true;
                }
                finally
                {
                    _isInitializing = false;
                }
            }
        }

        /// <summary>
        /// 태그를 다시 로드합니다 (에디터 전용).
        /// </summary>
        public static void ReloadTags()
        {
            using (Marker_ReloadTags.Auto())
            {
                _isInitialized = false;
                _isInitializing = false;
                TagSpecsDict.Clear();
                _dynamicTagDefinitions.Clear();

                InitializeIfNeeded();

                _hasBeenReloaded = true;

                if (Application.isPlaying)
                {
                    Debug.LogWarning("게임플레이 태그가 런타임에 다시 로드되었습니다." +
                                     " 기존 태그를 사용하는 데이터 구조가 예상대로 작동하지 않을 수 있습니다." +
                                     " 도메인 리로드가 필요합니다.");
                }
            }
        }

        /// <summary>
        /// 현재 등록된 태그들의 해시값을 계산합니다.
        /// </summary>
        private static int CalculateTagsHash()
        {
            using (Marker_CalculateTagsHash.Auto())
            {
                if (_tagsDefinitions == null || _tagsDefinitions.Count == 0)
                    return 0;

                // 정렬된 태그 이름을 기반으로 해시 계산
                var sortedTagNames = _tagsDefinitions
                    .Select(def => def.TagName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .OrderBy(name => name)
                    .ToArray();

                unchecked
                {
                    int hash = 17;
                    foreach (string tagName in sortedTagNames)
                    {
                        hash = hash * 31 + tagName.GetHashCode();
                    }
                    return hash;
                }
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 시작 시 태그 시스템을 초기화합니다.
        /// GameplayTagCodeGenerator에서 호출됩니다.
        /// </summary>
        public static void InitializeAfterSO()
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
            using (Marker_InitializeOnRuntime.Auto())
            {
                InitializeIfNeeded();
            }
        }

        public static bool IsValidTagName(string rawTagName)
        {
            using (Marker_IsValidTagName.Auto())
            {
                if (string.IsNullOrEmpty(rawTagName))
                    return false;
                InitializeIfNeeded();
                return TagSpecsDict.ContainsKey(rawTagName);
            }
        }
    }
}
