using System.Collections.ObjectModel;
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

    partial void OnPlanOptionsChanged(ObservableCollection<RegisterPlanOption> value)
    {
        if (SelectedPlanOption is not null || value.Count == 0)
            return;

        var starter = value.FirstOrDefault(x =>
            x.IsAvailable && string.Equals(x.Plan.Id, "starter", StringComparison.OrdinalIgnoreCase));

        if (starter is not null)
            ApplyPlanSelection(starter);
    }
}
