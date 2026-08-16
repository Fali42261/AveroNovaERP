namespace AveroNova.App.UI.Controls.Inputs;

public partial class AppEditor : ContentView
{
    public AppEditor()
    {
        InitializeComponent();
    }

    #region Label

    public static readonly BindableProperty LabelProperty =
        BindableProperty.Create(
            nameof(Label),
            typeof(string),
            typeof(AppEditor),
            string.Empty);

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    #endregion

    #region Placeholder

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(
            nameof(Placeholder),
            typeof(string),
            typeof(AppEditor),
            string.Empty);

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    #endregion

    #region Text

    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(
            nameof(Text),
            typeof(string),
            typeof(AppEditor),
            string.Empty,
            BindingMode.TwoWay);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion

    #region ReadOnly

    public static readonly BindableProperty IsReadOnlyProperty =
        BindableProperty.Create(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(AppEditor),
            false);

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    #endregion

    #region Error

    public static readonly BindableProperty HasErrorProperty =
        BindableProperty.Create(
            nameof(HasError),
            typeof(bool),
            typeof(AppEditor),
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
            typeof(AppEditor),
            string.Empty);

    public string ErrorMessage
    {
        get => (string)GetValue(ErrorMessageProperty);
        set => SetValue(ErrorMessageProperty, value);
    }

    #endregion
}