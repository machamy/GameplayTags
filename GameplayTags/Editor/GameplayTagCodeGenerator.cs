using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Machamy.GameplayTags.Runtime;

namespace Machamy.GameplayTags.Editor
{
    /// <summary>
    /// GameplayTag 코드를 생성하는 에디터 유틸리티
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayTagCodeGenerator
    {
        // EditorPrefs 키
        private const string EditorPrefKey_AutoGenerate = "GameplayTag_AutoGenerate";
        private const string EditorPrefKey_OutputPath = "GameplayTag_OutputPath";
        private const string EditorPrefKey_ResourcePath = "GameplayTag_ResourcePath";
        private const string EditorPrefKey_GeneratedFolder = "GameplayTag_GeneratedFolder";
        private const string EditorPrefKey_GameplayTagsFolder = "GameplayTag_GameplayTagsFolder";
        
        private static readonly bool AutoGenerateOnLoad = true;
        
        // 기본 경로값
        private const string DefaultOutputPath = "Assets/GameplayTags/Generated/AllGameplayTags.g.cs";
        private const string DefaultResourcePath = "GameplayTags/Generated/";
        private const string DefaultResourceFileName = "GameplayTagDatabase";
        private const string DefaultGeneratedFolder = "Assets/GameplayTags/Generated";
        private const string DefaultGameplayTagsFolder = "Assets/GameplayTags";
        
        // EditorPrefs에서 경로 가져오기
        private static string OutputPathRelative => EditorPrefs.GetString(EditorPrefKey_OutputPath, DefaultOutputPath);
        
        private static string ResourcePathRelative => EditorPrefs.GetString(EditorPrefKey_ResourcePath, DefaultResourcePath);
        private static string GeneratedFolderPathRelative => EditorPrefs.GetString(EditorPrefKey_GeneratedFolder, DefaultGeneratedFolder);
        private static string GameplayTagsFolderPathRelative => EditorPrefs.GetString(EditorPrefKey_GameplayTagsFolder, DefaultGameplayTagsFolder);
        
        // 절대 경로로 변환
        private static string GetAbsolutePath(string relativePath) 
            => Path.Combine(Application.dataPath.Replace("Assets", ""), relativePath);
        
        
        static GameplayTagCodeGenerator()
        {
            if (AutoGenerateOnLoad && EditorPrefs.GetBool(EditorPrefKey_AutoGenerate, true))
            {
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[GameplayTag] InitializeOnLoad에서 자동 코드 생성 중...");
                    GenerateCodeStatic();
                };
            }
        }

        #region 메뉴 아이템
        
        [MenuItem("Tools/Gameplay Tags/Generate Tag Code")]
        public static void GenerateCodeMenuItem()
        {
            Debug.Log("[GameplayTag] 메뉴 아이템에서 코드 생성 시작...");
            GenerateCodeStatic();
        }

        [MenuItem("Tools/Gameplay Tags/Toggle Auto-Generate On Load")]
        public static void ToggleAutoGenerate()
        {
            bool current = EditorPrefs.GetBool(EditorPrefKey_AutoGenerate, true);
            EditorPrefs.SetBool(EditorPrefKey_AutoGenerate, !current);
            Debug.Log($"[GameplayTag] 자동 생성: {(!current ? "활성화" : "비활성화")}");
        }

        [MenuItem("Tools/Gameplay Tags/Toggle Auto-Generate On Load", true)]
        public static bool ToggleAutoGenerateValidate()
        {
            bool current = EditorPrefs.GetBool(EditorPrefKey_AutoGenerate, true);
            Menu.SetChecked("Tools/Gameplay Tags/Toggle Auto-Generate On Load", current);
            return true;
        }
        
        [MenuItem("Tools/Gameplay Tags/Reset Paths to Default")]
        public static void ResetPathsToDefault()
        {
            EditorPrefs.SetString(EditorPrefKey_OutputPath, DefaultOutputPath);
            EditorPrefs.SetString(EditorPrefKey_ResourcePath, DefaultResourcePath);
            EditorPrefs.SetString(EditorPrefKey_GeneratedFolder, DefaultGeneratedFolder);
            EditorPrefs.SetString(EditorPrefKey_GameplayTagsFolder, DefaultGameplayTagsFolder);
            Debug.Log("[GameplayTag] 경로가 기본값으로 초기화되었습니다.");
        }

        #endregion

        #region Static 함수

        /// <summary>
        /// 외부에서 호출 가능한 정적 생성 함수
        /// </summary>
        public static void GenerateCodeStatic()
        {
            try
            {
                // 모든 어셈블리에서 GameplayTagDefAttribute 수집
                var attributes = CollectGameplayTagAttributes();

                if (attributes.Count == 0)
                {
                    Debug.LogWarning("[GameplayTag] GameplayTagDefAttribute가 발견되지 않았습니다.");
                    return;
                }

                // 태그 트리 구조 생성
                var tagRoot = BuildTagTree(attributes);

                // 코드 생성
                string generatedCode = GenerateCode(tagRoot);

                // 파일 저장
                SaveGeneratedCode(generatedCode);
                Debug.Log($"[GameplayTag] 코드 생성 완료: {attributes.Count}개의 태그 발견 → {OutputPathRelative}");

                // ScriptableObject 생성 또는 갱신
                CreateOrRefreshSO(attributes);

            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameplayTag] 코드 생성 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }

        #endregion

        private static void CreateOrRefreshSO(List<GameplayTagDefAttribute> attributes)
        {
            try
            {
                // Resources.Load용 경로 (예: "GameplayTags/Generated")
                string resourceLoadPath = ResourcePathRelative;
                
                // AssetDatabase용 전체 경로 (예: "Assets/Resources/GameplayTags/Generated")
                string resourceDir = Path.GetDirectoryName(resourceLoadPath)?.Replace("\\", "/") ?? "";
                string assetFolderPath = "Assets/Resources/" + resourceDir;
                string assetFileName = DefaultResourceFileName + ".asset";
                string assetFullPath = assetFolderPath + "/" + assetFileName;
                
                // Resources.Load는 Resources 폴더 기준 상대 경로 사용
                string loadPath = resourceDir + "/" + DefaultResourceFileName;
                
                var so = Resources.Load<GameplayTagDatabase>(loadPath);
                
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<GameplayTagDatabase>();
                    
                    // 폴더가 없으면 생성 (재귀적으로)
                    EnsureFolderExists(assetFolderPath);
                    
                    AssetDatabase.CreateAsset(so, assetFullPath);
                    Debug.Log($"[GameplayTag] 새로운 GameplayTagDatabase ScriptableObject 생성: {assetFullPath}");
                }
                
                // 내부 메서드 사용 (IsAutoGenerated 체크 우회)
                so.InternalClearTags();
                foreach (var attribute in attributes)
                {
                    so.InternalAddTag(attribute.TagName, attribute.Description);
                }
                
                // 마지막에 IsAutoGenerated 설정
                so.IsAutoGenerated = true;
                Debug.Log($"[GameplayTag] IsAutoGenerated 설정: {so.IsAutoGenerated} (경로: {assetFullPath})");
                
                EditorUtility.SetDirty(so);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[GameplayTag] GameplayTagDatabase ScriptableObject 갱신 완료: {so.Tags.Count}개의 태그 등록됨");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameplayTag] ScriptableObject 생성/갱신 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// AssetDatabase 폴더가 존재하지 않으면 재귀적으로 생성
        /// </summary>
        private static void EnsureFolderExists(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            // Unity 경로 구분자로 통일
            folderPath = folderPath.Replace("\\", "/");
            
            string[] folders = folderPath.Split('/');
            string currentPath = folders[0];

            for (int i = 1; i < folders.Length; i++)
            {
                string nextPath = currentPath + "/" + folders[i];
                
                if (!AssetDatabase.IsValidFolder(nextPath))
                {
                    string result = AssetDatabase.CreateFolder(currentPath, folders[i]);
                    if (string.IsNullOrEmpty(result))
                    {
                        Debug.LogError($"[GameplayTag] 폴더 생성 실패: {nextPath}");
                        return;
                    }
                    Debug.Log($"[GameplayTag] 폴더 생성됨: {nextPath}");
                }
                
                currentPath = nextPath;
            }
        }

        private static List<GameplayTagDefAttribute> CollectGameplayTagAttributes()
        {
            var attributes = new List<GameplayTagDefAttribute>();

            // 모든 어셈블리 순회
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // 어셈블리 레벨 어트리뷰트 수집
                    var assemblyAttributes = assembly.GetCustomAttributes<GameplayTagDefAttribute>();
                    attributes.AddRange(assemblyAttributes);
                }
                catch (Exception ex)
                {
                    // 일부 어셈블리는 접근 불가할 수 있음
                    Debug.LogWarning($"[GameplayTag] 어셈블리 '{assembly.FullName}' 스캔 실패: {ex.Message}");
                }
            }

            return attributes;
        }

        private static TagNode BuildTagTree(List<GameplayTagDefAttribute> attributes)
        {
            var root = new TagNode("", "RootGameplayTag");

            foreach (var attribute in attributes)
            {
                string tagName = attribute.TagName;
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                AddTag(root, tagName);
            }

            return root;
        }

        private static void AddTag(TagNode root, string fullTagName)
        {
            var parts = fullTagName.Split('.');
            var currentNode = root;
            var currentPathBuilder = new StringBuilder();

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (i > 0) currentPathBuilder.Append(".");
                currentPathBuilder.Append(part);
                var currentPath = currentPathBuilder.ToString();

                if (!currentNode.Children.ContainsKey(part))
                {
                    currentNode.Children[part] = new TagNode(currentPath, part);
                }

                currentNode = currentNode.Children[part];
            }
        }

        private static string GenerateCode(TagNode root)
        {
            var sb = new StringBuilder();

            // 헤더
            sb.AppendLine($@"
            /// <auto-generated>
            /// 생성 시간: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            /// 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
            /// </auto-generated> 
               
            using Machamy.GameplayTags.Runtime;

            namespace Machamy.GameplayTags.Generated
            {{
                /// <summary>
                /// 자동 생성된 GameplayTag 정적 접근자
                /// </summary>
                public static class AllGameplayTags
                {{
");

            // 태그 클래스 생성
            GenerateTagClass(sb, root, 1);

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static void GenerateTagClass(StringBuilder sb, TagNode node, int level)
        {
            var indent = new string(' ', level * 4);

            foreach (var child in node.Children.Values.OrderBy(c => c.Name))
            {
                sb.AppendLine($"{indent}public static class {child.Name}");
                sb.AppendLine($"{indent}{{");

                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// 태그 전체 이름: {child.FullName}");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public const string RawTag = \"{child.FullName}\";");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// GameplayTag 인스턴스를 가져옵니다");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public static GameplayTag Get() => GameplayTagManager.RequestTag(RawTag);");
                sb.AppendLine();
                sb.AppendLine($"{indent}    /// <summary>");
                sb.AppendLine($"{indent}    /// GameplayTag 속성 (캐시됨)");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public static GameplayTag Tag => Get();");

                // 자식 노드가 있으면 재귀 생성
                if (child.Children.Count > 0)
                {
                    sb.AppendLine();
                    GenerateTagClass(sb, child, level + 1);
                }

                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }

        private static void SaveGeneratedCode(string code)
        {
            // Generated 폴더가 없으면 생성 (AssetDatabase는 상대 경로 사용)
            if (!AssetDatabase.IsValidFolder(GeneratedFolderPathRelative))
            {
                if (!AssetDatabase.IsValidFolder(GameplayTagsFolderPathRelative))
                {
                    AssetDatabase.CreateFolder("Assets", "GameplayTags");
                }
                
                string folderName = Path.GetFileName(GeneratedFolderPathRelative);
                AssetDatabase.CreateFolder(GameplayTagsFolderPathRelative, folderName);
            }

            // 파일 저장 (절대 경로로 변환)
            string absolutePath = GetAbsolutePath(OutputPathRelative);
            string directoryPath = Path.GetDirectoryName(absolutePath);
            
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
            
            if (!string.IsNullOrEmpty(absolutePath))
            {
                File.WriteAllText(absolutePath, code, Encoding.UTF8);
            }

            // AssetDatabase 리프레시
            AssetDatabase.Refresh();

            Debug.Log($"[GameplayTag] 코드가 생성되었습니다: {OutputPathRelative}");
        }
        

        private class TagNode
        {
            public string Name { get; }
            public string FullName { get; }
            
            public Dictionary<string, TagNode> Children { get; }

            public TagNode(string fullName, string name)
            {
                FullName = fullName;
                Name = name;
                Children = new Dictionary<string, TagNode>();
            }
        }

    }
}

