using System.Windows.Input;

namespace AveroNova.App.UI.Controls.Buttons;

public partial class AppButton : ContentView
{
	public AppButton()
	{
		InitializeComponent();
	}

    // Text

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(AppButton),
            "");

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    // Background Color

    public static readonly BindableProperty ButtonColorProperty =
        BindableProperty.Create(
            nameof(ButtonColor),
            typeof(Color),
            typeof(AppButton),
            Colors.Blue);

    public Color ButtonColor
    {
        get => (Color)GetValue(ButtonColorProperty);
        set => SetValue(ButtonColorProperty, value);
    }

    // Text Color

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(
            nameof(TextColor),
            typeof(Color),
            typeof(AppButton),
            Colors.White);

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    // Command

    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(ICommand),
            typeof(AppButton));

    public ICommand Command
    {
        get => (ICommand)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}