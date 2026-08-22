using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var dbPath = @"C:\Users\NCB\AppData\Local\User Name\com.companyname.averonova.app.ui\Data\AveroNovaLocal.db";
var companyA = Guid.Parse("845BA939-CB32-4DD3-98A6-8B876F09C726");
var companyB = Guid.Parse("EEBF7AFF-7707-4DB1-9EDF-14E63D6BE8C5");
var productA = Guid.Parse("BB67D145-A5B2-4A8D-95BB-A0C1192CF7C5");
var productB = Guid.Parse("C9A5B79F-80F6-424E-A1FB-B24FFA7A76FB");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

await using (var db = new AppDbContext(options))
{
    var raw = await db.Products.AsNoTracking().ToListAsync();
    Console.WriteLine($"RAW_COUNT={raw.Count}");
    foreach (var p in raw)
        Console.WriteLine($"RAW {p.Id} company={p.CompanyId} name={p.Name} sku={p.SKU} price={p.PurchasePrice}/{p.SellingPrice} stock={p.Stock} min={p.MinimumStock} status={p.Status} deleted={p.IsDeleted}");
}

var factory = new SimpleFactory(options);
var repo = new ProductRepository(factory);

var own = await repo.GetByIdAsync(companyA, productA);
var cross = await repo.GetByIdAsync(companyA, productB);
var wrong = await repo.GetByIdAsync(companyB, productA);
var query = await repo.QueryAsync(companyA, null, null, 0, 10);

Console.WriteLine($"GET_OWN={(own == null ? "NULL" : own.Name)}");
Console.WriteLine($"GET_CROSS_A_SEES_B={(cross != null)}");
Console.WriteLine($"GET_CROSS_B_SEES_A={(wrong != null)}");
Console.WriteLine($"QUERY_A_COUNT={query.TotalCount}");
foreach (var p in query.Items)
    Console.WriteLine($"QUERY {p.SKU} {p.Name}");

sealed class SimpleFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;
    public SimpleFactory(DbContextOptions<AppDbContext> options) => _options = options;
    public AppDbContext CreateDbContext() => new(_options);
    public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(new AppDbContext(_options));
}
