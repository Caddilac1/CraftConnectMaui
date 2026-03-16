using System.Diagnostics;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.Pages
{
    [QueryProperty(nameof(Email), "email")]
    [QueryProperty(nameof(AmountPesewas), "amount")]
    [QueryProperty(nameof(Reference), "reference")]
    [QueryProperty(nameof(DeliveryAddress), "deliveryAddress")]
    [QueryProperty(nameof(DeliveryInstructions), "deliveryInstructions")]
    [QueryProperty(nameof(PaymentMethod), "paymentMethod")]
    public partial class PaystackWebViewPage : ContentPage
    {
        public string Email { get; set; } = string.Empty;
        public string AmountPesewas { get; set; } = "0";
        public string Reference { get; set; } = string.Empty;
        public string DeliveryAddress { get; set; } = string.Empty;
        public string DeliveryInstructions { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "paystack";

        private const string CallbackUrl = "https://craftconnect.app/paystack-callback";
        private const string CancelUrl = "https://craftconnect.app/paystack-cancel";
        private const string PublicKey = "pk_test_fa1586c219a357c196ebe3f654f3ff9f15c311d8";

        private bool _paymentHandled = false;

        public PaystackWebViewPage()
        {
            InitializeComponent();

#if ANDROID
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("PaystackWebView", (handler, view) =>
            {
                handler.PlatformView.Settings.JavaScriptEnabled = true;
                handler.PlatformView.Settings.JavaScriptCanOpenWindowsAutomatically = true;
                handler.PlatformView.Settings.SetSupportMultipleWindows(true);
                handler.PlatformView.Settings.DomStorageEnabled = true;
                handler.PlatformView.Settings.AllowContentAccess = true;
                handler.PlatformView.Settings.LoadsImagesAutomatically = true;
                handler.PlatformView.SetWebChromeClient(new PaystackWebChromeClient());
            });
#endif
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = InitializeAndLoadAsync();
        }

        private async Task InitializeAndLoadAsync()
        {
            try
            {
                if (LoadingOverlay != null)
                    LoadingOverlay.IsVisible = true;

                Debug.WriteLine("=== PAYSTACK INIT (Inline HTML) ===");
                Debug.WriteLine($"Email:   '{Email}'");
                Debug.WriteLine($"Amount:  '{AmountPesewas}'");
                Debug.WriteLine($"Ref:     '{Reference}'");

                if (string.IsNullOrEmpty(Email))
                {
                    await ShowErrorAndGoBack("No email address found. Please log out and log in again.");
                    return;
                }

                if (!int.TryParse(AmountPesewas, out int amount) || amount <= 0)
                {
                    await ShowErrorAndGoBack("Invalid payment amount.");
                    return;
                }

                // Inline HTML — no server needed, no SSL issues.
                // The Paystack script loads from js.paystack.co (valid cert).
                // Key fix: use script onload to guarantee PaystackPop is ready
                // before calling setup — avoids race condition with async loading.
                var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Payment</title>
    <style>
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            font-family: Arial, sans-serif;
            background: #f8f9fa;
        }}
        .container {{
            text-align: center;
            padding: 30px 20px;
            max-width: 340px;
            width: 100%;
        }}
        .spinner {{
            width: 48px; height: 48px;
            border: 5px solid #eee;
            border-top-color: #ff4500;
            border-radius: 50%;
            animation: spin 0.8s linear infinite;
            margin: 0 auto 20px;
        }}
        @keyframes spin {{ to {{ transform: rotate(360deg); }} }}
        #msg {{ color: #555; font-size: 16px; margin-bottom: 20px; }}
        #payBtn {{
            display: none;
            padding: 14px 28px;
            background: #ff4500;
            color: white;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            cursor: pointer;
            width: 100%;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='spinner' id='spinner'></div>
        <p id='msg'>Loading payment...</p>
        <button id='payBtn' onclick='startPayment()'>Tap to Pay</button>
    </div>

    <script>
        // Load Paystack script dynamically so we know exactly when it's ready
        function loadPaystackScript() {{
            document.getElementById('msg').textContent = 'Connecting to Paystack...';
            var s = document.createElement('script');
            s.src = 'https://js.paystack.co/v1/inline.js';
            s.onload = function() {{
                document.getElementById('msg').textContent = 'Starting payment...';
                // Small delay to let Paystack fully initialize
                setTimeout(startPayment, 300);
            }};
            s.onerror = function() {{
                document.getElementById('spinner').style.display = 'none';
                document.getElementById('msg').textContent = 'Failed to load payment. Check internet and tap retry.';
                document.getElementById('payBtn').textContent = 'Retry';
                document.getElementById('payBtn').style.display = 'block';
            }};
            document.head.appendChild(s);
        }}

        function startPayment() {{
            try {{
                if (typeof PaystackPop === 'undefined') {{
                    document.getElementById('spinner').style.display = 'none';
                    document.getElementById('msg').textContent = 'Payment not ready. Tap to try again.';
                    document.getElementById('payBtn').style.display = 'block';
                    return;
                }}

                document.getElementById('spinner').style.display = 'none';
                document.getElementById('msg').textContent = 'Opening payment...';

                var handler = PaystackPop.setup({{
                    key: '{PublicKey}',
                    email: '{Email}',
                    amount: {amount},
                    currency: 'GHS',
                    ref: '{Reference}',
                    callback: function(response) {{
                        window.location.href = 'https://craftconnect.app/paystack-callback?reference=' + response.reference;
                    }},
                    onClose: function() {{
                        window.location.href = 'https://craftconnect.app/paystack-cancel';
                    }}
                }});
                handler.openIframe();
            }} catch(e) {{
                document.getElementById('spinner').style.display = 'none';
                document.getElementById('msg').textContent = 'Error: ' + e.message;
                document.getElementById('payBtn').style.display = 'block';
            }}
        }}

        // Start loading when page is ready
        loadPaystackScript();
    </script>
</body>
</html>";

                Debug.WriteLine("[PAYSTACK] Loading inline HTML");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PaystackWebView.Source = new HtmlWebViewSource
                    {
                        Html = html,
                        BaseUrl = "https://js.paystack.co"
                    };
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PAYSTACK] EXCEPTION: {ex.Message}");
                await ShowErrorAndGoBack($"Could not load payment page: {ex.Message}");
            }
        }

        private void OnNavigating(object sender, WebNavigatingEventArgs e)
        {
            Debug.WriteLine($"[PAYSTACK] Navigating: {e.Url}");

            if (e.Url.StartsWith(CancelUrl, StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                if (!_paymentHandled)
                {
                    _paymentHandled = true;
                    HandleCancelled();
                }
                return;
            }

            if (!e.Url.StartsWith(CallbackUrl, StringComparison.OrdinalIgnoreCase))
                return;

            e.Cancel = true;
            if (_paymentHandled) return;
            _paymentHandled = true;

            var uri = new Uri(e.Url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var reference = query["reference"] ?? query["trxref"];

            if (!string.IsNullOrEmpty(reference))
            {
                Debug.WriteLine($"[PAYSTACK] SUCCESS — ref: {reference}");
                HandleSuccess(reference);
            }
            else
            {
                Debug.WriteLine("[PAYSTACK] CANCELLED — no reference in callback");
                HandleCancelled();
            }
        }

        private void OnNavigated(object sender, WebNavigatedEventArgs e)
        {
            Debug.WriteLine($"[PAYSTACK] OnNavigated: {e.Url} Result: {e.Result}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (LoadingOverlay != null)
                    LoadingOverlay.IsVisible = false;
            });
        }

        private void OnCancelTapped(object sender, EventArgs e) => HandleCancelled();

        private void HandleSuccess(string reference)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                MessagingCenter.Send(this, "PaystackSuccess", reference);
                await Shell.Current.GoToAsync("..");
            });
        }

        private void HandleCancelled()
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Shell.Current.GoToAsync("..");
                var page = Application.Current?.Windows[0].Page;
                if (page != null)
                    await page.DisplayAlert("Payment Cancelled", "No charge was made. You can try again.", "OK");
            });
        }

        private async Task ShowErrorAndGoBack(string message)
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.GoToAsync("..");
                var page = Application.Current?.Windows[0].Page;
                if (page != null)
                    await page.DisplayAlert("Payment Error", message, "OK");
            });
        }
    }
}