using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PetelApp.BlazorServer.DTOs;

namespace PetelApp.BlazorServer.Components.Pages
{
    public partial class AccountTransactions
    {
        [Parameter]
        public int AccountId { get; set; }

        // Page security identifier
        protected override string PageName => "accounttransactions";

        // State
        private bool _isLoading = true;
        private bool _showFilters = false;
        private bool _showDetailsModal = false;

        // Account data
        private TransactionAccountDto? _account;
        private string _accountName = string.Empty;

        // Transactions data
        private List<TransactionDto> _transactions = new();
        private List<TransactionDto> _filteredTransactions = new();
        private int _totalTransactions = 0;

        // Transaction details
        private TransactionDto? _selectedTransaction;
        private List<TransactionDetailDto> _transactionDetails = new();

        // Lookup data
        private List<TransactionTypeDto> _transactionTypes = new();

        // Filter state
        private DateTime? _filterStartDate;
        private DateTime? _filterEndDate;
        private string _filterTransactionTypeId = "";
        private decimal? _filterMinAmount;
        private decimal? _filterMaxAmount;

        protected override async Task OnPageInitializedAsync()
        {
            await LoadAccountData();
            await LoadLookupData();
            await LoadTransactions();
        }

        private async Task LoadAccountData()
        {
            try
            {
                _account = await ApiService.GetAsync<TransactionAccountDto>($"transactionaccounts/{AccountId}");
                if (_account != null)
                {
                    _accountName = _account.AccountName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading account: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת נתוני חשבון");
            }
        }

        private async Task LoadLookupData()
        {
            try
            {
                _transactionTypes = await ApiService.GetAsync<List<TransactionTypeDto>>("transactions/types") ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading lookup data: {ex.Message}");
            }
        }

        private async Task LoadTransactions()
        {
            _isLoading = true;
            StateHasChanged();

            try
            {
                var queryParams = new List<string>();

                if (_filterStartDate.HasValue)
                    queryParams.Add($"startDate={_filterStartDate.Value:yyyy-MM-dd}");

                if (_filterEndDate.HasValue)
                    queryParams.Add($"endDate={_filterEndDate.Value:yyyy-MM-dd}");

                if (!string.IsNullOrWhiteSpace(_filterTransactionTypeId))
                    queryParams.Add($"transactionTypeId={_filterTransactionTypeId}");

                if (_filterMinAmount.HasValue)
                    queryParams.Add($"minAmount={_filterMinAmount.Value}");

                if (_filterMaxAmount.HasValue)
                    queryParams.Add($"maxAmount={_filterMaxAmount.Value}");

                var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
                var endpoint = $"transactions/account/{AccountId}{query}";

                _transactions = await ApiService.GetAsync<List<TransactionDto>>(endpoint) ?? new();
                _filteredTransactions = _transactions;
                _totalTransactions = _transactions.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading transactions: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת עסקאות");
                _transactions = new();
                _filteredTransactions = new();
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        private async Task ViewTransactionDetails(int transactionId)
        {
            try
            {
                var result = await ApiService.GetAsync<TransactionWithDetailsDto>($"transactions/{transactionId}/details");
                
                if (result != null)
                {
                    _selectedTransaction = result.Transaction;
                    _transactionDetails = result.Details;
                    _showDetailsModal = true;
                    StateHasChanged();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading transaction details: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת פירוט עסקה");
            }
        }

        private void CloseDetailsModal()
        {
            _showDetailsModal = false;
            _selectedTransaction = null;
            _transactionDetails = new();
            StateHasChanged();
        }

        private void ToggleFilters()
        {
            _showFilters = !_showFilters;
            StateHasChanged();
        }

        private async Task ApplyFilters()
        {
            await LoadTransactions();
        }

        private async Task ClearFilters()
        {
            _filterStartDate = null;
            _filterEndDate = null;
            _filterTransactionTypeId = "";
            _filterMinAmount = null;
            _filterMaxAmount = null;

            await LoadTransactions();
        }

        private async Task ShowAddTransactionDialog()
        {
            // TODO: Implement add transaction dialog
            await JSRuntime.InvokeVoidAsync("alert", "הוספת עסקה תתווסף בגרסה הבאה");
        }

        private async Task RefreshData()
        {
            await LoadAccountData();
            await LoadTransactions();
        }

        private void NavigateBackToAccounts()
        {
            NavigationManager.NavigateTo("/transactionaccounts");
        }
    }
}
