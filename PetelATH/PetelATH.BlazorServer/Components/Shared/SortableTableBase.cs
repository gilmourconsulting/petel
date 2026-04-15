using Microsoft.AspNetCore.Components;
using System.Linq.Expressions;

namespace PetelATH.BlazorServer.Components.Shared
{
    /// <summary>
    /// Base class for sortable table components.
    /// Provides generic sorting functionality that can be reused across all table components.
    /// </summary>
    /// <typeparam name="T">The type of items in the table</typeparam>
    public abstract class SortableTableBase<T> : ComponentBase where T : class
    {
        /// <summary>
        /// The source data for the table
        /// </summary>
        [Parameter]
        public List<T> Items { get; set; } = new();

        /// <summary>
        /// Whether the table is in edit mode (shows action buttons)
        /// </summary>
        [Parameter]
        public bool IsEditMode { get; set; }

        /// <summary>
        /// Currently sorted column name
        /// </summary>
        protected string _sortColumn = string.Empty;

        /// <summary>
        /// Current sort direction (true = ascending, false = descending)
        /// </summary>
        protected bool _sortAscending = true;

        /// <summary>
        /// Sorted and filtered data to display in the table
        /// </summary>
        protected List<T> _sortedItems = new();

        /// <summary>
        /// Column definitions for the table
        /// </summary>
        protected List<ColumnDefinition> _columns = new();

        /// <summary>
        /// Gets the default sort column name. Override in derived classes.
        /// </summary>
        protected abstract string GetDefaultSortColumn();

        /// <summary>
        /// Define columns for the table. Override in derived classes.
        /// </summary>
        protected abstract List<ColumnDefinition> DefineColumns();

        /// <summary>
        /// Called when initialized. Sets up columns.
        /// </summary>
        protected override void OnInitialized()
        {
            _columns = DefineColumns();
        }

        /// <summary>
        /// Called when parameters are set. Applies initial sort.
        /// </summary>
        protected override void OnParametersSet()
        {
            if (string.IsNullOrEmpty(_sortColumn))
            {
                _sortColumn = GetDefaultSortColumn();
            }
            ApplySort();
        }

        /// <summary>
        /// Sort by a specific column. Toggles direction if already sorted by this column.
        /// </summary>
        /// <param name="columnName">Name of the property to sort by</param>
        protected void SortBy(string columnName)
        {
            if (_sortColumn == columnName)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = columnName;
                _sortAscending = true;
            }
            ApplySort();
        }

        /// <summary>
        /// Applies the current sort to the items.
        /// Override this method in derived classes for custom sorting logic.
        /// </summary>
        protected virtual void ApplySort()
        {
            if (Items == null || !Items.Any())
            {
                _sortedItems = new List<T>();
                return;
            }

            var sortedQuery = ApplySortLogic(Items, _sortColumn, _sortAscending);
            _sortedItems = sortedQuery.ToList();
        }

        /// <summary>
        /// Applies sorting logic to the query. Override for custom column sorting.
        /// </summary>
        /// <param name="items">Items to sort</param>
        /// <param name="columnName">Column to sort by</param>
        /// <param name="ascending">Sort direction</param>
        /// <returns>Sorted enumerable</returns>
        protected abstract IEnumerable<T> ApplySortLogic(List<T> items, string columnName, bool ascending);

        /// <summary>
        /// Gets the sort indicator icon for a column header
        /// </summary>
        /// <param name="columnName">Column name to check</param>
        /// <returns>Sort indicator (▲ for ascending, ▼ for descending, empty string for unsorted)</returns>
        protected string GetSortIcon(string columnName)
        {
            if (_sortColumn != columnName)
                return string.Empty;

            return _sortAscending ? " ▲" : " ▼";
        }

        /// <summary>
        /// Renders table header columns generically
        /// </summary>
        protected RenderFragment RenderHeaders() => builder =>
        {
            int sequence = 0;
            
            foreach (var column in _columns)
            {
                if (column.IsSortable)
                {
                    builder.OpenElement(sequence++, "th");
                    builder.AddAttribute(sequence++, "class", "sortable-column");
                    builder.AddAttribute(sequence++, "onclick", EventCallback.Factory.Create(this, () => SortBy(column.PropertyName)));
                    builder.AddContent(sequence++, column.Label);
                    builder.AddContent(sequence++, GetSortIcon(column.PropertyName));
                    builder.CloseElement();
                }
                else
                {
                    builder.OpenElement(sequence++, "th");
                    builder.AddContent(sequence++, column.Label);
                    builder.CloseElement();
                }
            }
        };

        /// <summary>
        /// Helper method to create a generic OrderBy expression dynamically
        /// </summary>
        protected IOrderedEnumerable<T> OrderByProperty(IEnumerable<T> items, string propertyName, bool ascending)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda<Func<T, object>>(
                Expression.Convert(property, typeof(object)),
                parameter
            );
            var compiled = lambda.Compile();

            return ascending
                ? items.OrderBy(compiled)
                : items.OrderByDescending(compiled);
        }

        /// <summary>
        /// Column definition for sortable tables
        /// </summary>
        protected class ColumnDefinition
        {
            public string Label { get; set; } = string.Empty;
            public string PropertyName { get; set; } = string.Empty;
            public bool IsSortable { get; set; } = true;
        }
    }
}
