using System.Security.Claims;
using AveroNova.Domain.Entities;
using AveroNova.Domain.Enums;
using AveroNova.Infrastructure.Auth;
using AveroNova.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AveroNova.API.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public sealed class CompaniesController : ControllerBase
{
    private readonly AppDbContext _db;

    public CompaniesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var companies = await _db.UserCompanies
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.IsActive && !x.IsDeleted)
            .Join(
                _db.Companies.AsNoTracking().Where(c => c.IsActive && !c.IsDeleted),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new
                {
                    id = c.Id,
                    c.CompanyCode,
                    c.CompanyName,
                    c.OwnerName,
                    c.GSTNumber,
                    c.PANNumber,
                    c.Email,
                    c.MobileNumber,
                    c.Address,
                    c.City,
                    c.State,
                    c.Country,
                    c.PinCode,
                    uc.IsOwner,
                    uc.IsDefault,
                    c.SyncVersion,
                    c.UpdatedAt
                })
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CompanyName)
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, data = companies });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var company = await _db.UserCompanies
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.CompanyId == id && x.IsActive && !x.IsDeleted)
            .Join(
                _db.Companies.AsNoTracking().Where(c => c.IsActive && !c.IsDeleted),
                uc => uc.CompanyId,
                c => c.Id,
                (uc, c) => new
                {
                    id = c.Id,
                    c.CompanyCode,
                    c.CompanyName,
                    c.OwnerName,
                    c.GSTNumber,
                    c.PANNumber,
                    c.Email,
                    c.MobileNumber,
                    c.Address,
                    c.City,
                    c.State,
                    c.Country,
                    c.PinCode,
                    uc.IsOwner,
                    uc.IsDefault,
                    c.SyncVersion,
                    c.UpdatedAt
                })
            .FirstOrDefaultAsync(cancellationToken);

        return company is null
            ? NotFound(new { success = false, error = "Company not found." })
            : Ok(new { success = true, data = company });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (request.Id == Guid.Empty)
            return BadRequest(new { success = false, error = "Client company id is required." });
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return BadRequest(new { success = false, error = "Company name is required." });

        var existing = await _db.Companies.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (existing is not null)
        {
            var linked = await _db.UserCompanies.AnyAsync(
                x => x.UserId == userId && x.CompanyId == request.Id && x.IsActive && !x.IsDeleted,
                cancellationToken);

            return linked
                ? Ok(new { success = true, data = new { id = existing.Id, existing.SyncVersion }, idempotent = true })
                : Conflict(new { success = false, error = "Company id already exists." });
        }

        var now = DateTime.UtcNow;
        var company = new Company
        {
            Id = request.Id,
            CompanyCode = string.IsNullOrWhiteSpace(request.CompanyCode)
                ? $"CMP-{request.Id.ToString("N")[..8].ToUpperInvariant()}"
                : request.CompanyCode.Trim(),
            CompanyName = request.CompanyName.Trim(),
            OwnerName = request.OwnerName?.Trim() ?? string.Empty,
            GSTNumber = request.GstNumber?.Trim() ?? string.Empty,
            PANNumber = request.PanNumber?.Trim() ?? string.Empty,
            Email = request.Email?.Trim() ?? string.Empty,
            MobileNumber = request.MobileNumber?.Trim() ?? string.Empty,
            Address = request.Address?.Trim() ?? string.Empty,
            City = request.City?.Trim() ?? string.Empty,
            State = request.State?.Trim() ?? string.Empty,
            Country = request.Country?.Trim() ?? string.Empty,
            PinCode = request.PinCode?.Trim() ?? string.Empty,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncStatus = RecordSyncStatus.Synced,
            SyncVersion = Math.Max(1, request.SyncVersion),
            LastSyncedAt = now
        };

        var membership = new UserCompany
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CompanyId = company.Id,
            IsOwner = true,
            IsDefault = false,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            SyncStatus = RecordSyncStatus.Synced,
            LastSyncedAt = now
        };

        _db.Companies.Add(company);
        _db.UserCompanies.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = company.Id }, new
        {
            success = true,
            data = new { id = company.Id, company.SyncVersion, company.UpdatedAt }
        });
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("Company.Manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertCompanyRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var canAccess = await _db.UserCompanies.AnyAsync(
            x => x.UserId == userId && x.CompanyId == id && x.IsActive && !x.IsDeleted,
            cancellationToken);
        if (!canAccess)
            return Forbid();

        var company = await _db.Companies.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (company is null)
            return NotFound(new { success = false, error = "Company not found." });

        if (request.SyncVersion > 0 && request.SyncVersion < company.SyncVersion)
            return Conflict(new
            {
                success = false,
                error = "Company has newer server changes.",
                data = new { id = company.Id, company.SyncVersion, company.UpdatedAt }
            });

        if (!string.IsNullOrWhiteSpace(request.CompanyName)) company.CompanyName = request.CompanyName.Trim();
        company.OwnerName = request.OwnerName?.Trim() ?? company.OwnerName;
        company.GSTNumber = request.GstNumber?.Trim() ?? company.GSTNumber;
        company.PANNumber = request.PanNumber?.Trim() ?? company.PANNumber;
        company.Email = request.Email?.Trim() ?? company.Email;
        company.MobileNumber = request.MobileNumber?.Trim() ?? company.MobileNumber;
        company.Address = request.Address?.Trim() ?? company.Address;
        company.City = request.City?.Trim() ?? company.City;
        company.State = request.State?.Trim() ?? company.State;
        company.Country = request.Country?.Trim() ?? company.Country;
        company.PinCode = request.PinCode?.Trim() ?? company.PinCode;
        company.SyncVersion = Math.Max(company.SyncVersion + 1, request.SyncVersion + 1);
        company.SyncStatus = RecordSyncStatus.Synced;
        company.UpdatedAt = DateTime.UtcNow;
        company.LastSyncedAt = company.UpdatedAt;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, data = new { id = company.Id, company.SyncVersion, company.UpdatedAt } });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    public sealed class UpsertCompanyRequest
    {
        public Guid Id { get; set; }
        public string? CompanyCode { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? GstNumber { get; set; }
        public string? PanNumber { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PinCode { get; set; }
        public long SyncVersion { get; set; }
    }
}
