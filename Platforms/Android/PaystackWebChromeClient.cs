using Android.Webkit;

namespace CraftConnect_Mobile_App
{
    public class PaystackWebChromeClient : WebChromeClient
    {
        public override bool OnCreateWindow(
            Android.Webkit.WebView? view,
            bool isDialog,
            bool isUserGesture,
            Android.OS.Message? resultMsg)
        {
            if (resultMsg?.Obj is Android.Webkit.WebView.WebViewTransport transport)
            {
                var newWebView = new Android.Webkit.WebView(view!.Context!);
                newWebView.Settings.JavaScriptEnabled = true;
                transport.WebView = newWebView;
                resultMsg.SendToTarget();
                return true;
            }
            return false;
        }
    }
}