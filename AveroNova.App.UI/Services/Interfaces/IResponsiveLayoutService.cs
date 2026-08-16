using AveroNova.App.UI.Layout;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IResponsiveLayoutService
{
    ScreenCategory Category { get; }
    double Width { get; }
    double Height { get; }

    bool IsCompact { get; }
    bool IsMedium { get; }
    bool IsExpanded { get; }

    /// <summary>Two-pane branding / sidebar layouts.</summary>
    bool UseTwoPane { get; }

    /// <summary>Desktop table presentation for ERP lists.</summary>
    bool UseTableLayout { get; }

    /// <summary>Card/list presentation for compact and medium widths.</summary>
    bool UseCardLayout { get; }

    event EventHandler? LayoutChanged;

    void Update(double width, double height);
}
