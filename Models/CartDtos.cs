using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Services
{
    // ══════════════════════════════════════════════════════════════════
    // These mirror the server's CartApiController response shapes exactly.
    // Keep them in sync if the server DTOs change.
    // ══════════════════════════════════════════════════════════════════

    public class CartDto
    {
        public int               ShoppingCartId { get; set; }
        public List<CartItemDto> Items          { get; set; } = new();
        public int               ItemCount      { get; set; }   // total units
        public int               LineItems      { get; set; }   // distinct rows
        public decimal           Subtotal       { get; set; }
        public decimal           VatRate        { get; set; }   // e.g. 0.15 for 15%
        public decimal           VatAmount      { get; set; }
        public decimal           Total          { get; set; }
        public string?           MergeNote      { get; set; }
    }

    public class CartItemDto
    {
        public int     CartItemId                       { get; set; }
        public int?    ProductCompanyBusinessLocationId { get; set; }
        public int?    ComboProductId                   { get; set; }
        public string  ItemType                         { get; set; } = "";
        public string  Name                             { get; set; } = "";
        public string? ThumbnailUrl                     { get; set; }
        public decimal UnitPrice                        { get; set; }
        public int     Quantity                         { get; set; }
        public decimal TotalPrice                       { get; set; }
        public int?    StockOnHand                      { get; set; }
        public bool    CanIncrement                     { get; set; }
    }

    public class CartCountDto
    {
        public int Count      { get; set; }   // distinct line items
        public int TotalItems { get; set; }   // total units across all lines
    }

    public class CartValidationDto
    {
        public bool                           IsValid     { get; set; }
        public List<string>                   Issues      { get; set; } = new();
        public List<CartItemValidationResult> ItemResults { get; set; } = new();
    }

    public class CartItemValidationResult
    {
        public int     CartItemId    { get; set; }
        public bool    IsAvailable   { get; set; }
        public int?    MaxAvailable  { get; set; }
        public bool    PriceChanged  { get; set; }
        public decimal CurrentPrice  { get; set; }
        public decimal OriginalPrice { get; set; }
        public string? Issue         { get; set; }
    }
}
