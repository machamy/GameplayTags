using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Machamy.GameplayTags.Runtime;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Machamy.GameplayTags.Editor
{
    internal readonly struct GameplayTagOption
    {
        public GameplayTagOption(string displayName, string rawValue, bool isInvalid = false, bool isDimmed = false)
        {
            DisplayName = displayName;
            RawValue = rawValue;
            IsInvalid = isInvalid;
            IsDimmed = isDimmed;
        }

        public string DisplayName { get; }
        public string RawValue { get; }
        public bool IsInvalid { get; }
        public bool IsDimmed { get; }
    }

    internal static class GameplayTagEditorUiUtility
    {
        public static List<GameplayTagOption> BuildTagOptions(FieldInfo fieldInfo, string currentTagName = null, bool includeNone = false, ISet<string> dimmedTagNames = null)
        {
            var options = new List<GameplayTagOption>();
            if (includeNone)
            {
                options.Add(new GameplayTagOption("None", string.Empty));
            }

            foreach (string tagName in GetFilteredTagNames(fieldInfo))
            {
                bool isDimmed = dimmedTagNames != null && dimmedTagNames.Contains(tagName);
                string displayName = isDimmed ? $"{tagName} (Already Added)" : tagName;
                options.Add(new GameplayTagOption(displayName, tagName, isInvalid: false, isDimmed: isDimmed));
            }

            if (IsInvalidTagName(currentTagName))
            {
                options.Insert(includeNone ? 1 : 0, new GameplayTagOption($"Invalid: {currentTagName}", currentTagName, isInvalid: true));
            }

            return options;
        }

        public static IEnumerable<string> GetFilteredTagNames(FieldInfo fieldInfo)
        {
            GameplayTagManager.InitializeIfNeeded();

            if (fieldInfo == null)
            {
                return GameplayTagManager.GetAllTags()
                    .Select(tag => tag.TagName)
                    .OrderBy(tagName => tagName, StringComparer.Ordinal)
                    .ToArray();
            }

            GameplayTagParentAttribute parentAttr = fieldInfo.GetCustomAttributes(typeof(GameplayTagParentAttribute), true)
                .FirstOrDefault() as GameplayTagParentAttribute;

            if (parentAttr == null || parentAttr.ParentTagNames.Length == 0)
            {
                return GameplayTagManager.GetAllTags()
                    .Select(tag => tag.TagName)
                    .OrderBy(tagName => tagName, StringComparer.Ordinal)
                    .ToArray();
            }

            List<GameplayTag> filteredTags = null;
            try
            {
                filteredTags = ListPool<GameplayTag>.Get();
                GameplayTagManager.RequestTagsFromParents(parentAttr.ParentTagNames, ref filteredTags);
                return filteredTags
                    .Select(tag => tag.TagName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(tagName => tagName, StringComparer.Ordinal)
                    .ToArray();
            }
            finally
            {
                if (filteredTags != null)
                {
                    ListPool<GameplayTag>.Release(filteredTags);
                }
            }
        }

        public static bool IsInvalidTagName(string tagName)
        {
            return !string.IsNullOrEmpty(tagName) && !GameplayTagManager.IsValidTagName(tagName);
        }
    }

    internal sealed class GameplayTagSearchPopup<TItem> : VisualElement
    {
        private enum MatchKind
        {
            None = int.MaxValue,
            Prefix = 0,
            MultiTokenPrefix = 1,
            ReversePrefix = 2,
            Contains = 3
        }

        private readonly struct SearchMatch
        {
            public SearchMatch(int category, int firstTokenIndex, int tokenSpan, int kindPenalty, int lengthPenalty)
            {
                Category = category;
                FirstTokenIndex = firstTokenIndex;
                TokenSpan = tokenSpan;
                KindPenalty = kindPenalty;
                LengthPenalty = lengthPenalty;
            }

            public int Category { get; }
            public int FirstTokenIndex { get; }
            public int TokenSpan { get; }
            public int KindPenalty { get; }
            public int LengthPenalty { get; }
        }

        private readonly Func<IEnumerable<TItem>> _itemSource;
        private readonly Func<TItem, string> _searchTextSelector;
        private readonly Action<Label, TItem> _bindLabel;
        private readonly Action<TItem> _onItemChosen;
        private readonly Action _onRefresh;
        private readonly List<TItem> _filteredItems = new();
        private readonly ToolbarSearchField _searchField;
        private readonly Label _emptyLabel;
        private readonly ScrollView _scrollView;

        public GameplayTagSearchPopup(
            Func<IEnumerable<TItem>> itemSource,
            Func<TItem, string> searchTextSelector,
            Action<Label, TItem> bindLabel,
            Action<TItem> onItemChosen,
            float listHeight = 220f,
            Action onRefresh = null
            )
        {
            _itemSource = itemSource;
            _searchTextSelector = searchTextSelector;
            _bindLabel = bindLabel;
            _onItemChosen = onItemChosen;
            _onRefresh = onRefresh;

            style.display = DisplayStyle.None;
            style.marginTop = 4f;
            style.paddingLeft = 6f;
            style.paddingRight = 6f;
            style.paddingTop = 6f;
            style.paddingBottom = 6f;
            style.borderTopWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderTopColor = new Color(0.23f, 0.23f, 0.23f);
            style.borderRightColor = new Color(0.23f, 0.23f, 0.23f);
            style.borderBottomColor = new Color(0.23f, 0.23f, 0.23f);
            style.borderLeftColor = new Color(0.23f, 0.23f, 0.23f);
            style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            _searchField = new ToolbarSearchField();
            _searchField.style.marginBottom = 6f;
            _searchField.RegisterValueChangedCallback(evt => Refresh(evt.newValue));
            Add(_searchField);

            _emptyLabel = new Label("No matching tags");
            _emptyLabel.style.display = DisplayStyle.None;
            _emptyLabel.style.height = 20f;
            _emptyLabel.style.paddingLeft = 4f;
            Add(_emptyLabel);

            _scrollView = new ScrollView
            {
                mode = ScrollViewMode.Vertical,
                style =
                {
                    height = listHeight,
                    flexGrow = 1f
                }
            };
            Add(_scrollView);
        }

        public string Query => _searchField.value;

        public void Open(bool resetSearch = true)
        {
            style.display = DisplayStyle.Flex;
            if (resetSearch)
            {
                _searchField.value = string.Empty;
            }

            Refresh(_searchField.value);
            _searchField.schedule.Execute(_searchField.Focus).ExecuteLater(0);
        }

        public void Close()
        {
            style.display = DisplayStyle.None;
        }

        public void Refresh(string query = null)
        {
            string currentQuery = query ?? _searchField.value ?? string.Empty;
            string trimmedQuery = currentQuery.Trim();

            _filteredItems.Clear();
            _onRefresh?.Invoke();
            if (trimmedQuery.Length == 0)
            {
                _filteredItems.AddRange(_itemSource());
            }
            else
            {
                var matchedItems = new List<(TItem Item, SearchMatch Match, string SearchText)>();
                foreach (TItem item in _itemSource())
                {
                    string searchText = _searchTextSelector(item) ?? string.Empty;
                    if (TryCreateSearchMatch(trimmedQuery, searchText, out SearchMatch match))
                    {
                        matchedItems.Add((item, match, searchText));
                    }
                }

                matchedItems.Sort(static (left, right) =>
                {
                    int category = left.Match.Category.CompareTo(right.Match.Category);
                    if (category != 0) return category;

                    int firstToken = left.Match.FirstTokenIndex.CompareTo(right.Match.FirstTokenIndex);
                    if (firstToken != 0) return firstToken;

                    int tokenSpan = left.Match.TokenSpan.CompareTo(right.Match.TokenSpan);
                    if (tokenSpan != 0) return tokenSpan;

                    int kindPenalty = left.Match.KindPenalty.CompareTo(right.Match.KindPenalty);
                    if (kindPenalty != 0) return kindPenalty;

                    int lengthPenalty = left.Match.LengthPenalty.CompareTo(right.Match.LengthPenalty);
                    if (lengthPenalty != 0) return lengthPenalty;

                    return string.Compare(left.SearchText, right.SearchText, StringComparison.OrdinalIgnoreCase);
                });

                foreach ((TItem item, _, _) in matchedItems)
                {
                    _filteredItems.Add(item);
                }
            }

            _emptyLabel.style.display = _filteredItems.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            RebuildRows();
        }

        private static bool TryCreateSearchMatch(string query, string candidate, out SearchMatch match)
        {
            string[] queryTokens = Tokenize(query);
            string[] candidateTokens = Tokenize(candidate);
            if (queryTokens.Length == 0 || candidateTokens.Length == 0)
            {
                match = default;
                return false;
            }

            if (TryMatchFromFront(queryTokens, candidateTokens, out match))
            {
                return true;
            }

            if (TryMatchOrdered(queryTokens, candidateTokens, allowReversePrefix: false, allowContains: false, out match))
            {
                match = new SearchMatch(1, match.FirstTokenIndex, match.TokenSpan, match.KindPenalty, match.LengthPenalty);
                return true;
            }

            if (TryMatchOrdered(queryTokens, candidateTokens, allowReversePrefix: true, allowContains: true, out match))
            {
                match = new SearchMatch(2, match.FirstTokenIndex, match.TokenSpan, match.KindPenalty, match.LengthPenalty);
                return true;
            }

            match = default;
            return false;
        }

        private static bool TryMatchFromFront(string[] queryTokens, string[] candidateTokens, out SearchMatch match)
        {
            if (queryTokens.Length > candidateTokens.Length)
            {
                match = default;
                return false;
            }

            int lengthPenalty = 0;
            for (int i = 0; i < queryTokens.Length; i++)
            {
                if (!candidateTokens[i].StartsWith(queryTokens[i], StringComparison.OrdinalIgnoreCase))
                {
                    match = default;
                    return false;
                }

                lengthPenalty += Math.Max(0, candidateTokens[i].Length - queryTokens[i].Length);
            }

            match = new SearchMatch(
                category: 0,
                firstTokenIndex: 0,
                tokenSpan: queryTokens.Length - 1,
                kindPenalty: 0,
                lengthPenalty: lengthPenalty);
            return true;
        }

        private static bool TryMatchOrdered(
            string[] queryTokens,
            string[] candidateTokens,
            bool allowReversePrefix,
            bool allowContains,
            out SearchMatch match)
        {
            int searchStartIndex = 0;
            int firstMatchedIndex = -1;
            int lastMatchedIndex = -1;
            int kindPenalty = 0;
            int lengthPenalty = 0;

            foreach (string queryToken in queryTokens)
            {
                MatchKind bestKind = MatchKind.None;
                int bestIndex = -1;

                for (int candidateIndex = searchStartIndex; candidateIndex < candidateTokens.Length; candidateIndex++)
                {
                    MatchKind kind = GetMatchKind(queryToken, candidateTokens, candidateIndex, allowReversePrefix, allowContains);
                    if (kind == MatchKind.None)
                    {
                        continue;
                    }

                    if (kind < bestKind)
                    {
                        bestKind = kind;
                        bestIndex = candidateIndex;

                        if (kind == MatchKind.Prefix)
                        {
                            break;
                        }
                    }
                }

                if (bestIndex < 0)
                {
                    match = default;
                    return false;
                }

                if (firstMatchedIndex < 0)
                {
                    firstMatchedIndex = bestIndex;
                }

                lastMatchedIndex = bestIndex;
                searchStartIndex = bestIndex + 1;
                kindPenalty += (int)bestKind;
                lengthPenalty += Math.Abs(candidateTokens[bestIndex].Length - queryToken.Length);
            }

            match = new SearchMatch(
                category: 0,
                firstTokenIndex: firstMatchedIndex,
                tokenSpan: lastMatchedIndex - firstMatchedIndex,
                kindPenalty: kindPenalty,
                lengthPenalty: lengthPenalty);
            return true;
        }

        private static MatchKind GetMatchKind(string queryToken, string candidateToken, bool allowReversePrefix, bool allowContains)
        {
            if (candidateToken.StartsWith(queryToken, StringComparison.OrdinalIgnoreCase))
            {
                return MatchKind.Prefix;
            }

            if (allowReversePrefix && queryToken.StartsWith(candidateToken, StringComparison.OrdinalIgnoreCase))
            {
                return MatchKind.ReversePrefix;
            }

            if (allowContains && candidateToken.IndexOf(queryToken, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return MatchKind.Contains;
            }

            return MatchKind.None;
        }

        private static MatchKind GetMatchKind(string queryToken, string[] candidateTokens, int candidateIndex, bool allowReversePrefix, bool allowContains)
        {
            MatchKind directKind = GetMatchKind(queryToken, candidateTokens[candidateIndex], allowReversePrefix, allowContains);
            if (directKind != MatchKind.None)
            {
                return directKind;
            }

            return IsMultiTokenPrefixMatch(queryToken, candidateTokens, candidateIndex)
                ? MatchKind.MultiTokenPrefix
                : MatchKind.None;
        }

        private static bool IsMultiTokenPrefixMatch(string queryToken, string[] candidateTokens, int startIndex)
        {
            if (queryToken.Length < 2)
            {
                return false;
            }

            int queryOffset = 0;
            int tokenIndex = startIndex;
            int consumedTokens = 0;

            while (queryOffset < queryToken.Length && tokenIndex < candidateTokens.Length)
            {
                string candidateToken = candidateTokens[tokenIndex];
                int consumedLength = GetCommonPrefixLength(queryToken, queryOffset, candidateToken);
                if (consumedLength <= 0)
                {
                    return false;
                }

                queryOffset += consumedLength;
                tokenIndex++;
                consumedTokens++;
            }

            return queryOffset == queryToken.Length && consumedTokens >= 2;
        }

        private static int GetCommonPrefixLength(string queryToken, int queryOffset, string candidateToken)
        {
            int maxLength = Math.Min(queryToken.Length - queryOffset, candidateToken.Length);
            int length = 0;
            while (length < maxLength)
            {
                char left = char.ToUpperInvariant(queryToken[queryOffset + length]);
                char right = char.ToUpperInvariant(candidateToken[length]);
                if (left != right)
                {
                    break;
                }

                length++;
            }

            return length;
        }

        private static string[] Tokenize(string value)
        {
            return value
                .Split(new[] { '.', ' ', '_', '-', '/', ':' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim())
                .Where(token => token.Length > 0)
                .ToArray();
        }

        private void RebuildRows()
        {
            _scrollView.Clear();

            foreach (TItem item in _filteredItems)
            {
                TItem capturedItem = item;

                var rowButton = new Button(() => _onItemChosen(capturedItem));
                rowButton.style.unityTextAlign = TextAnchor.MiddleLeft;
                rowButton.style.marginTop = 0f;
                rowButton.style.marginBottom = 0f;
                rowButton.style.marginLeft = 0f;
                rowButton.style.marginRight = 0f;
                rowButton.style.paddingTop = 0f;
                rowButton.style.paddingBottom = 0f;
                rowButton.style.paddingLeft = 0f;
                rowButton.style.paddingRight = 0f;
                rowButton.style.height = 22f;

                var label = new Label();
                label.style.paddingLeft = 6f;
                label.style.paddingRight = 6f;
                label.style.unityTextAlign = TextAnchor.MiddleLeft;
                _bindLabel(label, capturedItem);

                rowButton.Add(label);
                _scrollView.Add(rowButton);
            }
        }
    }
}
