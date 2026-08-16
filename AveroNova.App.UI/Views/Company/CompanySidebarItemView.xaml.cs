using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace AveroNova.App.UI.Views.Company;

public partial class CompanySidebarItemView : ContentView
{
	public CompanySidebarItemView()
	{
		InitializeComponent();
        UpdateUI();

    }

    // Step Number

    public static readonly BindableProperty StepNumberProperty =
        BindableProperty.Create(nameof(StepNumber), typeof(string), typeof(CompanySidebarItemView), "");

    public string StepNumber
    {
        get => (string)GetValue(StepNumberProperty);
        set => SetValue(StepNumberProperty, value);
    }

    // Title

    public static readonly BindableProperty TitleProperty =
        BindableProperty.Create(nameof(Title), typeof(string), typeof(CompanySidebarItemView), "");

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }


    // Subtitle

    public static readonly BindableProperty SubtitleProperty =
        BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(CompanySidebarItemView), "");

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    // Is Active

    public static readonly BindableProperty IsActiveProperty =
        BindableProperty.Create(
            nameof(IsActive),
            typeof(bool),
            typeof(CompanySidebarItemView),
            false,
            propertyChanged: OnIsActiveChanged);

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }


    private static void OnIsActiveChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (CompanySidebarItemView)bindable;
        control.UpdateUI();
    }


    private void UpdateUI()
    {
        if (ItemBorder == null)
            return;

        if (IsActive)
        {
            ItemBorder.BackgroundColor = Color.FromArgb("#2563EB");
            ItemBorder.Stroke = Color.FromArgb("#3B82F6");

            StepCircle.BackgroundColor = Colors.White;

            StepNumberLabel.TextColor = Color.FromArgb("#2563EB");

            TitleLabel.TextColor = Colors.White;
            SubtitleLabel.TextColor = Color.FromArgb("#D6E4FF");
        }
        else
        {
            ItemBorder.BackgroundColor = Colors.Transparent;
            ItemBorder.Stroke = Colors.Transparent;

            StepCircle.BackgroundColor = Color.FromArgb("#233B73");

            StepNumberLabel.TextColor = Colors.White;

            TitleLabel.TextColor = Colors.White;
            SubtitleLabel.TextColor = Color.FromArgb("#94A3B8");
        }
    }
}