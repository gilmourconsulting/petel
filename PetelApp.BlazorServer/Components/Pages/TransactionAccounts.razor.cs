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

        // Add Account Modal
        private bool _showAddDialog = false;
        private CreateAccountDto _newAccount = new();
        private List<AccountTypeDto> _accountTypes = new();
        private List<EntityDto> _availableEntities = new();
        private string _selectedAccountTypeName = "";

        // Create Council Entity Modal
        private bool _showCreateCouncilDialog = false;
        private string _councilSearchText = "";
        private List<CouncilDto> _allCouncils = new();
        private List<CouncilDto> _filteredCouncils = new();
        private CouncilDto? _selectedCouncil = null;
        private bool _showCouncilDropdown = false;
        private int? _hoveredCouncilId = null;

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
            var executed = await ExecuteSecureActionAsync(
                actionName: "accounts_addAccount",
                functionName: "ShowAddAccountDialog",
                action: async () =>
                {
                    // Load account types only
                    await LoadAccountTypes();
                    
                    // Initialize new account
                    _newAccount = new CreateAccountDto
                    {
                        IsActive = true,
                        Balance = 0
                    };
                    
                    // Clear entities list - will be loaded when account type is selected
                    _availableEntities = new List<EntityDto>();
                    _selectedAccountTypeName = "";
                    
                    _showAddDialog = true;
                    StateHasChanged();
                }
            );
        }

        /// <summary>
        /// Load account types from API
        /// </summary>
        private async Task LoadAccountTypes()
        {
            try
            {
                // API returns { success: true, data: [...] }
                var response = await ApiService.GetAsync<ApiResponse<List<AccountTypeDto>>>("transactionaccounts/account-types");
                _accountTypes = response?.Data ?? new List<AccountTypeDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading account types: {ex.Message}");
                _accountTypes = new List<AccountTypeDto>();
            }
        }

        /// <summary>
        /// Load available entities for the related entity dropdown
        /// Filters by entity type based on selected account type
        /// </summary>
        private async Task LoadAvailableEntities()
        {
            try
            {
                Console.WriteLine($"🔍 LoadAvailableEntities called. Selected account type: {_selectedAccountTypeName}");
                
                string endpoint;
                
                // Build endpoint with optional entity type filter
                if (_selectedAccountTypeName == "external_students_fees")
                {
                    Console.WriteLine("🔽 Requesting only council entities (type 2)");
                    endpoint = "transactionaccounts/available-entities?entityTypeId=2";
                }
                else
                {
                    Console.WriteLine("🔽 Requesting all available entities");
                    endpoint = "transactionaccounts/available-entities";
                }
                
                var response = await ApiService.GetAsync<ApiResponse<List<EntityDto>>>(endpoint);
                _availableEntities = response?.Data ?? new List<EntityDto>();

                Console.WriteLine($"✅ Loaded {_availableEntities.Count} entities");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading entities: {ex.Message}");
                _availableEntities = new List<EntityDto>();
            }
        }

        /// <summary>
        /// Close add dialog
        /// </summary>
        private void CloseAddDialog()
        {
            _showAddDialog = false;
            _newAccount = new CreateAccountDto();
            _selectedAccountTypeName = "";
        }

        /// <summary>
        /// Handle account type change - reload entities based on type
        /// </summary>
        private async Task OnAccountTypeChanged()
        {
            Console.WriteLine($"🔄 Account type changed. AccountTypeId: {_newAccount.AccountTypeId}");
            
            // Get selected account type name
            var selectedType = _accountTypes.FirstOrDefault(t => t.Id == _newAccount.AccountTypeId);
            _selectedAccountTypeName = selectedType?.Name ?? "";
            
            Console.WriteLine($"📋 Selected account type name: {_selectedAccountTypeName}");

            // Clear selected entity
            _newAccount.RelatedEntityId = 0;
            
            // Clear account name - will be set when entity is selected
            _newAccount.AccountName = "";

            // Reload entities with new filter
            await LoadAvailableEntities();
            
            Console.WriteLine($"✅ Loaded {_availableEntities.Count} entities after filtering");
            
            StateHasChanged();
        }
        
        /// <summary>
        /// Handle entity selection - update default account name
        /// </summary>
        private void OnEntityChanged()
        {
            if (_newAccount.RelatedEntityId == 0)
            {
                _newAccount.AccountName = "";
                return;
            }
            
            var selectedEntity = _availableEntities.FirstOrDefault(e => e.Id == _newAccount.RelatedEntityId);
            var selectedType = _accountTypes.FirstOrDefault(t => t.Id == _newAccount.AccountTypeId);
            
            if (selectedEntity != null && selectedType != null)
            {
                _newAccount.AccountName = $"{selectedType.Description} - {selectedEntity.EntityName}";
                Console.WriteLine($"✅ Set default account name: {_newAccount.AccountName}");
            }
            
            StateHasChanged();
        }

        /// <summary>
        /// Show create council entity dialog
        /// </summary>
        private async Task ShowCreateCouncilEntityDialog()
        {
            try
            {
                // Load all councils (no year required)
                var response = await ApiService.GetAsync<ApiResponse<List<CouncilDto>>>("systemattributes/councils");
                _allCouncils = response?.Data ?? new List<CouncilDto>();

                // Get all existing council entities to filter them out
                // Since EntityDto doesn't expose council_id, we'll show all councils
                // and let the API handle the duplicate check on create
                
                _councilSearchText = "";
                _filteredCouncils = new List<CouncilDto>();
                _selectedCouncil = null;
                _showCreateCouncilDialog = true;
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading councils: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בטעינת מועצות: {ex.Message}");
            }
        }

        /// <summary>
        /// Filter councils based on search text
        /// </summary>
        private void FilterCouncils()
        {
            if (string.IsNullOrWhiteSpace(_councilSearchText))
            {
                _filteredCouncils = _allCouncils.Take(50).ToList(); // Show first 50 when empty
            }
            else
            {
                _filteredCouncils = _allCouncils
                    .Where(c => c.CouncilName.Contains(_councilSearchText, StringComparison.OrdinalIgnoreCase))
                    .Take(50)
                    .ToList();
            }
        }

        /// <summary>
        /// Handle council search input
        /// </summary>
        private void OnCouncilSearchInput(ChangeEventArgs e)
        {
            _councilSearchText = e.Value?.ToString() ?? "";
            FilterCouncils();
            _showCouncilDropdown = true;
            StateHasChanged();
        }

        /// <summary>
        /// Handle council input focus
        /// </summary>
        private void OnCouncilFocus()
        {
            // Show councils when focusing
            if (string.IsNullOrWhiteSpace(_councilSearchText))
            {
                _filteredCouncils = _allCouncils.Take(50).ToList();
            }
            else
            {
                FilterCouncils();
            }
            _showCouncilDropdown = true;
            StateHasChanged();
        }

        /// <summary>
        /// Select a council from the autocomplete
        /// </summary>
        private void SelectCouncil(CouncilDto council)
        {
            _selectedCouncil = council;
            _councilSearchText = council.CouncilName;
            _showCouncilDropdown = false;
            _filteredCouncils = new List<CouncilDto>();
            StateHasChanged();
        }

        /// <summary>
        /// Create entity for selected council
        /// </summary>
        private async Task CreateCouncilEntity()
        {
            if (_selectedCouncil == null)
            {
                await JSRuntime.InvokeVoidAsync("alert", "נא לבחור מועצה");
                return;
            }

            try
            {
                var createRequest = new
                {
                    CouncilId = _selectedCouncil.Id
                };

                var response = await ApiService.PostAsync<object, CreateCouncilEntityResponse>(
                    "transactionaccounts/create-council-entity",
                    createRequest
                );

                if (response?.Success == true)
                {
                    await JSRuntime.InvokeVoidAsync("alert", $"הישות עבור {_selectedCouncil.CouncilName} נוצרה בהצלחה");

                    // Close council dialog
                    CloseCreateCouncilDialog();

                    // Reload available entities
                    await LoadAvailableEntities();

                    // Auto-select the new entity
                    if (response.Data != null)
                    {
                        _newAccount.RelatedEntityId = response.Data.Id;
                        StateHasChanged();
                    }
                }
                else
                {
                    string errorMsg = response?.Message ?? "שגיאה לא ידועה";
                    await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating council entity: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", $"שגיאה ביצירת ישות: {ex.Message}");
            }
        }

        /// <summary>
        /// Close create council dialog
        /// </summary>
        private void CloseCreateCouncilDialog()
        {
            _showCreateCouncilDialog = false;
            _councilSearchText = "";
            _filteredCouncils = new List<CouncilDto>();
            _selectedCouncil = null;
            _showCouncilDropdown = false;
            _hoveredCouncilId = null;
        }

        /// <summary>
        /// Save new account
        /// </summary>
        private async Task SaveNewAccount()
        {
            // Validation
            if (_newAccount.AccountTypeId == 0)
            {
                await JSRuntime.InvokeVoidAsync("alert", "נא לבחור סוג חשבון");
                return;
            }

            if (_newAccount.RelatedEntityId == 0)
            {
                await JSRuntime.InvokeVoidAsync("alert", "נא לבחור ישות קשורה");
                return;
            }

            if (string.IsNullOrWhiteSpace(_newAccount.AccountName))
            {
                await JSRuntime.InvokeVoidAsync("alert", "נא להזין שם חשבון");
                return;
            }

            try
            {
                var response = await ApiService.PostAsync<CreateAccountDto, ApiResponse<object>>(
                    "transactionaccounts",
                    _newAccount
                );

                if (response?.Success == true)
                {
                    await JSRuntime.InvokeVoidAsync("alert", "החשבון נוסף בהצלחה");
                    CloseAddDialog();
                    await LoadData(); // Refresh the list
                }
                else
                {
                    await JSRuntime.InvokeVoidAsync("alert", $"שגיאה: {response?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving account: {ex.Message}");
                await JSRuntime.InvokeVoidAsync("alert", $"שגיאה בשמירת החשבון: {ex.Message}");
            }
        }

        /// <summary>
        /// View account details
        /// <summary>
        /// Navigate to account transactions view
        /// </summary>
        private void ViewTransactions(int accountId)
        {
            NavigationManager.NavigateTo($"/accounttransactions/{accountId}");
        }

        /// <summary>
        /// View account details (deprecated - replaced with ViewTransactions)
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

    /// <summary>
    /// DTO for creating a new account
    /// </summary>
    public class CreateAccountDto
    {
        public int AccountTypeId { get; set; }
        public int RelatedEntityId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// DTO for account type
    /// </summary>
    public class AccountTypeDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
