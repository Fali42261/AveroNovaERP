using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Views.Layout;

public partial class MainLayoutView
{
    private bool _productUxConnectivityHooked;

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is not null)
        {
            HideOfflineBanners();

            if (!_productUxConnectivityHooked)
            {
                _connectivity.StatusChanged += OnProductUxConnectivityChanged;
                _productUxConnectivityHooked = true;
            }
        }
        else if (_productUxConnectivityHooked)
        {
            _connectivity.StatusChanged -= OnProductUxConnectivityChanged;
            _productUxConnectivityHooked = false;
        }
    }

    private void OnProductUxConnectivityChanged(object? sender, ConnectivityStatus status)
    {
        MainThread.BeginInvokeOnMainThread(HideOfflineBanners);
    }

    private void HideOfflineBanners()
    {
        OfflineBanner.IsVisible = false;
        MOfflineBanner.IsVisible = false;
    }
}
