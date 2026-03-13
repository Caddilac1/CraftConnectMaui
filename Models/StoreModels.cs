using System;
using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Models
{
    /// <summary>
    /// Enum to differentiate between products and services
    /// </summary>
    public enum StoreItemType
    {
        Product,  // Physical/digital items that can be added to cart
        Service   // Services that need to be booked/requested
    }

    /// <summary>
    /// Base class for store items (both products and services)
    /// </summary>
    public class StoreItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Price { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public StoreItemType Type { get; set; }
        public Guid SellerId { get; set; }
        public string SellerName { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }

        // Product-specific
        public int? StockQuantity { get; set; }  // null for services
        public bool IsInStock => Type == StoreItemType.Service || (StockQuantity.HasValue && StockQuantity.Value > 0);

        // Service-specific
        public string Duration { get; set; }  // e.g., "2 hours", "1 day"
        public bool RequiresQuote { get; set; }  // Some services need custom quotes

        // Display helpers
        public string DisplayPrice => RequiresQuote ? "Get Quote" : $"${Price:N2}";
        public string ActionButtonText => Type == StoreItemType.Product ? "Add to Cart" : (RequiresQuote ? "Request Quote" : "Book Service");
        public string TypeBadge => Type == StoreItemType.Product ? "Product" : "Service";

        // ─────────────────────────────────────────────────────────────────
        // ADD THESE TWO PROPERTIES to your existing StoreItem model class
        // ─────────────────────────────────────────────────────────────────

        // The backend ProductCompanyBusinessLocationId — used for cart/order API calls.
        // The Guid Id is kept for local list operations; ApiProductId is the real DB key.
        public int ApiProductId { get; set; }

        // Set when a promotional price is active. Null means no discount.
        // The UI can show a strikethrough on OriginalPrice alongside the discounted Price.
        public decimal? OriginalPrice { get; set; }
    }

    /// <summary>
    /// Cart item for products
    /// </summary>
    public class CartItem
    {
        public Guid Id { get; set; }
        public StoreItem Item { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => Item.Price * Quantity;
    }

    /// <summary>
    /// Service booking request
    /// </summary>
    public class ServiceBooking
    {
        public Guid Id { get; set; }
        public StoreItem Service { get; set; }
        public DateTime PreferredDate { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }  // "Pending", "Confirmed", "Completed"
    }
}