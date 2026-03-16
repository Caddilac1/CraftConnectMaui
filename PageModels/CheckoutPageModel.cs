using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Pages;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    public class CartDisplayItem
    {
        public int CartItemId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public string? SellerName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
    }

    public class DeliveryAddressOption
    {
        public int StaffTownId { get; set; }
        public string TownName { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Landmark { get; set; }
        public decimal DeliveryFee { get; set; } = 15.00m;
        public double VatRate { get; set; }

        public string DisplayText =>
            string.IsNullOrEmpty(Landmark)
                ? $"{TownName}, {RegionName}\n{Address}"
                : $"{TownName}, {RegionName}\n{Address}\nLandmark: {Landmark}";
    }

    public class CheckoutPageModel : BasePageModel
    {
        private readonly ICartApiService _cartApiService;
        private static readonly JwtSecurityTokenHandler _jwtHandler = new();

        public ObservableCollection<CartDisplayItem> CartItems { get; } = new();

        public Command GoBackCommand { get; }
        public Command SelectPickupCommand { get; }
        public Command SelectDeliveryCommand { get; }
        public Command SelectAddressCommand { get; }
        public Command SelectCardPaymentCommand { get; }
        public Command SelectCashPaymentCommand { get; }
        public Command CheckoutCommand { get; }

        private bool _isPickup = true;
        public bool IsPickup
        {
            get => _isPickup;
            set
            {
                _isPickup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDelivery));
                RecalculateTotal();
            }
        }
        public bool IsDelivery => !_isPickup;

        private DeliveryAddressOption? _selectedAddress;
        public bool HasSelectedAddress => _selectedAddress != null;
        public string SelectedAddressText => _selectedAddress?.DisplayText ?? string.Empty;

        private bool _isCardPayment = true;
        public bool IsCardPayment
        {
            get => _isCardPayment;
            set
            {
                _isCardPayment = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCashPayment));
                UpdateCheckoutButton();
            }
        }
        public bool IsCashPayment => !_isCardPayment;

        private string _deliveryInstructions = string.Empty;
        public string DeliveryInstructions
        {
            get => _deliveryInstructions;
            set { _deliveryInstructions = value; OnPropertyChanged(); }
        }

        private decimal _subtotal;
        public decimal Subtotal
        {
            get => _subtotal;
            set { _subtotal = value; OnPropertyChanged(); }
        }

        private decimal _deliveryFee;
        public decimal DeliveryFee
        {
            get => _deliveryFee;
            set
            {
                _deliveryFee = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeliveryFeeDisplay));
                OnPropertyChanged(nameof(DeliveryFeeText));
                RecalculateTotal();
            }
        }

        public string DeliveryFeeDisplay => IsPickup ? "Free" : $"GH₵{_deliveryFee:N2}";
        public string DeliveryFeeText => $"From GH₵{_deliveryFee:N2}";

        private decimal _vatRate;
        private decimal _vatAmount;
        public decimal VatAmount
        {
            get => _vatAmount;
            set { _vatAmount = value; OnPropertyChanged(); }
        }

        private string _vatLabel = "VAT (0%)";
        public string VatLabel
        {
            get => _vatLabel;
            set { _vatLabel = value; OnPropertyChanged(); }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set { _grandTotal = value; OnPropertyChanged(); }
        }

        private int _cartItemCount;
        public int CartItemCount
        {
            get => _cartItemCount;
            set { _cartItemCount = value; OnPropertyChanged(); }
        }

        private string _checkoutButtonText = "🔒  Secure Checkout";
        public string CheckoutButtonText
        {
            get => _checkoutButtonText;
            set { _checkoutButtonText = value; OnPropertyChanged(); }
        }

        private static Page? CurrentPage =>
            Application.Current?.Windows[0].Page;

        public CheckoutPageModel(ICartApiService cartApiService)
        {
            _cartApiService = cartApiService;

            GoBackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
            SelectPickupCommand = new Command(() => { IsPickup = true; DeliveryFee = 0; });
            SelectDeliveryCommand = new Command(() => { IsPickup = false; DeliveryFee = _selectedAddress?.DeliveryFee ?? 15.00m; });
            SelectAddressCommand = new Command(async () => await PickDeliveryAddressAsync());
            SelectCardPaymentCommand = new Command(() => IsCardPayment = true);
            SelectCashPaymentCommand = new Command(() => IsCardPayment = false);
            CheckoutCommand = new Command(async () => await ProcessCheckoutAsync());

            MessagingCenter.Subscribe<PaystackWebViewPage, string>(
                this, "PaystackSuccess", async (_, reference) =>
                    await OnPaymentSuccessful(reference));
        }

        public async Task InitializeAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                await LoadCartAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadCartAsync()
        {
            try
            {
                var cart = await _cartApiService.GetCartAsync();
                CartItems.Clear();

                if (cart == null || !cart.Items.Any())
                {
                    CartItemCount = 0;
                    RecalculateTotal();
                    return;
                }

                foreach (var item in cart.Items)
                {
                    CartItems.Add(new CartDisplayItem
                    {
                        CartItemId = item.CartItemId,
                        ProductName = item.Name,
                        ProductImage = item.ThumbnailUrl,
                        SellerName = item.ItemType,
                        UnitPrice = item.UnitPrice,
                        Quantity = item.Quantity,
                        TotalPrice = item.TotalPrice
                    });
                }

                CartItemCount = cart.ItemCount;
                Subtotal = cart.Subtotal;
                _vatRate = cart.VatRate;
                VatLabel = $"VAT ({_vatRate * 100:F0}%)";
                RecalculateTotal();

                Debug.WriteLine($"[CHECKOUT MODEL] ✅ Loaded {CartItems.Count} items. Subtotal: GH₵{Subtotal:N2}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHECKOUT MODEL] ❌ LoadCart: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Failed to load cart. Please try again.", "OK");
            }
        }

        // ── Read email from the saved JWT token ───────────────────────
        // Your app only stores "auth_token" in SecureStorage — no separate
        // email key. So we decode it from the JWT claims directly.
        private async Task<string> GetEmailFromTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token)) return string.Empty;

                var jwt = _jwtHandler.ReadJwtToken(token);

                // Try standard email claim first, then fall back to name identifier
                var email =
                    jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress")?.Value ??
                    jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ??
                    string.Empty;

                Debug.WriteLine($"[CHECKOUT MODEL] Email from JWT: '{email}'");
                return email;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHECKOUT MODEL] ❌ GetEmail: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task PickDeliveryAddressAsync()
        {
            try
            {
                var addresses = await _cartApiService.GetDeliveryAddressesAsync();

                if (addresses == null || addresses.Count == 0)
                {
                    await CurrentPage!.DisplayAlert(
                        "No Saved Addresses",
                        "You have no saved delivery addresses. Please add one first.",
                        "OK");
                    return;
                }

                var options = addresses.Select(a => $"{a.TownName}, {a.RegionName}").ToArray();
                var choice = await CurrentPage!.DisplayActionSheet(
                    "Select Delivery Address", "Cancel", null, options);

                if (choice == null || choice == "Cancel") return;

                var idx = Array.IndexOf(options, choice);
                if (idx < 0) return;

                _selectedAddress = addresses[idx];
                OnPropertyChanged(nameof(HasSelectedAddress));
                OnPropertyChanged(nameof(SelectedAddressText));

                DeliveryFee = _selectedAddress.DeliveryFee;
                _vatRate = (decimal)(_selectedAddress.VatRate / 100.0);
                VatLabel = $"VAT ({_selectedAddress.VatRate:F1}%)";
                RecalculateTotal();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHECKOUT MODEL] ❌ PickAddress: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Failed to load addresses.", "OK");
            }
        }

        private void RecalculateTotal()
        {
            var effectiveDelivery = IsPickup ? 0m : DeliveryFee;
            var vatAmount = (Subtotal + effectiveDelivery) * _vatRate;
            VatAmount = vatAmount;
            GrandTotal = Subtotal + effectiveDelivery + vatAmount;
            OnPropertyChanged(nameof(DeliveryFeeDisplay));
        }

        private void UpdateCheckoutButton()
        {
            CheckoutButtonText = IsCardPayment
                ? "🔒  Secure Checkout"
                : "📦  Place Order (Cash)";
        }

        private async Task ProcessCheckoutAsync()
        {
            if (CartItems.Count == 0)
            {
                await CurrentPage!.DisplayAlert("Empty Cart", "Your cart is empty.", "OK");
                return;
            }

            if (IsDelivery && _selectedAddress == null)
            {
                await CurrentPage!.DisplayAlert(
                    "Address Required",
                    "Please select a delivery address before continuing.",
                    "OK");
                return;
            }

            if (IsCardPayment)
                await OpenPaystackAsync();
            else
                await PlaceOrderAsync(paymentReference: null);
        }

        private async Task OpenPaystackAsync()
        {
            try
            {
                IsBusy = true;
                CheckoutButtonText = "Opening payment...";

                // Get email from JWT — not from a separate SecureStorage key
                var email = await GetEmailFromTokenAsync();
                var amountPesewas = (long)(GrandTotal * 100);
                var reference = $"craftconnect_{DateTime.UtcNow.Ticks}";

                var deliveryAddress = IsPickup
                    ? "In-Store Pickup"
                    : _selectedAddress!.DisplayText;

                Debug.WriteLine($"[CHECKOUT MODEL] Opening Paystack — email: '{email}', amount: {amountPesewas}");

                await Shell.Current.GoToAsync(
                    $"paystackwebview" +
                    $"?email={Uri.EscapeDataString(email)}" +
                    $"&amount={amountPesewas}" +
                    $"&reference={Uri.EscapeDataString(reference)}" +
                    $"&deliveryAddress={Uri.EscapeDataString(deliveryAddress)}" +
                    $"&deliveryInstructions={Uri.EscapeDataString(DeliveryInstructions)}" +
                    $"&paymentMethod=paystack");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHECKOUT MODEL] ❌ OpenPaystack: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Failed to open payment page. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
                UpdateCheckoutButton();
            }
        }

        private async Task PlaceOrderAsync(string? paymentReference)
        {
            try
            {
                IsBusy = true;
                CheckoutButtonText = "Placing Order...";

                var deliveryAddress = IsPickup
                    ? "In-Store Pickup"
                    : _selectedAddress!.DisplayText;

                var success = await _cartApiService.PlaceOrderAsync(
                    deliveryAddress: deliveryAddress,
                    paymentMethod: paymentReference != null ? "paystack" : "Cash",
                    deliveryInstructions: DeliveryInstructions,
                    paystackReference: paymentReference);

                if (success)
                {
                    await CurrentPage!.DisplayAlert("✅ Order Placed", "Your order has been placed successfully!", "OK");
                    await Shell.Current.GoToAsync("//GroupChatListPage");
                }
                else
                {
                    await CurrentPage!.DisplayAlert("Error", "Failed to place order. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHECKOUT MODEL] ❌ PlaceOrder: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", $"Order failed: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
                UpdateCheckoutButton();
            }
        }

        public async Task OnPaymentSuccessful(string reference)
        {
            Debug.WriteLine($"[CHECKOUT MODEL] ✅ Payment successful — ref: {reference}");
            await PlaceOrderAsync(reference);
        }
    }
}