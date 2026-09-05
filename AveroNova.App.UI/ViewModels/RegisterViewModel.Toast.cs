using AveroNova.App.UI.Services;

namespace AveroNova.App.UI.ViewModels;

public partial class RegisterViewModel
{
    partial void OnGeneralErrorChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _ = AppToast.ErrorAsync(value);
    }

    partial void OnGeneralSuccessChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _ = AppToast.SuccessAsync(value);
    }
}
