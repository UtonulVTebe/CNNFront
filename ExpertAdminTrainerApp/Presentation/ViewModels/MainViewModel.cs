using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Presentation.Controls;
using ExpertAdminTrainerApp.Services;
using Microsoft.Win32;

namespace ExpertAdminTrainerApp.Presentation.ViewModels;

public partial class MainViewModel(IApiClient apiClient, ITokenStore tokenStore, IAppNavigator appNavigator) : ObservableObject
{
    private bool _suppressSelectedOrderChanged;

    /// <summary>Сообщение для экрана входа, если восстановление сессии не удалось (однократно через <see cref="ConsumeLoginScreenHint"/>).</summary>
    private string? _loginScreenHint;
    // ===== Auth =====
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private int? currentUserId;
    [ObservableProperty] private string statusText = "Готово.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isAuthenticated;

    /// <summary>Email из JWT (логин).</summary>
    [ObservableProperty] private string currentUserEmail = string.Empty;

    /// <summary>Выпадающее меню учётной записи в шапке главного окна.</summary>
    [ObservableProperty] private bool isAccountMenuOpen;

    [ObservableProperty] private bool isProfileEditorVisible;

    [ObservableProperty] private string editableProfileName = string.Empty;

    /// <summary>Баланс из GET api/ExpertInfos/{userId}; null — не загружен или профиль недоступен.</summary>
    [ObservableProperty] private int? expertAccountBalance;

    /// <summary>Строка состояния: выделить как ошибку (контрастнее цвет в MainWindow).</summary>
    public bool StatusBarIsError => StatusTextIndicatesError(StatusText);

    /// <summary>Сводка в меню (скрыта в режиме редактирования имени).</summary>
    public bool ShowAccountSummary => !IsProfileEditorVisible;

    public bool HasUserEmail => !string.IsNullOrWhiteSpace(CurrentUserEmail);

    /// <summary>Текст строки баланса в меню профиля (видна только у Expert).</summary>
    public string ExpertBalanceSubtitle =>
        !IsExpert ? string.Empty
        : ExpertAccountBalance.HasValue ? $"Баланс: {ExpertAccountBalance.Value}" : "Баланс: нет данных";

    // ===== CNN Catalog =====
    [ObservableProperty] private CnnListItemDto? selectedCnn;
    [ObservableProperty] private bool isCreatingCnn;
    [ObservableProperty] private string newCnnSubject = string.Empty;
    [ObservableProperty] private int newCnnOption = 1;
    [ObservableProperty] private bool isEditingCnn;
    [ObservableProperty] private string editCnnSubject = string.Empty;
    [ObservableProperty] private int editCnnOption;

    // ===== Material Editing =====
    [ObservableProperty] private bool isEditingMaterial;
    [ObservableProperty] private bool isNewMaterial;
    [ObservableProperty] private MaterialKind editingMaterialKind;
    [ObservableProperty] private CnnMaterialDto? editingMaterial;
    [ObservableProperty] private string materialTitle = string.Empty;
    [ObservableProperty] private string materialUrl = string.Empty;
    [ObservableProperty] private int materialSortOrder;

    // ===== Orders =====
    public const string QueueFilterServerDefault = "(по умолчанию сервера)";

    [ObservableProperty] private string queueStatusFilter = QueueFilterServerDefault;
    [ObservableProperty] private OrderAnswerReadDto? selectedQueueOrder;
    [ObservableProperty] private string updateAnswerUrl = string.Empty;
    [ObservableProperty] private string rejectionReason = string.Empty;
    [ObservableProperty] private bool isRejectingOrder;

    [ObservableProperty] private DateTime? expertMyOrdersFrom;
    [ObservableProperty] private DateTime? expertMyOrdersTo;

    // ===== Просмотр пакета ответа ученика (JSON) =====
    [ObservableProperty] private ExamSubmissionDocument? orderSubmissionDocument;
    [ObservableProperty] private string? orderSubmissionBlankJsonUrl;
    /// <summary>Индекс страницы в полном шаблоне для ключей answers (передаётся в канву превью).</summary>
    [ObservableProperty] private int orderSubmissionTemplatePageIndex;
    [ObservableProperty] private ImageSource? orderSubmissionPageImage;
    [ObservableProperty] private OrderExpertSheet2PageItem? selectedOrderExpertSheet2Page;
    [ObservableProperty] private double orderPreviewZoom = 1.0;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrderSubmissionLoadError))]
    private string? orderSubmissionLoadError;
    [ObservableProperty] private bool hasOrderExpertSheet2Pages;
    [ObservableProperty] private bool hasOrderExpPartRows;
    [ObservableProperty] private bool showOrderSubmissionNoSheet2Hint;

    // ===== Review =====
    [ObservableProperty] private ReviewReadDto? currentReview;
    [ObservableProperty] private bool isEditingReview;
    [ObservableProperty] private int reviewTotalScore;
    [ObservableProperty] private string reviewGeneralComment = string.Empty;

    // ===== Users / Expert Management =====
    [ObservableProperty] private string userSearch = string.Empty;
    [ObservableProperty] private string userRoleFilter = "Expert";
    [ObservableProperty] private UserListItemDto? selectedUser;
    [ObservableProperty] private int expertInfoBalance;

    /// <summary>Редактирование выбранного пользователя (админ): ФИО, email, пароль.</summary>
    [ObservableProperty] private string adminEditUserName = string.Empty;

    [ObservableProperty] private string adminEditUserEmail = string.Empty;

    /// <summary>Только ввод; не хранится после сохранения. Синхронизируется с PasswordBox в UsersView.</summary>
    [ObservableProperty] private string adminEditUserPassword = string.Empty;

    // ===== Collections =====
    public ObservableCollection<OrderAnswerReadDto> QueueOrders { get; } = [];
    public ObservableCollection<OrderAnswerReadDto> MyCheckingOrders { get; } = [];
    public ObservableCollection<CnnListItemDto> Cnns { get; } = [];
    public ObservableCollection<CnnMaterialDto> KimMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> CriteriaMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> BlanksMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> OtherMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> OrderKimMaterials { get; } = [];
    public ObservableCollection<CnnMaterialDto> OrderCriteriaMaterials { get; } = [];
    public ObservableCollection<UserListItemDto> Users { get; } = [];
    public ObservableCollection<ReviewCriterionDto> ReviewCriteria { get; } = [];
    public ObservableCollection<OrderExpertSheet2PageItem> OrderExpertSheet2Pages { get; } = [];
    public ObservableCollection<AnswerExpPartItem> OrderExpPartRows { get; } = [];
    public ObservableCollection<ZoneDefinition> OrderSubmissionCurrentZones { get; } = [];

    private DictionaryAnswerSink? _orderSubmissionAnswerSink;

    public IReadOnlyList<string> QueueStatusOptions { get; } =
        [QueueFilterServerDefault, .. Enum.GetNames<OrderAnswerStatus>()];

    // ===== Computed =====
    public bool IsExpert => IsAuthenticated && string.Equals(Role, "Expert", StringComparison.OrdinalIgnoreCase);
    public bool IsAdmin => IsAuthenticated && string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);
    public bool IsAdminOrExpert => IsExpert || IsAdmin;
    public bool HasSelectedCnn => SelectedCnn is not null;
    public bool HasNoSelectedCnn => SelectedCnn is null;
    public bool HasSelectedOrder => SelectedQueueOrder is not null;
    public bool HasNoSelectedOrder => SelectedQueueOrder is null;
    public bool HasSelectedUser => SelectedUser is not null;
    public bool HasNoSelectedUser => SelectedUser is null;

    /// <summary>Карточка «Назначить Expert»: только если выбранный пользователь ещё не Expert и не Admin.</summary>
    public bool ShowAdminPromoteExpert =>
        SelectedUser is not null && !IsUserRoleExpertOrAdmin(SelectedUser.Role);

    /// <summary>Карточка «Понизить до Student»: только Expert, не своя учётная запись.</summary>
    public bool ShowAdminDemoteExpert =>
        SelectedUser is not null
        && IsUserRoleExpert(SelectedUser.Role)
        && SelectedUser.Id != CurrentUserId;

    /// <summary>Блок профиля эксперта (баланс): только для роли Expert.</summary>
    public bool ShowAdminExpertProfile =>
        SelectedUser is not null && IsUserRoleExpert(SelectedUser.Role);
    public bool HasReview => CurrentReview is not null;
    public bool HasNoReview => CurrentReview is null;
    public IZoneAnswerSink? OrderSubmissionAnswerSink => _orderSubmissionAnswerSink;
    public bool HasOrderSubmissionLoadError => !string.IsNullOrWhiteSpace(OrderSubmissionLoadError);
    public string EditingMaterialKindLabel => KindLabel(EditingMaterialKind);

    /// <summary>Кнопка «Взять в работу»: Expert или Admin; только <see cref="OrderAnswerStatus.QueueForCheck"/>; без эксперта; не свой заказ (если из JWT известен числовой Id).</summary>
    public bool CanClaimSelectedOrder =>
        SelectedQueueOrder is not null && ExplainWhyCannotClaimOrder() is null;

    public bool CanRejectSelectedOrder =>
        IsAdminOrExpert && SelectedQueueOrder is { } o &&
        o.Status == OrderAnswerStatus.Checking &&
        (IsAdmin || (CurrentUserId.HasValue && o.ExpertId == CurrentUserId.Value));

    /// <summary>Новая проверка (POST) в статусе Checking.</summary>
    public bool CanSubmitNewReview =>
        IsAdminOrExpert && SelectedQueueOrder is { } o &&
        o.Status == OrderAnswerStatus.Checking &&
        (IsAdmin || (CurrentUserId.HasValue && o.ExpertId == CurrentUserId.Value));

    public bool CanEditExistingReview =>
        IsAdminOrExpert && SelectedQueueOrder is { Status: OrderAnswerStatus.Checked } && HasReview;

    public bool CanOpenReviewEditor => CanSubmitNewReview || CanEditExistingReview;

    public string SelectedOrderStatusText =>
        SelectedQueueOrder is null ? string.Empty : FormatOrderStatus(SelectedQueueOrder.Status);

    public static string KindLabel(MaterialKind kind) => kind switch
    {
        MaterialKind.Kim => "КИМ (задания)",
        MaterialKind.Criteria => "Критерии оценивания",
        MaterialKind.Blanks => "Бланки ответов",
        MaterialKind.Other => "Прочие материалы",
        _ => kind.ToString()
    };

    // ===== Change Handlers =====
    partial void OnIsAuthenticatedChanged(bool value) => RaiseAll();

    partial void OnStatusTextChanged(string value) => OnPropertyChanged(nameof(StatusBarIsError));

    partial void OnExpertAccountBalanceChanged(int? value) => OnPropertyChanged(nameof(ExpertBalanceSubtitle));
    partial void OnIsProfileEditorVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowAccountSummary));
    partial void OnCurrentUserEmailChanged(string value) => OnPropertyChanged(nameof(HasUserEmail));
    partial void OnRoleChanged(string value) => RaiseAll();
    partial void OnEditingMaterialKindChanged(MaterialKind value) => OnPropertyChanged(nameof(EditingMaterialKindLabel));

    partial void OnCurrentReviewChanged(ReviewReadDto? value)
    {
        OnPropertyChanged(nameof(HasReview));
        OnPropertyChanged(nameof(HasNoReview));
        RaiseExpertOrderFlags();
    }

    partial void OnCurrentUserIdChanged(int? value)
    {
        RaiseExpertOrderFlags();
        OnPropertyChanged(nameof(ShowAdminDemoteExpert));
        DemoteExpertToStudentCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCnnChanged(CnnListItemDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedCnn));
        OnPropertyChanged(nameof(HasNoSelectedCnn));
        ClearMaterialCollections();
        IsEditingCnn = false;
        IsEditingMaterial = false;
        if (value is not null)
        {
            EditCnnSubject = value.Subject;
            EditCnnOption = value.Option;
            _ = LoadMaterialsForSelectedCnn();
        }
    }

    partial void OnSelectedQueueOrderChanged(OrderAnswerReadDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedOrder));
        OnPropertyChanged(nameof(HasNoSelectedOrder));
        OnPropertyChanged(nameof(SelectedOrderStatusText));
        if (value is null)
        {
            UpdateAnswerUrl = string.Empty;
            RejectionReason = string.Empty;
            IsRejectingOrder = false;
            OrderKimMaterials.Clear();
            OrderCriteriaMaterials.Clear();
            CurrentReview = null;
            ReviewCriteria.Clear();
            IsEditingReview = false;
            ClearOrderSubmissionPreview();
            RaiseExpertOrderFlags();
            return;
        }

        if (_suppressSelectedOrderChanged)
        {
            UpdateAnswerUrl = value.AnswerUrl ?? string.Empty;
            RejectionReason = string.Empty;
            IsRejectingOrder = false;
            _ = LoadOrderCnnDetails(value.CnnId);
            if (IsAdminOrExpert) _ = LoadReview(value.Id);
            OnPropertyChanged(nameof(SelectedOrderStatusText));
            RaiseExpertOrderFlags();
            return;
        }

        UpdateAnswerUrl = value.AnswerUrl ?? string.Empty;
        RejectionReason = string.Empty;
        IsRejectingOrder = false;
        OrderKimMaterials.Clear();
        OrderCriteriaMaterials.Clear();
        CurrentReview = null;
        ReviewCriteria.Clear();
        IsEditingReview = false;
        _ = LoadOrderCnnDetails(value.CnnId);
        if (IsAdminOrExpert) _ = LoadReview(value.Id);
        _ = HydrateSelectedOrderFromServerAsync(value);
    }

    partial void OnSelectedOrderExpertSheet2PageChanged(OrderExpertSheet2PageItem? value) =>
        _ = LoadOrderSubmissionPageAsync();

    partial void OnSelectedUserChanged(UserListItemDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(HasNoSelectedUser));
        if (value is not null)
        {
            ExpertInfoBalance = value.Balance ?? 0;
            AdminEditUserName = value.Name ?? string.Empty;
            AdminEditUserEmail = value.Email ?? string.Empty;
            AdminEditUserPassword = string.Empty;
        }
        else
        {
            AdminEditUserName = string.Empty;
            AdminEditUserEmail = string.Empty;
            AdminEditUserPassword = string.Empty;
        }

        OnPropertyChanged(nameof(ShowAdminPromoteExpert));
        OnPropertyChanged(nameof(ShowAdminDemoteExpert));
        OnPropertyChanged(nameof(ShowAdminExpertProfile));
        PromoteToExpertCommand.NotifyCanExecuteChanged();
        DemoteExpertToStudentCommand.NotifyCanExecuteChanged();
        CreateExpertInfoCommand.NotifyCanExecuteChanged();
        SaveAdminUserEditsCommand.NotifyCanExecuteChanged();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsExpert));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAdminOrExpert));
        OnPropertyChanged(nameof(ExpertBalanceSubtitle));
        RaiseExpertOrderFlags();
    }

    private void RaiseExpertOrderFlags()
    {
        OnPropertyChanged(nameof(CanClaimSelectedOrder));
        OnPropertyChanged(nameof(CanRejectSelectedOrder));
        OnPropertyChanged(nameof(CanSubmitNewReview));
        OnPropertyChanged(nameof(CanEditExistingReview));
        OnPropertyChanged(nameof(CanOpenReviewEditor));
        OnPropertyChanged(nameof(SelectedOrderStatusText));
    }

    // ========== AUTH ==========

    /// <summary>Восстановление сессии при старте приложения или перед показом главного окна.</summary>
    public async Task<bool> TryRestoreSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken)) return false;
        try
        {
            ApplyTokenContext(tokenStore.AccessToken);
            if (!IsAuthenticated) return false;
            await apiClient.GetMineAsync();
            await TryHydrateProfileFromUsersApiAsync();
            StatusText = $"Сессия восстановлена: {UserName} ({Role})";
            await PostLoginLoad();
            return true;
        }
        catch (Exception ex)
        {
            _loginScreenHint =
                "Не удалось восстановить сессию (токен устарел или сервер недоступен). Войдите снова. Подробности: "
                + ex.Message;
            tokenStore.Clear();
            IsAuthenticated = false;
            CurrentUserEmail = string.Empty;
            ExpertAccountBalance = null;
            IsAccountMenuOpen = false;
            IsProfileEditorVisible = false;
            StatusText = _loginScreenHint;
            return false;
        }
    }

    /// <summary>Текст для показа на LoginWindow после неудачного <see cref="TryRestoreSessionAsync"/>.</summary>
    public string? ConsumeLoginScreenHint()
    {
        var t = _loginScreenHint;
        _loginScreenHint = null;
        return t;
    }

    /// <summary>После успешного API-логина: разбор JWT, загрузка данных роли.</summary>
    public async Task<bool> ApplyLoginResponseAsync(string accessToken)
    {
        ApplyTokenContext(accessToken);
        if (!IsAuthenticated) return false;
        await TryHydrateProfileFromUsersApiAsync();
        StatusText = $"Вход выполнен: {UserName} ({Role})";
        await PostLoginLoad();
        return true;
    }

    [RelayCommand]
    private void Logout()
    {
        tokenStore.Clear();
        IsAuthenticated = false;
        UserName = Role = string.Empty;
        CurrentUserEmail = string.Empty;
        ExpertAccountBalance = null;
        IsAccountMenuOpen = false;
        IsProfileEditorVisible = false;
        EditableProfileName = string.Empty;
        CurrentUserId = null;
        QueueOrders.Clear();
        MyCheckingOrders.Clear();
        Cnns.Clear();
        Users.Clear();
        ClearMaterialCollections();
        StatusText = "Вы вышли из системы.";
        appNavigator.ReturnToLoginAfterLogout();
    }

    [RelayCommand]
    private void ToggleAccountMenu()
    {
        var opening = !IsAccountMenuOpen;
        IsAccountMenuOpen = opening;
        if (opening)
        {
            IsProfileEditorVisible = false;
            if (IsExpert && CurrentUserId.HasValue)
                _ = TryHydrateExpertBalanceAsync(clearFirst: false);
        }
    }

    [RelayCommand]
    private void BeginEditProfile()
    {
        EditableProfileName = (UserName ?? string.Empty).Trim();
        IsProfileEditorVisible = true;
    }

    [RelayCommand]
    private void CancelEditProfile()
    {
        IsProfileEditorVisible = false;
        EditableProfileName = string.Empty;
    }

    [RelayCommand]
    private async Task SaveProfileAsync()
    {
        var name = (EditableProfileName ?? string.Empty).Trim();
        if (name.Length < 2)
        {
            StatusText = "Имя должно содержать не менее 2 символов.";
            return;
        }

        if (!CurrentUserId.HasValue)
        {
            StatusText = "Не удалось определить учётную запись (id).";
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await apiClient.UpdateUserAsync(CurrentUserId.Value, new UserUpdateDto { Name = name, Role = null });
            UserName = string.IsNullOrWhiteSpace(updated.Name) ? name : updated.Name.Trim();
            IsProfileEditorVisible = false;
            IsAccountMenuOpen = false;
            EditableProfileName = string.Empty;
            StatusText = "Профиль сохранён.";
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось сохранить профиль: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PostLoginLoad()
    {
        if (IsAdmin)
        {
            await LoadCnns();
            await LoadUsers();
        }
        if (IsAdminOrExpert)
        {
            await LoadQueueInternal(true);
            await LoadMyCheckingInternal(true);
        }
    }

    // ========== CNN CATALOG ==========

    [RelayCommand]
    private async Task LoadCnns()
    {
        try
        {
            IsBusy = true;
            Cnns.Clear();
            var list = await apiClient.GetCnnsAsync();
            foreach (var item in list) Cnns.Add(item);
            StatusText = $"Загружено вариантов: {Cnns.Count}";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки каталога: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BeginCreateCnn()
    {
        IsCreatingCnn = true;
        NewCnnSubject = string.Empty;
        NewCnnOption = 1;
    }

    [RelayCommand] private void CancelCreateCnn() => IsCreatingCnn = false;

    [RelayCommand]
    private async Task ConfirmCreateCnn()
    {
        if (string.IsNullOrWhiteSpace(NewCnnSubject)) { StatusText = "Укажите предмет."; return; }
        try
        {
            IsBusy = true;
            var created = await apiClient.CreateCnnAsync(new CnnWriteDto { Subject = NewCnnSubject.Trim(), Option = NewCnnOption });
            Cnns.Add(created);
            IsCreatingCnn = false;
            SelectedCnn = created;
            StatusText = $"Создан: {created.Subject} — Вариант {created.Option}";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BeginEditCnn()
    {
        if (SelectedCnn is null) return;
        EditCnnSubject = SelectedCnn.Subject;
        EditCnnOption = SelectedCnn.Option;
        IsEditingCnn = true;
    }

    [RelayCommand] private void CancelEditCnn() => IsEditingCnn = false;

    [RelayCommand]
    private async Task ConfirmEditCnn()
    {
        if (SelectedCnn is null) return;
        try
        {
            IsBusy = true;
            var updated = await apiClient.UpdateCnnAsync(SelectedCnn.Id, new CnnWriteDto { Subject = EditCnnSubject.Trim(), Option = EditCnnOption });
            var idx = Cnns.ToList().FindIndex(c => c.Id == updated.Id);
            if (idx >= 0) Cnns[idx] = updated;
            SelectedCnn = updated;
            IsEditingCnn = false;
            StatusText = $"Обновлен: {updated.Subject} — Вариант {updated.Option}";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteCnn()
    {
        if (SelectedCnn is null) return;
        try
        {
            IsBusy = true;
            await apiClient.DeleteCnnAsync(SelectedCnn.Id);
            Cnns.Remove(SelectedCnn);
            SelectedCnn = null;
            StatusText = "Вариант удален.";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ========== MATERIALS ==========

    [RelayCommand]
    private void BeginAddMaterial(string kindName)
    {
        if (SelectedCnn is null) { StatusText = "Сначала выберите вариант."; return; }
        if (!Enum.TryParse<MaterialKind>(kindName, true, out var kind)) return;
        IsNewMaterial = true;
        IsEditingMaterial = true;
        EditingMaterialKind = kind;
        EditingMaterial = null;
        MaterialTitle = string.Empty;
        MaterialUrl = string.Empty;
        MaterialSortOrder = 0;
    }

    [RelayCommand]
    private void BeginEditMaterial(CnnMaterialDto? material)
    {
        if (material is null || SelectedCnn is null) return;
        IsNewMaterial = false;
        IsEditingMaterial = true;
        EditingMaterialKind = material.Kind;
        EditingMaterial = material;
        MaterialTitle = material.Title ?? string.Empty;
        MaterialUrl = material.Url;
        MaterialSortOrder = material.SortOrder;
    }

    [RelayCommand] private void CancelEditMaterial() => IsEditingMaterial = false;

    [RelayCommand]
    private async Task PickMaterialFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл материала",
            Filter = "Все файлы|*.*|PDF|*.pdf|Изображения|*.png;*.jpg;*.jpeg"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            IsBusy = true;
            StatusText = "Загрузка файла...";
            var kindStr = EditingMaterialKind switch
            {
                MaterialKind.Kim => "kim",
                MaterialKind.Criteria => "criteria",
                MaterialKind.Blanks => "blanks",
                _ => "other"
            };
            var result = await apiClient.UploadFileAsync(dlg.FileName, kindStr);
            MaterialUrl = result.Url ?? string.Empty;
            if (string.IsNullOrWhiteSpace(MaterialTitle))
                MaterialTitle = result.FileName ?? Path.GetFileNameWithoutExtension(dlg.FileName);
            StatusText = $"Файл загружен: {result.FileName} ({result.SizeBytes / 1024} кБ)";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки файла: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SaveMaterial()
    {
        if (SelectedCnn is null || string.IsNullOrWhiteSpace(MaterialUrl))
        {
            StatusText = "URL материала обязателен.";
            return;
        }
        try
        {
            IsBusy = true;
            var dto = new CnnMaterialWriteDto
            {
                Kind = EditingMaterialKind,
                Title = string.IsNullOrWhiteSpace(MaterialTitle) ? null : MaterialTitle.Trim(),
                Url = MaterialUrl.Trim(),
                SortOrder = MaterialSortOrder
            };
            if (IsNewMaterial)
            {
                var created = await apiClient.CreateMaterialAsync(SelectedCnn.Id, dto);
                GetCollectionForKind(created.Kind).Add(created);
                StatusText = $"Добавлен материал: {KindLabel(created.Kind)}";
            }
            else if (EditingMaterial is not null)
            {
                var updated = await apiClient.UpdateMaterialAsync(SelectedCnn.Id, EditingMaterial.Id, dto);
                GetCollectionForKind(EditingMaterial.Kind).Remove(EditingMaterial);
                GetCollectionForKind(updated.Kind).Add(updated);
                StatusText = "Материал обновлен.";
            }
            IsEditingMaterial = false;
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteMaterialItem(CnnMaterialDto? material)
    {
        if (material is null || SelectedCnn is null) return;
        try
        {
            IsBusy = true;
            await apiClient.DeleteMaterialAsync(SelectedCnn.Id, material.Id);
            GetCollectionForKind(material.Kind).Remove(material);
            StatusText = "Материал удален.";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ========== ORDERS ==========

    [RelayCommand]
    private Task LoadQueue() => LoadQueueInternal(true);

    private async Task LoadQueueInternal(bool restoreSelectionAfter)
    {
        var keepId = restoreSelectionAfter ? SelectedQueueOrder?.Id : null;
        try
        {
            IsBusy = true;
            QueueOrders.Clear();
            var filter = ParseQueueFilter(QueueStatusFilter);
            var list = await apiClient.GetQueueAsync(filter);
            foreach (var item in list) QueueOrders.Add(item);
            StatusText = $"Заказов в очереди: {QueueOrders.Count}";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            if (restoreSelectionAfter)
                TryReselectOrderByIdAfterListReload(keepId);
        }
    }

    [RelayCommand]
    private Task LoadMyChecking() => LoadMyCheckingInternal(true);

    private async Task LoadMyCheckingInternal(bool restoreSelectionAfter)
    {
        var keepId = restoreSelectionAfter ? SelectedQueueOrder?.Id : null;
        try
        {
            IsBusy = true;
            MyCheckingOrders.Clear();
            var page = await apiClient.GetOrdersPagedAsync(
                cnnId: null, studentId: null, expertId: null,
                status: OrderAnswerStatus.Checking,
                from: ExpertMyOrdersFrom, to: ExpertMyOrdersTo,
                page: 1, pageSize: 50);
            foreach (var item in page.Items) MyCheckingOrders.Add(item);
            StatusText = IsAdmin
                ? $"Заказы «На проверке»: {MyCheckingOrders.Count} из {page.TotalCount}."
                : $"Мои заказы в работе: {MyCheckingOrders.Count} из {page.TotalCount}.";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки «Мои в работе»: {ex.Message}"; }
        finally
        {
            IsBusy = false;
            if (restoreSelectionAfter)
                TryReselectOrderByIdAfterListReload(keepId);
        }
    }

    /// <summary>После Clear/перезагрузки списков восстановить выбранный заказ по Id (новый экземпляр DTO из API).</summary>
    private void TryReselectOrderByIdAfterListReload(int? orderId)
    {
        if (orderId is null)
            return;

        var found = QueueOrders.FirstOrDefault(o => o.Id == orderId)
                    ?? MyCheckingOrders.FirstOrDefault(o => o.Id == orderId);
        if (found is null)
            return;

        _suppressSelectedOrderChanged = true;
        try
        {
            SelectedQueueOrder = found;
        }
        finally
        {
            _suppressSelectedOrderChanged = false;
        }

        _ = TryLoadOrderSubmissionPreviewAsync(found);
    }

    /// <summary>Почему нельзя взять заказ; <c>null</c> — можно (или заказ не выбран — смотрите <see cref="CanClaimSelectedOrder"/>).</summary>
    private string? ExplainWhyCannotClaimOrder()
    {
        if (SelectedQueueOrder is not { } o)
            return null;

        if (!IsAdminOrExpert)
            return "Войдите под ролью Expert или Admin. Роль Student в этом приложении не может брать заказы.";

        if (o.Status != OrderAnswerStatus.QueueForCheck)
            return
                $"Статус заказа: «{FormatOrderStatus(o.Status)}». Взять можно только заказ «В очереди на проверку» — ученик должен отправить его в очередь (вкладка «Проверки»).";

        if (o.ExpertId.HasValue)
            return "У заказа уже назначен эксперт.";

        if (CurrentUserId.HasValue && o.UserId == CurrentUserId.Value)
            return
                "Это заказ с вашего же аккаунта (UserId совпадает с Id из токена). Такой заказ нельзя взять в работу — войдите другим экспертом или создайте заказ с другого ученика.";

        return null;
    }

    [RelayCommand]
    private async Task ClaimSelectedOrder()
    {
        if (SelectedQueueOrder is null) { StatusText = "Выберите заказ."; return; }
        var block = ExplainWhyCannotClaimOrder();
        if (block is not null) { StatusText = block; return; }
        try
        {
            IsBusy = true;
            var updated = await apiClient.ClaimOrderAsync(SelectedQueueOrder.Id);
            _suppressSelectedOrderChanged = true;
            try
            {
                ReplaceOrderInLists(updated);
                SelectedQueueOrder = updated;
            }
            finally
            {
                _suppressSelectedOrderChanged = false;
            }

            await LoadQueueInternal(false);
            await LoadMyCheckingInternal(false);
            TryReselectOrderByIdAfterListReload(updated.Id);
            StatusText = $"Заказ #{updated.Id} взят в работу.";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BeginRejectOrder()
    {
        if (!CanRejectSelectedOrder)
        {
            StatusText = "Отклонение недоступно для этого заказа.";
            return;
        }

        IsRejectingOrder = true;
        RejectionReason = string.Empty;
    }

    [RelayCommand] private void CancelRejectOrder() => IsRejectingOrder = false;

    [RelayCommand]
    private async Task ConfirmRejectOrder()
    {
        if (SelectedQueueOrder is null || string.IsNullOrWhiteSpace(RejectionReason))
        {
            StatusText = "Укажите причину отклонения.";
            return;
        }

        var reason = RejectionReason.Trim();
        if (reason.Length > 1024)
        {
            StatusText = "Причина отклонения не длиннее 1024 символов.";
            return;
        }

        if (!CanRejectSelectedOrder)
        {
            StatusText = "Отклонение доступно администратору или назначенному эксперту в статусе «На проверке».";
            return;
        }

        try
        {
            IsBusy = true;
            await apiClient.RejectOrderAsync(SelectedQueueOrder.Id, reason);
            await LoadQueueInternal(false);
            await LoadMyCheckingInternal(false);
            SelectedQueueOrder = null;
            IsRejectingOrder = false;
            StatusText = "Заказ отклонён экспертом.";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ========== REVIEW ==========

    [RelayCommand]
    private void BeginEditReview()
    {
        if (!CanSubmitNewReview && !CanEditExistingReview)
        {
            StatusText = "Редактирование проверки сейчас недоступно.";
            return;
        }

        IsEditingReview = true;
        if (CurrentReview is not null)
        {
            ReviewTotalScore = CurrentReview.TotalScore;
            ReviewGeneralComment = CurrentReview.GeneralComment ?? string.Empty;
            ReviewCriteria.Clear();
            foreach (var c in CurrentReview.Criteria) ReviewCriteria.Add(c);
        }
        else
        {
            ReviewTotalScore = 0;
            ReviewGeneralComment = string.Empty;
            ReviewCriteria.Clear();
        }
    }

    [RelayCommand] private void CancelEditReview() => IsEditingReview = false;

    [RelayCommand]
    private void AddReviewCriterion() =>
        ReviewCriteria.Add(new ReviewCriterionDto { TaskNumber = 1, CriterionCode = "K1", Score = 0 });

    [RelayCommand]
    private void RemoveReviewCriterion(ReviewCriterionDto? c)
    {
        if (c is not null) ReviewCriteria.Remove(c);
    }

    [RelayCommand]
    private async Task SaveReview()
    {
        if (SelectedQueueOrder is null) return;
        if (ReviewTotalScore < 0)
        {
            StatusText = "Итоговый балл не может быть отрицательным.";
            return;
        }

        var general = string.IsNullOrWhiteSpace(ReviewGeneralComment) ? null : ReviewGeneralComment.Trim();
        if (general is { Length: > 2048 })
        {
            StatusText = "Общий комментарий не длиннее 2048 символов.";
            return;
        }

        foreach (var c in ReviewCriteria)
        {
            if (c.TaskNumber < 1)
            {
                StatusText = "Номер задания в критерии должен быть не меньше 1.";
                return;
            }

            if (string.IsNullOrWhiteSpace(c.CriterionCode) || c.CriterionCode.Trim().Length > 32)
            {
                StatusText = "Код критерия обязателен, не длиннее 32 символов.";
                return;
            }

            if (c.Score < 0)
            {
                StatusText = "Баллы по критерию не могут быть отрицательными.";
                return;
            }

            if (c.Comment is { Length: > 1024 })
            {
                StatusText = "Комментарий к критерию не длиннее 1024 символов.";
                return;
            }
        }

        try
        {
            IsBusy = true;
            var dto = new ReviewWriteDto
            {
                TotalScore = ReviewTotalScore,
                GeneralComment = general,
                Criteria = [.. ReviewCriteria.Select(c => new ReviewCriterionDto
                {
                    TaskNumber = c.TaskNumber,
                    CriterionCode = c.CriterionCode.Trim(),
                    Score = c.Score,
                    Comment = string.IsNullOrWhiteSpace(c.Comment) ? null : c.Comment.Trim()
                })]
            };
            var orderId = SelectedQueueOrder.Id;
            ReviewReadDto saved;
            if (CurrentReview is null)
                saved = await apiClient.CreateReviewAsync(orderId, dto);
            else
                saved = await apiClient.UpdateReviewAsync(orderId, dto);

            CurrentReview = saved;
            IsEditingReview = false;
            StatusText = $"Проверка сохранена. Итог: {saved.TotalScore} баллов.";

            try
            {
                var fresh = await apiClient.GetOrderByIdAsync(orderId);
                _suppressSelectedOrderChanged = true;
                try
                {
                    ReplaceOrderInLists(fresh);
                    if (SelectedQueueOrder?.Id == orderId)
                        SelectedQueueOrder = fresh;
                }
                finally
                {
                    _suppressSelectedOrderChanged = false;
                }
            }
            catch { /* карточка уже актуальна по Review */ }

            await LoadQueueInternal(false);
            await LoadMyCheckingInternal(false);
            TryReselectOrderByIdAfterListReload(orderId);
            RaiseExpertOrderFlags();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (msg.Contains("409", StringComparison.Ordinal) ||
                msg.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
                msg.Contains("Review already", StringComparison.OrdinalIgnoreCase))
            {
                var existing = await apiClient.GetReviewAsync(SelectedQueueOrder.Id);
                if (existing is not null)
                {
                    CurrentReview = existing;
                    ReviewTotalScore = existing.TotalScore;
                    ReviewGeneralComment = existing.GeneralComment ?? string.Empty;
                    ReviewCriteria.Clear();
                    foreach (var c in existing.Criteria) ReviewCriteria.Add(c);
                    IsEditingReview = true;
                    StatusText = "Отзыв уже существует на сервере — загружена копия. Сохраните снова для обновления (PUT).";
                }
                else
                    StatusText = $"Ошибка: {msg}";
            }
            else
                StatusText = $"Ошибка: {msg}";
        }
        finally { IsBusy = false; }
    }

    // ========== USERS & EXPERT MANAGEMENT ==========

    [RelayCommand]
    private async Task LoadUsers()
    {
        var keepId = SelectedUser?.Id;
        try
        {
            IsBusy = true;
            Users.Clear();
            var result = await apiClient.GetUsersAsync(
                role: string.IsNullOrWhiteSpace(UserRoleFilter) ? null : UserRoleFilter.Trim(),
                search: string.IsNullOrWhiteSpace(UserSearch) ? null : UserSearch.Trim(),
                pageSize: 50);
            foreach (var u in result.Items) Users.Add(u);
            if (keepId.HasValue)
                SelectedUser = Users.FirstOrDefault(u => u.Id == keepId.Value);
            StatusText = "Готово.";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки пользователей: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private bool CanPromoteToExpert() =>
        SelectedUser is not null && !IsUserRoleExpertOrAdmin(SelectedUser.Role);

    [RelayCommand(CanExecute = nameof(CanPromoteToExpert))]
    private async Task PromoteToExpert()
    {
        if (SelectedUser is null || !CanPromoteToExpert()) return;
        var name = AdminEditUserName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText = "Укажите отображаемое имя (ФИО) перед назначением роли Expert.";
            return;
        }

        try
        {
            IsBusy = true;
            await apiClient.UpdateUserAsync(SelectedUser.Id, new UserUpdateDto
            {
                Name = name,
                Role = "Expert",
                Email = string.IsNullOrWhiteSpace(AdminEditUserEmail) ? null : AdminEditUserEmail.Trim()
            });
            StatusText = $"Роль «Expert» назначена пользователю {name}.";
            await LoadUsers();
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private bool CanDemoteExpertToStudent() =>
        SelectedUser is not null
        && IsUserRoleExpert(SelectedUser.Role)
        && SelectedUser.Id != CurrentUserId;

    [RelayCommand(CanExecute = nameof(CanDemoteExpertToStudent))]
    private async Task DemoteExpertToStudent()
    {
        if (SelectedUser is null || !CanDemoteExpertToStudent()) return;
        var name = AdminEditUserName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText = "Укажите отображаемое имя (ФИО) перед сменой роли.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Понизить пользователя «{name}» с роли Expert до Student?\n\nПрофиль ExpertInfo на сервере может остаться — при необходимости обработайте это отдельно.",
            "Подтверждение",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            await apiClient.UpdateUserAsync(SelectedUser.Id, new UserUpdateDto
            {
                Name = name,
                Role = "Student",
                Email = string.IsNullOrWhiteSpace(AdminEditUserEmail) ? null : AdminEditUserEmail.Trim()
            });
            StatusText = $"Пользователь «{name}» переведён в роль Student.";
            await LoadUsers();
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private bool CanSaveAdminUserEdits() => SelectedUser is not null;

    [RelayCommand(CanExecute = nameof(CanSaveAdminUserEdits))]
    private async Task SaveAdminUserEdits()
    {
        if (SelectedUser is null) return;
        var name = AdminEditUserName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText = "Имя (ФИО) не может быть пустым.";
            return;
        }

        var email = AdminEditUserEmail.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusText = "Укажите email пользователя.";
            return;
        }

        if (!string.IsNullOrEmpty(AdminEditUserPassword) && AdminEditUserPassword.Length < AdminUserPasswordMinLength)
        {
            StatusText = $"Новый пароль не короче {AdminUserPasswordMinLength} символов или оставьте поле пустым.";
            return;
        }

        try
        {
            IsBusy = true;
            var dto = new UserUpdateDto
            {
                Name = name,
                Role = null,
                Email = email,
                Password = string.IsNullOrWhiteSpace(AdminEditUserPassword) ? null : AdminEditUserPassword
            };
            await apiClient.UpdateUserAsync(SelectedUser.Id, dto);
            AdminEditUserPassword = string.Empty;
            StatusText = "Данные пользователя сохранены.";
            await LoadUsers();
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private bool CanCreateExpertInfo() =>
        SelectedUser is not null && IsUserRoleExpert(SelectedUser.Role);

    [RelayCommand(CanExecute = nameof(CanCreateExpertInfo))]
    private async Task CreateExpertInfo()
    {
        if (SelectedUser is null) return;
        try
        {
            IsBusy = true;
            var info = await apiClient.CreateExpertInfoAsync(new ExpertInfoCreateDto
            {
                UserId = SelectedUser.Id,
                InitialBalance = ExpertInfoBalance
            });
            StatusText = $"Профиль эксперта создан. Баланс: {info.Balance}";
            await LoadUsers();
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private const int AdminUserPasswordMinLength = 8;

    private static bool IsUserRoleExpert(string? role) =>
        string.Equals(role?.Trim(), "Expert", StringComparison.OrdinalIgnoreCase);

    private static bool IsUserRoleExpertOrAdmin(string? role)
    {
        var r = role?.Trim();
        return string.Equals(r, "Expert", StringComparison.OrdinalIgnoreCase)
               || string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    // ========== TOOLS ==========

    [RelayCommand]
    private async Task ValidatePayload()
    {
        try
        {
            IsBusy = true;
            var valid = await apiClient.ValidateAnswerPayloadAsync(PayloadToValidate);
            StatusText = valid ? "Payload валиден." : "Payload не прошел валидацию.";
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [ObservableProperty] private string payloadToValidate = "{\n  \"Meta\": {},\n  \"Auto_Part\": [],\n  \"Exp_Part\": []\n}";

    [RelayCommand]
    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* browser not available */ }
    }

    // ========== HELPERS ==========

    private async Task LoadMaterialsForSelectedCnn()
    {
        if (SelectedCnn is null) return;
        try
        {
            var details = await apiClient.GetCnnDetailsAsync(SelectedCnn.Id);
            ClearMaterialCollections();
            foreach (var m in details.Materials.OrderBy(m => m.SortOrder))
                GetCollectionForKind(m.Kind).Add(m);
        }
        catch (Exception ex) { StatusText = $"Не удалось загрузить материалы: {ex.Message}"; }
    }

    private async Task LoadOrderCnnDetails(int cnnId)
    {
        try
        {
            var details = await apiClient.GetCnnDetailsAsync(cnnId);
            OrderKimMaterials.Clear();
            OrderCriteriaMaterials.Clear();
            foreach (var m in details.Materials)
            {
                if (m.Kind == MaterialKind.Kim) OrderKimMaterials.Add(m);
                else if (m.Kind == MaterialKind.Criteria) OrderCriteriaMaterials.Add(m);
            }
        }
        catch { /* non-critical */ }
    }

    private async Task LoadReview(int orderAnswerId)
    {
        CurrentReview = await apiClient.GetReviewAsync(orderAnswerId);
        ReviewCriteria.Clear();
        if (CurrentReview is not null)
            foreach (var c in CurrentReview.Criteria) ReviewCriteria.Add(c);
    }

    private ObservableCollection<CnnMaterialDto> GetCollectionForKind(MaterialKind kind) => kind switch
    {
        MaterialKind.Kim => KimMaterials,
        MaterialKind.Criteria => CriteriaMaterials,
        MaterialKind.Blanks => BlanksMaterials,
        _ => OtherMaterials
    };

    private void ClearMaterialCollections()
    {
        KimMaterials.Clear();
        CriteriaMaterials.Clear();
        BlanksMaterials.Clear();
        OtherMaterials.Clear();
    }

    private void ReplaceOrderInLists(OrderAnswerReadDto updated)
    {
        var qIdx = QueueOrders.ToList().FindIndex(x => x.Id == updated.Id);
        if (qIdx >= 0) QueueOrders[qIdx] = updated;
        var mIdx = MyCheckingOrders.ToList().FindIndex(x => x.Id == updated.Id);
        if (mIdx >= 0) MyCheckingOrders[mIdx] = updated;
    }

    private async Task HydrateSelectedOrderFromServerAsync(OrderAnswerReadDto selected)
    {
        OrderAnswerReadDto order;
        try
        {
            order = await apiClient.GetOrderByIdAsync(selected.Id);
            // Сначала подменяем элемент в списке и SelectedQueueOrder под одним suppress, иначе ListBox по SelectedItem сбрасывает выбор (старая ссылка исчезает из ItemsSource).
            _suppressSelectedOrderChanged = true;
            try
            {
                ReplaceOrderInLists(order);
                SelectedQueueOrder = order;
            }
            finally
            {
                _suppressSelectedOrderChanged = false;
            }
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            // Некоторые API отдают заказ в списке очереди, но не по GET /{id}; используем уже загруженный DTO.
            order = selected;
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось обновить заказ #{selected.Id}: {ex.Message}";
            return;
        }

        UpdateAnswerUrl = order.AnswerUrl ?? string.Empty;
        await LoadOrderCnnDetails(order.CnnId);
        if (IsAdminOrExpert)
            await LoadReview(order.Id);
        OnPropertyChanged(nameof(SelectedOrderStatusText));
        RaiseExpertOrderFlags();
        await TryLoadOrderSubmissionPreviewAsync(order);
    }

    private void ClearOrderSubmissionPreview()
    {
        OrderSubmissionDocument = null;
        OrderSubmissionBlankJsonUrl = null;
        OrderSubmissionTemplatePageIndex = 0;
        OrderSubmissionPageImage = null;
        OrderExpertSheet2Pages.Clear();
        OrderExpPartRows.Clear();
        SelectedOrderExpertSheet2Page = null;
        OrderSubmissionCurrentZones.Clear();
        _orderSubmissionAnswerSink = null;
        OrderPreviewZoom = 1.0;
        OrderSubmissionLoadError = null;
        HasOrderExpertSheet2Pages = false;
        HasOrderExpPartRows = false;
        ShowOrderSubmissionNoSheet2Hint = false;
        OnPropertyChanged(nameof(OrderSubmissionAnswerSink));
    }

    private async Task TryLoadOrderSubmissionPreviewAsync(OrderAnswerReadDto order)
    {
        ClearOrderSubmissionPreview();
        if (string.IsNullOrWhiteSpace(order.AnswerUrl))
            return;

        try
        {
            string text;
            try
            {
                text = await apiClient.DownloadTextAsync(order.AnswerUrl.Trim());
            }
            catch (Exception ex)
            {
                OrderSubmissionLoadError = $"Не удалось загрузить JSON ответа: {ex.Message}";
                StatusText = OrderSubmissionLoadError;
                return;
            }

            ExamSubmissionDocument? doc;
            try
            {
                doc = ExamSubmissionDocument.Deserialize(text);
            }
            catch (Exception ex)
            {
                OrderSubmissionLoadError = $"Ответ не является ожидаемым JSON пакета: {ex.Message}";
                StatusText = OrderSubmissionLoadError;
                return;
            }

            if (doc?.Template is not { Pages.Count: > 0 })
            {
                OrderSubmissionLoadError = "В пакете нет шаблона бланка (template.pages пуст).";
                StatusText = OrderSubmissionLoadError;
                return;
            }

            _orderSubmissionAnswerSink = new DictionaryAnswerSink(
                new Dictionary<string, string>(doc.Answers, StringComparer.Ordinal));
            OnPropertyChanged(nameof(OrderSubmissionAnswerSink));

            OrderSubmissionDocument = doc;

            OrderExpPartRows.Clear();
            if (doc.AnswerPayload?.ExpPart is { Count: > 0 } expPart)
            {
                foreach (var row in expPart)
                    OrderExpPartRows.Add(row);
            }

            HasOrderExpPartRows = OrderExpPartRows.Count > 0;

            OrderExpertSheet2Pages.Clear();
            for (var i = 0; i < doc.Template.Pages.Count; i++)
            {
                var p = doc.Template.Pages[i];
                if (p.BlankType == BlankType.AnswerSheet2)
                    OrderExpertSheet2Pages.Add(new OrderExpertSheet2PageItem(p, i));
            }

            HasOrderExpertSheet2Pages = OrderExpertSheet2Pages.Count > 0;
            ShowOrderSubmissionNoSheet2Hint = doc.Template.Pages.Count > 0 && !HasOrderExpertSheet2Pages;

            try
            {
                var details = await apiClient.GetCnnDetailsAsync(order.CnnId);
                var blankMat = details.Materials.FirstOrDefault(m =>
                    m.Kind == MaterialKind.Blanks
                    && string.Equals(m.Title?.Trim(), BlankTemplateSyncService.RemoteMaterialTitle,
                        StringComparison.OrdinalIgnoreCase));
                OrderSubmissionBlankJsonUrl = blankMat?.Url?.Trim();
            }
            catch
            {
                OrderSubmissionBlankJsonUrl = null;
            }

            SelectedOrderExpertSheet2Page = OrderExpertSheet2Pages.Count > 0 ? OrderExpertSheet2Pages[0] : null;
        }
        catch (Exception ex)
        {
            ClearOrderSubmissionPreview();
            OrderSubmissionLoadError = $"Ошибка при разборе пакета: {ex.Message}";
            StatusText = OrderSubmissionLoadError;
        }
    }

    private async Task LoadOrderSubmissionPageAsync()
    {
        OrderSubmissionPageImage = null;
        OrderSubmissionCurrentZones.Clear();
        if (SelectedOrderExpertSheet2Page is not { } slot)
        {
            OrderSubmissionTemplatePageIndex = 0;
            return;
        }

        var page = slot.Page;
        OrderSubmissionTemplatePageIndex = slot.TemplatePageIndex;
        foreach (var z in page.Zones)
            OrderSubmissionCurrentZones.Add(z);

        var resolved = TemplateMediaResolver.Resolve(page.ImagePath ?? string.Empty, OrderSubmissionBlankJsonUrl);
        if (string.IsNullOrWhiteSpace(resolved))
            return;

        try
        {
            if (Uri.TryCreate(resolved, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var bytes = await apiClient.DownloadBytesAsync(resolved);
                OrderSubmissionPageImage = BitmapImageFromBytes(bytes);
                return;
            }

            if (File.Exists(resolved))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(resolved, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                OrderSubmissionPageImage = bmp;
            }
        }
        catch
        {
            /* ignore */
        }
    }

    private static BitmapImage BitmapImageFromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>Имя в JWT часто устаревает или отсутствует — подтягиваем актуальное с API.</summary>
    private async Task TryHydrateProfileFromUsersApiAsync()
    {
        if (!CurrentUserId.HasValue || !IsAuthenticated) return;
        try
        {
            var user = await apiClient.GetUserAsync(CurrentUserId.Value);
            if (!string.IsNullOrWhiteSpace(user.Name))
                UserName = user.Name.Trim();
            if (string.IsNullOrWhiteSpace(CurrentUserEmail) && !string.IsNullOrWhiteSpace(user.Email))
                CurrentUserEmail = user.Email.Trim();
        }
        catch (Exception ex)
        {
            if (string.Equals(UserName.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase))
                StatusText = "Имя не загружено с сервера (GET профиля). " + ex.Message;
        }

        await TryHydrateExpertBalanceAsync(clearFirst: true);
    }

    /// <summary>GET api/ExpertInfos/{userId} — только для роли Expert, свой id.</summary>
    /// <param name="clearFirst">После входа — сбросить баланс до загрузки; при обновлении из меню — оставить старое значение на время запроса.</param>
    private async Task TryHydrateExpertBalanceAsync(bool clearFirst = true)
    {
        if (clearFirst)
        {
            ExpertAccountBalance = null;
            OnPropertyChanged(nameof(ExpertBalanceSubtitle));
        }

        if (!CurrentUserId.HasValue || !IsExpert) return;
        try
        {
            var info = await apiClient.GetExpertInfoAsync(CurrentUserId.Value);
            ExpertAccountBalance = info.Balance;
        }
        catch
        {
            if (clearFirst)
                ExpertAccountBalance = null;
        }

        OnPropertyChanged(nameof(ExpertBalanceSubtitle));
    }

    private void ApplyTokenContext(string accessToken)
    {
        var payload = ReadJwtPayload(accessToken);
        UserName = GetClaimValue(payload,
            "name", "unique_name", "given_name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/givenname") ?? "Unknown";
        Role = GetClaimValue(payload,
            "role", "roles",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? string.Empty;

        if (string.Equals(Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            tokenStore.Clear();
            IsAuthenticated = false;
            CurrentUserId = null;
            CurrentUserEmail = string.Empty;
            StatusText = "Вход под ролью Student не поддерживается. Свяжитесь с поддержкой.";
            return;
        }
        if (!string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Role, "Expert", StringComparison.OrdinalIgnoreCase))
        {
            tokenStore.Clear();
            IsAuthenticated = false;
            CurrentUserId = null;
            CurrentUserEmail = string.Empty;
            StatusText = $"Роль «{Role}» не поддерживается.";
            return;
        }

        CurrentUserEmail = GetClaimValue(payload,
            "email",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
            "preferred_username") ?? string.Empty;

        CurrentUserId = TryParseUserIdClaim(payload);
        IsAuthenticated = true;
    }

    private static Dictionary<string, object?> ReadJwtPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) return new();
        var segment = parts[1].Replace('-', '+').Replace('_', '/');
        while (segment.Length % 4 != 0) segment += "=";
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(segment));
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
    }

    private static string? GetClaimValue(Dictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value is null) continue;
            if (value is JsonElement el)
            {
                if (el.ValueKind == JsonValueKind.Array && el.GetArrayLength() > 0 && el[0].ValueKind == JsonValueKind.String)
                    return el[0].GetString();
                if (el.ValueKind == JsonValueKind.String) return el.GetString();
                return el.ToString();
            }
            return value.ToString();
        }
        return null;
    }

    private static int? TryParseUserIdClaim(Dictionary<string, object?> payload)
    {
        var raw = GetClaimValue(payload,
            "userId",
            "UserId",
            "userid",
            "uid",
            "nameid",
            "sub",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/userdata");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (int.TryParse(raw, out var id))
            return id;
        // sub часто GUID — если в токене есть отдельный числовой claim выше, он уже сработал
        return null;
    }

    private static string FormatOrderStatus(OrderAnswerStatus s) => OrderAnswerStatusLabels.Russian(s);

    private OrderAnswerStatus? ParseQueueFilter(string raw) =>
        string.Equals(raw, QueueFilterServerDefault, StringComparison.Ordinal)
            ? null
            : ParseStatus(raw);

    private static OrderAnswerStatus? ParseStatus(string raw) =>
        Enum.TryParse<OrderAnswerStatus>(raw, true, out var p) ? p : null;

    private static bool StatusTextIndicatesError(string? t)
    {
        if (string.IsNullOrWhiteSpace(t)) return false;
        return t.Contains("Ошибка", StringComparison.OrdinalIgnoreCase)
               || t.Contains("Не удалось", StringComparison.OrdinalIgnoreCase)
               || t.Contains("не восстановлена", StringComparison.OrdinalIgnoreCase)
               || t.Contains("не загружено", StringComparison.OrdinalIgnoreCase)
               || t.Contains("недоступен", StringComparison.OrdinalIgnoreCase);
    }
}
