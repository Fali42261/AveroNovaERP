namespace AveroNova.App.UI.Models;

public sealed class CustomerListQuery
{
    public string? SearchText { get; init; }

    public CustomerStatus? Status { get; init; }

    public int Skip { get; init; }

    /// <summary>
    /// Number of rows to return. Zero returns the full current-company result
    /// while still reporting <see cref="CustomerListResult.TotalCount"/> for later paging.
    /// </summary>
    public int Take { get; init; }
}

public sealed class CustomerListResult
{
    public IReadOnlyList<CustomerModel> Items { get; init; } = [];

    public int TotalCount { get; init; }
}
