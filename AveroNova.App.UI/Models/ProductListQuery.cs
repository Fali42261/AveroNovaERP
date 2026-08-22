namespace AveroNova.App.UI.Models;

public sealed class ProductListQuery
{
    public string? SearchText { get; init; }

    public ProductStatus? Status { get; init; }

    public int Skip { get; init; }

    /// <summary>
    /// Number of rows to return. Zero returns the full current-company result
    /// while still reporting <see cref="ProductListResult.TotalCount"/> for later paging.
    /// </summary>
    public int Take { get; init; }
}

public sealed class ProductListResult
{
    public IReadOnlyList<ProductModel> Items { get; init; } = [];

    public int TotalCount { get; init; }
}
