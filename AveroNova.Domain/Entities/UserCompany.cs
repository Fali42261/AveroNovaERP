namespace AveroNova.Domain.Entities;

public class UserCompany : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsOwner { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
