using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PetelApp.BlazorServer.DTOs;
using PetelApp.BlazorServer.Services;
using System.Text.Json;

namespace PetelApp.BlazorServer.Components.Pages
{
    public partial class TransactionAccounts
    {
        // Page identification for security
        protected override string PageName => "transactionaccounts";

        // Component state
        private bool _isLoading = true;
        private List<TransactionAccountDto> _accounts = new();
        private List<TransactionAccountDto> _filteredAccounts = new();
        private string _entityDisplay = "";
        private int _totalAccounts = 0;
        private int _activeAccounts = 0;
        private decimal _totalBalance = 0;

        // Sorting state
        private string? _sortColumn = null;
        private bool _sortAscending = true;

        /// <summary>
        /// Called after page access verified - load initial data
        /// </summary>
        protected override async Task OnPageInitializedAsync()
        {
            await LoadData();
        }

        /// <summary>
        /// Load transaction accounts based on user's entity type
        /// </summary>
        private async Task LoadData()
        {
            _isLoading = true;
            StateHasChanged();

            try
            {
                var session = await SessionStateService.GetSessionAsync();
                if (session == null)
                {
                    await JSRuntime.InvokeVoidAsync("alert", "אין מידע על הפעלה");
                    NavigationManager.NavigateTo("/login");
                    return;
                }

                // Set entity display
                _entityDisplay = $"ישות: {session.EntityName}";

                // Load accounts from API
                var response = await ApiService.GetAsync<ApiResponse<List<TransactionAccountDto>>>("transactionaccounts");

                if (response?.Success == true && response.Data != null)
                {
                    _accounts = response.Data;
                    _filteredAccounts = new List<TransactionAccountDto>(_accounts);

                    // Calculate summary statistics
                    _totalAccounts = _accounts.Count;
                    _activeAccounts = _accounts.Count(a => a.IsActive);
                    _totalBalance = _accounts.Where(a => a.IsActive).Sum(a => a.Balance);

                    // Apply default sorting
                    SortTable(nameof(TransactionAccountDto.AccountName));
                }
                else
                {
                    Console.WriteLine($"Failed to load accounts: {response?.Message}");
                    _accounts = new List<TransactionAccountDto>();
                    _filteredAccounts = new List<TransactionAccountDto>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading accounts: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בטעינת החשבונות: {ex.Message}");
                _accounts = new List<TransactionAccountDto>();
                _filteredAccounts = new List<TransactionAccountDto>();
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        /// <summary>
        /// Refresh data from server
        /// </summary>
        private async Task RefreshData()
        {
            await LoadData();
            await JSRuntime.InvokeVoidAsync("alert", "הנתונים עודכנו בהצלחה");
        }

        /// <summary>
        /// Sort table by column
        /// </summary>
        private void SortTable(string columnName)
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

            _filteredAccounts = columnName switch
            {
                nameof(TransactionAccountDto.AccountName) => _sortAscending
                    ? _filteredAccounts.OrderBy(a => a.AccountName).ToList()
                    : _filteredAccounts.OrderByDescending(a => a.AccountName).ToList(),
                nameof(TransactionAccountDto.RelatedEntityName) => _sortAscending
                    ? _filteredAccounts.OrderBy(a => a.RelatedEntityName).ToList()
                    : _filteredAccounts.OrderByDescending(a => a.RelatedEntityName).ToList(),
                nameof(TransactionAccountDto.AccountTypeName) => _sortAscending
                    ? _filteredAccounts.OrderBy(a => a.AccountTypeName).ToList()
                    : _filteredAccounts.OrderByDescending(a => a.AccountTypeName).ToList(),
                nameof(TransactionAccountDto.Balance) => _sortAscending
                    ? _filteredAccounts.OrderBy(a => a.Balance).ToList()
                    : _filteredAccounts.OrderByDescending(a => a.Balance).ToList(),
                nameof(TransactionAccountDto.IsActive) => _sortAscending
                    ? _filteredAccounts.OrderBy(a => a.IsActive).ToList()
                    : _filteredAccounts.OrderByDescending(a => a.IsActive).ToList(),
                _ => _filteredAccounts
            };

            StateHasChanged();
        }

        /// <summary>
        /// Get sort arrow for column header
        /// </summary>
        private string GetSortArrow(string columnName)
        {
            if (_sortColumn != columnName)
                return "";

            return _sortAscending ? "▲" : "▼";
        }

        /// <summary>
        /// Show add account dialog
        /// </summary>
        private async Task ShowAddAccountDialog()
        {
            await JSRuntime.InvokeVoidAsync("alert", "פונקציונליות הוספת חשבון תיושם בשלב הבא");
            // TODO: Implement add account modal
        }

        /// <summary>
        /// View account details
        /// </summary>
        private async Task ViewAccount(int accountId)
        {
            var executed = await ExecuteSecureActionAsync(
                actionName: "accounts_viewAccount",
                functionName: "ViewAccount",
                action: async () =>
                {
                    // TODO: Navigate to account details page or show modal
                    await JSRuntime.InvokeVoidAsync("alert", $"צפייה בחשבון {accountId}");
                },
                actionParams: $"accountId={accountId}"
            );
        }

        /// <summary>
        /// Edit account
        /// </summary>
        private async Task EditAccount(int accountId)
        {
            var executed = await ExecuteSecureActionAsync(
                actionName: "accounts_editAccount",
                functionName: "EditAccount",
                action: async () =>
                {
                    // TODO: Show edit modal
                    await JSRuntime.InvokeVoidAsync("alert", $"עריכת חשבון {accountId}");
                },
                actionParams: $"accountId={accountId}"
            );
        }

        /// <summary>
        /// Deactivate account
        /// </summary>
        private async Task DeactivateAccount(int accountId)
        {
            var executed = await ExecuteSecureActionAsync(
                actionName: "accounts_deactivateAccount",
                functionName: "DeactivateAccount",
                action: async () =>
                {
                    var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", "האם אתה בטוח שברצונך להשבית חשבון זה?");
                    if (!confirmed)
                        return;

                    try
                    {
                        // Update account to inactive
                        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
                        if (account == null)
                            return;

                        var updateRequest = new
                        {
                            AccountName = account.AccountName,
                            Description = account.Description,
                            IsActive = false
                        };

                        var response = await ApiService.PutAsync<object, ApiResponse<object>>(
                            $"transactionaccounts/{accountId}",
                            updateRequest
                        );

                        if (response?.Success == true)
                        {
                            await JSRuntime.InvokeVoidAsync("alert", "החשבון הושבת בהצלחה");
                            await LoadData(); // Refresh list
                        }
                        else
                        {
                            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {response?.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בהשבתת חשבון: {ex.Message}");
                    }
                },
                actionParams: $"accountId={accountId}"
            );
        }

        /// <summary>
        /// Activate account
        /// </summary>
        private async Task ActivateAccount(int accountId)
        {
            var executed = await ExecuteSecureActionAsync(
                actionName: "accounts_activateAccount",
                functionName: "ActivateAccount",
                action: async () =>
                {
                    try
                    {
                        // Update account to active
                        var account = _accounts.FirstOrDefault(a => a.Id == accountId);
                        if (account == null)
                            return;

                        var updateRequest = new
                        {
                            AccountName = account.AccountName,
                            Description = account.Description,
                            IsActive = true
                        };

                        var response = await ApiService.PutAsync<object, ApiResponse<object>>(
                            $"transactionaccounts/{accountId}",
                            updateRequest
                        );

                        if (response?.Success == true)
                        {
                            await JSRuntime.InvokeVoidAsync("alert", "החשבון הופעל בהצלחה");
                            await LoadData(); // Refresh list
                        }
                        else
                        {
                            await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {response?.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בהפעלת חשבון: {ex.Message}");
                    }
                },
                actionParams: $"accountId={accountId}"
            );
        }
    }

    /// <summary>
    /// DTO for transaction account data
    /// </summary>
    public class TransactionAccountDto
    {
        public int Id { get; set; }
        public int OwnerEntityId { get; set; }
        public string OwnerEntityName { get; set; } = string.Empty;
        public int RelatedEntityId { get; set; }
        public string RelatedEntityName { get; set; } = string.Empty;
        public int AccountTypeId { get; set; }
        public string AccountTypeName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Generic API response wrapper
    /// </summary>
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }
}
