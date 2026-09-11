namespace AveroNova.App.UI.Services.Interfaces;

public interface ISessionInactivityService
{
    TimeSpan Timeout { get; }
    void Start();
    void RecordActivity();
    void Track(View root);
    Task CheckNowAsync();
}
