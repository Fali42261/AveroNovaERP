using System.ComponentModel;
using AveroNova.App.UI.Layout;
using AveroNova.App.UI.Services.Interfaces;

namespace AveroNova.App.UI.Services;

public sealed class ResponsiveLayoutService : IResponsiveLayoutService, INotifyPropertyChanged
{
    private ScreenCategory _category = ScreenCategory.Expanded;
    private double _width;
    private double _height;

    public ScreenCategory Category => _category;
    public double Width => _width;
    public double Height => _height;

    public bool IsCompact => _category == ScreenCategory.Compact;
    public bool IsMedium => _category == ScreenCategory.Medium;
    public bool IsExpanded => _category == ScreenCategory.Expanded;
    public bool UseTwoPane => _category == ScreenCategory.Expanded;
    public bool UseTableLayout => _category == ScreenCategory.Expanded;
    public bool UseCardLayout => _category != ScreenCategory.Expanded;

    public event EventHandler? LayoutChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(double width, double height)
    {
        if (width <= 0)
            return;

        var next = ResponsiveBreakpoints.FromWidth(width);
        var changed = Math.Abs(_width - width) > 0.5
                      || Math.Abs(_height - height) > 0.5
                      || next != _category;

        _width = width;
        _height = height;

        if (!changed)
            return;

        _category = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }
}
