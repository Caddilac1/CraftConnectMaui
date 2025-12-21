using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class SendProposalPage : ContentPage
    {
        private readonly UserFeedDto _feed;
        private Entry _priceEntry;
        private Editor _proposalEditor;
        private DatePicker _deliveryDatePicker;

        public SendProposalPage(UserFeedDto feed)
        {
            _feed = feed;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Title = "Send Proposal";
            BackgroundColor = Color.FromArgb("#F0F2F5");

            var scrollView = new ScrollView();
            var mainLayout = new VerticalStackLayout { Spacing = 16, Padding = 16 };

            // Feed Info Card
            var feedInfoCard = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 12,
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = _feed.Title,
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.Black
                        },
                        new Label
                        {
                            Text = $"{_feed.JobCategory} • {_feed.Location}",
                            FontSize = 14,
                            TextColor = Color.FromArgb("#667781")
                        }
                    }
                }
            };

            mainLayout.Add(feedInfoCard);

            // Proposal Form
            mainLayout.Add(new Label
            {
                Text = "Your Proposal",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black,
                Margin = new Thickness(0, 16, 0, 0)
            });

            // Price Input
            var priceCard = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 12,
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = "Your Price (GHS)",
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.Black
                        },
                        (_priceEntry = new Entry
                        {
                            Placeholder = "Enter your price",
                            Keyboard = Keyboard.Numeric,
                            BackgroundColor = Color.FromArgb("#F0F2F5"),
                            TextColor = Colors.Black
                        })
                    }
                }
            };
            mainLayout.Add(priceCard);

            // Delivery Date
            var dateCard = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 12,
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = "Expected Delivery Date",
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.Black
                        },
                        (_deliveryDatePicker = new DatePicker
                        {
                            MinimumDate = DateTime.Today,
                            Date = DateTime.Today.AddDays(7),
                            BackgroundColor = Color.FromArgb("#F0F2F5"),
                            TextColor = Colors.Black
                        })
                    }
                }
            };
            mainLayout.Add(dateCard);

            // Proposal Description
            var descriptionCard = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 12,
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label
                        {
                            Text = "Proposal Details",
                            FontSize = 14,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.Black
                        },
                        new Label
                        {
                            Text = "Describe your approach, experience, and why you're the best fit for this project.",
                            FontSize = 12,
                            TextColor = Color.FromArgb("#667781")
                        },
                        (_proposalEditor = new Editor
                        {
                            Placeholder = "Write your proposal here...",
                            HeightRequest = 200,
                            BackgroundColor = Color.FromArgb("#F0F2F5"),
                            TextColor = Colors.Black
                        })
                    }
                }
            };
            mainLayout.Add(descriptionCard);

            // Submit Button
            var submitButton = new Button
            {
                Text = "Submit Proposal",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#25D366"),
                TextColor = Colors.White,
                HeightRequest = 56,
                CornerRadius = 12,
                Margin = new Thickness(0, 16, 0, 32)
            };
            submitButton.Clicked += OnSubmitClicked;

            mainLayout.Add(submitButton);

            scrollView.Content = mainLayout;
            Content = scrollView;
        }

        private async void OnSubmitClicked(object sender, EventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(_priceEntry.Text))
            {
                await DisplayAlert("Error", "Please enter your price", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_proposalEditor.Text))
            {
                await DisplayAlert("Error", "Please write your proposal", "OK");
                return;
            }

            if (!decimal.TryParse(_priceEntry.Text, out decimal price) || price <= 0)
            {
                await DisplayAlert("Error", "Please enter a valid price", "OK");
                return;
            }

            // Show confirmation
            var confirm = await DisplayAlert(
                "Confirm Submission",
                $"Send proposal for GHS {price:N2}?",
                "Yes",
                "No"
            );

            if (confirm)
            {
                try
                {
                    // TODO: Call API to submit proposal
                    // await _proposalService.SubmitProposalAsync(new ProposalDto
                    // {
                    //     FeedId = _feed.Id,
                    //     Price = price,
                    //     DeliveryDate = _deliveryDatePicker.Date,
                    //     Description = _proposalEditor.Text
                    // });

                    await DisplayAlert(
                        "Success",
                        "Your proposal has been submitted successfully!",
                        "OK"
                    );

                    await Navigation.PopAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to submit proposal: {ex.Message}", "OK");
                }
            }
        }
    }
}