import os
import sqlite3

candidates = []
local = os.environ.get("LOCALAPPDATA", "")
for root, dirs, files in os.walk(os.path.join(local, "User Name")):
    for f in files:
        if f == "AveroNovaLocal.db":
            candidates.append(os.path.join(root, f))

print("dbs", candidates)
for p in candidates:
    c = sqlite3.connect(p)
    cols = [r[1] for r in c.execute("PRAGMA table_info(UserRoles)")]
    print("path", p)
    print("UserRoles.columns", cols)
    print("users", c.execute("select count(*) from Users").fetchone()[0])
    print("roles", list(c.execute("select Name from Roles")))
    print("user_roles", c.execute("select count(*) from UserRoles").fetchone()[0])
    print("role_permissions", c.execute("select count(*) from RolePermissions").fetchone()[0])
    if "CompanyId" in cols:
        print("scoped", c.execute("select count(*) from UserRoles where CompanyId is not null and CompanyId != ''").fetchone()[0])
        print("unscoped", c.execute("select count(*) from UserRoles where CompanyId is null or CompanyId = ''").fetchone()[0])
    c.close()
