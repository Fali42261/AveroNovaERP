using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

public interface IReportingService
{
    Task<(FinancialReportSummary? Summary, string? Error)> GetSummaryAsync(
        Guid companyId,
        ReportPeriod period,
        CancellationToken cancellationToken = default);
}

