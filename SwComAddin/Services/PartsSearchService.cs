using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SwComAddin.Models;

namespace SwComAddin.Services
{
    /// <summary>
    /// Handles filtering the standard parts TreeView based on search text.
    /// Matches against part Name and Id fields.
    /// </summary>
    public class PartsSearchService
    {
        private const string PlaceholderText = "搜索零件...";

        /// <summary>
        /// Returns true if the search box is showing placeholder text.
        /// </summary>
        public bool IsPlaceholder(string text)
        {
            return string.IsNullOrWhiteSpace(text) || text == PlaceholderText;
        }

        /// <summary>
        /// Filter all TreeView items based on the search keyword.
        /// Shows items whose Name or Id contains the keyword (case-insensitive).
        /// When keyword is empty/null, all items are shown.
        /// </summary>
        public void ApplySearch(TreeView treeView, string keyword)
        {
            if (treeView == null || treeView.Items == null || treeView.Items.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(keyword) || keyword == PlaceholderText)
            {
                SetAllItemsVisibility(treeView, Visibility.Visible, expand: false);
                return;
            }

            keyword = keyword.Trim().ToLowerInvariant();

            foreach (var catItem in treeView.Items)
            {
                if (catItem is Category category)
                {
                    bool catHasMatch = false;

                    // Check subcategories
                    if (category.SubCategories != null)
                    {
                        foreach (var subCat in category.SubCategories)
                        {
                            bool subCatHasMatch = false;
                            if (subCat.Parts != null)
                            {
                                foreach (var part in subCat.Parts)
                                {
                                    bool match = PartMatches(part, keyword);
                                    subCatHasMatch = subCatHasMatch || match;
                                }
                            }
                            subCatHasMatch = subCatHasMatch || subCat.Name.ToLowerInvariant().Contains(keyword);
                            catHasMatch = catHasMatch || subCatHasMatch;
                        }
                    }

                    // Also check flat Parts list
                    if (category.Parts != null)
                    {
                        foreach (var part in category.Parts)
                        {
                            catHasMatch = catHasMatch || PartMatches(part, keyword);
                        }
                    }

                    catHasMatch = catHasMatch || category.Name.ToLowerInvariant().Contains(keyword);

                    // Update visibility for the category TreeViewItem
                    var catContainer = treeView.ItemContainerGenerator.ContainerFromItem(category) as TreeViewItem;
                    if (catContainer != null)
                    {
                        catContainer.Visibility = catHasMatch ? Visibility.Visible : Visibility.Collapsed;
                        if (catHasMatch) catContainer.IsExpanded = true;
                    }
                }
            }
        }

        /// <summary>
        /// Check if a StandardPart matches the keyword by Name or Id.
        /// </summary>
        private bool PartMatches(StandardPart part, string keyword)
        {
            return (part.Name != null && part.Name.ToLowerInvariant().Contains(keyword))
                || (part.Id != null && part.Id.ToLowerInvariant().Contains(keyword));
        }

        /// <summary>
        /// Recursively set visibility on all TreeViewItems.
        /// </summary>
        private void SetAllItemsVisibility(ItemsControl parent, Visibility visibility, bool expand)
        {
            if (parent?.Items == null) return;
            foreach (var item in parent.Items)
            {
                var container = parent.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    container.Visibility = visibility;
                    if (expand) container.IsExpanded = true;
                    SetAllItemsVisibility(container, visibility, expand);
                }
            }
        }

        // --- Placeholder handling for search TextBox ---

        public bool IsSearchFocused { get; private set; }

        public void OnSearchGotFocus(TextBox searchBox)
        {
            IsSearchFocused = true;
            if (searchBox.Text == PlaceholderText)
                searchBox.Text = "";
            searchBox.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        }

        public void OnSearchLostFocus(TextBox searchBox)
        {
            IsSearchFocused = false;
            if (string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.Text = PlaceholderText;
                searchBox.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            }
        }

        public string GetSearchText(TextBox searchBox)
        {
            var text = searchBox.Text.Trim();
            return IsPlaceholder(text) ? "" : text;
        }
    }
}
