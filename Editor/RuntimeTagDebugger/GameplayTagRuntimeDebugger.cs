using System;
using System.Collections.Generic;
using System.Linq;
using Machamy.GameplayTags.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Machamy.GameplayTags.Editor
{
    /// <summary>
    /// 런타임 중 GameplayTagManager에 등록된 태그들을 디버깅하기 위한 에디터 윈도우
    /// </summary>
    public class GameplayTagRuntimeDebugger : EditorWindow
    {
        private enum ViewMode
        {
            IdSorted,      // ID 순으로 정렬
            StringSorted,  // 문자열 순으로 정렬
            TreeMode       // 트리 구조로 표시
        }

        [SerializeField]
        private VisualTreeAsset visualTreeAsset;

 
        private ViewMode _currentViewMode = ViewMode.IdSorted;
        private TextField _searchField;
        private Label _statusLabel;
        private Button _idSortButton;
        private Button _stringSortButton;
        private Button _treeModeButton;
        private TextField _requestTagField;
        private Button _requestTagButton;
        private Label _requestResultLabel;
        private ScrollView _contentArea;
        private VisualElement _warningContainer;
        private VisualElement _mainContent;
        private Label _headerTitle;
        private Label _headerSubtitle;
        private Label _requestTitle;
        private Label _warningLabel;
        private Label _warningDesc;

        private string _searchQuery = "";
        private const float RefreshInterval = 0.5f;
        private double _lastRefreshTime;

        [MenuItem("Tools/Gameplay Tags/Runtime Debugger")]
        public static void ShowWindow()
        {
            GameplayTagRuntimeDebugger wnd = GetWindow<GameplayTagRuntimeDebugger>();
            wnd.titleContent = new GUIContent("Gameplay Tag Runtime Debugger");
            wnd.minSize = new Vector2(600, 400);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            
            // 창이 열릴 때마다 UI 갱신 (로컬라이제이션 적용)
            if (rootVisualElement != null && rootVisualElement.childCount > 0)
            {
                // 이미 UI가 생성되어 있으면 로컬라이제이션만 다시 적용
                UpdateLocalization();
            }
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 플레이 모드 진입 또는 종료 시 UI 재생성
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                rootVisualElement.Clear();
                CreateGUI();
            }
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // UXML 로드 - null이면 동적으로 로드
            if (visualTreeAsset == null)
            {
                string[] guids = AssetDatabase.FindAssets("GameplayTagRuntimeDebugger t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }

            if (visualTreeAsset != null)
            {
                visualTreeAsset.CloneTree(root);
            }
            else
            {
                root.Add(new Label("UXML 파일을 찾을 수 없습니다. Assets/GameplayTags/Editor/RuntimeTagDebugger/GameplayTagRuntimeDebugger.uxml 파일이 존재하는지 확인해주세요."));
                return;
            }


            // UI 요소 참조
            _headerTitle = root.Q<Label>("header-title");
            _headerSubtitle = root.Q<Label>("header-subtitle");
            _warningContainer = root.Q<VisualElement>("warning-container");
            _warningLabel = root.Q<Label>("warning-label");
            _warningDesc = root.Q<Label>("warning-desc");
            _mainContent = root.Q<VisualElement>("main-content");
            _idSortButton = root.Q<Button>("btn-id-sort");
            _stringSortButton = root.Q<Button>("btn-string-sort");
            _treeModeButton = root.Q<Button>("btn-tree-mode");
            _searchField = root.Q<TextField>("search-field");
            _statusLabel = root.Q<Label>("status-label");
            _contentArea = root.Q<ScrollView>("content-area");
            _requestTitle = root.Q<Label>("request-title");
            _requestTagField = root.Q<TextField>("request-field");
            _requestTagButton = root.Q<Button>("btn-request-tag");
            _requestResultLabel = root.Q<Label>("request-result");

            // 이벤트 핸들러 설정
            SetupEventHandlers();

            // 플레이 모드 체크
            UpdatePlayModeUI();
            
            // 로컬라이제이션 적용
            UpdateLocalization();

            // 초기 새로고침
            if (EditorApplication.isPlaying)
            {
                RefreshTagList();
            }
        }

        private void SetupEventHandlers()
        {
            if (_idSortButton != null)
                _idSortButton.clicked += () => SetViewMode(ViewMode.IdSorted);

            if (_stringSortButton != null)
                _stringSortButton.clicked += () => SetViewMode(ViewMode.StringSorted);

            if (_treeModeButton != null)
                _treeModeButton.clicked += () => SetViewMode(ViewMode.TreeMode);

            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt =>
                {
                    _searchQuery = evt.newValue.ToLower();
                    RefreshTagList();
                });
            }

            if (_requestTagButton != null)
                _requestTagButton.clicked += OnRequestTag;
        }

        private void UpdatePlayModeUI()
        {
            bool isPlaying = EditorApplication.isPlaying;

            if (_warningContainer != null)
                _warningContainer.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;

            if (_mainContent != null)
                _mainContent.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void UpdateLocalization()
        {
            // 디버그: 로컬라이제이션 적용 확인
            var currentLang = GameplayTagEditorLocalization.CurrentLanguage;
            Debug.Log($"[RuntimeDebugger] UpdateLocalization() 호출됨. 현재 언어: {currentLang}");
            
            if (_headerTitle != null)
            {
                _headerTitle.text = GameplayTagEditorLocalization.Get("debugger.title");
                Debug.Log($"[RuntimeDebugger] header-title 설정: {_headerTitle.text}");
            }
            else
            {
                Debug.LogWarning("[RuntimeDebugger] _headerTitle이 null입니다!");
            }

            if (_headerSubtitle != null)
                _headerSubtitle.text = GameplayTagEditorLocalization.Get("debugger.subtitle");

            if (_warningLabel != null)
                _warningLabel.text = GameplayTagEditorLocalization.Get("debugger.warning");

            if (_warningDesc != null)
                _warningDesc.text = GameplayTagEditorLocalization.Get("debugger.warningDesc");

            if (_idSortButton != null)
                _idSortButton.text = GameplayTagEditorLocalization.Get("debugger.viewIdSort");

            if (_stringSortButton != null)
                _stringSortButton.text = GameplayTagEditorLocalization.Get("debugger.viewStringSort");

            if (_treeModeButton != null)
                _treeModeButton.text = GameplayTagEditorLocalization.Get("debugger.viewTreeMode");

            if (_searchField != null)
                _searchField.label = GameplayTagEditorLocalization.Get("debugger.search");

            if (_requestTitle != null)
                _requestTitle.text = GameplayTagEditorLocalization.Get("debugger.requestTitle");

            if (_requestTagField != null)
                _requestTagField.value = GameplayTagEditorLocalization.Get("debugger.requestPlaceholder");

            if (_requestTagButton != null)
                _requestTagButton.text = GameplayTagEditorLocalization.Get("debugger.requestButton");
        }

        private void SetViewMode(ViewMode mode)
        {
            _currentViewMode = mode;
            UpdateButtonStates();
            RefreshTagList();
        }

        private void UpdateButtonStates()
        {
            // 모든 버튼에서 active 클래스 제거
            _idSortButton?.RemoveFromClassList("view-mode-button-active");
            _stringSortButton?.RemoveFromClassList("view-mode-button-active");
            _treeModeButton?.RemoveFromClassList("view-mode-button-active");

            // 현재 선택된 버튼에 active 클래스 추가
            Button activeButton = _currentViewMode switch
            {
                ViewMode.IdSorted => _idSortButton,
                ViewMode.StringSorted => _stringSortButton,
                ViewMode.TreeMode => _treeModeButton,
                _ => null
            };

            activeButton?.AddToClassList("view-mode-button-active");
        }

        private void OnRequestTag()
        {
            if (!EditorApplication.isPlaying)
            {
                _requestResultLabel.text = GameplayTagEditorLocalization.Get("debugger.requestPlayModeOnly");
                _requestResultLabel.style.color = Color.red;
                return;
            }

            string tagString = _requestTagField.value;
            if (string.IsNullOrWhiteSpace(tagString) || 
                tagString == GameplayTagEditorLocalization.Get("debugger.requestPlaceholder"))
            {
                _requestResultLabel.text = GameplayTagEditorLocalization.Get("debugger.requestEmptyField");
                _requestResultLabel.style.color = Color.red;
                return;
            }

            try
            {
                var tag = GameplayTagManager.RequestTag(tagString);
                if (tag.IsValid)
                {
                    _requestResultLabel.text = GameplayTagEditorLocalization.Format("debugger.requestSuccess", tag.RawTagId, tag.TagName);
                    _requestResultLabel.style.color = Color.green;
                }
                else
                {
                    _requestResultLabel.text = GameplayTagEditorLocalization.Format("debugger.requestNotFound", tagString);
                    _requestResultLabel.style.color = Color.red;
                }
            }
            catch (Exception ex)
            {
                _requestResultLabel.text = GameplayTagEditorLocalization.Format("debugger.requestError", ex.Message);
                _requestResultLabel.style.color = Color.red;
            }
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying)
                return;

            // 주기적으로 새로고침
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > RefreshInterval)
            {
                RefreshTagList();
            }
        }

        private void RefreshTagList()
        {
            if (!EditorApplication.isPlaying)
                return;

            _lastRefreshTime = EditorApplication.timeSinceStartup;
            _contentArea?.Clear();

            ReadOnlySpan<GameplayTag> allTagsSpan = GameplayTagManager.GetAllTags();
            List<GameplayTag> allTags = new List<GameplayTag>(allTagsSpan.ToArray());
            
            if (allTags.Count == 0)
            {
                if (_statusLabel != null)
                    _statusLabel.text = GameplayTagEditorLocalization.Get("debugger.statusEmpty");
                return;
            }

            // 검색 필터 적용
            var filteredTags = allTags;
            if (!string.IsNullOrWhiteSpace(_searchQuery))
            {
                filteredTags = allTags.Where(tag => 
                    tag.TagName.ToLower().Contains(_searchQuery) || 
                    tag.RawTagId.ToString().Contains(_searchQuery)
                ).ToList();
            }

            if (_statusLabel != null)
                _statusLabel.text = GameplayTagEditorLocalization.Format("debugger.statusCount", allTags.Count, filteredTags.Count);

            // 뷰 모드에 따라 표시
            switch (_currentViewMode)
            {
                case ViewMode.IdSorted:
                    DisplayIdSorted(filteredTags);
                    break;
                case ViewMode.StringSorted:
                    DisplayStringSorted(filteredTags);
                    break;
                case ViewMode.TreeMode:
                    DisplayTreeMode(filteredTags);
                    break;
            }
        }

        private void DisplayIdSorted(List<GameplayTag> tags)
        {
            var sortedTags = tags.OrderBy(t => t.RawTagId).ToList();

            foreach (var tag in sortedTags)
            {
                var tagElement = CreateTagElement(tag);
                _contentArea.Add(tagElement);
            }
        }

        private void DisplayStringSorted(List<GameplayTag> tags)
        {
            var sortedTags = tags.OrderBy(t => t.TagName).ToList();

            foreach (var tag in sortedTags)
            {
                var tagElement = CreateTagElement(tag);
                _contentArea.Add(tagElement);
            }
        }

        private void DisplayTreeMode(List<GameplayTag> tags)
        {
            // 태그를 계층 구조로 구성
            var rootNodes = BuildTagTree(tags);

            // 트리 렌더링
            foreach (var node in rootNodes.OrderBy(n => n.Name))
            {
                RenderTreeNode(_contentArea, node, 0);
            }
        }

        private List<TagTreeNode> BuildTagTree(List<GameplayTag> tags)
        {
            var rootNodes = new List<TagTreeNode>();
            var nodeDict = new Dictionary<string, TagTreeNode>();

            foreach (var tag in tags)
            {
                var parts = tag.TagName.Split('.');
                TagTreeNode parentNode = null;
                string currentPath = "";

                for (int i = 0; i < parts.Length; i++)
                {
                    if (i > 0) currentPath += ".";
                    currentPath += parts[i];

                    if (!nodeDict.TryGetValue(currentPath, out var node))
                    {
                        node = new TagTreeNode
                        {
                            Name = parts[i],
                            FullPath = currentPath,
                            Tag = (i == parts.Length - 1) ? tag : default
                        };
                        nodeDict[currentPath] = node;

                        if (parentNode == null)
                        {
                            rootNodes.Add(node);
                        }
                        else
                        {
                            parentNode.Children.Add(node);
                        }
                    }

                    parentNode = node;
                }
            }

            return rootNodes;
        }

        private void RenderTreeNode(VisualElement parent, TagTreeNode node, int depth)
        {
            // 자식이 있는 경우
            if (node.Children.Count > 0)
            {
                var foldout = new Foldout();
                foldout.text = node.Tag.IsValid 
                    ? $"{node.Name} [ID: {node.Tag.RawTagId}]" 
                    : node.Name;
                foldout.value = true;
                foldout.AddToClassList("tree-foldout");

                if (node.Tag.IsValid)
                {
                    foldout.AddToClassList("tree-node-label-bold");
                }

                parent.Add(foldout);

                foreach (var child in node.Children.OrderBy(c => c.Name))
                {
                    RenderTreeNode(foldout, child, 0);
                }
            }
            else
            {
                var label = new Label(node.Tag.IsValid 
                    ? $"• {node.Name} [ID: {node.Tag.RawTagId}]" 
                    : $"• {node.Name}");
                label.AddToClassList("tree-node-label");
                
                if (node.Tag.IsValid)
                {
                    label.AddToClassList("tree-node-label-bold");
                }

                parent.Add(label);
            }
        }

        private VisualElement CreateTagElement(GameplayTag tag)
        {
            var container = new VisualElement();
            container.AddToClassList("tag-item");

            // ID 라벨
            var idLabel = new Label($"ID: {tag.RawTagId}");
            idLabel.AddToClassList("tag-id");
            container.Add(idLabel);

            // 태그 문자열 라벨
            var stringLabel = new Label(tag.TagName);
            stringLabel.AddToClassList("tag-name");
            container.Add(stringLabel);

            // 복사 버튼
            var copyButton = new Button(() => 
            {
                GUIUtility.systemCopyBuffer = tag.TagName;
                Debug.Log(GameplayTagEditorLocalization.Format("debugger.tagCopied", tag.TagName));
            }) { text = "📋" };
            copyButton.AddToClassList("tag-copy-button");
            container.Add(copyButton);

            return container;
        }

        private class TagTreeNode
        {
            public string Name;
            public string FullPath;
            public GameplayTag Tag;
            public List<TagTreeNode> Children = new List<TagTreeNode>();
        }
    }
}

