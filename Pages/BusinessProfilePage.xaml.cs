using Microsoft.Maui.Controls;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// Artisan-only page for advanced / legal fields:
    ///   - License number
    ///   - Certification
    ///   - Business registration
    ///   - Tax ID / TIN
    ///   - Insurance details
    ///   - Verification status (read-only)
    ///
    /// Loads:  GET api/ProfilesApi/MyProfile
    /// Saves:  PUT api/ProfilesApi  (merges with existing data)
    /// </summary>
    public partial class BusinessProfilePage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly ApiConfig _apiConfig;

        // Preserve fields we're not editing so PUT doesn't wipe them
        private string _originalEmail;
        private string _originalPhone;
        private JsonElement _originalUserProfile;
        private JsonElement _originalArtisanProfile;
        private bool _hasOriginal;

        public BusinessProfilePage(AuthService authService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _authService = authService;
            _apiConfig = apiConfig;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataAsync();
        }

        // ── Load ──────────────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            try
            {
                PageLoadingIndicator.IsRunning = true;
                PageLoadingIndicator.IsVisible = true;

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi/MyProfile";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Error", "Could not load business profile.", "OK");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement.Clone();
                _hasOriginal = true;

                _originalEmail = root.TryGetProperty("email", out var em) ? em.GetString() : "";
                _originalPhone = root.TryGetProperty("phoneNumber", out var ph) ? ph.GetString() : "";

                if (root.TryGetProperty("userProfile", out var up))
                    _originalUserProfile = up.Clone();

                if (root.TryGetProperty("artisanProfile", out var ap) &&
                    ap.ValueKind != JsonValueKind.Null)
                {
                    _originalArtisanProfile = ap.Clone();
                    PopulateFields(ap);
                    PopulateVerificationCard(ap);
                }

                VerificationCard.IsVisible = true;
                LegalSection.IsVisible     = true;
                InfoBanner.IsVisible       = true;
                SaveButton.IsVisible       = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BUSINESS PROFILE] Load error: {ex.Message}");
                await DisplayAlert("Error", "Failed to load business profile.", "OK");
            }
            finally
            {
                PageLoadingIndicator.IsRunning = false;
                PageLoadingIndicator.IsVisible = false;
            }
        }

        // ── Field population ──────────────────────────────────────────

        private void PopulateFields(JsonElement ap)
        {
            LicenseEntry.Text       = ap.TryGetProperty("licenseNumber",        out var ln)  ? ln.GetString()  : "";
            CertificationEditor.Text = ap.TryGetProperty("certification",       out var ce)  ? ce.GetString()  : "";
            BusinessRegEntry.Text   = ap.TryGetProperty("businessRegistration", out var br)  ? br.GetString()  : "";
            TaxIdEntry.Text         = ap.TryGetProperty("taxId",                out var ti)  ? ti.GetString()  : "";
            InsuranceEditor.Text    = ap.TryGetProperty("insuranceDetails",     out var ins) ? ins.GetString() : "";
        }

        private void PopulateVerificationCard(JsonElement ap)
        {
            var isVerified = ap.TryGetProperty("isVerified", out var iv) && iv.GetBoolean();
            var verifiedDate = ap.TryGetProperty("verifiedDate", out var vd) && vd.ValueKind != JsonValueKind.Null
                ? vd.GetDateTime()
                : (DateTime?)null;

            VerificationStatusLabel.Text = isVerified ? "Verified" : "Unverified";
            VerificationBadge.BackgroundColor = isVerified
                ? Color.FromArgb("#10B981")
                : Color.FromArgb("#F59E0B");

            VerifiedDateLabel.Text = isVerified && verifiedDate.HasValue
                ? $"Verified on {verifiedDate.Value:MMM d, yyyy}"
                : "Submit your details to request verification";
        }

        // ── Save ──────────────────────────────────────────────────────

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                SaveIndicator.IsRunning = true;
                SaveIndicator.IsVisible = true;

                // Build merged artisan payload — preserve all existing fields, only overwrite legal ones
                var mergedArtisan = BuildMergedArtisanPayload();

                // Rebuild userProfile from original (unchanged)
                var userProfilePayload = BuildUserProfileFromOriginal();

                var payload = new
                {
                    email          = _originalEmail,
                    phoneNumber    = _originalPhone,
                    userProfile    = userProfilePayload,
                    artisanProfile = mergedArtisan
                };

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var body = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi";
                var response = await client.PutAsync(url, body);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Success", "Business profile updated!", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    var err = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[BUSINESS PROFILE] Save failed: {err}");
                    await DisplayAlert("Error", "Failed to save changes.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BUSINESS PROFILE] Save error: {ex.Message}");
                await DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveIndicator.IsRunning = false;
                SaveIndicator.IsVisible = false;
            }
        }

        // ── Payload builders ──────────────────────────────────────────

        /// <summary>
        /// Merges the current (unchanged) artisan fields with the edited legal fields.
        /// </summary>
        private object BuildMergedArtisanPayload()
        {
            // Pull existing non-legal fields from original
            string businessName      = null, specialization  = null, artisanSpeciality = null;
            string availabilityStatus = null, experienceLevel = null, about = null;
            string professionalBio   = null, servicesOffered = null, businessAddress   = null;
            decimal? hourlyRate      = null;
            int yearsOfExperience    = 1;
            int? serviceRadius       = null;

            if (_hasOriginal && _originalArtisanProfile.ValueKind == JsonValueKind.Object)
            {
                var ap = _originalArtisanProfile;
                businessName       = ap.TryGetProperty("businessName",       out var bn)  ? bn.GetString()   : "";
                specialization     = ap.TryGetProperty("specialization",      out var sp)  ? sp.GetString()   : "";
                artisanSpeciality  = ap.TryGetProperty("artisanSpeciality",   out var asf) ? asf.GetString()  : null;
                availabilityStatus = ap.TryGetProperty("availabilityStatus",  out var avs) ? avs.GetString()  : "AVAILABLE";
                experienceLevel    = ap.TryGetProperty("experienceLevel",     out var el)  ? el.GetString()   : "BEGINNER";
                about              = ap.TryGetProperty("about",               out var ab)  ? ab.GetString()   : null;
                professionalBio    = ap.TryGetProperty("professionalBio",     out var pb)  ? pb.GetString()   : null;
                servicesOffered    = ap.TryGetProperty("servicesOffered",     out var svc) ? svc.GetString()  : "General Services";
                businessAddress    = ap.TryGetProperty("businessAddress",     out var ba)  ? ba.GetString()   : null;

                if (ap.TryGetProperty("yearsOfExperience", out var yr))
                    yearsOfExperience = yr.GetInt32();
                if (ap.TryGetProperty("hourlyRate",   out var hr) && hr.ValueKind != JsonValueKind.Null)
                    hourlyRate = hr.GetDecimal();
                if (ap.TryGetProperty("serviceRadius", out var sr) && sr.ValueKind != JsonValueKind.Null)
                    serviceRadius = sr.GetInt32();
            }

            return new
            {
                businessName,
                specialization,
                artisanSpeciality,
                availabilityStatus,
                experienceLevel,
                about,
                professionalBio,
                servicesOffered,
                businessAddress,
                hourlyRate,
                yearsOfExperience,
                serviceRadius,
                // Edited legal fields:
                licenseNumber        = LicenseEntry.Text?.Trim(),
                certification        = CertificationEditor.Text?.Trim(),
                businessRegistration = BusinessRegEntry.Text?.Trim(),
                taxId                = TaxIdEntry.Text?.Trim(),
                insuranceDetails     = InsuranceEditor.Text?.Trim()
            };
        }

        private object BuildUserProfileFromOriginal()
        {
            if (!_hasOriginal || _originalUserProfile.ValueKind != JsonValueKind.Object)
                return null;

            var up = _originalUserProfile;
            return new
            {
                fullName          = up.TryGetProperty("fullName",          out var fn)  ? fn.GetString()  : null,
                bio               = up.TryGetProperty("bio",               out var b)   ? b.GetString()   : null,
                address           = up.TryGetProperty("address",           out var a)   ? a.GetString()   : null,
                city              = up.TryGetProperty("city",              out var c)   ? c.GetString()   : null,
                state             = up.TryGetProperty("state",             out var s)   ? s.GetString()   : null,
                country           = up.TryGetProperty("country",           out var co)  ? co.GetString()  : null,
                postalCode        = up.TryGetProperty("postalCode",        out var pc)  ? pc.GetString()  : null,
                profilePictureUrl = up.TryGetProperty("profilePictureUrl", out var pu)  ? pu.GetString()  : null,
                preferredLanguage = up.TryGetProperty("preferredLanguage", out var pl)  ? pl.GetString()  : null,
                timezone          = up.TryGetProperty("timezone",          out var tz)  ? tz.GetString()  : null
            };
        }

        // ── Navigation ────────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
