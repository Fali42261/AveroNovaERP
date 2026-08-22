import sqlite3

db = r"C:\Users\NCB\AppData\Local\User Name\com.companyname.averonova.app.ui\Data\AveroNovaLocal.db"
con = sqlite3.connect(db)
con.row_factory = sqlite3.Row
cur = con.cursor()

print("=== Customers CompanyId ===")
for r in cur.execute("SELECT Id, CompanyId, Name FROM Customers LIMIT 5"):
    print(dict(r))

print("\n=== Products vs company join ===")
print("exact", cur.execute("SELECT COUNT(*) c FROM Products p JOIN Companies c ON p.CompanyId = c.Id").fetchone()["c"])
print("lower", cur.execute("SELECT COUNT(*) c FROM Products p JOIN Companies c ON lower(p.CompanyId) = lower(c.Id)").fetchone()["c"])

print("\n=== hex dump first company/product ids ===")
for table, col in [("Companies", "Id"), ("Products", "CompanyId"), ("Customers", "CompanyId")]:
    try:
        val = cur.execute(f"SELECT {col} FROM {table} LIMIT 1").fetchone()
        if val:
            s = val[0]
            print(table, col, s, type(s).__name__, [hex(ord(ch)) for ch in s[:8]])
    except Exception as e:
        print(table, e)
