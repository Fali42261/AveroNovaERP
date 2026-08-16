
using Microsoft.Maui.Controls;
using System.Collections;
namespace AveroNova.App.UI.Controls.Inputs;

public partial class AppPicker : ContentView
{
	public AppPicker()
	{
		InitializeComponent();
	}
    // Label

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(
            nameof(Label),
            typeof(string),
            typeof(AppPicker),
            string.Empty);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    // Placeholder

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(AppPicker),
            "Select");

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }
    public static readonly BindableProperty ItemsSourceProperty =
    BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(AppPicker),
        null);

    public IEnumerable ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty =
    BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(AppPicker),
        null,
        BindingMode.TwoWay);

    public object SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty HasErrorProperty =
    BindableProperty.Create(
        nameof(HasError),
        typeof(bool),
        typeof(AppPicker),
        false);

    public bool HasError
    {
        get => (bool)GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    public static readonly BindableProperty ErrorMessageProperty =
        BindableProperty.Create(
            nameof(ErrorMessage),
            typeof(string),
            typeof(AppPicker),
            string.Empty);

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    
}