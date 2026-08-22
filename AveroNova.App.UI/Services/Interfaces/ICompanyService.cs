using AveroNova.App.UI.Models;

namespace AveroNova.App.UI.Services.Interfaces;

// ═══════════════════════════════════════════════════════════════
//  ICompanyService
//
//  ONLINE FLOW:  ViewModel → ICompanyService → API → Server Database
//  OFFLINE FLOW: ViewModel → ICompanyService → Local DB → Sync Queue
//
//  TODO: Connect to AveroNova API during backend phase.
// ═══════════════════════════════════════════════════════════════

public interface ICompanyService
{
    CompanyModel?       CurrentCompany { get; }

    event EventHandler? CurrentCompanyChanged;

    Task<List<CompanyModel>> GetAllAsync();
    Task<CompanyModel?>      GetCurrentAsync();
    Task<CompanyModel?>      GetByIdAsync(Guid id);
    Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company);
    Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company);
    Task<(bool Ok, string? Error)> DeleteAsync(Guid id);
    Task<(bool Ok, string? Error)> SwitchCompanyAsync(Guid id);
}
