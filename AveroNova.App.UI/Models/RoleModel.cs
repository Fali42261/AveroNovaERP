namespace AveroNova.App.UI.Models;

public class RoleModel : BaseModel
{
    public string       Name        { get; set; } = string.Empty;
    public string       Description { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = [];
    public bool         IsSystem    { get; set; }
    public int          UserCount   { get; set; }
    public Guid         CompanyId   { get; set; }
}

public class PermissionModel
{
    public string   Key         { get; set; } = string.Empty;
    public string   Module      { get; set; } = string.Empty;
    public string   Label       { get; set; } = string.Empty;
    public string   Description { get; set; } = string.Empty;
    public bool     IsGranted   { get; set; }
}
