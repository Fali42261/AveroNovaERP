namespace AveroNova.App.UI.Helpers;

public static class NetworkStatus
{
    public static bool HasInternet
    {
        get
        {
            var access = Connectivity.Current.NetworkAccess;
            return access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
        }
    }
}
