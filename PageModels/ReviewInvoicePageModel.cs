using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CraftConnect_Mobile_App.PageModels
{
    public class ReviewInvoicePageModel : INotifyPropertyChanged
    {
        private readonly InvoiceService _invoiceService;

        // ── Nav params ────────────────────────────────────────────────
        public string InvoiceId { get; set; } = string.Empty;
        public string FeedTitle { get; set; } = string.Empty;
        public string ProposalId { get; set; } = string.Empty;

        // ── Backing fields ────────────────────────────────────────────
        private InvoiceDetailDto? _invoice;   // ← was InvoiceDto
        private bool _isLoading;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;

        public ReviewInvoicePageModel(InvoiceService invoiceService)
        {
            _invoiceService = invoiceService;

            SubmitCommand = new Command(async () => await SubmitAsync(), () => !IsBusy);
            SubmitAndDownloadCommand = new Command(async () => await SubmitAndDownloadAsync(), () => !IsBusy);
        }

        // ══════════════════════════════════════════════════════════════
        // COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand SubmitCommand { get; }
        public ICommand SubmitAndDownloadCommand { get; }

        // ══════════════════════════════════════════════════════════════
        // BOUND PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public InvoiceDetailDto? Invoice   // ← was InvoiceDto
        {
            get => _invoice;
            private set
            {
                _invoice = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasInvoice));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(HasNotes));
            }
        }

        public bool HasInvoice => Invoice is not null;
        public bool HasDiscount => Invoice?.OverallDiscountAmount > 0;
        public bool HasNotes => !string.IsNullOrWhiteSpace(Invoice?.Notes);

        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                _isBusy = value;
                OnPropertyChanged();
                ((Command)SubmitCommand).ChangeCanExecute();
                ((Command)SubmitAndDownloadCommand).ChangeCanExecute();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            private set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
        }

        public string SuccessMessage
        {
            get => _successMessage;
            private set { _successMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSuccess)); }
        }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

        // ══════════════════════════════════════════════════════════════
        // LOAD
        // ══════════════════════════════════════════════════════════════

        public async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(InvoiceId))
            {
                ErrorMessage = "No invoice ID provided.";
                return;
            }

            ClearMessages();
            IsLoading = true;

            try
            {
                var result = await _invoiceService.GetInvoiceAsync(InvoiceId);

                if (result.Success && result.Invoice is not null)
                {
                    Invoice = result.Invoice;
                    Debug.WriteLine($"[REVIEW PM] Invoice loaded: {Invoice.InvoiceNumber}");
                }
                else
                {
                    ErrorMessage = result.Error ?? "Failed to load invoice.";
                    Debug.WriteLine($"[REVIEW PM] Load failed: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                Debug.WriteLine($"[REVIEW PM] ❌ {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SUBMIT — submit invoice to client
        // ══════════════════════════════════════════════════════════════

        private async Task SubmitAsync()
        {
            if (IsBusy) return;
            ClearMessages();
            IsBusy = true;

            try
            {
                Debug.WriteLine($"[REVIEW PM] Submitting invoice. InvoiceId={InvoiceId}");

                // ← was SubmitProposalAsync(ProposalId, InvoiceId)
                var result = await _invoiceService.SubmitInvoiceAsync(InvoiceId);

                if (result.Success)
                {
                    SuccessMessage = result.Message ?? "Invoice submitted successfully!";
                    Debug.WriteLine("[REVIEW PM] ✅ Invoice submitted.");

                    await Task.Delay(1500);
                    await Shell.Current.GoToAsync("//UpdatesFeedPage");
                }
                else
                {
                    ErrorMessage = result.Error ?? "Failed to submit invoice.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                Debug.WriteLine($"[REVIEW PM] ❌ {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SUBMIT + DOWNLOAD — submit then save PDF to device
        // ══════════════════════════════════════════════════════════════

        private async Task SubmitAndDownloadAsync()
        {
            if (IsBusy) return;
            ClearMessages();
            IsBusy = true;

            try
            {
                Debug.WriteLine($"[REVIEW PM] Submit+Download. InvoiceId={InvoiceId}");

                // 1. Submit invoice
                // ← was SubmitProposalAsync(ProposalId, InvoiceId)
                var submitResult = await _invoiceService.SubmitInvoiceAsync(InvoiceId);

                if (!submitResult.Success)
                {
                    ErrorMessage = submitResult.Error ?? "Failed to submit invoice.";
                    return;
                }

                SuccessMessage = "Submitted! Downloading PDF…";
                Debug.WriteLine("[REVIEW PM] ✅ Submitted. Downloading PDF…");

                // 2. Download PDF bytes and save to device
                // ← was GetPdfDownloadUrlAsync(InvoiceId) + Browser.OpenAsync
                var downloadResult = await _invoiceService.DownloadPdfAsync(InvoiceId);

                if (downloadResult.Success && downloadResult.PdfBytes is not null)
                {
                    var fileName = downloadResult.FileName ?? $"Invoice-{InvoiceId}.pdf";
                    var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                    await File.WriteAllBytesAsync(filePath, downloadResult.PdfBytes);

                    await Share.Default.RequestAsync(new ShareFileRequest
                    {
                        Title = "Invoice PDF",
                        File = new ShareFile(filePath)
                    });

                    SuccessMessage = "Invoice downloaded successfully!";
                    Debug.WriteLine($"[REVIEW PM] ✅ PDF saved: {filePath}");
                }
                else
                {
                    // Submission succeeded; download failed — non-fatal
                    SuccessMessage = "Proposal submitted! PDF download unavailable right now.";
                    Debug.WriteLine($"[REVIEW PM] ⚠️ PDF download failed: {downloadResult.Error}");
                }

                await Task.Delay(2000);
                await Shell.Current.GoToAsync("//UpdatesFeedPage");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Unexpected error: {ex.Message}";
                Debug.WriteLine($"[REVIEW PM] ❌ {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // HELPERS
        // ══════════════════════════════════════════════════════════════

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
        }

        // ══════════════════════════════════════════════════════════════
        // INotifyPropertyChanged
        // ══════════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}