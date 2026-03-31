using CraftConnect_Mobile_App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CraftConnect_Mobile_App.PageModels
{
    // ══════════════════════════════════════════════════════════════════
    // CREATE INVOICE PAGE MODEL
    //
    // Receives ProposalId, UserFeedId, FeedTitle, PrefilledWorkmanship
    // from CreateProposalPage via Shell navigation params.
    //
    // Flow:
    //   AddProduct → build line items → set workmanship/discount/notes
    //   → "Generate & Preview" → POST /api/invoices → POST generate
    //   → navigate to ReviewInvoicePage
    // ══════════════════════════════════════════════════════════════════

    public class CreateInvoicePageModel : INotifyPropertyChanged
    {
        private readonly InvoiceService _invoiceService;

        private string  _workmanship    = "0.00";
        private string  _overallDiscount = string.Empty;
        private string  _notes          = string.Empty;
        private bool    _isBusy;
        private string  _errorMessage   = string.Empty;

        // Passed in from proposal page via Shell query params
        public string?  ProposalId             { get; set; }
        public string?  UserFeedId             { get; set; }
        public string?  FeedTitle              { get; set; }
        public decimal  PrefilledWorkmanship   { get; set; }

        public CreateInvoicePageModel(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;

            AddLineItemCommand    = new Command(OnAddLineItemRequested);
            RemoveLineItemCommand = new Command<InvoiceLineItemEntry>(OnRemoveLineItem);
            GenerateCommand       = new Command(async () => await GenerateAsync(), () => !IsBusy);
        }

        // ══════════════════════════════════════════════════════════════
        // COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand AddLineItemCommand    { get; }
        public ICommand RemoveLineItemCommand { get; }
        public ICommand GenerateCommand       { get; }

        // Raised when the page should open the product picker
        public event EventHandler? AddLineItemRequested;

        // ══════════════════════════════════════════════════════════════
        // LINE ITEMS
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<InvoiceLineItemEntry> LineItems { get; } = new();

        /// <summary>
        /// Call from code-behind after the product picker returns a selection.
        /// </summary>
        public void AddLineItem(int productId, string productName, decimal unitPrice)
        {
            var entry = new InvoiceLineItemEntry
            {
                ProductId   = productId,
                ProductName = productName,
                UnitPrice   = unitPrice,
                Quantity    = 1,
            };
            entry.PropertyChanged += (_, _) => RecalcTotals();
            LineItems.Add(entry);
            RecalcTotals();
            Debug.WriteLine($"[INVOICE PM] Added: {productName} @ ₵{unitPrice:N2}");
        }

        private void OnAddLineItemRequested() =>
            AddLineItemRequested?.Invoke(this, EventArgs.Empty);

        private void OnRemoveLineItem(InvoiceLineItemEntry entry)
        {
            LineItems.Remove(entry);
            RecalcTotals();
        }

        // ══════════════════════════════════════════════════════════════
        // FORM FIELDS
        // ══════════════════════════════════════════════════════════════

        public string Workmanship
        {
            get => _workmanship;
            set { _workmanship = value; OnPropertyChanged(); RecalcTotals(); }
        }

        public string OverallDiscount
        {
            get => _overallDiscount;
            set { _overallDiscount = value; OnPropertyChanged(); RecalcTotals(); }
        }

        public string Notes
        {
            get => _notes;
            set { _notes = value; OnPropertyChanged(); }
        }

        // ══════════════════════════════════════════════════════════════
        // LIVE TOTALS
        // ══════════════════════════════════════════════════════════════

        private decimal _materialCost;
        private decimal _discountAmount;
        private decimal _afterDiscount;
        private decimal _grandTotal;

        public decimal MaterialCost   { get => _materialCost;   private set { _materialCost   = value; OnPropertyChanged(); } }
        public decimal DiscountAmount { get => _discountAmount; private set { _discountAmount = value; OnPropertyChanged(); } }
        public decimal AfterDiscount  { get => _afterDiscount;  private set { _afterDiscount  = value; OnPropertyChanged(); } }
        public decimal GrandTotal     { get => _grandTotal;     private set { _grandTotal     = value; OnPropertyChanged(); } }

        public bool HasOverallDiscount => DiscountAmount > 0;

        private void RecalcTotals()
        {
            var material = LineItems.Sum(li => li.LineTotal);
            var od       = decimal.TryParse(OverallDiscount, out var d) && d > 0 ? d : 0m;
            var odAmt    = material * (od / 100m);
            var after    = material - odAmt;
            var work     = decimal.TryParse(Workmanship, out var w) ? w : 0m;

            MaterialCost   = material;
            DiscountAmount = odAmt;
            AfterDiscount  = after;
            GrandTotal     = after + work;
            OnPropertyChanged(nameof(HasOverallDiscount));
        }

        // ══════════════════════════════════════════════════════════════
        // UI STATE
        // ══════════════════════════════════════════════════════════════

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); ((Command)GenerateCommand).ChangeCanExecute(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        // ══════════════════════════════════════════════════════════════
        // INITIALIZE  —  call from OnAppearing
        // ══════════════════════════════════════════════════════════════

        public void Initialize()
        {
            Workmanship = PrefilledWorkmanship > 0
                ? PrefilledWorkmanship.ToString("F2")
                : "0.00";

            RecalcTotals();
            Debug.WriteLine($"[INVOICE PM] Initialized. Workmanship={Workmanship} FeedId={UserFeedId} ProposalId={ProposalId}");
        }

        // ══════════════════════════════════════════════════════════════
        // VALIDATION
        // ══════════════════════════════════════════════════════════════

        public string? Validate()
        {
            if (!LineItems.Any())
                return "Please add at least one product.";

            if (!decimal.TryParse(Workmanship, out var w) || w < 0)
                return "Please enter a valid workmanship amount.";

            if (!string.IsNullOrEmpty(OverallDiscount))
                if (!decimal.TryParse(OverallDiscount, out var od) || od < 0 || od > 100)
                    return "Overall discount must be between 0 and 100.";

            return null;
        }

        // ══════════════════════════════════════════════════════════════
        // GENERATE — create invoice → generate PDF → navigate to Review
        // ══════════════════════════════════════════════════════════════

        private async Task GenerateAsync()
        {
            if (IsBusy) return;
            ErrorMessage = string.Empty;

            var error = Validate();
            if (error != null) { ErrorMessage = error; return; }

            IsBusy = true;
            try
            {
                // 1. Create invoice record
                var request = new CreateInvoiceRequest
                {
                    UserFeedId = string.IsNullOrWhiteSpace(UserFeedId) ? null : UserFeedId,
                    ArtisanProposalId = string.IsNullOrWhiteSpace(ProposalId) ? null : ProposalId,  // ← fix
                    Workmanship = decimal.Parse(Workmanship),
                    OverallDiscountPercent = string.IsNullOrEmpty(OverallDiscount) ? null
        : decimal.TryParse(OverallDiscount, out var od) && od > 0 ? od : null,
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    LineItems = LineItems.Select(li => new InvoiceLineItemRequest
                    {
                        ProductId = li.ProductId,
                        Quantity = li.Quantity,
                        DiscountPercent = li.DiscountPercent > 0 ? li.DiscountPercent : null,
                    }).ToList()
                };

                Debug.WriteLine($"[INVOICE PM] Creating invoice — {request.LineItems.Count} items");
                var createResult = await _invoiceService.CreateInvoiceAsync(request);

                if (!createResult.Success)
                {
                    ErrorMessage = createResult.Error ?? "Failed to create invoice.";
                    return;
                }

                var invoiceId = createResult.Invoice!.Id;
                Debug.WriteLine($"[INVOICE PM] Invoice created: {invoiceId}");

                // 2. Generate PDF on server (non-fatal if it fails)
                var genResult = await _invoiceService.GeneratePdfAsync(invoiceId);
                if (!genResult.Success)
                    Debug.WriteLine($"[INVOICE PM] ⚠️ PDF generation warning: {genResult.Error}");

                // 3. Navigate to Review page
                var navParams = new Dictionary<string, object>
                {
                    { "invoiceId",  invoiceId },
                    { "feedTitle",  FeedTitle ?? string.Empty },
                    { "proposalId", ProposalId ?? string.Empty },
                };

                await Shell.Current.GoToAsync("ReviewInvoicePage", navParams);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICE PM] ❌ {ex.GetType().FullName}: {ex.Message}");
                ErrorMessage = $"Unexpected error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // INotifyPropertyChanged
        // ══════════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ══════════════════════════════════════════════════════════════════
    // LINE ITEM ENTRY — observable row in the invoice builder
    // ══════════════════════════════════════════════════════════════════

    public class InvoiceLineItemEntry : INotifyPropertyChanged
    {
        private int     _quantity        = 1;
        private decimal _discountPercent = 0;

        public int     ProductId   { get; set; }
        public string  ProductName { get; set; } = string.Empty;
        public decimal UnitPrice   { get; set; }

        public int Quantity
        {
            get => _quantity;
            set
            {
                _quantity = value < 1 ? 1 : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LineSubtotal));
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                _discountPercent = value < 0 ? 0 : value > 100 ? 100 : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(HasDiscount));
            }
        }

        public decimal LineSubtotal   => UnitPrice * Quantity;
        public decimal DiscountAmount => LineSubtotal * (DiscountPercent / 100m);
        public decimal LineTotal      => LineSubtotal - DiscountAmount;
        public bool    HasDiscount    => DiscountPercent > 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
