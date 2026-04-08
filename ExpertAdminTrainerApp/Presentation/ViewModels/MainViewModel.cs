using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpertAdminTrainerApp.Domain;
using ExpertAdminTrainerApp.Services;
using Microsoft.Win32;

namespace ExpertAdminTrainerApp.Presentation.ViewModels;

public partial class MainViewModel(IApiClient apiClient, ITokenStore tokenStore) : ObservableObject
{
    private bool _suppressSelectedOrderChanged;
    // ===== Auth =====
    [ObservableProperty] private string email = string.Empty;
    [ObservableProperty] private string password = string.Empty;
    [ObservableProperty] private string userName = string.Empty;
    [ObservableProperty] private string role = string.Empty;
    [ObservableProperty] private int? currentUserId;
    [ObservableProperty] private string statusText = "Введите логин и пароль.";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isAuthenticated;

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
    public bool HasReview => CurrentReview is not null;
    public bool HasNoReview => CurrentReview is null;
    public string EditingMaterialKindLabel => KindLabel(EditingMaterialKind);

    public bool CanClaimSelectedOrder =>
        IsExpert && SelectedQueueOrder is { } o &&
        o.Status == OrderAnswerStatus.QueueForCheck &&
        !o.ExpertId.HasValue &&
        CurrentUserId.HasValue &&
        o.UserId != CurrentUserId.Value;

    public bool CanRejectSelectedOrder =>
        IsExpert && SelectedQueueOrder is { } o &&
        o.Status == OrderAnswerStatus.Checking &&
        CurrentUserId.HasValue &&
        o.ExpertId == CurrentUserId.Value;

    /// <summary>Новая проверка (POST) в статусе Checking.</summary>
    public bool CanSubmitNewReview =>
        IsExpert && SelectedQueueOrder is { Status: OrderAnswerStatus.Checking } &&
        CurrentUserId.HasValue && SelectedQueueOrder!.ExpertId == CurrentUserId.Value;

    public bool CanEditExistingReview =>
        IsExpert && SelectedQueueOrder is { Status: OrderAnswerStatus.Checked } && HasReview;

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
    partial void OnRoleChanged(string value) => RaiseAll();
    partial void OnEditingMaterialKindChanged(MaterialKind value) => OnPropertyChanged(nameof(EditingMaterialKindLabel));

    partial void OnCurrentReviewChanged(ReviewReadDto? value)
    {
        OnPropertyChanged(nameof(HasReview));
        OnPropertyChanged(nameof(HasNoReview));
        RaiseExpertOrderFlags();
    }

    partial void OnCurrentUserIdChanged(int? value) => RaiseExpertOrderFlags();

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
            RaiseExpertOrderFlags();
            return;
        }

        if (_suppressSelectedOrderChanged)
        {
            UpdateAnswerUrl = value.AnswerUrl ?? string.Empty;
            RejectionReason = string.Empty;
            IsRejectingOrder = false;
            _ = LoadOrderCnnDetails(value.CnnId);
            if (IsExpert) _ = LoadReview(value.Id);
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
        if (IsExpert) _ = LoadReview(value.Id);
        _ = HydrateSelectedOrderFromServerAsync(value.Id);
    }

    partial void OnSelectedUserChanged(UserListItemDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(HasNoSelectedUser));
        if (value is not null) ExpertInfoBalance = value.Balance ?? 0;
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(IsExpert));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsAdminOrExpert));
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

    [RelayCommand]
    private async Task Login()
    {
        try
        {
            IsBusy = true;
            var auth = await apiClient.LoginAsync(new LoginDto { Email = Email.Trim(), Password = Password });
            ApplyTokenContext(auth.AccessToken);
            StatusText = $"Вход выполнен: {UserName} ({Role})";
            await PostLoginLoad();
        }
        catch (Exception ex) { StatusText = $"Ошибка входа: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Logout()
    {
        tokenStore.Clear();
        IsAuthenticated = false;
        UserName = Role = string.Empty;
        CurrentUserId = null;
        QueueOrders.Clear();
        MyCheckingOrders.Clear();
        Cnns.Clear();
        Users.Clear();
        ClearMaterialCollections();
        StatusText = "Вы вышли из системы.";
    }

    [RelayCommand]
    private async Task RestoreSession()
    {
        if (string.IsNullOrWhiteSpace(tokenStore.AccessToken)) return;
        try
        {
            ApplyTokenContext(tokenStore.AccessToken);
            await apiClient.GetMineAsync();
            StatusText = $"Сессия восстановлена: {UserName} ({Role})";
            await PostLoginLoad();
        }
        catch
        {
            tokenStore.Clear();
            IsAuthenticated = false;
        }
    }

    private async Task PostLoginLoad()
    {
        if (IsAdmin)
        {
            await LoadCnns();
            await LoadUsers();
        }
        if (IsExpert)
        {
            await LoadQueue();
            await LoadMyChecking();
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
    private async Task LoadQueue()
    {
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
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadMyChecking()
    {
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
            StatusText = $"Мои проверки (Checking): {MyCheckingOrders.Count} из {page.TotalCount}.";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки «моих проверок»: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ClaimSelectedOrder()
    {
        if (SelectedQueueOrder is null) { StatusText = "Выберите заказ."; return; }
        if (!CanClaimSelectedOrder) { StatusText = "Нельзя взять этот заказ (условия claim не выполнены)."; return; }
        try
        {
            IsBusy = true;
            var updated = await apiClient.ClaimOrderAsync(SelectedQueueOrder.Id);
            ReplaceOrderInLists(updated);
            _suppressSelectedOrderChanged = true;
            try
            {
                SelectedQueueOrder = updated;
            }
            finally
            {
                _suppressSelectedOrderChanged = false;
            }

            await LoadQueue();
            await LoadMyChecking();
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
            StatusText = "Отклонение доступно только назначенному эксперту в статусе «На проверке».";
            return;
        }

        try
        {
            IsBusy = true;
            await apiClient.RejectOrderAsync(SelectedQueueOrder.Id, reason);
            await LoadQueue();
            await LoadMyChecking();
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
                ReplaceOrderInLists(fresh);
                _suppressSelectedOrderChanged = true;
                try
                {
                    if (SelectedQueueOrder?.Id == orderId)
                        SelectedQueueOrder = fresh;
                }
                finally
                {
                    _suppressSelectedOrderChanged = false;
                }
            }
            catch { /* карточка уже актуальна по Review */ }

            await LoadQueue();
            await LoadMyChecking();
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
        try
        {
            IsBusy = true;
            Users.Clear();
            var result = await apiClient.GetUsersAsync(
                role: string.IsNullOrWhiteSpace(UserRoleFilter) ? null : UserRoleFilter.Trim(),
                search: string.IsNullOrWhiteSpace(UserSearch) ? null : UserSearch.Trim(),
                pageSize: 50);
            foreach (var u in result.Items) Users.Add(u);
            StatusText = $"Пользователей: {result.TotalCount} (показано {result.Items.Count})";
        }
        catch (Exception ex) { StatusText = $"Ошибка загрузки пользователей: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PromoteToExpert()
    {
        if (SelectedUser is null) return;
        try
        {
            IsBusy = true;
            await apiClient.UpdateUserAsync(SelectedUser.Id, new UserUpdateDto
            {
                Name = SelectedUser.Name ?? string.Empty,
                Role = "Expert"
            });
            StatusText = $"Роль «Expert» назначена пользователю {SelectedUser.Name}.";
            await LoadUsers();
        }
        catch (Exception ex) { StatusText = $"Ошибка: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
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

    private async Task HydrateSelectedOrderFromServerAsync(int id)
    {
        try
        {
            var fresh = await apiClient.GetOrderByIdAsync(id);
            ReplaceOrderInLists(fresh);
            _suppressSelectedOrderChanged = true;
            try
            {
                if (SelectedQueueOrder?.Id == id)
                    SelectedQueueOrder = fresh;
            }
            finally
            {
                _suppressSelectedOrderChanged = false;
            }

            UpdateAnswerUrl = fresh.AnswerUrl ?? string.Empty;
            await LoadOrderCnnDetails(fresh.CnnId);
            if (IsExpert)
                await LoadReview(fresh.Id);
            OnPropertyChanged(nameof(SelectedOrderStatusText));
            RaiseExpertOrderFlags();
        }
        catch (Exception ex)
        {
            StatusText = $"Не удалось обновить заказ #{id}: {ex.Message}";
        }
    }

    private void ApplyTokenContext(string accessToken)
    {
        var payload = ReadJwtPayload(accessToken);
        UserName = GetClaimValue(payload,
            "name", "unique_name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress") ?? "Unknown";
        Role = GetClaimValue(payload,
            "role", "roles",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role") ?? string.Empty;

        if (string.Equals(Role, "Student", StringComparison.OrdinalIgnoreCase))
        {
            tokenStore.Clear();
            IsAuthenticated = false;
            CurrentUserId = null;
            StatusText = "Вход под ролью Student не поддерживается. Свяжитесь с поддержкой.";
            return;
        }
        if (!string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Role, "Expert", StringComparison.OrdinalIgnoreCase))
        {
            tokenStore.Clear();
            IsAuthenticated = false;
            CurrentUserId = null;
            StatusText = $"Роль «{Role}» не поддерживается.";
            return;
        }

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
            "sub",
            "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/userdata",
            "nameid");
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return int.TryParse(raw.Trim(), out var id) ? id : null;
    }

    private static string FormatOrderStatus(OrderAnswerStatus s) => s switch
    {
        OrderAnswerStatus.NoCheck => "Без проверки",
        OrderAnswerStatus.PaymentInProgress => "Оплата",
        OrderAnswerStatus.QueueForCheck => "В очереди на проверку",
        OrderAnswerStatus.Checking => "На проверке",
        OrderAnswerStatus.Checked => "Проверено",
        OrderAnswerStatus.RejectedByExpert => "Отклонено экспертом",
        _ => s.ToString()
    };

    private OrderAnswerStatus? ParseQueueFilter(string raw) =>
        string.Equals(raw, QueueFilterServerDefault, StringComparison.Ordinal)
            ? null
            : ParseStatus(raw);

    private static OrderAnswerStatus? ParseStatus(string raw) =>
        Enum.TryParse<OrderAnswerStatus>(raw, true, out var p) ? p : null;
}
