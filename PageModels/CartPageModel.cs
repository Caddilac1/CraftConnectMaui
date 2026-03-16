using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    // ══════════════════════════════════════════════════════════════════
    // CartItemViewModel — one row in the cart CollectionView.
    // Inherits BasePageModel for OnPropertyChanged support.
    // ══════════════════════════════════════════════════════════════════
    public class CartItemViewModel : BasePageModel
    {
        public int    CartItemId                       { get; set; }
        public int?   ProductCompanyBusinessLocationId { get; set; }
        public int?   ComboProductId                   { get; set; }
        public string ItemType                         { get; set; } = string.Empty;
        public string? ThumbnailUrl                    { get; set; }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { _unitPrice = value; OnPropertyChanged(); }
        }

        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity  = value;
                TotalPrice = _unitPrice * value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TotalPrice));
                OnPropertyChanged(nameof(StockWarning));
                OnPropertyChanged(nameof(HasStockWarning));
                OnPropertyChanged(nameof(CanIncrement));
            }
        }

        private decimal _totalPrice;
        public decimal TotalPrice
        {
            get => _totalPrice;
            set { _totalPrice = value; OnPropertyChanged(); }
        }

        private int? _stockOnHand;
        public int? StockOnHand
        {
            get => _stockOnHand;
            set
            {
                _stockOnHand = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanIncrement));
                OnPropertyChanged(nameof(StockWarning));
                OnPropertyChanged(nameof(HasStockWarning));
            }
        }

        // False when quantity == stock — drives + button colour via converter
        private bool _apiCanIncrement = true;
        public bool CanIncrement
        {
            get => _apiCanIncrement && (_stockOnHand == null || _quantity < _stockOnHand.Value);
            set { _apiCanIncrement = value; OnPropertyChanged(); }
        }

        // Shown in red below the product name
        public string? StockWarning =>
            _stockOnHand == 0
                ? "Out of stock"
                : (_stockOnHand.HasValue && _stockOnHand.Value <= 3)
                    ? $"Only {_stockOnHand.Value} left!"
                    : null;

        public bool HasStockWarning => !string.IsNullOrEmpty(StockWarning);
    }

    // ══════════════════════════════════════════════════════════════════
    // CartPageModel
    //
    // Security:
    //   • Every API call goes through ICartApiService which adds the
    //     JWT Bearer token per-request (never on DefaultRequestHeaders).
    //   • IDOR is enforced server-side; the client only knows CartItemId.
    //
    // Speed:
    //   • Quantity changes are debounced 200 ms — rapid taps send ONE
    //     API call, not one per tap.
    //   • Totals are updated locally immediately (optimistic) so the UI
    //     feels instant even on slow connections.
    //   • Remove / Clear use optimistic deletion with revert on failure.
    //   • IsLoading gate prevents duplicate concurrent loads.
    // ══════════════════════════════════════════════════════════════════
    public class CartPageModel : BasePageModel
    {
        private readonly ICartApiService _cartApiService;

        // Debounce token for quantity updates
        private CancellationTokenSource? _qtyCts;

        // ── Observable collection ─────────────────────────────────────
        public ObservableCollection<CartItemViewModel> CartItems { get; } = new();

        // ── Commands ─────────────────────────────────────────────────
        public Command GoBackCommand                              { get; }
        public Command GoToStoreCommand                           { get; }
        public Command ClearCartCommand                           { get; }
        public Command ProceedToCheckoutCommand                   { get; }
        public Command<CartItemViewModel> RemoveItemCommand       { get; }
        public Command<CartItemViewModel> IncrementCommand        { get; }
        public Command<CartItemViewModel> DecrementCommand        { get; }

        // ── Loading / empty ───────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
                NotifyListState();
            }
        }

        // HasItems: cart has rows AND we are not in the initial load
        public bool HasItems => CartItems.Any() && !_isLoading;
        public bool IsEmpty  => !CartItems.Any() && !_isLoading;

        // ── Validation banner ─────────────────────────────────────────
        private string? _validationMessage;
        public string? ValidationMessage
        {
            get => _validationMessage;
            set
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationIssues));
            }
        }
        public bool HasValidationIssues => !string.IsNullOrEmpty(_validationMessage);

        // ── Pricing ───────────────────────────────────────────────────
        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set { _subtotal = value; OnPropertyChanged(); }
        }

        private decimal _vatRate;

        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            set { _vatAmount = value; OnPropertyChanged(); }
        }

        private string _vatLabel = "VAT";
        public string VatLabel
        {
            get => _vatLabel;
            set { _vatLabel = value; OnPropertyChanged(); }
        }

        private decimal _total;
        public decimal Total
        {
            get => _total;
            set { _total = value; OnPropertyChanged(); }
        }

        // ── Header summary label ──────────────────────────────────────
        private string _cartSummaryLabel = "";
        public string CartSummaryLabel
        {
            get => _cartSummaryLabel;
            set { _cartSummaryLabel = value; OnPropertyChanged(); }
        }

        private static Page? CurrentPage =>
            Application.Current?.Windows[0].Page;

        // ── Constructor ───────────────────────────────────────────────
        public CartPageModel(ICartApiService cartApiService)
        {
            _cartApiService = cartApiService;

            GoBackCommand            = new Command(async () => await Shell.Current.GoToAsync(".."));
            GoToStoreCommand         = new Command(async () => await Shell.Current.GoToAsync("//StorePage"));
            ClearCartCommand         = new Command(async () => await ClearCartAsync());
            ProceedToCheckoutCommand = new Command(async () => await ProceedToCheckoutAsync());
            RemoveItemCommand        = new Command<CartItemViewModel>(async item => await RemoveItemAsync(item));
            IncrementCommand         = new Command<CartItemViewModel>(async item => await ChangeQuantityAsync(item, +1));
            DecrementCommand         = new Command<CartItemViewModel>(async item => await ChangeQuantityAsync(item, -1));
        }

        // ── Called from CartPage.OnAppearing ─────────────────────────
        public async Task InitializeAsync()
        {
            if (_isLoading) return;
            await LoadCartAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // LOAD  →  GET /api/cart
        // ═══════════════════════════════════════════════════════════════
        private async Task LoadCartAsync()
        {
            try
            {
                IsLoading         = true;
                ValidationMessage = null;

                var cart = await _cartApiService.GetCartAsync();

                CartItems.Clear();

                if (cart == null || !cart.Items.Any())
                {
                    UpdateSummaryLabel(0);
                    RecalculateLocally();
                    return;
                }

                foreach (var dto in cart.Items)
                {
                    CartItems.Add(new CartItemViewModel
                    {
                        CartItemId                       = dto.CartItemId,
                        ProductCompanyBusinessLocationId = dto.ProductCompanyBusinessLocationId,
                        ComboProductId                   = dto.ComboProductId,
                        ItemType                         = Capitalise(dto.ItemType),
                        Name                             = dto.Name,
                        ThumbnailUrl                     = dto.ThumbnailUrl,
                        UnitPrice                        = dto.UnitPrice,
                        Quantity                         = dto.Quantity,
                        TotalPrice                       = dto.TotalPrice,
                        StockOnHand                      = dto.StockOnHand,
                        CanIncrement                     = dto.CanIncrement
                    });
                }

                // Sync totals from server (authoritative)
                _vatRate  = cart.VatRate;
                Subtotal  = cart.Subtotal;
                VatLabel  = $"VAT ({_vatRate * 100:F0}%)";
                VatAmount = cart.VatAmount;
                Total     = cart.Total;

                UpdateSummaryLabel(cart.ItemCount);
                NotifyListState();

                Debug.WriteLine($"[CART MODEL] ✅ Loaded {CartItems.Count} line items. Total GH₵{Total:N2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART MODEL] ❌ LoadCart: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Could not load your cart. Please try again.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INCREMENT / DECREMENT  →  PATCH /api/cart/items/{id}
        //
        // Debounced 200 ms so rapid taps only fire ONE network call.
        // UI updates optimistically so it feels instant.
        // ═══════════════════════════════════════════════════════════════
        private async Task ChangeQuantityAsync(CartItemViewModel item, int delta)
        {
            if (item == null) return;

            var newQty = item.Quantity + delta;

            // Going to 0 = remove the item
            if (newQty < 1)
            {
                await RemoveItemAsync(item);
                return;
            }

            // Hard client-side stock guard
            if (item.StockOnHand.HasValue && newQty > item.StockOnHand.Value)
            {
                await CurrentPage!.DisplayAlert(
                    "Stock Limit",
                    $"Only {item.StockOnHand.Value} unit(s) of \"{item.Name}\" available.",
                    "OK");
                return;
            }

            // Optimistic update — instant UI
            item.Quantity = newQty;
            RecalculateLocally();

            // Cancel any pending debounced call for the same item
            _qtyCts?.Cancel();
            _qtyCts = new CancellationTokenSource();
            var token = _qtyCts.Token;

            try
            {
                await Task.Delay(200, token);   // debounce window

                var result = await _cartApiService.UpdateItemQuantityAsync(item.CartItemId, newQty);

                if (result == null)
                {
                    // Server rejected — revert
                    item.Quantity = newQty - delta;
                    RecalculateLocally();
                    await CurrentPage!.DisplayAlert("Error", "Could not update quantity.", "OK");
                    return;
                }

                // Sync with authoritative server values
                item.TotalPrice   = result.TotalPrice;
                item.StockOnHand  = result.StockOnHand;
                item.CanIncrement = result.CanIncrement;
                RecalculateLocally();
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer tap — do nothing
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART MODEL] ❌ ChangeQty: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REMOVE ITEM  →  DELETE /api/cart/items/{id}
        // Optimistic deletion with revert on failure.
        // ═══════════════════════════════════════════════════════════════
        private async Task RemoveItemAsync(CartItemViewModel item)
        {
            if (item == null) return;

            var confirmed = await CurrentPage!.DisplayAlert(
                "Remove Item",
                $"Remove \"{item.Name}\" from your cart?",
                "Remove", "Cancel");

            if (!confirmed) return;

            // Optimistic remove
            CartItems.Remove(item);
            RecalculateLocally();
            NotifyListState();

            var success = await _cartApiService.RemoveItemAsync(item.CartItemId);

            if (!success)
            {
                // Revert
                CartItems.Add(item);
                RecalculateLocally();
                NotifyListState();
                await CurrentPage!.DisplayAlert("Error", "Could not remove item. Please try again.", "OK");
            }
            else
            {
                UpdateSummaryLabel(CartItems.Sum(i => i.Quantity));
                Debug.WriteLine($"[CART MODEL] ✅ Removed CartItemId={item.CartItemId}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CLEAR CART  →  DELETE /api/cart
        // ═══════════════════════════════════════════════════════════════
        private async Task ClearCartAsync()
        {
            var confirmed = await CurrentPage!.DisplayAlert(
                "Clear Cart", "Remove all items from your cart?",
                "Clear All", "Cancel");

            if (!confirmed) return;

            var backup = CartItems.ToList();   // keep for revert

            CartItems.Clear();
            RecalculateLocally();
            NotifyListState();

            var success = await _cartApiService.ClearCartAsync();

            if (!success)
            {
                foreach (var item in backup) CartItems.Add(item);
                RecalculateLocally();
                NotifyListState();
                await CurrentPage!.DisplayAlert("Error", "Could not clear cart. Please try again.", "OK");
            }
            else
            {
                UpdateSummaryLabel(0);
                Debug.WriteLine("[CART MODEL] ✅ Cart cleared");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PROCEED TO CHECKOUT
        // Runs POST /api/cart/validate first — catches stock / price
        // changes before the user reaches the checkout form.
        // ═══════════════════════════════════════════════════════════════
        private async Task ProceedToCheckoutAsync()
        {
            if (!CartItems.Any()) return;

            try
            {
                IsBusy            = true;
                ValidationMessage = null;

                var validation = await _cartApiService.ValidateCartAsync();

                if (validation != null && !validation.IsValid)
                {
                    ValidationMessage = string.Join("  •  ", validation.Issues.Take(3));

                    // Reload so quantities / warnings reflect server state
                    await LoadCartAsync();

                    await CurrentPage!.DisplayAlert(
                        "Cart Updated",
                        "Some items were updated due to stock or price changes.\n\n• " +
                        string.Join("\n• ", validation.Issues.Take(5)),
                        "OK");
                    return;
                }

                // Navigate to CheckoutPage (the renamed existing CartPage)
                await Shell.Current.GoToAsync("CheckoutPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART MODEL] ❌ ProceedToCheckout: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Could not validate cart. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Local helpers ─────────────────────────────────────────────

        /// <summary>
        /// Recalculate subtotal / VAT / total from local CartItems only.
        /// No API call — used after every optimistic UI change for speed.
        /// </summary>
        private void RecalculateLocally()
        {
            var sub  = CartItems.Sum(i => i.TotalPrice);
            var vat  = sub * _vatRate;
            Subtotal  = sub;
            VatAmount = vat;
            Total     = sub + vat;
        }

        private void UpdateSummaryLabel(int totalUnits)
        {
            CartSummaryLabel = totalUnits switch
            {
                0 => "Your cart is empty",
                1 => "1 item",
                _ => $"{totalUnits} items"
            };
        }

        private void NotifyListState()
        {
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private static string Capitalise(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
    }
}
