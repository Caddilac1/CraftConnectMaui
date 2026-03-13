using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    public class StorePageModel : BasePageModel
    {
        private readonly IStoreService _storeService;

        // ── Collections ───────────────────────────────────────────────
        // All items currently shown in the grid (products + services)
        public ObservableCollection<StoreItem> StoreItems { get; } = new();

        // Full unfiltered product list — used when switching filters
        private List<StoreItem> _allProducts = new();

        // Cart (products only)
        public ObservableCollection<CartItem> CartItems { get; } = new();

        // Service bookings — populated when services endpoint is ready
        public ObservableCollection<ServiceBooking> ServiceBookings { get; } = new();

        // ── Commands ──────────────────────────────────────────────────
        public Command RefreshCommand { get; }
        public Command<StoreItem> ItemTappedCommand { get; }
        public Command<StoreItem> AddToCartOrBookCommand { get; }
        public Command ViewCartCommand { get; }
        public Command<string> FilterCategoryCommand { get; }

        // ── Properties ────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilter(_activeFilter);
            }
        }

        private int _cartItemCount;
        public int CartItemCount
        {
            get => _cartItemCount;
            set { _cartItemCount = value; OnPropertyChanged(); }
        }

        private string _activeFilter = "All";
        public string ActiveFilter
        {
            get => _activeFilter;
            set { _activeFilter = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // ── Safe page reference ───────────────────────────────────────
        private static Page? CurrentPage =>
            Application.Current?.Windows[0].Page;

        // ── Constructor ───────────────────────────────────────────────
        public StorePageModel(IStoreService storeService)
        {
            _storeService = storeService;

            RefreshCommand = new Command(async () => await LoadStoreItemsAsync());
            ItemTappedCommand = new Command<StoreItem>(async item => await ViewItemDetailsAsync(item));
            AddToCartOrBookCommand = new Command<StoreItem>(async item => await HandleItemActionAsync(item));
            ViewCartCommand = new Command(async () => await NavigateToCartAsync());
            FilterCategoryCommand = new Command<string>(category => ApplyFilter(category));

            Debug.WriteLine("[STORE PAGE MODEL] Initialized with IStoreService");
        }

        // ── Public API ────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            await LoadStoreItemsAsync();
        }

        // ── Load products from API ────────────────────────────────────

        private async Task LoadStoreItemsAsync()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                HasError = false;

                Debug.WriteLine("[STORE PAGE MODEL] 📡 Loading products from API...");

                var result = await _storeService.GetProductsAsync(
                    page: 1,
                    pageSize: 40,    // load enough for a full first screen
                    sortBy: "popular");

                _allProducts = result.Items;

                Debug.WriteLine($"[STORE PAGE MODEL] ✅ API returned {_allProducts.Count} products");

                // Apply whatever filter is currently active
                ApplyFilter(_activeFilter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ Load error: {ex.Message}");
                HasError = true;
                ErrorMessage = "Failed to load store items. Pull down to retry.";

                await CurrentPage!.DisplayAlert(
                    "Error",
                    $"Failed to load store: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Filter logic ──────────────────────────────────────────────
        // Keeps the category chips working while services are not yet
        // from the API. Service items will be added to _allProducts
        // when that endpoint is ready — the filter logic stays the same.

        private void ApplyFilter(string category)
        {
            ActiveFilter = category;

            IEnumerable<StoreItem> filtered = _allProducts;

            // Category chip filter
            filtered = category switch
            {
                "Products" => filtered.Where(i => i.Type == StoreItemType.Product),
                "Services" => filtered.Where(i => i.Type == StoreItemType.Service),
                _ => filtered   // "All" and any future chips
            };

            // Search text filter (applied on top of category)
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = _searchText.Trim().ToLower();
                filtered = filtered.Where(i =>
                    i.Name.ToLower().Contains(q) ||
                    i.Description.ToLower().Contains(q) ||
                    i.SellerName.ToLower().Contains(q));
            }

            StoreItems.Clear();
            foreach (var item in filtered)
                StoreItems.Add(item);

            Debug.WriteLine($"[STORE PAGE MODEL] Filter '{category}' → {StoreItems.Count} items shown");
        }

        // ── Item action dispatcher ────────────────────────────────────

        private async Task HandleItemActionAsync(StoreItem item)
        {
            if (item == null) return;

            if (item.Type == StoreItemType.Product)
                await AddToCartAsync(item);
            else
                await BookServiceAsync(item);
        }

        // ── Add to cart ───────────────────────────────────────────────

        private async Task AddToCartAsync(StoreItem product)
        {
            try
            {
                // Optimistic local update first for instant UI feedback
                var existing = CartItems.FirstOrDefault(c => c.Item.Id == product.Id);
                if (existing != null)
                    existing.Quantity++;
                else
                    CartItems.Add(new CartItem { Id = Guid.NewGuid(), Item = product, Quantity = 1 });

                CartItemCount = CartItems.Sum(c => c.Quantity);

                // Then persist to API in the background
                var success = await _storeService.AddToCartAsync(product.ApiProductId, 1);

                if (!success)
                {
                    // Rollback optimistic update
                    var added = CartItems.FirstOrDefault(c => c.Item.Id == product.Id);
                    if (added != null)
                    {
                        if (added.Quantity > 1) added.Quantity--;
                        else CartItems.Remove(added);
                    }
                    CartItemCount = CartItems.Sum(c => c.Quantity);

                    await CurrentPage!.DisplayAlert("Error", "Could not add item to cart. Please try again.", "OK");
                    return;
                }

                await CurrentPage!.DisplayAlert("✓ Added to Cart", $"{product.Name} added to your cart.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ AddToCart error: {ex.Message}");
                await CurrentPage!.DisplayAlert("Error", "Failed to add item to cart.", "OK");
            }
        }

        // ── Book service ──────────────────────────────────────────────

        private async Task BookServiceAsync(StoreItem service)
        {
            try
            {
                if (service.RequiresQuote)
                {
                    await CurrentPage!.DisplayAlert(
                        "Request Quote",
                        $"A quote request for '{service.Name}' will be sent to {service.SellerName}.",
                        "OK");
                }
                else
                {
                    await CurrentPage!.DisplayAlert(
                        "Book Service",
                        $"Booking '{service.Name}' — Duration: {service.Duration}\nSelect a date and time in the next step.",
                        "Continue",
                        "Cancel");
                    // TODO: await Shell.Current.GoToAsync($"bookservice?serviceId={service.Id}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ BookService error: {ex.Message}");
            }
        }

        // ── View item details ─────────────────────────────────────────

        private async Task ViewItemDetailsAsync(StoreItem item)
        {
            if (item == null) return;
            // TODO: await Shell.Current.GoToAsync($"itemdetails?itemId={item.ApiProductId}");
            await CurrentPage!.DisplayAlert(
                item.Name,
                $"{item.Description}\n\nPrice: {item.DisplayPrice}\nSeller: {item.SellerName}\nRating: {item.Rating}⭐ ({item.ReviewCount} reviews)",
                "OK");
        }

        // ── Cart navigation ───────────────────────────────────────────

        private async Task NavigateToCartAsync()
        {
            if (CartItemCount == 0)
            {
                await CurrentPage!.DisplayAlert("Cart Empty", "Add some products to get started!", "OK");
                return;
            }

            var summary = string.Join("\n", CartItems.Select(c => $"• {c.Item.Name} x{c.Quantity} — {c.Item.DisplayPrice}"));
            var total = CartItems.Sum(c => c.Subtotal);

            await CurrentPage!.DisplayAlert("Your Cart", $"{summary}\n\nTotal: ${total:N2}", "OK");
            // TODO: await Shell.Current.GoToAsync("cart");
        }
    }
}