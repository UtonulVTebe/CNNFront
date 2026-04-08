using System.Collections.ObjectModel;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrainerStudApp.Domain;
using TrainerStudApp.Presentation.Converters;
using TrainerStudApp.Services;

namespace TrainerStudApp.Presentation.ViewModels;

public partial class StudentOrdersViewModel(IApiClient apiClient) : ObservableObject
{
    private bool _suppressSelectionSync;

    [ObservableProperty] private string ordersMessage = string.Empty;

    [ObservableProperty] private bool isBusy;

    [ObservableProperty] private OrderAnswerReadDto? selectedOrder;

    [ObservableProperty] private string editAnswerUrl = string.Empty;

    [ObservableProperty] private bool showCreatePanel;

    [ObservableProperty] private CnnListItemDto? newOrderSelectedCnn;

    [ObservableProperty] private string newOrderAnswerUrl = string.Empty;

    [ObservableProperty] private bool queueImmediatelyAfterCreate = true;

    [ObservableProperty] private ReviewReadDto? currentReview;

    [ObservableProperty] private bool reviewNotFound;

    [ObservableProperty] private string? rejectionDisplay;

    public ObservableCollection<OrderAnswerReadDto> Orders { get; } = [];
    public ObservableCollection<CnnListItemDto> CnnOptions { get; } = [];

    public bool CanSaveUrl =>
        SelectedOrder is not null
        && !string.IsNullOrWhiteSpace(EditAnswerUrl)
        && SelectedOrder.Status is not (OrderAnswerStatus.Checking or OrderAnswerStatus.Checked);

    public bool CanSubmitQueue =>
        SelectedOrder is not null
        && !string.IsNullOrWhiteSpace(EditAnswerUrl)
        && SelectedOrder.Status is OrderAnswerStatus.NoCheck or OrderAnswerStatus.Rejected;

    public bool ShowRejection =>
        SelectedOrder?.Status == OrderAnswerStatus.Rejected && !string.IsNullOrWhiteSpace(RejectionDisplay);

    public bool ShowReviewSection =>
        SelectedOrder?.Status == OrderAnswerStatus.Checked && (CurrentReview is not null || ReviewNotFound);

    public bool HasSelectedOrder => SelectedOrder is not null;

    public bool HasCurrentReview => CurrentReview is not null;

    partial void OnSelectedOrderChanged(OrderAnswerReadDto? value)
    {
        OnPropertyChanged(nameof(HasSelectedOrder));
        if (_suppressSelectionSync) return;
        _ = SyncSelectionAsync(value);
    }

    partial void OnCurrentReviewChanged(ReviewReadDto? value) => OnPropertyChanged(nameof(HasCurrentReview));

    partial void OnEditAnswerUrlChanged(string value)
    {
        OnPropertyChanged(nameof(CanSaveUrl));
        OnPropertyChanged(nameof(CanSubmitQueue));
    }

    public void Reset()
    {
        Orders.Clear();
        CnnOptions.Clear();
        SelectedOrder = null;
        OnPropertyChanged(nameof(HasSelectedOrder));
        EditAnswerUrl = string.Empty;
        ShowCreatePanel = false;
        NewOrderSelectedCnn = null;
        NewOrderAnswerUrl = string.Empty;
        CurrentReview = null;
        ReviewNotFound = false;
        RejectionDisplay = null;
        OrdersMessage = string.Empty;
    }

    [RelayCommand]
    private async Task RefreshMineAsync()
    {
        IsBusy = true;
        try
        {
            var list = await apiClient.GetMyOrderAnswersMineAsync(default);
            Orders.Clear();
            foreach (var o in list)
                Orders.Add(o);
            OrdersMessage = $"Заказов: {Orders.Count}.";
            await EnsureCnnOptionsAsync();
        }
        catch (HttpApiException ex)
        {
            OrdersMessage = $"Ошибка списка: {ex.Message}";
        }
        catch (Exception ex)
        {
            OrdersMessage = $"Ошибка списка: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EnsureCnnOptionsAsync()
    {
        if (CnnOptions.Count > 0) return;
        try
        {
            var cnns = await apiClient.GetCnnsAsync(default);
            foreach (var c in cnns.OrderBy(x => x.Subject).ThenBy(x => x.Option))
                CnnOptions.Add(c);
        }
        catch
        {
            /* каталог опционален для формы */
        }
    }

    private async Task SyncSelectionAsync(OrderAnswerReadDto? selected)
    {
        CurrentReview = null;
        ReviewNotFound = false;
        RejectionDisplay = null;
        if (selected is null)
        {
            EditAnswerUrl = string.Empty;
            OnPropertyChanged(nameof(CanSaveUrl));
            OnPropertyChanged(nameof(CanSubmitQueue));
            OnPropertyChanged(nameof(ShowRejection));
            OnPropertyChanged(nameof(ShowReviewSection));
            return;
        }

        try
        {
            var fresh = await apiClient.GetOrderAnswerByIdAsync(selected.Id, default);
            var idx = -1;
            for (var i = 0; i < Orders.Count; i++)
            {
                if (Orders[i].Id != fresh.Id) continue;
                idx = i;
                break;
            }

            if (idx >= 0)
                Orders[idx] = fresh;
            _suppressSelectionSync = true;
            try
            {
                SelectedOrder = fresh;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            EditAnswerUrl = fresh.AnswerUrl ?? string.Empty;
            RejectionDisplay = fresh.RejectionReason;
            await ApplyReviewStateAsync(fresh);
        }
        catch (Exception ex)
        {
            OrdersMessage = $"Не удалось загрузить заказ: {ex.Message}";
        }

        OnPropertyChanged(nameof(CanSaveUrl));
        OnPropertyChanged(nameof(CanSubmitQueue));
        OnPropertyChanged(nameof(ShowRejection));
        OnPropertyChanged(nameof(ShowReviewSection));
    }

    private async Task ApplyReviewStateAsync(OrderAnswerReadDto o)
    {
        CurrentReview = null;
        ReviewNotFound = false;
        if (o.Status != OrderAnswerStatus.Checked)
        {
            OnPropertyChanged(nameof(ShowReviewSection));
            return;
        }

        try
        {
            CurrentReview = await apiClient.GetOrderAnswerReviewAsync(o.Id, default);
            ReviewNotFound = false;
        }
        catch (HttpApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            ReviewNotFound = true;
            CurrentReview = null;
        }

        OnPropertyChanged(nameof(ShowReviewSection));
        OnPropertyChanged(nameof(CanSaveUrl));
        OnPropertyChanged(nameof(CanSubmitQueue));
    }

    [RelayCommand]
    private async Task SaveAnswerUrlAsync()
    {
        if (SelectedOrder is null) return;
        IsBusy = true;
        try
        {
            var updated = await apiClient.UpdateOrderAnswerAsync(
                SelectedOrder.Id,
                new OrderAnswerUpdateDto { AnswerUrl = EditAnswerUrl.Trim() },
                default);
            ReplaceInList(updated);
            _suppressSelectionSync = true;
            try
            {
                SelectedOrder = updated;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            await ApplyReviewStateAsync(updated);
            OnPropertyChanged(nameof(CanSaveUrl));
            OnPropertyChanged(nameof(CanSubmitQueue));
            OrdersMessage = "Ссылка на ответ сохранена.";
        }
        catch (Exception ex)
        {
            OrdersMessage = $"Сохранение: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SubmitToQueueAsync()
    {
        if (SelectedOrder is null) return;
        IsBusy = true;
        try
        {
            var updated = await apiClient.UpdateOrderAnswerAsync(
                SelectedOrder.Id,
                new OrderAnswerUpdateDto
                {
                    AnswerUrl = string.IsNullOrWhiteSpace(EditAnswerUrl) ? null : EditAnswerUrl.Trim(),
                    Status = OrderAnswerStatus.QueueForCheck
                },
                default);
            ReplaceInList(updated);
            _suppressSelectionSync = true;
            try
            {
                SelectedOrder = updated;
            }
            finally
            {
                _suppressSelectionSync = false;
            }
            await ApplyReviewStateAsync(updated);
            OnPropertyChanged(nameof(CanSaveUrl));
            OnPropertyChanged(nameof(CanSubmitQueue));
            OrdersMessage = "Заказ отправлен в очередь на проверку.";
        }
        catch (Exception ex)
        {
            OrdersMessage = $"Очередь: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        if (NewOrderSelectedCnn is null || string.IsNullOrWhiteSpace(NewOrderAnswerUrl))
        {
            OrdersMessage = "Выберите вариант (CNN) и укажите URL файла ответа.";
            return;
        }

        IsBusy = true;
        try
        {
            var dto = new OrderAnswerCreateDto
            {
                CnnId = NewOrderSelectedCnn.Id,
                AnswerUrl = NewOrderAnswerUrl.Trim(),
                Status = QueueImmediatelyAfterCreate ? OrderAnswerStatus.QueueForCheck : null
            };
            var created = await apiClient.CreateOrderAnswerAsync(dto, default);
            Orders.Insert(0, created);
            ShowCreatePanel = false;
            NewOrderAnswerUrl = string.Empty;
            SelectedOrder = created;
            OrdersMessage = QueueImmediatelyAfterCreate
                ? "Заказ создан и поставлен в очередь."
                : "Заказ создан (черновик).";
        }
        catch (Exception ex)
        {
            OrdersMessage = $"Создание: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleCreatePanel()
    {
        ShowCreatePanel = !ShowCreatePanel;
        if (ShowCreatePanel)
            _ = EnsureCnnOptionsAsync();
    }

    private void ReplaceInList(OrderAnswerReadDto updated)
    {
        for (var i = 0; i < Orders.Count; i++)
        {
            if (Orders[i].Id != updated.Id) continue;
            Orders[i] = updated;
            return;
        }

        Orders.Insert(0, updated);
    }

    public static string StatusLabel(OrderAnswerStatus s) => OrderAnswerStatusConverter.Format(s);
}
