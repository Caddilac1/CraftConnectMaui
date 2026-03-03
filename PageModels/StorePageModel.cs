using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    public class StorePageModel : BasePageModel
    {
        // Store items (products & services)
        public ObservableCollection<StoreItem> StoreItems { get; } = new();

        // Cart (only for products)
        public ObservableCollection<CartItem> CartItems { get; } = new();

        // Service bookings
        public ObservableCollection<ServiceBooking> ServiceBookings { get; } = new();

        public Command RefreshCommand { get; }
        public Command<StoreItem> ItemTappedCommand { get; }
        public Command<StoreItem> AddToCartOrBookCommand { get; }
        public Command ViewCartCommand { get; }
        public Command<string> FilterCategoryCommand { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                // TODO: Implement search filtering
            }
        }

        private int _cartItemCount;
        public int CartItemCount
        {
            get => _cartItemCount;
            set
            {
                _cartItemCount = value;
                OnPropertyChanged();
            }
        }

        // ─── Helper to get current Page safely ───────────────────────────────────
        private static Page? CurrentPage =>
            Application.Current?.Windows[0].Page;

        public StorePageModel()
        {
            RefreshCommand = new Command(async () => await LoadStoreItems());
            ItemTappedCommand = new Command<StoreItem>(async (item) => await ViewItemDetails(item));
            AddToCartOrBookCommand = new Command<StoreItem>(async (item) => await HandleItemAction(item));
            ViewCartCommand = new Command(async () => await NavigateToCart());
            FilterCategoryCommand = new Command<string>((category) => FilterByCategory(category));

            Debug.WriteLine("[STORE PAGE MODEL] Initialized");
        }

        /// <summary>
        /// Initialize and load store items
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadStoreItems();
        }

        /// <summary>
        /// Load store items (products and services)
        /// </summary>
        private async Task LoadStoreItems()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                StoreItems.Clear();

                // TODO: Replace with actual API call
                // var items = await _storeService.GetStoreItemsAsync();

                // DEMO DATA - Remove when you have real API
                var demoItems = GenerateDemoData();

                foreach (var item in demoItems)
                {
                    StoreItems.Add(item);
                }

                Debug.WriteLine($"[STORE PAGE MODEL] ✅ Loaded {StoreItems.Count} items");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ Error: {ex.Message}");
                await CurrentPage.DisplayAlert(
                    "Error",
                    $"Failed to load store items: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Handle item action - Add to Cart for products, Book for services
        /// </summary>
        private async Task HandleItemAction(StoreItem item)
        {
            if (item == null) return;

            Debug.WriteLine($"[STORE PAGE MODEL] Action for: {item.Name} (Type: {item.Type})");

            if (item.Type == StoreItemType.Product)
            {
                await AddToCart(item);
            }
            else
            {
                await BookService(item);
            }
        }

        /// <summary>
        /// Add product to cart
        /// </summary>
        private async Task AddToCart(StoreItem product)
        {
            try
            {
                var existingItem = CartItems.FirstOrDefault(c => c.Item.Id == product.Id);

                if (existingItem != null)
                {
                    existingItem.Quantity++;
                    Debug.WriteLine($"[STORE PAGE MODEL] Increased quantity: {product.Name} x{existingItem.Quantity}");
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        Id = Guid.NewGuid(),
                        Item = product,
                        Quantity = 1
                    };
                    CartItems.Add(cartItem);
                    Debug.WriteLine($"[STORE PAGE MODEL] Added to cart: {product.Name}");
                }

                CartItemCount = CartItems.Sum(c => c.Quantity);

                await CurrentPage.DisplayAlert(
                    "✓ Added to Cart",
                    $"{product.Name} has been added to your cart",
                    "OK");

                // TODO: Save cart to database or service
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ Error adding to cart: {ex.Message}");
                await CurrentPage.DisplayAlert(
                    "Error",
                    "Failed to add item to cart",
                    "OK");
            }
        }

        /// <summary>
        /// Book a service or request a quote
        /// </summary>
        private async Task BookService(StoreItem service)
        {
            try
            {
                if (service.RequiresQuote)
                {
                    Debug.WriteLine($"[STORE PAGE MODEL] Requesting quote for: {service.Name}");

                    await CurrentPage.DisplayAlert(
                        "Request Quote",
                        $"A quote request for '{service.Name}' will be sent to {service.SellerName}. They will contact you with pricing details.",
                        "OK");

                    // TODO: Navigate to quote request page or send request
                    // await Shell.Current.GoToAsync($"quote?serviceId={service.Id}");
                }
                else
                {
                    Debug.WriteLine($"[STORE PAGE MODEL] Booking service: {service.Name}");

                    await CurrentPage.DisplayAlert(
                        "Book Service",
                        $"Booking '{service.Name}' - Duration: {service.Duration}\nYou'll be able to select a date and time in the next step.",
                        "Continue",
                        "Cancel");

                    // TODO: Navigate to service booking page
                    // await Shell.Current.GoToAsync($"bookservice?serviceId={service.Id}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE PAGE MODEL] ❌ Error booking service: {ex.Message}");
                await CurrentPage.DisplayAlert(
                    "Error",
                    "Failed to process service request",
                    "OK");
            }
        }

        /// <summary>
        /// View item details page
        /// </summary>
        private async Task ViewItemDetails(StoreItem item)
        {
            if (item == null) return;

            Debug.WriteLine($"[STORE PAGE MODEL] Viewing details for: {item.Name}");

            // TODO: Navigate to item details page
            // await Shell.Current.GoToAsync($"itemdetails?itemId={item.Id}");

            await CurrentPage.DisplayAlert(
                item.Name,
                $"{item.Description}\n\nPrice: {item.DisplayPrice}\nSeller: {item.SellerName}\nRating: {item.Rating}⭐ ({item.ReviewCount} reviews)",
                "OK");
        }

        /// <summary>
        /// Navigate to cart page
        /// </summary>
        private async Task NavigateToCart()
        {
            Debug.WriteLine($"[STORE PAGE MODEL] Navigating to cart ({CartItemCount} items)");

            if (CartItemCount == 0)
            {
                await CurrentPage.DisplayAlert(
                    "Cart Empty",
                    "Your cart is empty. Add some products to get started!",
                    "OK");
                return;
            }

            // TODO: Navigate to cart page
            // await Shell.Current.GoToAsync("cart");

            var cartSummary = string.Join("\n", CartItems.Select(c => $"• {c.Item.Name} x{c.Quantity} - ${c.Subtotal:N2}"));
            var total = CartItems.Sum(c => c.Subtotal);

            await CurrentPage.DisplayAlert(
                "Your Cart",
                $"{cartSummary}\n\nTotal: ${total:N2}",
                "OK");
        }

        /// <summary>
        /// Filter items by category
        /// </summary>
        private void FilterByCategory(string category)
        {
            Debug.WriteLine($"[STORE PAGE MODEL] Filtering by category: {category}");
            // TODO: Implement category filtering
        }

        /// <summary>
        /// Generate demo data (remove when you have real API)
        /// </summary>
        private List<StoreItem> GenerateDemoData()
        {
            return new List<StoreItem>
            {
                // PRODUCTS
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Premium Hammer",
                    Description = "Professional grade steel hammer with ergonomic grip",
                    Price = 29.99m,
                    ImageUrl = "https://via.placeholder.com/300x300/4F46E5/FFFFFF?text=Hammer",
                    Category = "Tools",
                    Type = StoreItemType.Product,
                    SellerId = Guid.NewGuid(),
                    SellerName = "ToolMaster Inc",
                    Rating = 4.8,
                    ReviewCount = 124,
                    StockQuantity = 45,
                    Duration = null,
                    RequiresQuote = false
                },
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Power Drill Set",
                    Description = "18V cordless drill with 50+ accessories",
                    Price = 149.99m,
                    ImageUrl = "https://via.placeholder.com/300x300/10B981/FFFFFF?text=Drill",
                    Category = "Tools",
                    Type = StoreItemType.Product,
                    SellerId = Guid.NewGuid(),
                    SellerName = "BuildPro",
                    Rating = 4.9,
                    ReviewCount = 89,
                    StockQuantity = 12,
                    Duration = null,
                    RequiresQuote = false
                },
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Safety Gear Kit",
                    Description = "Complete safety kit with helmet, goggles, and gloves",
                    Price = 79.99m,
                    ImageUrl = "https://via.placeholder.com/300x300/F59E0B/FFFFFF?text=Safety",
                    Category = "Safety",
                    Type = StoreItemType.Product,
                    SellerId = Guid.NewGuid(),
                    SellerName = "SafeWork Co",
                    Rating = 4.7,
                    ReviewCount = 56,
                    StockQuantity = 28,
                    Duration = null,
                    RequiresQuote = false
                },

                // SERVICES
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Plumbing Repair",
                    Description = "Professional plumbing services for leaks, installations, and repairs",
                    Price = 75.00m,
                    ImageUrl = "https://via.placeholder.com/300x300/3B82F6/FFFFFF?text=Plumbing",
                    Category = "Services",
                    Type = StoreItemType.Service,
                    SellerId = Guid.NewGuid(),
                    SellerName = "John's Plumbing",
                    Rating = 4.9,
                    ReviewCount = 203,
                    StockQuantity = null,
                    Duration = "2-3 hours",
                    RequiresQuote = false
                },
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Home Renovation",
                    Description = "Complete home renovation and remodeling services",
                    Price = 0m,
                    ImageUrl = "https://via.placeholder.com/300x300/EF4444/FFFFFF?text=Renovation",
                    Category = "Services",
                    Type = StoreItemType.Service,
                    SellerId = Guid.NewGuid(),
                    SellerName = "Elite Renovations",
                    Rating = 5.0,
                    ReviewCount = 67,
                    StockQuantity = null,
                    Duration = "Varies",
                    RequiresQuote = true
                },
                new StoreItem
                {
                    Id = Guid.NewGuid(),
                    Name = "Electrical Work",
                    Description = "Licensed electrician for wiring, installations, and repairs",
                    Price = 85.00m,
                    ImageUrl = "https://via.placeholder.com/300x300/8B5CF6/FFFFFF?text=Electric",
                    Category = "Services",
                    Type = StoreItemType.Service,
                    SellerId = Guid.NewGuid(),
                    SellerName = "Spark Electric",
                    Rating = 4.8,
                    ReviewCount = 142,
                    StockQuantity = null,
                    Duration = "1-2 hours",
                    RequiresQuote = false
                }
            };
        }
    }
}