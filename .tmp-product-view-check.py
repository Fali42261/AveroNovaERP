import sqlite3

db = r"C:\Users\NCB\AppData\Local\User Name\com.companyname.averonova.app.ui\Data\AveroNovaLocal.db"
con = sqlite3.connect(db)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=== PRODUCTS ===")
for r in cur.execute("SELECT Id, CompanyId, Name, SKU, IsDeleted FROM Products"):
    print(dict(r))

print("\n=== COMPANY abc ===")
for r in cur.execute("SELECT Id, CompanyName FROM Companies WHERE CompanyName = 'abc'"):
    print(dict(r))

print("\n=== RolePermissions for products ===")
rows = cur.execute(
    """
    SELECT r.Name, p.PermissionName
    FROM RolePermissions rp
    JOIN Roles r ON r.Id = rp.RoleId
    JOIN Permissions p ON p.Id = rp.PermissionId
    WHERE p.PermissionName LIKE 'products%'
      AND IFNULL(rp.IsDeleted,0)=0
    """
)
for r in rows:
    print(dict(r))

print("\n=== User D28FC role perms ===")
rows = cur.execute(
    """
    SELECT p.PermissionName
    FROM UserRoles ur
    JOIN RolePermissions rp ON rp.RoleId = ur.RoleId AND IFNULL(rp.IsDeleted,0)=0
    JOIN Permissions p ON p.Id = rp.PermissionId
    WHERE ur.UserId = 'D28FC58F-924C-40EB-AC47-38E7521ECBBB'
      AND ur.CompanyId = '845BA939-CB32-4DD3-98A6-8B876F09C726'
      AND IFNULL(ur.IsDeleted,0)=0
      AND p.PermissionName LIKE 'products%'
    """
)
for r in rows:
    print(dict(r))
