using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    [QueryProperty(nameof(ProposalId), "proposalId")]
    [QueryProperty(nameof(UserFeedId), "userFeedId")]
    [QueryProperty(nameof(FeedTitle), "feedTitle")]
    [QueryProperty(nameof(PrefilledWorkmanship), "prefilledWorkmanship")]
    public partial class CreateInvoicePage : ContentPage
    {
        private readonly CreateInvoicePageModel _pm;
        private readonly IStoreService _storeService;

        // ══════════════════════════════════════════════════════════════
        // ▌ SHELL QUERY PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public string ProposalId
        {
            set => _pm.ProposalId = value;
        }

        public string UserFeedId
        {
            set => _pm.UserFeedId = value;
        }

        public string FeedTitle
        {
            set
            {
                _pm.FeedTitle = value;

                if (FeedTitleLabel is null) return;

                var display = string.IsNullOrWhiteSpace(value) ? string.Empty : value;

                FeedTitleLabel.Text = display;
                FeedTitleStrip.IsVisible = !string.IsNullOrEmpty(display);
            }
        }

        public string PrefilledWorkmanship
        {
            set
            {
                if (decimal.TryParse(value, out var w))
                    _pm.PrefilledWorkmanship = w;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public CreateInvoicePage(InvoiceService invoiceService, IStoreService storeService)
        {
            InitializeComponent();
            _storeService = storeService;
            _pm = new CreateInvoicePageModel(invoiceService);
            BindingContext = _pm;

            _pm.AddLineItemRequested += OnAddLineItemRequested;
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ LIFECYCLE
        // ══════════════════════════════════════════════════════════════

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _pm.Initialize();
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ ADD PRODUCT
        //
        // The "Add Product" tap (Border + inner Grid both carry the
        // TapGestureRecognizer in the XAML, so either touch area
        // fires this handler).
        // ══════════════════════════════════════════════════════════════

        private void OnAddProductClicked(object sender, EventArgs e) =>
            _pm.AddLineItemCommand.Execute(null);

        private async void OnAddLineItemRequested(object? sender, EventArgs e)
        {
            try
            {
                var productsResult = await _storeService.GetProductsAsync(pageSize: 50);
                var products = productsResult.Items
                    .Where(p => p.Type == Models.StoreItemType.Product)
                    .OrderBy(p => p.Name)
                    .ToList();

                if (!products.Any())
                {
                    await DisplayAlert("No Products",
                        "No products are available in the catalogue.", "OK");
                    return;
                }

                var options = products
                    .Select(p => $"{p.Name}  ₵{p.Price:N2}")
                    .ToArray();

                var chosen = await DisplayActionSheet(
                    "Select a Product", "Cancel", null, options);

                if (chosen == null || chosen == "Cancel") return;

                var idx = Array.IndexOf(options, chosen);
                var product = products[idx];

                _pm.AddLineItem(product.ApiProductId, product.Name, product.Price);

                Debug.WriteLine(
                    $"[CREATE INVOICE PAGE] Product picked: {product.Name} id={product.ApiProductId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CREATE INVOICE PAGE] ❌ Add product error: {ex.Message}");
                await DisplayAlert("Error", "Could not load products. Please try again.", "OK");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // ▌ NAVIGATION
        //
        // Both the header back-chevron and the footer "Back" button
        // call this single handler.
        // ══════════════════════════════════════════════════════════════

        private async void OnBackClicked(object sender, EventArgs e) =>
            await Navigation.PopAsync();
    }
}