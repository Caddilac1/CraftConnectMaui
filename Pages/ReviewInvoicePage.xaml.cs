using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Services;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    [QueryProperty(nameof(InvoiceId),  "invoiceId")]
    [QueryProperty(nameof(FeedTitle),  "feedTitle")]
    [QueryProperty(nameof(ProposalId), "proposalId")]
    public partial class ReviewInvoicePage : ContentPage
    {
        private readonly ReviewInvoicePageModel _pm;

        // ── Query properties set by Shell navigation ──────────────────

        public string InvoiceId
        {
            set
            {
                _pm.InvoiceId = value;
                Debug.WriteLine($"[REVIEW PAGE] InvoiceId set: {value}");
            }
        }

        public string FeedTitle
        {
            set => _pm.FeedTitle = value;
        }

        public string ProposalId
        {
            set => _pm.ProposalId = value;
        }

        // ── Constructor ───────────────────────────────────────────────

        public ReviewInvoicePage(InvoiceService invoiceService)
        {
            InitializeComponent();
            _pm            = new ReviewInvoicePageModel(invoiceService);
            BindingContext = _pm;
        }

        // ── Lifecycle ─────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _pm.LoadAsync();
        }

        // ── Event handlers ────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e) =>
            await Navigation.PopAsync();
    }
}
