using System;

namespace AveroNova.Domain.Entities
{
    public class Product : BaseEntity
    {
        public Guid CompanyId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string SKU { get; set; } = string.Empty;

        public string Barcode { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;

        public string Unit { get; set; } = "pcs";

        public decimal PurchasePrice { get; set; }

        public decimal SellingPrice { get; set; }

        public decimal TaxPercent { get; set; }

        public decimal DiscountPercent { get; set; }

        public int Stock { get; set; }

        public int OpeningStock { get; set; }

        public int MinimumStock { get; set; }

        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Stored as <c>AveroNova.App.UI.Models.ProductStatus</c> integer:
        /// 0 Active, 1 Inactive, 2 Discontinued.
        /// </summary>
        public int Status { get; set; }

        public Company Company { get; set; } = null!;
    }
}
