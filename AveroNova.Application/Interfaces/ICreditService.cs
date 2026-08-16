using System;
using System.Collections.Generic;
using System.Text;

namespace AveroNova.Application.Interfaces
{
    public interface ICreditService
    {
        Task<int> GetCreditLimitAsync(Guid companyId);
        Task<int> GetCreditsUsedAsync(Guid companyId);
        Task<int> GetRemainingCreditsAsync(Guid companyId);
        Task<bool> CanConsumeCreditsAsync(Guid companyId, int creditsToConsume);
        Task<(bool Success, string? Error)> ConsumeCreditsAsync(Guid companyId, int creditsToConsume);
        Task<(bool Success, string? Error)> RefundCreditsAsync(Guid companyId, int creditsToRefund);
    }
}
