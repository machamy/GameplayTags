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
    /// GameplayTag 통합 관리 에디터 윈도우
    /// </summary>
    public class GameplayTagEditor : EditorWindow
    {
        public enum SortMode
        {
            Alphabetical,
            Hierarchical,
            DateAdded
        }

        public enum ViewMode
        {
            All,
            Assembly,
            User
        }

        [SerializeField]
        private VisualTreeAsset visualTreeAsset;

        // 탭 뷰
        private VisualElement viewAll;
        private VisualElement viewAssembly;
        private VisualElement viewUser;
        private Button tabAllButton;
        private Button tabAssemblyButton;
        private Button tabUserButton;
        
        // 모든 태그 뷰
        private ScrollView allTagsScroll;
        private Label allCountLabel;
        private Toggle showAssemblyToggle;
        private Toggle showUserToggle;
        private VisualElement filterSection;
        
        // 공통 정렬
        private DropdownField sortDropdown;

        private Label assemblyCountLabel;
        private Label userCountLabel;
        private Label statusLabel;
        private Label headerTitle;
        private Label headerSubtitle;
        private ScrollView assemblyTagsScroll;
        private ScrollView userTagsScroll;
        private DropdownField databaseDropdown;
        private DropdownField languageDropdown;
        private TextField tagNameField;
        private TextField tagDescriptionField;
        private Button addTagButton;
        private Button generateCodeButton;
        private Button reloadSystemButton;
        private Button openDebuggerButton;
        private Button createDatabaseButton;
        private Toggle autoGenerateToggle;

        private List<GameplayTagDefAttribute> assemblyTags = new();
        private List<GameplayTagDatabase> userDatabases = new();
        private GameplayTagDatabase selectedDatabase;

        private ViewMode currentViewMode = ViewMode.All;
        private SortMode sortMode = SortMode.Alphabetical;
        private bool showAssemblyTags = true;
        private bool showUserTags = true;

        [MenuItem("Tools/Gameplay Tags/Open Tag Manager ??")]
        public static void ShowWindow()
        {
            GameplayTagEditor wnd = GetWindow<GameplayTagEditor>();
            wnd.titleContent = new GUIContent("Gameplay Tag Manager");
            wnd.minSize = new Vector2(900, 600);
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            // UXML 로드 - null이면 동적으로 로드
            if (visualTreeAsset == null)
            {
                // UXML 파일 경로를 찾아서 로드
                string[] guids = AssetDatabase.FindAssets("GameplayTagEditor t:VisualTreeAsset");
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
                root.Add(new Label("UXML 파일을 찾을 수 없습니다. Assets/GameplayTags/Editor/TagEditorWindow/GameplayTagEditor.uxml 파일이 존재하는지 확인해주세요."));
                return;
            }

            // UI 요소 참조
            headerTitle = root.Q<Label>("header-title");
            headerSubtitle = root.Q<Label>("header-subtitle");
            
            // 탭 뷰
            viewAll = root.Q<VisualElement>("view-all");
            viewAssembly = root.Q<VisualElement>("view-assembly");
            viewUser = root.Q<VisualElement>("view-user");
            
            tabAllButton = root.Q<Button>("btn-tab-all");
            tabAssemblyButton = root.Q<Button>("btn-tab-assembly");
            tabUserButton = root.Q<Button>("btn-tab-user");
            
            // 모든 태그 뷰
            allTagsScroll = root.Q<ScrollView>("scroll-all-tags");
            allCountLabel = root.Q<Label>("label-all-count");
            showAssemblyToggle = root.Q<Toggle>("toggle-show-assembly");
            showUserToggle = root.Q<Toggle>("toggle-show-user");
            filterSection = root.Q<VisualElement>("filter-section");
            
            // 공통 정렬
            sortDropdown = root.Q<DropdownField>("dropdown-sort");
            
            assemblyCountLabel = root.Q<Label>("label-assembly-count");
            userCountLabel = root.Q<Label>("label-user-count");
            statusLabel = root.Q<Label>("label-status");
            assemblyTagsScroll = root.Q<ScrollView>("scroll-assembly-tags");
            userTagsScroll = root.Q<ScrollView>("scroll-user-tags");
            databaseDropdown = root.Q<DropdownField>("dropdown-databases");
            languageDropdown = root.Q<DropdownField>("dropdown-language");
            tagNameField = root.Q<TextField>("field-tag-name");
            tagDescriptionField = root.Q<TextField>("field-tag-description");
            addTagButton = root.Q<Button>("btn-add-tag");
            generateCodeButton = root.Q<Button>("btn-generate-code");
            reloadSystemButton = root.Q<Button>("btn-reload-system");
            openDebuggerButton = root.Q<Button>("btn-open-debugger");
            createDatabaseButton = root.Q<Button>("btn-create-database");
            autoGenerateToggle = root.Q<Toggle>("toggle-auto-generate");

            SetupTabs();
            SetupLanguageDropdown();
            SetupSortDropdowns();
            SetupEventHandlers();
            
            // 데이터 로드
            RefreshData();
            UpdateLocalization();
        }

        private void SetupTabs()
        {
            if (tabAllButton != null)
            {
                tabAllButton.clicked += () => SwitchTab(ViewMode.All);
            }
            
            if (tabAssemblyButton != null)
            {
                tabAssemblyButton.clicked += () => SwitchTab(ViewMode.Assembly);
            }
            
            if (tabUserButton != null)
            {
                tabUserButton.clicked += () => SwitchTab(ViewMode.User);
            }
            
            // 필터 토글
            if (showAssemblyToggle != null)
            {
                showAssemblyToggle.value = showAssemblyTags;
                showAssemblyToggle.RegisterValueChangedCallback(evt =>
                {
                    showAssemblyTags = evt.newValue;
                    RefreshAllTagsUI();
                });
            }
            
            if (showUserToggle != null)
            {
                showUserToggle.value = showUserTags;
                showUserToggle.RegisterValueChangedCallback(evt =>
                {
                    showUserTags = evt.newValue;
                    RefreshAllTagsUI();
                });
            }
            
            // 초기 탭 선택
            SwitchTab(ViewMode.All);
        }

        private void SwitchTab(ViewMode mode)
        {
            currentViewMode = mode;
            
            // 모든 탭 버튼 비활성화
            tabAllButton?.RemoveFromClassList("tab-button-active");
            tabAssemblyButton?.RemoveFromClassList("tab-button-active");
            tabUserButton?.RemoveFromClassList("tab-button-active");
            
            // 모든 뷰 숨기기
            if (viewAll != null) viewAll.style.display = DisplayStyle.None;
            if (viewAssembly != null) viewAssembly.style.display = DisplayStyle.None;
            if (viewUser != null) viewUser.style.display = DisplayStyle.None;
            
            // 선택된 탭 활성화
            switch (mode)
            {
                case ViewMode.All:
                    tabAllButton?.AddToClassList("tab-button-active");
                    if (viewAll != null) viewAll.style.display = DisplayStyle.Flex;
                    if (filterSection != null) filterSection.style.display = DisplayStyle.Flex;
                    RefreshAllTagsUI();
                    break;
                    
                case ViewMode.Assembly:
                    tabAssemblyButton?.AddToClassList("tab-button-active");
                    if (viewAssembly != null) viewAssembly.style.display = DisplayStyle.Flex;
                    if (filterSection != null) filterSection.style.display = DisplayStyle.None;
                    RefreshAssemblyTagsUI();
                    break;
                    
                case ViewMode.User:
                    tabUserButton?.AddToClassList("tab-button-active");
                    if (viewUser != null) viewUser.style.display = DisplayStyle.Flex;
                    if (filterSection != null) filterSection.style.display = DisplayStyle.None;
                    RefreshUserTagsUI();
                    break;
            }
        }

        private void SetupLanguageDropdown()
        {
            if (languageDropdown == null) return;

            languageDropdown.choices = new List<string> { "한국어 (Korean)", "English" };
            languageDropdown.index = GameplayTagEditorLocalization.CurrentLanguage == 
                GameplayTagEditorLocalization.Language.Korean ? 0 : 1;

            languageDropdown.RegisterValueChangedCallback(evt =>
            {
                GameplayTagEditorLocalization.CurrentLanguage = 
                    evt.newValue.Contains("Korean") ? 
                    GameplayTagEditorLocalization.Language.Korean : 
                    GameplayTagEditorLocalization.Language.English;
                
                UpdateLocalization();
                
                // 현재 뷰 새로고침
                switch (currentViewMode)
                {
                    case ViewMode.All:
                        RefreshAllTagsUI();
                        break;
                    case ViewMode.Assembly:
                        RefreshAssemblyTagsUI();
                        break;
                    case ViewMode.User:
                        RefreshUserTagsUI();
                        break;
                }
            });
        }

        private void SetupSortDropdowns()
        {
            var sortChoices = new List<string> 
            { 
                GameplayTagEditorLocalization.Get("sort.alphabetical"),
                GameplayTagEditorLocalization.Get("sort.hierarchical")
            };

            if (sortDropdown != null)
            {
                sortDropdown.choices = sortChoices;
                sortDropdown.index = 0;
                sortDropdown.RegisterValueChangedCallback(_ =>
                {
                    sortMode = sortDropdown.index == 0 ? SortMode.Alphabetical : SortMode.Hierarchical;
                    
                    switch (currentViewMode)
                    {
                        case ViewMode.All:
                            RefreshAllTagsUI();
                            break;
                        case ViewMode.Assembly:
                            RefreshAssemblyTagsUI();
                            break;
                        case ViewMode.User:
                            RefreshUserTagsUI();
                            break;
                    }
                });
            }
        }

        private void SetupEventHandlers()
        {
            // 이벤트 연결 및 툴팁 설정
            if (generateCodeButton != null)
            {
                generateCodeButton.clicked += OnGenerateCodeClicked;
            }

            if (reloadSystemButton != null)
            {
                reloadSystemButton.clicked += OnReloadSystemClicked;
            }

            if (openDebuggerButton != null)
            {
                openDebuggerButton.clicked += OnOpenDebuggerClicked;
            }

            if (createDatabaseButton != null)
            {
                createDatabaseButton.clicked += OnCreateDatabaseClicked;
            }

            if (addTagButton != null)
            {
                addTagButton.clicked += OnAddTagClicked;
            }

            if (databaseDropdown != null)
            {
                databaseDropdown.RegisterValueChangedCallback(OnDatabaseChanged);
            }

            // 자동 생성 토글
            if (autoGenerateToggle != null)
            {
                autoGenerateToggle.value = EditorPrefs.GetBool("GameplayTag_AutoGenerate", true);
                autoGenerateToggle.RegisterValueChangedCallback(evt =>
                {
                    EditorPrefs.SetBool("GameplayTag_AutoGenerate", evt.newValue);
                    UpdateStatus(evt.newValue ? "status.autoGenerateOn" : "status.autoGenerateOff");
                });
            }
        }

        private void UpdateLocalization()
        {
            Func<string, string> L = GameplayTagEditorLocalization.Get;

            // 헤더
            if (headerTitle != null)
                headerTitle.text = L("header.title");
            if (headerSubtitle != null)
                headerSubtitle.text = L("header.subtitle");

            // 탭 버튼
            if (tabAllButton != null)
                tabAllButton.text = L("tab.all");
            if (tabAssemblyButton != null)
                tabAssemblyButton.text = L("tab.assembly");
            if (tabUserButton != null)
                tabUserButton.text = L("tab.user");

            // 버튼
            if (generateCodeButton != null)
            {
                generateCodeButton.text = L("toolbar.generateCode");
                generateCodeButton.tooltip = L("tooltip.generateCode");
            }
            if (reloadSystemButton != null)
            {
                reloadSystemButton.text = L("toolbar.reloadSystem");
                reloadSystemButton.tooltip = L("tooltip.reloadSystem");
            }
            if (openDebuggerButton != null)
            {
                openDebuggerButton.text = L("debugger.openButton");
            }
            if (createDatabaseButton != null)
            {
                createDatabaseButton.text = L("database.createNew");
                createDatabaseButton.tooltip = L("tooltip.createDatabase");
            }
            if (addTagButton != null)
            {
                addTagButton.text = L("addTag.button");
                addTagButton.tooltip = L("tooltip.addTag");
            }

            // 토글
            if (autoGenerateToggle != null)
            {
                autoGenerateToggle.label = L("toolbar.autoGenerate");
                autoGenerateToggle.tooltip = L("tooltip.autoGenerate");
            }
            
            if (showAssemblyToggle != null)
            {
                showAssemblyToggle.label = L("filter.showAssembly");
            }
            
            if (showUserToggle != null)
            {
                showUserToggle.label = L("filter.showUser");
            }

            // 입력 필드
            if (tagNameField != null)
            {
                tagNameField.label = L("addTag.name");
                tagNameField.tooltip = L("tooltip.tagName");
            }
            if (tagDescriptionField != null)
            {
                tagDescriptionField.label = L("addTag.description");
                tagDescriptionField.tooltip = L("tooltip.tagDescription");
            }

            // 드롭다운
            if (databaseDropdown != null)
            {
                databaseDropdown.tooltip = L("tooltip.databaseDropdown");
            }

            // 카운트 라벨
            if (assemblyCountLabel != null)
            {
                assemblyCountLabel.tooltip = L("tooltip.assemblyCount");
            }
            if (userCountLabel != null)
            {
                userCountLabel.tooltip = L("tooltip.userCount");
            }

            // 스크롤 뷰
            if (assemblyTagsScroll != null)
            {
                assemblyTagsScroll.tooltip = L("tooltip.assemblyList");
            }
            if (userTagsScroll != null)
            {
                userTagsScroll.tooltip = L("tooltip.userList");
            }

            // 정렬 드롭다운 업데이트
            SetupSortDropdowns();
            
            // 뷰 설명 라벨 업데이트
            UpdateViewDescriptions();

            // 상태
            if (statusLabel != null)
                statusLabel.text = L("status.ready");
        }
        
        private void UpdateViewDescriptions()
        {
            Func<string, string> L = GameplayTagEditorLocalization.Get;
            var root = rootVisualElement;
            
            // 각 뷰의 패널 타이틀과 설명 업데이트
            if (viewAll != null)
            {
                var panelTitle = viewAll.Q<Label>("panel-title");
                if (panelTitle != null)
                    panelTitle.text = L("panel.allTags");
                    
                var descLabels = viewAll.Query<Label>(className: "panel-description").ToList();
                if (descLabels.Count > 0)
                    descLabels[0].text = L("panel.allDesc");
            }
            
            if (viewAssembly != null)
            {
                var panelTitle = viewAssembly.Q<Label>("panel-title");
                if (panelTitle != null)
                    panelTitle.text = L("panel.assemblyTags");
                    
                var descLabels = viewAssembly.Query<Label>(className: "panel-description").ToList();
                if (descLabels.Count > 0)
                    descLabels[0].text = L("panel.assemblyDesc");
            }
            
            if (viewUser != null)
            {
                var panelTitle = viewUser.Q<Label>("panel-title");
                if (panelTitle != null)
                    panelTitle.text = L("panel.userTags");
                    
                var descLabels = viewUser.Query<Label>(className: "panel-description").ToList();
                if (descLabels.Count > 0)
                    descLabels[0].text = L("panel.userDesc");
                    
                // 데이터베이스 라벨
                var dbSelector = viewUser.Q<VisualElement>(className: "database-selector");
                if (dbSelector != null)
                {
                    var labels = dbSelector.Query<Label>().ToList();
                    if (labels.Count > 0)
                        labels[0].text = L("database.label");
                }
                
                // "새 태그 추가" 섹션 타이틀
                var addTagSection = viewUser.Q<VisualElement>(className: "add-tag-section");
                if (addTagSection != null)
                {
                    var sectionTitles = addTagSection.Query<Label>(className: "section-title").ToList();
                    if (sectionTitles.Count > 0)
                        sectionTitles[0].text = L("addTag.title");
                }
            }
            
            // 왼쪽 패널의 "보기 모드" 라벨
            var leftPanel = root.Q<VisualElement>(className: "panel");
            if (leftPanel != null)
            {
                var panelTitles = leftPanel.Query<Label>(className: "panel-title").ToList();
                if (panelTitles.Count > 0)
                    panelTitles[0].text = L("panel.viewMode");
            }
            
            // 정렬 라벨
            var sortSections = root.Query<VisualElement>(className: "sort-section").ToList();
            foreach (var section in sortSections)
            {
                var labels = section.Query<Label>().ToList();
                if (labels.Count > 0)
                    labels[0].text = L("sort.label");
            }
            
            // 필터 라벨
            if (filterSection != null)
            {
                var sectionTitles = filterSection.Query<Label>(className: "section-title").ToList();
                if (sectionTitles.Count > 0)
                    sectionTitles[0].text = L("filter.label");
            }
        }

        private void RefreshData()
        {
            LoadAssemblyTags();
            LoadUserDatabases();
            
            // 현재 뷰에 따라 적절한 UI 갱신
            switch (currentViewMode)
            {
                case ViewMode.All:
                    RefreshAllTagsUI();
                    break;
                case ViewMode.Assembly:
                    RefreshAssemblyTagsUI();
                    break;
                case ViewMode.User:
                    RefreshUserTagsUI();
                    break;
            }
            
            UpdateStatus("status.ready");
        }

        private void LoadAssemblyTags()
        {
            assemblyTags.Clear();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var attributes = assembly.GetCustomAttributes(typeof(GameplayTagDefAttribute), false);
                    foreach (var attr in attributes)
                    {
                        if (attr is GameplayTagDefAttribute tagDef)
                        {
                            assemblyTags.Add(tagDef);
                        }
                    }
                }
                catch (Exception)
                {
                    // 일부 어셈블리는 접근 불가
                }
            }
        }

        private void LoadUserDatabases()
        {
            userDatabases.Clear();
            var databases = Resources.LoadAll<GameplayTagDatabase>("");
            
            // 자동 생성되지 않은 데이터베이스만 필터링
            userDatabases = databases.Where(db => db != null && !db.IsAutoGenerated).ToList();

            // 드롭다운 업데이트
            if (databaseDropdown != null)
            {
                var choices = userDatabases.Select(db => db.name).ToList();
                if (choices.Count == 0)
                {
                    choices.Add(GameplayTagEditorLocalization.Get("database.none"));
                }
                
                databaseDropdown.choices = choices;
                
                if (userDatabases.Count > 0)
                {
                    databaseDropdown.index = 0;
                    selectedDatabase = userDatabases[0];
                }
                else
                {
                    selectedDatabase = null;
                }
            }
        }

        private void RefreshAllTagsUI()
        {
            if (allTagsScroll == null) return;

            allTagsScroll.Clear();

            var allTags = new List<(string tagName, string description, string source, string database, bool isReadOnly)>();

            // 어셈블리 태그 추가
            if (showAssemblyTags)
            {
                foreach (var tag in assemblyTags)
                {
                    allTags.Add((tag.TagName, tag.Description, "assembly", "", true));
                }
            }

            // 사용자 태그 추가
            if (showUserTags)
            {
                foreach (var db in userDatabases)
                {
                    foreach (var tag in db.Tags)
                    {
                        allTags.Add((tag.TagName, tag.Description, "user", db.name, false));
                    }
                }
            }

            // 정렬
            var sortedTags = SortTags(allTags.Select(t => (t.tagName, t.description)).ToList(), sortMode);

            if (allCountLabel != null)
            {
                allCountLabel.text = $"{allTags.Count}개";
            }

            if (sortMode == SortMode.Hierarchical)
            {
                BuildHierarchicalViewWithSource(allTagsScroll, allTags, sortMode);
            }
            else
            {
                foreach (var (tagName, description, source, database, isReadOnly) in allTags.OrderBy(t => t.tagName))
                {
                    var tagItem = CreateTagItemWithSource(tagName, description, source, database, isReadOnly);
                    allTagsScroll.Add(tagItem);
                }
            }
        }

        private VisualElement CreateTagItemWithSource(
            string tagName, 
            string description, 
            string source, 
            string databaseName,
            bool isReadOnly)
        {
            var container = new VisualElement();
            container.AddToClassList("tag-item");
            
            if (isReadOnly)
            {
                container.AddToClassList("tag-item-readonly");
            }

            Func<string, string> L = GameplayTagEditorLocalization.Get;

            // 툴팁
            string tooltipText = $"{tagName}";
            if (!string.IsNullOrEmpty(description))
            {
                tooltipText += $"\n{description}";
            }
            tooltipText += $"\n\n{L("source.label")}: {(source == "assembly" ? L("source.assembly") : L("source.user"))}";
            if (!string.IsNullOrEmpty(databaseName))
            {
                tooltipText += $" ({databaseName})";
            }
            container.tooltip = tooltipText;

            // 컨텐츠 영역
            var content = new VisualElement();
            content.AddToClassList("tag-item-content");

            // 이름과 출처를 한 줄에
            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            // 출처 라벨
            var sourceLabel = new Label(source == "assembly" ? L("source.assembly") : L("source.user"));
            sourceLabel.AddToClassList("source-label");
            sourceLabel.AddToClassList(source == "assembly" ? "source-label-assembly" : "source-label-user");
            nameRow.Add(sourceLabel);

            var nameLabel = new Label(tagName);
            nameLabel.AddToClassList("tag-name");
            nameRow.Add(nameLabel);

            // 데이터베이스 이름 (사용자 태그인 경우)
            if (source == "user" && !string.IsNullOrEmpty(databaseName))
            {
                var dbLabel = new Label($"{L("source.database")} {databaseName}");
                dbLabel.AddToClassList("source-label-database");
                nameRow.Add(dbLabel);
            }

            content.Add(nameRow);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("tag-description");
                content.Add(descLabel);
            }

            container.Add(content);

            // 액션 영역
            var actions = new VisualElement();
            actions.AddToClassList("tag-actions");

            if (isReadOnly)
            {
                var readonlyLabel = new Label(L("tag.readOnly"));
                readonlyLabel.AddToClassList("readonly-label");
                readonlyLabel.tooltip = L("tooltip.readOnly");
                actions.Add(readonlyLabel);
            }
            else
            {
                // 사용자 태그는 삭제 가능 (해당 데이터베이스를 찾아서 삭제)
                var deleteButton = new Button(() => OnDeleteTagFromDatabase(tagName, databaseName));
                deleteButton.text = L("tag.delete");
                deleteButton.AddToClassList("tag-button");
                deleteButton.AddToClassList("tag-button-delete");
                deleteButton.tooltip = GameplayTagEditorLocalization.Format("tooltip.deleteTag", tagName);
                actions.Add(deleteButton);
            }

            container.Add(actions);

            return container;
        }

        private void BuildHierarchicalViewWithSource(
            VisualElement container,
            List<(string tagName, string description, string source, string database, bool isReadOnly)> allTags,
            SortMode mode)
        {
            var hierarchy = new Dictionary<string, List<(string fullName, string description, string source, string database, bool isReadOnly)>>();
            var rootTags = new List<(string tagName, string description, string source, string database, bool isReadOnly)>();

            foreach (var tag in allTags)
            {
                var parts = tag.tagName.Split('.');
                if (parts.Length == 1)
                {
                    rootTags.Add(tag);
                }
                else
                {
                    string parent = parts[0];
                    if (!hierarchy.ContainsKey(parent))
                    {
                        hierarchy[parent] = new List<(string, string, string, string, bool)>();
                    }
                    hierarchy[parent].Add((tag.tagName, tag.description, tag.source, tag.database, tag.isReadOnly));
                }
            }

            // 루트 태그 먼저 추가
            foreach (var tag in rootTags.OrderBy(t => t.tagName))
            {
                var tagItem = CreateTagItemWithSource(tag.tagName, tag.description, tag.source, tag.database, tag.isReadOnly);
                container.Add(tagItem);
            }

            // 계층 구조 추가
            foreach (var kvp in hierarchy.OrderBy(k => k.Key))
            {
                var foldout = new Foldout { text = $"📁 {kvp.Key}", value = false };
                foldout.AddToClassList("tag-foldout");

                foreach (var (fullName, description, source, database, isReadOnly) in kvp.Value.OrderBy(t => t.fullName))
                {
                    var shortName = fullName.Substring(kvp.Key.Length + 1);
                    var tagItem = CreateHierarchicalTagItemWithSource(fullName, shortName, description, source, database, isReadOnly);
                    foldout.Add(tagItem);
                }

                container.Add(foldout);
            }
        }

        private VisualElement CreateHierarchicalTagItemWithSource(
            string fullTagName,
            string displayName,
            string description,
            string source,
            string databaseName,
            bool isReadOnly)
        {
            var container = new VisualElement();
            container.AddToClassList("tag-item");
            container.AddToClassList("tag-item-hierarchical");
            
            if (isReadOnly)
            {
                container.AddToClassList("tag-item-readonly");
            }

            Func<string, string> L = GameplayTagEditorLocalization.Get;
            
            string tooltipText = $"{fullTagName}";
            if (!string.IsNullOrEmpty(description))
            {
                tooltipText += $"\n{description}";
            }
            tooltipText += $"\n\n{L("source.label")}: {(source == "assembly" ? L("source.assembly") : L("source.user"))}";
            if (!string.IsNullOrEmpty(databaseName))
            {
                tooltipText += $" ({databaseName})";
            }
            container.tooltip = tooltipText;

            // 컨텐츠 영역
            var content = new VisualElement();
            content.AddToClassList("tag-item-content");

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;

            var sourceLabel = new Label(source == "assembly" ? L("source.assembly") : L("source.user"));
            sourceLabel.AddToClassList("source-label");
            sourceLabel.AddToClassList(source == "assembly" ? "source-label-assembly" : "source-label-user");
            nameRow.Add(sourceLabel);

            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("tag-name");
            nameRow.Add(nameLabel);

            if (source == "user" && !string.IsNullOrEmpty(databaseName))
            {
                var dbLabel = new Label($"{L("source.database")} {databaseName}");
                dbLabel.AddToClassList("source-label-database");
                nameRow.Add(dbLabel);
            }

            content.Add(nameRow);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("tag-description");
                content.Add(descLabel);
            }

            container.Add(content);

            // 액션 영역
            var actions = new VisualElement();
            actions.AddToClassList("tag-actions");

            if (isReadOnly)
            {
                var readonlyLabel = new Label(L("tag.readOnly"));
                readonlyLabel.AddToClassList("readonly-label");
                readonlyLabel.tooltip = L("tooltip.readOnly");
                actions.Add(readonlyLabel);
            }
            else
            {
                var deleteButton = new Button(() => OnDeleteTagFromDatabase(fullTagName, databaseName));
                deleteButton.text = L("tag.delete");
                deleteButton.AddToClassList("tag-button");
                deleteButton.AddToClassList("tag-button-delete");
                deleteButton.tooltip = GameplayTagEditorLocalization.Format("tooltip.deleteTag", fullTagName);
                actions.Add(deleteButton);
            }

            container.Add(actions);

            return container;
        }

        private void OnDeleteTagFromDatabase(string tagName, string databaseName)
        {
            var db = userDatabases.FirstOrDefault(d => d.name == databaseName);
            if (db == null) return;

            Func<string, string> L = GameplayTagEditorLocalization.Get;

            if (EditorUtility.DisplayDialog(
                L("dialog.deleteTag.title"),
                GameplayTagEditorLocalization.Format("dialog.deleteTag.message", tagName),
                L("dialog.delete"),
                L("dialog.cancel")))
            {
                db.RemoveTag(tagName);
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssets();

                RefreshAllTagsUI();
                UpdateStatus(GameplayTagEditorLocalization.Format("status.tagDeleted", tagName));
            }
        }

        private void RefreshAssemblyTagsUI()
        {
            if (assemblyTagsScroll == null) return;

            assemblyTagsScroll.Clear();

            var sortedTags = SortTags(assemblyTags.Select(t => (t.TagName, t.Description)).ToList(), sortMode);

            if (assemblyCountLabel != null)
            {
                assemblyCountLabel.text = $"{assemblyTags.Count}개";
            }

            if (sortMode == SortMode.Hierarchical)
            {
                BuildHierarchicalView(assemblyTagsScroll, sortedTags, true);
            }
            else
            {
                foreach (var (tagName, description) in sortedTags)
                {
                    var tagItem = CreateTagItem(tagName, description, true);
                    assemblyTagsScroll.Add(tagItem);
                }
            }
        }

        private void RefreshUserTagsUI()
        {
            if (userTagsScroll == null) return;

            userTagsScroll.Clear();

            int totalUserTags = 0;

            if (selectedDatabase != null)
            {
                totalUserTags = selectedDatabase.Tags.Count;

                var sortedTags = SortTags(
                    selectedDatabase.Tags.Select(t => (t.TagName, t.Description)).ToList(), 
                    sortMode);

                if (sortMode == SortMode.Hierarchical)
                {
                    BuildHierarchicalView(userTagsScroll, sortedTags, false);
                }
                else
                {
                    foreach (var (tagName, description) in sortedTags)
                    {
                        var tagItem = CreateTagItem(tagName, description, false);
                        userTagsScroll.Add(tagItem);
                    }
                }
            }

            if (userCountLabel != null)
            {
                userCountLabel.text = $"{totalUserTags}개";
            }

            // 태그 추가 버튼 활성화 상태
            if (addTagButton != null)
            {
                addTagButton.SetEnabled(selectedDatabase != null);
            }
        }

        private List<(string tagName, string description)> SortTags(
            List<(string tagName, string description)> tags, 
            SortMode mode)
        {
            switch (mode)
            {
                case SortMode.Alphabetical:
                    return tags.OrderBy(t => t.tagName).ToList();
                
                case SortMode.Hierarchical:
                    return tags.OrderBy(t => t.tagName).ToList();
                
                case SortMode.DateAdded:
                default:
                    return tags;
            }
        }

        private void BuildHierarchicalView(
            VisualElement container, 
            List<(string tagName, string description)> tags, 
            bool isReadOnly)
        {
            var hierarchy = new Dictionary<string, List<(string fullName, string description)>>();
            var rootTags = new List<(string tagName, string description)>();

            foreach (var tag in tags)
            {
                var parts = tag.tagName.Split('.');
                if (parts.Length == 1)
                {
                    rootTags.Add(tag);
                }
                else
                {
                    string parent = parts[0];
                    if (!hierarchy.ContainsKey(parent))
                    {
                        hierarchy[parent] = new List<(string, string)>();
                    }
                    hierarchy[parent].Add((tag.tagName, tag.description));
                }
            }

            // 루트 태그 먼저 추가
            foreach (var tag in rootTags)
            {
                var tagItem = CreateTagItem(tag.tagName, tag.description, isReadOnly);
                container.Add(tagItem);
            }

            // 계층 구조 추가
            foreach (var kvp in hierarchy.OrderBy(k => k.Key))
            {
                var foldout = new Foldout { text = $"?? {kvp.Key}", value = false };
                foldout.AddToClassList("tag-foldout");

                foreach (var (fullName, description) in kvp.Value.OrderBy(t => t.fullName))
                {
                    var shortName = fullName.Substring(kvp.Key.Length + 1);
                    var tagItem = CreateHierarchicalTagItem(fullName, shortName, description, isReadOnly);
                    foldout.Add(tagItem);
                }

                container.Add(foldout);
            }
        }

        private VisualElement CreateHierarchicalTagItem(
            string fullTagName, 
            string displayName, 
            string description, 
            bool isReadOnly)
        {
            var container = new VisualElement();
            container.AddToClassList("tag-item");
            container.AddToClassList("tag-item-hierarchical");
            
            if (isReadOnly)
            {
                container.AddToClassList("tag-item-readonly");
            }

            Func<string, string> L = GameplayTagEditorLocalization.Get;
            string tooltipText = $"{L("tag.readOnly" )}: {fullTagName}";
            if (!string.IsNullOrEmpty(description))
            {
                tooltipText += $"\n{description}";
            }
            container.tooltip = tooltipText;

            // 컨텐츠 영역
            var content = new VisualElement();
            content.AddToClassList("tag-item-content");

            var nameLabel = new Label(displayName);
            nameLabel.AddToClassList("tag-name");
            content.Add(nameLabel);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("tag-description");
                content.Add(descLabel);
            }

            container.Add(content);

            // 액션 영역
            var actions = new VisualElement();
            actions.AddToClassList("tag-actions");

            if (isReadOnly)
            {
                var readonlyLabel = new Label(L("tag.readOnly"));
                readonlyLabel.AddToClassList("readonly-label");
                readonlyLabel.tooltip = L("tooltip.readOnly");
                actions.Add(readonlyLabel);
            }
            else
            {
                var deleteButton = new Button(() => OnDeleteTag(fullTagName));
                deleteButton.text = L("tag.delete");
                deleteButton.AddToClassList("tag-button");
                deleteButton.AddToClassList("tag-button-delete");
                deleteButton.tooltip = GameplayTagEditorLocalization.Format("tooltip.deleteTag", fullTagName);
                actions.Add(deleteButton);
            }

            container.Add(actions);

            return container;
        }

        private VisualElement CreateTagItem(string tagName, string description, bool isReadOnly)
        {
            var container = new VisualElement();
            container.AddToClassList("tag-item");
            
            if (isReadOnly)
            {
                container.AddToClassList("tag-item-readonly");
            }

            Func<string, string> L = GameplayTagEditorLocalization.Get;

            // 전체 컨테이너 툴팁
            string tooltipText = $"{L("tag.readOnly")}: {tagName}";
            if (!string.IsNullOrEmpty(description))
            {
                tooltipText += $"\n{description}";
            }
            if (isReadOnly)
            {
                tooltipText += $"\n\n[{L("panel.assemblyDesc")}]";
            }
            container.tooltip = tooltipText;

            // 컨텐츠 영역
            var content = new VisualElement();
            content.AddToClassList("tag-item-content");

            var nameLabel = new Label(tagName);
            nameLabel.AddToClassList("tag-name");
            content.Add(nameLabel);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("tag-description");
                content.Add(descLabel);
            }

            container.Add(content);

            // 액션 영역
            var actions = new VisualElement();
            actions.AddToClassList("tag-actions");

            if (isReadOnly)
            {
                var readonlyLabel = new Label(L("tag.readOnly"));
                readonlyLabel.AddToClassList("readonly-label");
                readonlyLabel.tooltip = L("tooltip.readOnly");
                actions.Add(readonlyLabel);
            }
            else
            {
                var deleteButton = new Button(() => OnDeleteTag(tagName));
                deleteButton.text = L("tag.delete");
                deleteButton.AddToClassList("tag-button");
                deleteButton.AddToClassList("tag-button-delete");
                deleteButton.tooltip = GameplayTagEditorLocalization.Format("tooltip.deleteTag", tagName);
                actions.Add(deleteButton);
            }

            container.Add(actions);

            return container;
        }

        private void OnGenerateCodeClicked()
        {
            UpdateStatus("status.generating");
            GameplayTagCodeGenerator.GenerateCodeStatic();
            RefreshData();
            UpdateStatus("status.generated");
        }

        private void OnReloadSystemClicked()
        {
            UpdateStatus("status.reloading");
            GameplayTagManager.ReloadTags();
            RefreshData();
            UpdateStatus("status.reloaded");
        }

        private void OnOpenDebuggerClicked()
        {
            GameplayTagRuntimeDebugger.ShowWindow();
        }

        private void OnCreateDatabaseClicked()
        {
            GameplayTagEditorMenu.CreateTagDatabase();
            LoadUserDatabases();
            RefreshUserTagsUI();
            UpdateStatus("status.databaseCreated");
        }

        private void OnAddTagClicked()
        {
            Func<string, string> L = GameplayTagEditorLocalization.Get;

            if (selectedDatabase == null)
            {
                EditorUtility.DisplayDialog(
                    L("dialog.error"), 
                    L("dialog.selectDatabase"), 
                    L("dialog.ok"));
                return;
            }

            string tagName = tagNameField?.value?.Trim() ?? "";
            string description = tagDescriptionField?.value?.Trim() ?? "";

            if (string.IsNullOrEmpty(tagName))
            {
                EditorUtility.DisplayDialog(
                    L("dialog.error"), 
                    L("dialog.enterTagName"), 
                    L("dialog.ok"));
                return;
            }

            selectedDatabase.AddTag(tagName, description);
            EditorUtility.SetDirty(selectedDatabase);
            AssetDatabase.SaveAssets();

            // UI 초기화
            if (tagNameField != null)
                tagNameField.value = "";
            if (tagDescriptionField != null)
                tagDescriptionField.value = "";

            RefreshUserTagsUI();
            UpdateStatus(GameplayTagEditorLocalization.Format("status.tagAdded", tagName));
        }

        private void OnDeleteTag(string tagName)
        {
            if (selectedDatabase == null) return;

            Func<string, string> L = GameplayTagEditorLocalization.Get;

            if (EditorUtility.DisplayDialog(
                L("dialog.deleteTag.title"),
                GameplayTagEditorLocalization.Format("dialog.deleteTag.message", tagName),
                L("dialog.delete"),
                L("dialog.cancel")))
            {
                selectedDatabase.RemoveTag(tagName);
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();

                RefreshUserTagsUI();
                UpdateStatus(GameplayTagEditorLocalization.Format("status.tagDeleted", tagName));
            }
        }

        private void OnDatabaseChanged(ChangeEvent<string> evt)
        {
            if (userDatabases.Count == 0) return;

            int index = databaseDropdown.index;
            if (index >= 0 && index < userDatabases.Count)
            {
                selectedDatabase = userDatabases[index];
                RefreshUserTagsUI();
                UpdateStatus(GameplayTagEditorLocalization.Format("status.databaseSelected", selectedDatabase.name));
            }
        }

        private void UpdateStatus(string messageKey)
        {
            if (statusLabel != null)
            {
                statusLabel.text = GameplayTagEditorLocalization.Get(messageKey);
            }
        }
    }
}

