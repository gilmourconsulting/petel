using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using PetelATH.BlazorServer.DTOs;

namespace PetelATH.BlazorServer.Components.Pages
{
    public partial class AccountTransactions : SecurePageBase
    {
        // Page security identifier
        protected override string PageName => "accounttransactions";

        // Account ID loaded from session
        private int _accountId;

        // State
        private bool _isLoading = true;
        private bool _showFilters = false;
        private bool _loadingDetails = false;
        private int? _expandedTransactionId = null;

        // Account data
        private TransactionAccountDto? _account;
        private string _accountName = string.Empty;
        private decimal _calculatedBalance = 0m;

        // Transactions data
        private List<TransactionDto> _transactions = new();
        private List<TransactionDto> _filteredTransactions = new();
        private int _totalTransactions = 0;

        // Transaction details
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
            // Get account ID from session
            var accountIdResponse = await ApiService.GetAsync<Dictionary<string, string>>(
                "session/property/SelectedAccountId");

            if (accountIdResponse == null || !accountIdResponse.TryGetValue("value", out var accountIdStr))
            {
                await JSRuntime.InvokeVoidAsync("alert", "לא נבחר חשבון");
                NavigationManager.NavigateTo("/transactionaccounts");
                return;
            }

            _accountId = int.Parse(accountIdStr);

            await LoadAccountData();
            await LoadLookupData();
            await LoadTransactions();
        }

        private async Task LoadAccountData()
        {
            try
            {
                // Use ApiResponse wrapper to match the backend response structure
                var response = await ApiService.GetAsync<ApiResponse<TransactionAccountDto>>($"transactionaccounts/{_accountId}");
                if (response?.Success == true && response.Data != null)
                {
                    _account = response.Data;
                    _accountName = _account.AccountName;
                    Console.WriteLine($"✅ Account loaded: {_accountName}");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to load account: {response?.Message}");
                    // Fallback: try as direct object
                    _account = await ApiService.GetAsync<TransactionAccountDto>($"transactionaccounts/{_accountId}");
                    if (_account != null)
                    {
                        _accountName = _account.AccountName;
                        Console.WriteLine($"✅ Account loaded (fallback): {_accountName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading account: {ex.Message}");
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
                var endpoint = $"transactions/account/{_accountId}{query}";

                _transactions = await ApiService.GetAsync<List<TransactionDto>>(endpoint) ?? new();
                _filteredTransactions = _transactions;
                _totalTransactions = _transactions.Count;
                
                // Calculate balance from actual transactions
                CalculateBalance();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading transactions: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת עסקאות");
                _transactions = new();
                _filteredTransactions = new();
                _calculatedBalance = 0m;
            }
            finally
            {
                _isLoading = false;
                StateHasChanged();
            }
        }

        private async Task ToggleTransactionDetails(int transactionId)
        {
            // If clicking the same transaction, collapse it
            if (_expandedTransactionId == transactionId)
            {
                _expandedTransactionId = null;
                _transactionDetails = new();
                StateHasChanged();
                return;
            }

            // Otherwise, expand the new transaction
            _expandedTransactionId = transactionId;
            _loadingDetails = true;
            StateHasChanged();

            try
            {
                var result = await ApiService.GetAsync<TransactionWithDetailsDto>($"transactions/{transactionId}/details");
                
                if (result != null)
                {
                    _transactionDetails = result.Details;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading transaction details: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בטעינת פירוט עסקה");
                _expandedTransactionId = null;
                _transactionDetails = new();
            }
            finally
            {
                _loadingDetails = false;
                StateHasChanged();
            }
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

        private void CalculateBalance()
        {
            // Calculate balance from all transactions
            // Credit transactions add to balance, debit transactions subtract
            _calculatedBalance = _transactions.Sum(t => t.IsCredit ? t.Amount : -t.Amount);
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

        private async Task NavigateToStudent(int studentId)
        {
            try
            {
                // Fetch student data with all navigation context
                var response = await ApiService.GetAsync<Dictionary<string, object>>($"students/{studentId}");
                
                if (response == null)
                {
                    await JSRuntime.InvokeVoidAsync("alert", "לא נמצא תלמיד");
                    return;
                }

                // Extract all required session properties from response
                var schoolYearId = response.ContainsKey("schoolYearId") ? response["schoolYearId"]?.ToString() : null;
                var schoolId = response.ContainsKey("schoolId") ? response["schoolId"]?.ToString() : null;
                var schoolName = response.ContainsKey("schoolName") ? response["schoolName"]?.ToString() : null;
                var yearId = response.ContainsKey("yearId") ? response["yearId"]?.ToString() : null;
                var yearValue = response.ContainsKey("yearValue") ? response["yearValue"]?.ToString() : null;

                if (string.IsNullOrEmpty(schoolYearId) || string.IsNullOrEmpty(schoolId) || 
                    string.IsNullOrEmpty(schoolName) || string.IsNullOrEmpty(yearId) || 
                    string.IsNullOrEmpty(yearValue))
                {
                    await JSRuntime.InvokeVoidAsync("alert", "נתוני תלמיד חסרים");
                    return;
                }

                // Set all required session properties for student navigation
                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedStudentId", value = studentId.ToString() });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedSchoolYearId", value = schoolYearId });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedSchoolId", value = schoolId });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedSchoolName", value = schoolName });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedYearId", value = yearId });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "SelectedYearValue", value = yearValue });

                await ApiService.PostAsync<object, object>(
                    "session/property",
                    new { key = "NavigationSource", value = "transactiondetails" });

                NavigationManager.NavigateTo("/student");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error navigating to student: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", "שגיאה בניווט לתלמיד");
            }
        }
    }
}
