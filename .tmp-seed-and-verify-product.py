import sqlite3
from datetime import datetime, timezone
from uuid import uuid4

db = r"C:\Users\NCB\AppData\Local\User Name\com.companyname.averonova.app.ui\Data\AveroNovaLocal.db"
con = sqlite3.connect(db)
con.row_factory = sqlite3.Row
cur = con.cursor()

company_a = "845BA939-CB32-4DD3-98A6-8B876F09C726"
company_b = "EEBF7AFF-7707-4DB1-9EDF-14E63D6BE8C5"
now = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M:%S.%f")

existing = cur.execute(
    "SELECT Id FROM Products WHERE SKU = ? AND CompanyId = ? AND IsDeleted = 0",
    ("P-VIEW-001", company_a),
).fetchone()
if existing:
    product_a = existing["Id"]
    print("EXISTING_A", product_a)
else:
    product_a = str(uuid4()).upper()
    cur.execute(
        """
        INSERT INTO Products (
            Id, CompanyId, Name, SKU, Barcode, Category, Brand, Unit,
            PurchasePrice, SellingPrice, TaxPercent, DiscountPercent,
            Stock, OpeningStock, MinimumStock, Description, Status,
            CreatedAt, UpdatedAt, IsDeleted
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
        """,
        (
            product_a,
            company_a,
            "ABC Premium Product International Edition Extra Long Name",
            "P-VIEW-001",
            "8901234567890",
            "General",
            "XYZ",
            "PCS",
            "50.00",
            "70.00",
            "18",
            "5",
            100,
            100,
            10,
            "Seeded for Product View verification",
            0,
            now,
            now,
        ),
    )
    print("INSERTED_A", product_a)

existing_b = cur.execute(
    "SELECT Id FROM Products WHERE SKU = ? AND CompanyId = ? AND IsDeleted = 0",
    ("P-B-SECRET", company_b),
).fetchone()
if existing_b:
    product_b = existing_b["Id"]
    print("EXISTING_B", product_b)
else:
    product_b = str(uuid4()).upper()
    cur.execute(
        """
        INSERT INTO Products (
            Id, CompanyId, Name, SKU, Barcode, Category, Brand, Unit,
            PurchasePrice, SellingPrice, TaxPercent, DiscountPercent,
            Stock, OpeningStock, MinimumStock, Description, Status,
            CreatedAt, UpdatedAt, IsDeleted
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 0)
        """,
        (
            product_b,
            company_b,
            "Company B Secret Product",
            "P-B-SECRET",
            "9999999999999",
            "Hidden",
            "OtherCo",
            "BOX",
            "9.00",
            "19.00",
            "12",
            "0",
            7,
            7,
            2,
            "Other company product",
            0,
            now,
            now,
        ),
    )
    print("INSERTED_B", product_b)

con.commit()

def scoped_get(company_id, product_id):
    return cur.execute(
        """
        SELECT Id, CompanyId, Name, SKU, Barcode, Category, Brand, Unit,
               PurchasePrice, SellingPrice, TaxPercent, DiscountPercent,
               Stock, OpeningStock, MinimumStock, Status, IsDeleted
        FROM Products
        WHERE CompanyId = ? AND Id = ? AND IsDeleted = 0
        """,
        (company_id, product_id),
    ).fetchone()

own = scoped_get(company_a, product_a)
cross = scoped_get(company_a, product_b)
wrong = scoped_get(company_b, product_a)
print("OWN_MATCH", None if own is None else dict(own))
print("CROSS_A_SEES_B", cross is not None)
print("CROSS_B_SEES_A", wrong is not None)
print("PRODUCT_A_ID", product_a)
print("PRODUCT_B_ID", product_b)
