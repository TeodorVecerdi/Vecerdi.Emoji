using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace Vecerdi.Logging.Unity.Editor {
    public class FilterableLogCategoryList : IList<LogCategory>, IList {
        public enum SortModes { None, Ascending, Descending }
        public enum SortFields { CategoryName, LogLevel }

        private readonly List<LogCategory> m_OriginalList;
        private List<LogCategory> m_FilteredAndSortedList;
        private List<int> m_IndexMapping;
        private string m_FilterText = "";

        public SortModes SortMode { get; private set; } = SortModes.None;
        public SortFields SortField { get; private set; } = SortFields.CategoryName;
        public BaseVerticalCollectionView CollectionView { get; set; }

        public FilterableLogCategoryList(List<LogCategory> originalList) {
            m_OriginalList = originalList;
            RefreshFilteredAndSortedList(true);
        }

        public void SetFilter(string filterText) {
            if (m_FilterText != filterText) {
                m_FilterText = filterText;
                RefreshFilteredAndSortedList();
            }
        }

        public void SetSort(SortModes sortMode, SortFields sortField) {
            if (SortMode != sortMode || SortField != sortField) {
                SortMode = sortMode;
                SortField = sortField;
                RefreshFilteredAndSortedList();
            }
        }

        public int GetOriginalIndex(int filteredIndex) {
            if (filteredIndex < 0 || filteredIndex >= m_IndexMapping.Count) {
                throw new ArgumentOutOfRangeException(nameof(filteredIndex));
            }

            return m_IndexMapping[filteredIndex];
        }

        private void RefreshFilteredAndSortedList(bool suppressRefresh = false) {
            IEnumerable<(LogCategory LogCategory, int Index)> view = m_OriginalList.Select((category, index) => (category, index));
            if (!string.IsNullOrEmpty(m_FilterText)) {
                view = view.Where(tuple => tuple.LogCategory.CategoryName.Contains(m_FilterText, StringComparison.OrdinalIgnoreCase));
            }

            if (SortMode != SortModes.None) {
                view = SortField == SortFields.CategoryName
                    ? SortMode == SortModes.Ascending
                        ? view.OrderBy(t => t.LogCategory.CategoryName)
                        : view.OrderByDescending(t => t.LogCategory.CategoryName)
                    : SortMode == SortModes.Ascending
                        ? view.OrderBy(t => t.LogCategory.LogLevel)
                        : view.OrderByDescending(t => t.LogCategory.LogLevel);
            }

            var list = view.ToList();
            m_FilteredAndSortedList = list.Select(tuple => tuple.LogCategory).ToList();
            m_IndexMapping = list.Select(tuple => tuple.Index).ToList();

            if (!suppressRefresh) {
                CollectionView.RefreshItems();
            }
        }

        // IList<LogCategory> implementation
        public LogCategory this[int index] {
            get => m_FilteredAndSortedList[index];
            set => throw new NotSupportedException("This list is read-only.");
        }

        public int Count => m_FilteredAndSortedList.Count;
        public bool IsReadOnly => true;
        bool ICollection.IsSynchronized => ((ICollection)m_FilteredAndSortedList).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)m_FilteredAndSortedList).SyncRoot;

        object IList.this[int index] {
            get => ((IList)m_FilteredAndSortedList)[index];
            set => throw new NotSupportedException("This list is read-only.");
        }

        public void Add(LogCategory item) => throw new NotSupportedException("This list is read-only.");
        int IList.Add(object value) => throw new NotSupportedException("This list is read-only.");
        public void Clear() => throw new NotSupportedException("This list is read-only.");
        public bool Contains(LogCategory item) => m_FilteredAndSortedList.Contains(item);
        bool IList.Contains(object value) => ((IList)m_FilteredAndSortedList).Contains(value);
        public IEnumerator<LogCategory> GetEnumerator() => m_FilteredAndSortedList.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(LogCategory[] array, int arrayIndex) => m_FilteredAndSortedList.CopyTo(array, arrayIndex);
        void ICollection.CopyTo(Array array, int index) => ((ICollection)m_FilteredAndSortedList).CopyTo(array, index);
        public int IndexOf(LogCategory item) => m_FilteredAndSortedList.IndexOf(item);
        int IList.IndexOf(object value) => ((IList)m_FilteredAndSortedList).IndexOf(value);
        public void Insert(int index, LogCategory item) => throw new NotSupportedException("This list is read-only.");
        void IList.Insert(int index, object value) => throw new NotSupportedException("This list is read-only.");
        public bool Remove(LogCategory item) => throw new NotSupportedException("This list is read-only.");
        void IList.Remove(object value) => throw new NotSupportedException("This list is read-only.");
        public void RemoveAt(int index) => throw new NotSupportedException("This list is read-only.");
        bool IList.IsFixedSize => ((IList)m_FilteredAndSortedList).IsFixedSize;
    }
}
