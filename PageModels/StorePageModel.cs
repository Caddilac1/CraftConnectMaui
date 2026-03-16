using System;
using System.Collections.Generic;
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
        private readonly ICartApiService _cartApiService;
        private readonly IServiceService _serviceService;

        // ── Collections ───────────────────────────────────────────────
        public ObservableCollection<StoreItem> StoreItems { get; } = new();
        private List<StoreItem> _allItems = new();

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
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(_activeFilter); }
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

        private static Page? CurrentPage =>
            Application.Current?.Windows[0].Page;

        // ── Constructor ───────────────────────────────────────────────
        public StorePageModel(
            IStoreService storeService,
            ICartApiService cartApiService,
            IServiceService serviceService)
        {
            _storeService = storeService;
            _cartApiService = cartApiService;
            _serviceService = serviceService;

            RefreshCommand = new Command(async () => await LoadStoreItemsAsync());
            ItemTappedCommand = new Command<StoreItem>(async item => await ViewItemDetailsAsync(item));
            AddToCartOrBookCommand = new Command<StoreItem>(async item => await HandleItemActionAsync(item));
            ViewCartCommand = new Command(async () => await NavigateToCartAsync());
            FilterCategoryCommand = new Command<string>(category => ApplyFilter(category));

            Debug.WriteLine("[STORE MODEL] ✅ Initialized");
        }

        public async Task InitializeAsync()
        {
            await LoadStoreItemsAsync();
            await RefreshCartBadgeAsync();
        }

        // ═══════════════════════════════════════════════════════════════
        // LOAD  —  products + services fetched in parallel inside StoreService
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadStoreItemsAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                HasError = false;

                var result = await _storeService.GetProductsAsync(
                    page: 1, pageSize: 40, sortBy: "popular");

                // result.Items already contains both products and services
                _allItems = result.Items;
                ApplyFilter(_activeFilter);

                Debug.WriteLine($"[STORE MODEL] ✅ {_allItems.Count} total items loaded " +
                    $"({_allItems.Count(i => i.Type == StoreItemType.Product)} products, " +
                    $"{_allItems.Count(i => i.Type == StoreItemType.Service)} services)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE MODEL] ❌ Load error: {ex.Message}");
                HasError = true;
                await CurrentPage!.DisplayAlert("Error", "Failed to load store items.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CART BADGE
        // ═══════════════════════════════════════════════════════════════

        private async Task RefreshCartBadgeAsync()
        {
            try
            {
                var count = await _cartApiService.GetCartCountAsync();
                CartItemCount = count?.TotalItems ?? 0;
                Debug.WriteLine($"[STORE MODEL] 🛒 Badge count: {CartItemCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE MODEL] ❌ RefreshBadge: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FILTER
        // "All"      → products + services
        // "Products" → products only
        // "Services" → services only
        // ═══════════════════════════════════════════════════════════════

        private void ApplyFilter(string category)
        {
            ActiveFilter = category;

            IEnumerable<StoreItem> filtered = category switch
            {
                "Products" => _allItems.Where(i => i.Type == StoreItemType.Product),
                "Services" => _allItems.Where(i => i.Type == StoreItemType.Service),
                _ => _allItems   // "All"
            };

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var q = _searchText.Trim().ToLower();
                filtered = filtered.Where(i =>
                    i.Name.ToLower().Contains(q) ||
                    i.Description.ToLower().Contains(q) ||
                    i.SellerName.ToLower().Contains(q));
            }

            StoreItems.Clear();
            foreach (var item in filtered) StoreItems.Add(item);
        }

        // ═══════════════════════════════════════════════════════════════
        // ITEM ACTION  —  routes to cart (product) or booking (service)
        // ═══════════════════════════════════════════════════════════════

        private async Task HandleItemActionAsync(StoreItem item)
        {
            if (item == null) return;

            if (item.Type == StoreItemType.Product)
                await AddToCartAsync(item);
            else
                await BookServiceAsync(item);
        }

        // ═══════════════════════════════════════════════════════════════
        // ADD TO CART
        // Optimistic badge update — reverts if API call fails.
        // ═══════════════════════════════════════════════════════════════

        private async Task AddToCartAsync(StoreItem product)
        {
            if (product == null) return;

            var token = await SecureStorage.GetAsync("auth_token");
            if (string.IsNullOrEmpty(token))
            {
                await CurrentPage!.DisplayAlert(
                    "Sign In Required",
                    "Please sign in to add items to your cart.",
                    "OK");
                return;
            }

            CartItemCount++;

            var result = await _cartApiService.AddItemAsync(
                productCompanyBusinessLocationId: product.ApiProductId,
                comboProductId: null,
                quantity: 1);

            if (result == null)
            {
                CartItemCount--;
                await CurrentPage!.DisplayAlert(
                    "Error", "Could not add item to cart. Please try again.", "OK");
                return;
            }

            CartItemCount = result.ItemCount;
            Debug.WriteLine($"[STORE MODEL] ✅ Added {product.Name}. Cart count: {CartItemCount}");
        }

        // ═══════════════════════════════════════════════════════════════
        // BOOK SERVICE
        //
        // Flow:
        //  1. Check auth — redirect to login if not signed in
        //  2. Check availability for today as a quick probe
        //  3. Navigate to service detail / booking page
        //     (replace DisplayAlert with Shell navigation once that page exists)
        // ═══════════════════════════════════════════════════════════════

        private async Task BookServiceAsync(StoreItem service)
        {
            if (service == null) return;

            try
            {
                // 1. Auth check
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                {
                    await CurrentPage!.DisplayAlert(
                        "Sign In Required",
                        "Please sign in to book a service.",
                        "OK");
                    return;
                }

                // 2. Quick availability probe for today
                var today = DateTime.Today;
                var availability = await _serviceService.GetAvailabilityAsync(
                    service.ApiServiceId, today);

                string availabilityInfo = availability != null && availability.IsAvailable
                    ? $"{availability.AvailableSlots.Count(s => s.IsAvailable)} slots available today"
                    : "Check other dates for availability";

                // 3. Navigate to booking page
                // TODO: Replace DisplayAlert with:
                //   await Shell.Current.GoToAsync(
                //       $"servicedetail?id={service.ApiServiceId}");
                // once ServiceDetailPage is built.
                var confirm = await CurrentPage!.DisplayAlert(
                    service.Name,
                    $"{service.SellerName}\nPrice: {service.DisplayPrice}\n{availabilityInfo}",
                    "Book Now",
                    "Cancel");

                if (confirm)
                {
                    await Shell.Current.GoToAsync(
                        $"servicedetail?id={service.ApiServiceId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE MODEL] ❌ BookService: {ex.Message}");
                await CurrentPage!.DisplayAlert(
                    "Error", "Could not load service details. Please try again.", "OK");
            }
        }

        // ── View item details ─────────────────────────────────────────

        private async Task ViewItemDetailsAsync(StoreItem item)
        {
            if (item == null) return;

            if (item.Type == StoreItemType.Service)
            {
                await Shell.Current.GoToAsync(
                    $"servicedetail?id={item.ApiServiceId}");
            }
            else
            {
                await Shell.Current.GoToAsync(
                    $"productdetail?id={item.ApiProductId}");
            }
        }

        // ── Navigate to cart ──────────────────────────────────────────

        private async Task NavigateToCartAsync()
        {
            await Shell.Current.GoToAsync("cart");
        }
    }
}