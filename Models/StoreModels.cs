using System;
using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Models
{
    public enum StoreItemType
    {
        Product,
        Service
    }

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
        public int? StockQuantity { get; set; }
        public bool IsInStock => Type == StoreItemType.Service || (StockQuantity.HasValue && StockQuantity.Value > 0);

        // Service-specific
        public string Duration { get; set; }
        public bool RequiresQuote { get; set; }

        // ── API identifiers ──────────────────────────────────────────────
        // ApiProductId  → ProductCompanyBusinessLocationId  (used for cart calls)
        // ApiServiceId  → ServiceCompanyBusinessLocationId  (used for booking calls)
        // Only one will be set depending on Type; the other stays 0.
        public int ApiProductId { get; set; }
        public int ApiServiceId { get; set; }

        // Promotional pricing — null means no active discount
        public decimal? OriginalPrice { get; set; }

        // ── Display helpers ──────────────────────────────────────────────
        public string DisplayPrice => RequiresQuote ? "Get Quote" : $"GH₵ {Price:N2}";

        public string ActionButtonText => Type == StoreItemType.Product
            ? "Add to Cart"
            : (RequiresQuote ? "Request Quote" : "Book Now");

        public string TypeBadge => Type == StoreItemType.Product ? "PRODUCT" : "SERVICE";
    }

    public class CartItem
    {
        public Guid Id { get; set; }
        public StoreItem Item { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => Item.Price * Quantity;
    }

    public class ServiceBooking
    {
        public Guid Id { get; set; }
        public StoreItem Service { get; set; }
        public DateTime PreferredDate { get; set; }
        public string Notes { get; set; }
        public string Status { get; set; }
    }
}