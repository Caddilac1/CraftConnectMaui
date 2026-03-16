using System.Collections.Generic;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Services
{
    // ══════════════════════════════════════════════════════════════════
    // ICartApiService
    //
    // Security note: implementations must set the JWT token per-request
    // on HttpRequestMessage.Headers — NEVER on DefaultRequestHeaders
    // (that causes race conditions when multiple calls run concurrently).
    // ══════════════════════════════════════════════════════════════════
    public interface ICartApiService
    {
        // Full cart with all items, totals, VAT
        Task<CartDto?> GetCartAsync();

        // Lightweight badge count — call this to refresh the cart icon
        Task<CartCountDto?> GetCartCountAsync();

        // Add a product or combo to the cart
        Task<CartDto?> AddItemAsync(int? productCompanyBusinessLocationId, int? comboProductId, int quantity = 1);

        // Change the quantity of one cart item — returns the updated item
        Task<CartItemDto?> UpdateItemQuantityAsync(int cartItemId, int newQuantity);

        // Remove one item
        Task<bool> RemoveItemAsync(int cartItemId);

        // Clear the entire cart
        Task<bool> ClearCartAsync();

        // Pre-checkout stock + price validation
        Task<CartValidationDto?> ValidateCartAsync();

        // Place the order (cash or after Paystack payment)
        Task<bool> PlaceOrderAsync(
            string  deliveryAddress,
            string  paymentMethod,
            string? deliveryInstructions,
            string? paystackReference);

        // Load the customer's saved delivery addresses
        Task<List<DeliveryAddressOption>> GetDeliveryAddressesAsync();
    }
}
