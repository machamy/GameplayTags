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
    /// GameplayTag 코드를 생성하는 에디터 유틸리티 클래스
    /// </summary>
    [InitializeOnLoad]
    public static class GameplayTagCodeGenerator
    {
        // EditorPrefs 키
        private const string EditorPrefKey_AutoGenerate = "GameplayTag_AutoGenerateCode";
        private const string EditorPrefKey_OutputPath = "GameplayTag_OutputPath";
        private const string EditorPrefKey_ResourcePath = "GameplayTag_ResourcePath";
        private const string EditorPrefKey_GeneratedFolder = "GameplayTag_GeneratedFolder";
        private const string EditorPrefKey_GameplayTagsFolder = "GameplayTag_GameplayTagsFolder";
        private const string EditorPrefKey_LastAssemblyHash = "GameplayTag_LastAssemblyHash";
        private const string EditorPrefKey_LastTagsHash = "GameplayTag_LastTagsHash";
        
        // 기본 경로값
        // 생성 코드의 클래스/네임스페이스 이름 (패키지 소스에 리터럴이 남지 않도록 조합)
        private const string GeneratedClassName = "All" + "GameplayTags";
        private const string GeneratedNamespace = "Machamy.GameplayTags." + "Generated";
        private const string DefaultOutputPath = "Assets/GameplayTags/Generated/" + GeneratedClassName + ".g.cs";
        private const string DefaultResourcePath = "GameplayTags/Generated/";
        private const string DefaultResourceFileName = "GameplayTagDatabase";
        private const string DefaultGeneratedFolder = "Assets/GameplayTags/Generated";
        private const string DefaultGameplayTagsFolder = "Assets/GameplayTags";
        
        
        private static bool IsAutoGenerateEnabled 
        {
            get => EditorPrefs.GetBool(EditorPrefKey_AutoGenerate, true);
            set => EditorPrefs.SetBool(EditorPrefKey_AutoGenerate, value);
        }
        
        // EditorPrefs에서 경로 가져오기
        private static string OutputPathRelative => EditorPrefs.GetString(EditorPrefKey_OutputPath, DefaultOutputPath);
        
        private static string ResourcePathRelative => EditorPrefs.GetString(EditorPrefKey_ResourcePath, DefaultResourcePath);
        private static string GeneratedFolderPathRelative => EditorPrefs.GetString(EditorPrefKey_GeneratedFolder, DefaultGeneratedFolder);
        private static string GameplayTagsFolderPathRelative => EditorPrefs.GetString(EditorPrefKey_GameplayTagsFolder, DefaultGameplayTagsFolder);
        
        // 절대 경로로 변환
        private static string GetAbsolutePath(string relativePath) 
            => Path.Combine(Application.dataPath.Replace("Assets", ""), relativePath);
        
        
        /// 실행 순서:
        /// 1. 어셈블리 스캔 - 모든 어셈블리에서 GameplayTagDefAttribute 수집
        /// 2. 어셈블리 해시 계산 및 비교 - 변경이 있으면 SO 갱신, 없으면 스킵
        /// 3. GameplayTagManager 초기화 - SO에서 태그 로드 및 태그 트리 생성
        /// 4. 태그 해시 계산 및 비교 - 변경이 있으면 코드 생성, 없으면 스킵
        static GameplayTagCodeGenerator()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            UpdateScriptableObjectFromAssembly();
            EditorApplication.delayCall += () =>
            {

                GameplayTagManager.InitializeAfterSO();
                CheckAndGenerateCode();
            };
        }

        /// <summary>
        /// 어셈블리에서 태그를 읽어와 ScriptableObject를 갱신합니다.
        /// </summary>
        private static void UpdateScriptableObjectFromAssembly()
        {
            try
            {
                var attributes = CollectGameplayTagAttributes();

                if (attributes.Count == 0)
                {
                    Debug.LogWarning("[GameplayTag] GameplayTagDefAttribute가 발견되지 않았습니다.");
                    return;
                }

                int currentAssemblyHash = CalculateAssemblyHash(attributes);
                int lastAssemblyHash = EditorPrefs.GetInt(EditorPrefKey_LastAssemblyHash, 0);
                Debug.Log($"[GameplayTag] 로드된 어셈블리 태그 수: {attributes.Count}");
                Debug.Log($"[GameplayTag] 현재 어셈블리 해시: {currentAssemblyHash}, 마지막 저장된 해시: {lastAssemblyHash}");
                if (currentAssemblyHash == lastAssemblyHash)
                {
                    Debug.Log($"[GameplayTag] 어셈블리 변경 없음 - SO 갱신 스킵");
                    return;
                }

                Debug.Log($"[GameplayTag] 어셈블리 변경 감지 - SO 갱신 시작");

                CreateOrRefreshSO(attributes);
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorPrefs.SetInt(EditorPrefKey_LastAssemblyHash, currentAssemblyHash);

                Debug.Log($"[GameplayTag] ScriptableObject 갱신 완료: {attributes.Count}개의 태그");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameplayTag] ScriptableObject 갱신 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 태그 해시값을 비교하여 변경되었으면 코드를 생성합니다.
        /// </summary>
        private static void CheckAndGenerateCode()
        {
            if (!IsAutoGenerateEnabled)
                return;

            GameplayTagManager.InitializeIfNeeded();

            int currentHash = GameplayTagManager.CurrentTagsHash;
            int lastHash = EditorPrefs.GetInt(EditorPrefKey_LastTagsHash, 0);


            if (currentHash != lastHash)
            {
                
                Debug.Log($"[GameplayTag] 태그 변경 감지, 파일 확인 중...");
                int generatedCodeHash = GetGeneratedCodeHash();
                Debug.Log($"[GameplayTag] 현재 태그 해시: {currentHash}, 마지막 저장된 해시: {lastHash}, 생성된 코드 해시: {generatedCodeHash}");
                if (generatedCodeHash == currentHash)
                {
                    Debug.Log($"[GameplayTag] 생성된 코드가 최신 상태입니다. 코드 생성 스킵");
                    EditorPrefs.SetInt(EditorPrefKey_LastTagsHash, currentHash);
                    return;
                }
                
                GenerateCodeOnly();
                
                EditorPrefs.SetInt(EditorPrefKey_LastTagsHash, currentHash);
            }
            else
            {
                Debug.Log($"[GameplayTag] 태그 변경 없음 - 코드 생성 스킵");
            }
        }
        
        private static int GetGeneratedCodeHash()
        {
            string absolutePath = GetAbsolutePath(OutputPathRelative);
            if (!File.Exists(absolutePath))
                return 0;

            string[] lines = File.ReadAllLines(absolutePath);
            if (lines.Length == 0)
                return 0;

            string firstLine = lines[0];
            if (firstLine.StartsWith("/// hash:"))
            {
                string hashString = firstLine.Substring("/// hash:".Length).Trim();
                if (int.TryParse(hashString, out int hash))
                {
                    return hash;
                }
            }

            return 0;
        }

        /// <summary>
        /// 코드만 생성합니다.
        /// </summary>
        private static void GenerateCodeOnly()
        {
            try
            {
                var attributes = CollectGameplayTagAttributes();

                if (attributes.Count == 0)
                {
                    Debug.LogWarning("[GameplayTag] GameplayTagDefAttribute가 발견되지 않았습니다.");
                    return;
                }

                var tagRoot = BuildTagTree(attributes);
                string generatedCode = GenerateCode(tagRoot);

                SaveGeneratedCode(generatedCode);
                Debug.Log($"[GameplayTag] 코드 생성 완료: {attributes.Count}개의 태그 발견 → {OutputPathRelative}");

                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameplayTag] 코드 생성 중 오류 발생: {ex.Message}\n{ex.StackTrace}");
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
            bool current = IsAutoGenerateEnabled;
            EditorPrefs.SetBool(EditorPrefKey_AutoGenerate, !current);
            Debug.Log($"[GameplayTag] 자동 생성: {(!current ? "활성화" : "비활성화")}");
        }

        [MenuItem("Tools/Gameplay Tags/Toggle Auto-Generate On Load", true)]
        public static bool ToggleAutoGenerateValidate()
        {
            bool current = IsAutoGenerateEnabled;
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
                var attributes = CollectGameplayTagAttributes();

                if (attributes.Count == 0)
                {
                    Debug.LogWarning("[GameplayTag] GameplayTagDefAttribute가 발견되지 않았습니다.");
                    return;
                }

                int currentAssemblyHash = CalculateAssemblyHash(attributes);
                EditorPrefs.SetInt(EditorPrefKey_LastAssemblyHash, currentAssemblyHash);

                CreateOrRefreshSO(attributes);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GameplayTagManager.ReloadTags();
                int currentHash = GameplayTagManager.CurrentTagsHash;

                var tagRoot = BuildTagTree(attributes);
                string generatedCode = GenerateCode(tagRoot);

                SaveGeneratedCode(generatedCode);
                Debug.Log($"[GameplayTag] 코드 생성 완료: {attributes.Count}개의 태그 발견 → {OutputPathRelative}");

                AssetDatabase.Refresh();

                EditorPrefs.SetInt(EditorPrefKey_LastTagsHash, currentHash);
                Debug.Log($"[GameplayTag] 해시값 저장 완료");
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
                string resourceLoadPath = ResourcePathRelative;
                string resourceDir = Path.GetDirectoryName(resourceLoadPath)?.Replace("\\", "/") ?? "";
                string assetFolderPath = "Assets/Resources/" + resourceDir;
                string assetFileName = DefaultResourceFileName + ".asset";
                string assetFullPath = assetFolderPath + "/" + assetFileName;
                string loadPath = resourceDir + "/" + DefaultResourceFileName;
                
                var so = Resources.Load<GameplayTagDatabaseSO>(loadPath);
                
                if (so == null)
                {
                    so = ScriptableObject.CreateInstance<GameplayTagDatabaseSO>();
                    EnsureFolderExists(assetFolderPath);
                    AssetDatabase.CreateAsset(so, assetFullPath);
                    Debug.Log($"[GameplayTag] 새로운 GameplayTagDatabase ScriptableObject 생성: {assetFullPath}");
                }
                
                so.InternalClearTags();
                foreach (var attribute in attributes)
                {
                    so.InternalAddTag(attribute.TagName, attribute.Description);
                }
                
                so.IsAutoGenerated = true;
                // Debug.Log($"[GameplayTag] IsAutoGenerated 설정: {so.IsAutoGenerated}");
                
                EditorUtility.SetDirty(so);

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

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var assemblyAttributes = assembly.GetCustomAttributes<GameplayTagDefAttribute>();
                    attributes.AddRange(assemblyAttributes);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GameplayTag] 어셈블리 '{assembly.FullName}' 스캔 실패: {ex.Message}");
                }
            }

            return attributes;
        }

        /// <summary>
        /// 어셈블리 attribute들의 해시값을 계산합니다.
        /// </summary>
        private static int CalculateAssemblyHash(List<GameplayTagDefAttribute> attributes)
        {
            if (attributes == null || attributes.Count == 0)
                return 0;

            var sortedData = attributes
                .Select(attr => $"{attr.TagName}|{attr.Description ?? ""}")
                .OrderBy(s => s)
                .ToArray();

            unchecked
            {
                int hash = 17;
                foreach (string data in sortedData)
                {
                    hash = hash * 31 + data.GetHashCode();
                }
                return hash;
            }
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

            sb.AppendLine($@"
/// hash: {GameplayTagManager.CurrentTagsHash}
/// <auto-generated>
/// 이 파일은 자동 생성되었습니다. 직접 수정하지 마세요.
/// </auto-generated> 
   
using Machamy.GameplayTags.Runtime;

namespace {GeneratedNamespace}
{{
    /// <summary>
    /// 자동 생성된 GameplayTag 정적 접근자
    /// </summary>
    public static class {GeneratedClassName}
    {{
");

            GenerateTagClass(sb, root, 1);

            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            // Enum 생성
            GeneratedEnum(sb, root, 0);

            return sb.ToString();
        }

        private static void GenerateTagClass(StringBuilder sb, TagNode node, int level)
        {
            var indent = new string(' ', level * 4);

            foreach (var child in node.Children.Values.OrderBy(c => c.Name))
            {
                sb.AppendLine($"{indent}public static class {ToValidIdentifier(child.Name)}");
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
                sb.AppendLine($"{indent}    /// GameplayTag 속성");
                sb.AppendLine($"{indent}    /// </summary>");
                sb.AppendLine($"{indent}    public static GameplayTag Tag => Get();");

                if (child.Children.Count > 0)
                {
                    sb.AppendLine();
                    GenerateTagClass(sb, child, level + 1);
                }

                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sb"></param>
        /// <param name="node"></param>
        /// <param name="level"></param>
        private static void GeneratedEnum(StringBuilder sb, TagNode node, int level)
        {
            // enum 추가
            var indent = new string(' ', level * 4);
            
            List<string> allTags = new List<string>();

            void CollectTags(TagNode n)
            {
                foreach (var child in n.Children.Values)
                {
                    allTags.Add(child.FullName);
                    CollectTags(child);
                }
            }
            CollectTags(node);
            
            #if UNITY_BEHAVIOR
            sb.AppendLine($"{indent}[Unity.Behavior.BlackboardEnum]");
            #else
            sb.AppendLine($"#if UNITY_BEHAVIOR");
            sb.AppendLine($"{indent}[Unity.Behavior.BlackboardEnum]");
            sb.AppendLine($"#endif");
            #endif
            sb.AppendLine($"{indent}public enum GameplayTagEnum");
            sb.AppendLine($"{indent}{{");
            foreach (var child in allTags)
            {
                sb.AppendLine($"{indent}    {ToValidIdentifier(child.Replace('.', '_'))},");
            }
            sb.AppendLine($"{indent}}}");
            
            // enum static helper 추가
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"{indent}/// <summary>");
            sb.AppendLine($"{indent}/// {node.Name}Enum 헬퍼 클래스");
            sb.AppendLine($"{indent}/// </summary>");
            sb.AppendLine($"{indent}public static class GameplayTagEnumHelper");
            sb.AppendLine($"{indent}{{");
            // ToTag 함수
            sb.AppendLine($"{indent}    /// <summary>");
            sb.AppendLine($"{indent}    /// GameplayTagEnum 값을 GameplayTag로 변환합니다.");
            sb.AppendLine($"{indent}    /// </summary>");
            sb.AppendLine($"{indent}    public static GameplayTag ToTag(this GameplayTagEnum tagEnum)");
            sb.AppendLine($"{indent}    {{");
            sb.AppendLine($"{indent}        switch (tagEnum)");;
            sb.AppendLine($"{indent}        {{");
            foreach (var child in allTags)
            {
                sb.AppendLine($"{indent}            case GameplayTagEnum.{ToValidIdentifier(child.Replace('.', '_'))}:");
                sb.AppendLine($"{indent}                return GameplayTagManager.RequestTag(\"{child}\");");
            }
            sb.AppendLine($"{indent}            default:");
            sb.AppendLine($"{indent}                return GameplayTag.None;");
            sb.AppendLine($"{indent}        }}");   
            sb.AppendLine($"{indent}    }}");
            sb.AppendLine($"{indent}}}");
            
        }

        private static void SaveGeneratedCode(string code)
        {
            if (!AssetDatabase.IsValidFolder(GeneratedFolderPathRelative))
            {
                if (!AssetDatabase.IsValidFolder(GameplayTagsFolderPathRelative))
                {
                    AssetDatabase.CreateFolder("Assets", "GameplayTags");
                }
                
                string folderName = Path.GetFileName(GeneratedFolderPathRelative);
                AssetDatabase.CreateFolder(GameplayTagsFolderPathRelative, folderName);
            }

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

            Debug.Log($"[GameplayTag] 코드가 생성되었습니다: {OutputPathRelative}");
        }

        private static string ToValidIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "_";
            }

            var sb = new StringBuilder(value.Length + 1);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool valid = i == 0
                    ? char.IsLetter(c) || c == '_'
                    : char.IsLetterOrDigit(c) || c == '_';

                if (valid)
                {
                    sb.Append(c);
                    continue;
                }

                if (i == 0 && char.IsDigit(c))
                {
                    sb.Append('_');
                    sb.Append(c);
                    continue;
                }

                sb.Append('_');
            }

            string identifier = sb.ToString();
            return CSharpKeywords.Contains(identifier) ? "_" + identifier : identifier;
        }

        private static readonly HashSet<string> CSharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
        };
        

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

