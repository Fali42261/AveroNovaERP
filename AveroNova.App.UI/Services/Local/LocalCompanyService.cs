using AveroNova.App.UI.Models;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.Domain.Entities;
using AveroNova.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.App.UI.Services.Local;

public sealed class LocalCompanyService : ICompanyService
{
    private readonly IDbContextFactory<AppDbContext> _factory;

    public LocalCompanyService(IDbContextFactory<AppDbContext> factory)
    {
        _factory = factory;
    }

    public CompanyModel? CurrentCompany
    {
        get
        {
            using var db = _factory.CreateDbContext();
            var id = LocalSessionStore.CompanyId;
            Company? company = null;
            if (id != null)
            {
                company = db.Companies.AsNoTracking()
                    .FirstOrDefault(c => c.Id == id.Value && !c.IsDeleted);
            }

            if (company == null)
            {
                var userId = LocalSessionStore.UserId;
                if (userId != null)
                {
                    company = db.Companies.AsNoTracking()
                        .FirstOrDefault(c => c.UserId == userId.Value && !c.IsDeleted);
                    if (company != null)
                        LocalSessionStore.Set(userId.Value, company.Id, LocalSessionStore.Email);
                }
            }

            return company == null ? null : Map(company, isCurrent: true);
        }
    }

    public async Task<List<CompanyModel>> GetAllAsync()
    {
        var userId = LocalSessionStore.UserId;
        await using var db = await _factory.CreateDbContextAsync();
        var currentId = LocalSessionStore.CompanyId;
        var query = db.Companies.AsNoTracking().Where(c => !c.IsDeleted);
        if (userId != null)
            query = query.Where(c => c.UserId == userId.Value);

        var rows = await query.ToListAsync();
        return rows.Select(c => Map(c, c.Id == currentId)).ToList();
    }

    public async Task<CompanyModel?> GetByIdAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var company = await db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        return company == null ? null : Map(company, company.Id == LocalSessionStore.CompanyId);
    }

    public async Task<(bool Ok, string? Error)> CreateAsync(CompanyModel company)
    {
        var userId = LocalSessionStore.UserId;
        if (userId == null)
            return (false, "No authenticated user.");

        await using var db = await _factory.CreateDbContextAsync();
        var entity = new Company
        {
            Id = company.LocalId == Guid.Empty ? Guid.NewGuid() : company.LocalId,
            UserId = userId.Value,
            CompanyCode = "C" + Convert.ToHexString(Guid.NewGuid().ToByteArray())[..8],
            CompanyName = company.Name,
            OwnerName = company.Name,
            Email = company.Email,
            MobileNumber = company.Phone,
            Address = company.Address,
            City = company.City,
            Country = company.Country,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        db.Companies.Add(entity);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> UpdateAsync(CompanyModel company)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.LocalId);
        if (existing == null)
            return (false, "Company not found.");

        existing.CompanyName = company.Name;
        existing.Email = company.Email;
        existing.MobileNumber = company.Phone;
        existing.Address = company.Address;
        existing.City = company.City;
        existing.Country = company.Country;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(Guid id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
            return (false, "Company not found.");
        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return (true, null);
    }

    public Task SwitchCompanyAsync(Guid id)
    {
        var userId = LocalSessionStore.UserId;
        var email = LocalSessionStore.Email;
        if (userId != null)
            LocalSessionStore.Set(userId.Value, id, email);
        return Task.CompletedTask;
    }

    private static CompanyModel Map(Company company, bool isCurrent) => new()
    {
        LocalId = company.Id,
        Name = company.CompanyName,
        CompanyCode = company.CompanyCode,
        OwnerName = company.OwnerName,
        Email = company.Email,
        Phone = company.MobileNumber,
        Address = company.Address,
        City = company.City,
        State = company.State,
        PinCode = company.PinCode,
        Country = company.Country,
        TaxNumber = company.GSTNumber,
        RegistrationNo = company.PANNumber,
        IsCurrentCompany = isCurrent,
        SyncStatus = SyncStatus.PendingSync
    };
}
