namespace AveroNova.App.UI.Helpers;

public static class BusyButton
{
    public static async Task RunAsync(Button button, Func<Task> action, string busyText = "Loading...")
    {
        if (!button.IsEnabled)
            return;

        var originalText = button.Text;
        button.IsEnabled = false;
        button.Text = busyText;

        try
        {
            await action();
        }
        finally
        {
            button.Text = originalText;
            button.IsEnabled = true;
        }
    }
}
