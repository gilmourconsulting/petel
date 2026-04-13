# Generic Sortable Table Pattern

## Overview

All table components in the Blazor application should inherit from `SortableTableBase<T>` to provide consistent, reusable sorting functionality across the system.

## Base Class Features

The `SortableTableBase<T>` provides:
- ✅ Generic sorting logic for any data type
- ✅ Automatic sort direction toggling (ascending/descending)
- ✅ Visual sort indicators (⇅ unsorted, ↑ ascending, ↓ descending)
- ✅ Consistent sort behavior across all tables
- ✅ Optional edit mode support

## How to Create a Sortable Table

### 1. Create Your Table Component

```razor
@using YourNamespace.DTOs
@inherits SortableTableBase<YourDto>

@if (Items != null && Items.Any())
{
    <div class="table-container">
        <table class="data-table">
            <thead>
                <tr>
                    <th style="cursor: pointer; user-select: none;" 
                        @onclick="() => SortBy(nameof(YourDto.PropertyName))">
                        Column Header @GetSortIcon(nameof(YourDto.PropertyName))
                    </th>
                    <!-- Add more sortable columns -->
                </tr>
            </thead>
            <tbody>
                @foreach (var item in _sortedItems)
                {
                    <tr>
                        <td>@item.PropertyName</td>
                        <!-- Display other properties -->
                    </tr>
                }
            </tbody>
        </table>
    </div>
}
```

### 2. Implement Required Methods

```csharp
@code {
    // Required: Define default sort column
    protected override string GetDefaultSortColumn() => nameof(YourDto.PropertyName);

    // Required: Define sorting logic for each column
    protected override IEnumerable<YourDto> ApplySortLogic(
        List<YourDto> items, 
        string columnName, 
        bool ascending)
    {
        return columnName switch
        {
            nameof(YourDto.PropertyName) => ascending
                ? items.OrderBy(x => x.PropertyName)
                : items.OrderByDescending(x => x.PropertyName),
            nameof(YourDto.AnotherProperty) => ascending
                ? items.OrderBy(x => x.AnotherProperty)
                : items.OrderByDescending(x => x.AnotherProperty),
            _ => items.OrderBy(x => x.PropertyName) // Default sort
        };
    }
}
```

### 3. Use the Table Component

```razor
<YourTable 
    Items="@yourDataList" 
    IsEditMode="@editMode" 
    OnEdit="HandleEdit" 
    OnDelete="HandleDelete" />
```

## Parameters

### Base Parameters (Inherited)
- `Items` (List<T>) - The data to display in the table
- `IsEditMode` (bool) - Whether to show edit/delete buttons

### Custom Parameters
Add your own parameters as needed:
```csharp
[Parameter] public EventCallback<YourDto> OnEdit { get; set; }
[Parameter] public EventCallback<int> OnDelete { get; set; }
```

## Advanced: Multi-Column Sorting

For tables that need secondary sort keys (e.g., sort by Level, then by ClassNumber):

```csharp
protected override IEnumerable<YourDto> ApplySortLogic(
    List<YourDto> items, 
    string columnName, 
    bool ascending)
{
    return columnName switch
    {
        nameof(YourDto.Level) => ascending
            ? items.OrderBy(x => x.Level).ThenBy(x => x.ClassNumber)
            : items.OrderByDescending(x => x.Level).ThenByDescending(x => x.ClassNumber),
        // Other columns...
    };
}
```

## Example Implementations

See these existing table components for reference:
- `SchoolTracksTable.razor` - Simple sorting
- `SchoolClassesTable.razor` - Multi-column sorting with secondary keys
- `AdditionalStudyProgramsTable.razor` - Sorting with nullable fields

## Styling Guidelines

### Column Headers
```html
<th style="cursor: pointer; user-select: none; padding: 12px; text-align: center; font-weight: 600;" 
    @onclick="() => SortBy(nameof(Dto.Property))">
    Header Text @GetSortIcon(nameof(Dto.Property))
</th>
```

### Key Styles
- `cursor: pointer` - Shows column is clickable
- `user-select: none` - Prevents text selection when clicking
- `padding: 12px` - Consistent padding
- `text-align: center` - Centered text (adjust for RTL if needed)

## Benefits of This Pattern

1. **DRY (Don't Repeat Yourself)** - Sorting logic written once, used everywhere
2. **Consistency** - All tables behave the same way
3. **Maintainability** - Bug fixes and improvements apply to all tables
4. **Type Safety** - Generic types provide compile-time checking
5. **Extensibility** - Easy to add new features to all tables at once

## Migration Checklist

When converting an existing table to use `SortableTableBase`:

- [ ] Add `@inherits SortableTableBase<YourDto>` to the top
- [ ] Change parameter from specific name to `Items`
- [ ] Change `@foreach` to use `_sortedItems`
- [ ] Add `@onclick` handlers to column headers
- [ ] Add `GetSortIcon()` calls to headers
- [ ] Implement `GetDefaultSortColumn()` method
- [ ] Implement `ApplySortLogic()` method
- [ ] Remove old sorting code
- [ ] Update parent components to pass `Items=` instead of old parameter name
- [ ] Test sorting on all columns
