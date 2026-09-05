using AveroNova.App.UI.Pages.Authentication;
using AveroNova.App.UI.Pages.Company;
using AveroNova.App.UI.Pages.Dashboard;
using AveroNova.App.UI.Pages.Splash;
using AveroNova.App.UI.ViewModels;
using AveroNova.App.UI.Views.Auth;
using AveroNova.App.UI.Views.Dashboard;
using AveroNova.App.UI.Views.Layout;
using AveroNova.App.UI.Views.Profile;
using AveroNova.App.UI.Pages.Customers;
using AveroNova.App.UI.Services;
using AveroNova.App.UI.Services.Api;
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.License;
using AveroNova.App.UI.Services.Mock;
using AveroNova.App.UI.Services.Security;
using AveroNova.App.UI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Administration;
using AveroNova.App.UI.Pages.Billing;
using AveroNova.App.UI.Pages.Expenses;
using AveroNova.App.UI.Pages.Help;
using AveroNova.App.UI.Pages.Inventory;
using AveroNova.App.UI.Pages.License;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Pages.Products;
using AveroNova.App.UI.Pages.Purchases;
using AveroNova.App.UI.Pages.Reports;
using AveroNova.App.UI.Pages.Returns;
using AveroNova.App.UI.Pages.Settings;
using AveroNova.App.UI.Pages.Subscription;
using AveroNova.App.UI.Pages.SyncCenter;

namespace AveroNova.App.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                AveroNova.App.UI.Helpers.NativeInputChrome.Register();
                // GlobalPointerCursor.Register() disabled: ViewHandler mapping + VisualTreeHelper
                // recursion blocked the UI thread on the authentication landing page.
                // ResponsivePage.Attach() disabled: walking/mutating the visual tree on SizeChanged
                // re-entered layout and froze the same page.
            });

        // ── Database ──────────────────────────────────────────────────────────
        //var dbPath = DatabasePath.GetDatabasePath(FileSystem.AppDataDirectory);
        //builder.Services.AddInfrastructure(dbPath);

        // ── Singletons ────────────────────────────────────────────────────────
        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IMainContentNavigator, MainContentNavigator>();

        // ── Auth pages ────────────────────────────────────────────────────────
        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ResetPasswordPage>();

        // ── Auth view models ──────────────────────────────────────────────────
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();

        // ── Auth views ────────────────────────────────────────────────────────
        builder.Services.AddTransient<LoginFormView>();

        // ── Company setup ─────────────────────────────────────────────────────
        // ── Company setup ─────────────────────────────────────────────────────
        builder.Services.AddTransient<CompanySetupPage>();
        builder.Services.AddTransient<CompanySetupViewModel>();


        // ── Main ERP shell ────────────────────────────────────────────────────
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainLayoutView>();       // single registration

        // ── Dashboard ─────────────────────────────────────────────────────────
        // builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DashboardView>();

        // ── Profile ───────────────────────────────────────────────────────────
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ProfileView>();



        // ── Main Layout Page Factories ────────────────────────────────────────

        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<Func<DashboardPage>>(sp => () => sp.GetRequiredService<DashboardPage>());

        builder.Services.AddTransient<CompanyListPage>();
        builder.Services.AddTransient<Func<CompanyListPage>>(sp => () => sp.GetRequiredService<CompanyListPage>());

        builder.Services.AddTransient<CustomersListPage>();
        builder.Services.AddTransient<Func<CustomersListPage>>(sp => () => sp.GetRequiredService<CustomersListPage>());

        builder.Services.AddTransient<ProductsListPage>();
        builder.Services.AddTransient<Func<ProductsListPage>>(sp => () => sp.GetRequiredService<ProductsListPage>());

        builder.Services.AddTransient<InventoryPage>();
        builder.Services.AddTransient<Func<InventoryPage>>(sp => () => sp.GetRequiredService<InventoryPage>());
        builder.Services.AddTransient<StockAdjustPage>();
        builder.Services.AddTransient<Func<StockAdjustPage>>(sp => () => sp.GetRequiredService<StockAdjustPage>());
        builder.Services.AddTransient<StockMovementPage>();
        builder.Services.AddTransient<Func<StockMovementPage>>(sp => () => sp.GetRequiredService<StockMovementPage>());

        builder.Services.AddTransient<BillingListPage>();
        builder.Services.AddTransient<Func<BillingListPage>>(sp => () => sp.GetRequiredService<BillingListPage>());

        builder.Services.AddTransient<PurchasesListPage>();
        builder.Services.AddTransient<Func<PurchasesListPage>>(sp => () => sp.GetRequiredService<PurchasesListPage>());

        builder.Services.AddTransient<PaymentsListPage>();
        builder.Services.AddTransient<Func<PaymentsListPage>>(sp => () => sp.GetRequiredService<PaymentsListPage>());

        builder.Services.AddTransient<SalesReturnsListPage>();
        builder.Services.AddTransient<Func<SalesReturnsListPage>>(sp => () => sp.GetRequiredService<SalesReturnsListPage>());

        builder.Services.AddTransient<PurchaseReturnsListPage>();
        builder.Services.AddTransient<Func<PurchaseReturnsListPage>>(sp => () => sp.GetRequiredService<PurchaseReturnsListPage>());

        builder.Services.AddTransient<ExpensesListPage>();
        builder.Services.AddTransient<Func<ExpensesListPage>>(sp => () => sp.GetRequiredService<ExpensesListPage>());

        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<Func<ReportsPage>>(sp => () => sp.GetRequiredService<ReportsPage>());

        builder.Services.AddTransient<UsersListPage>();
        builder.Services.AddTransient<Func<UsersListPage>>(sp => () => sp.GetRequiredService<UsersListPage>());

        builder.Services.AddTransient<RolesListPage>();
        builder.Services.AddTransient<Func<RolesListPage>>(sp => () => sp.GetRequiredService<RolesListPage>());

        builder.Services.AddTransient<PermissionsPage>();
        builder.Services.AddTransient<Func<PermissionsPage>>(sp => () => sp.GetRequiredService<PermissionsPage>());

        builder.Services.AddTransient<LicenseViewModel>();
        builder.Services.AddTransient<LicensePage>();
        builder.Services.AddTransient<Func<LicensePage>>(sp => () => sp.GetRequiredService<LicensePage>());
        builder.Services.AddTransient<LicenseActivationPage>();

        builder.Services.AddTransient<SubscriptionPage>();
        builder.Services.AddTransient<Func<SubscriptionPage>>(sp => () => sp.GetRequiredService<SubscriptionPage>());

        builder.Services.AddTransient<SyncCenterPage>();
        builder.Services.AddTransient<Func<SyncCenterPage>>(sp => () => sp.GetRequiredService<SyncCenterPage>());

        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<Func<SettingsPage>>(sp => () => sp.GetRequiredService<SettingsPage>());

        builder.Services.AddTransient<HelpAboutPage>();
        builder.Services.AddTransient<Func<HelpAboutPage>>(sp => () => sp.GetRequiredService<HelpAboutPage>());


        // ── Services / Repositories ───────────────────────────────────────────

        // ── Local SQLite (Offline-First foundation) ───────────────────────────
        // Local offline DB — NEVER the server AveroNovaDev.db.
        var localDbPath = Path.Combine(FileSystem.AppDataDirectory, "AveroNovaLocal.db");
        builder.Services.AddDbContextFactory<LocalAppDbContext>(options =>
            options.UseSqlite($"Data Source={localDbPath}"));
        builder.Services.AddScoped(sp =>
            sp.GetRequiredService<IDbContextFactory<LocalAppDbContext>>().CreateDbContext());
        builder.Services.AddScoped<ILocalDatabaseInitializer, LocalDatabaseInitializer>();
        builder.Services.AddSingleton<ISecureTokenStore, MauiSecureTokenStore>();
        builder.Services.AddSingleton<IPendingRegistrationSecretStore, MauiPendingRegistrationSecretStore>();
        builder.Services.AddSingleton<IStableDeviceIdProvider, MauiStableDeviceIdProvider>();
        builder.Services.AddSingleton<IInstallationService, InstallationService>();
        builder.Services.AddSingleton<ILocalSessionPolicy, LocalSessionPolicy>();
        builder.Services.AddSingleton<IAppSessionContext, AppSessionContext>();
        builder.Services.AddSingleton<ILocalAuthSessionStore, LocalAuthSessionStore>();
        builder.Services.AddSingleton<IOfflineRegistrationStore, OfflineRegistrationStore>();

        var apiSettings = ApiSettingsLoader.Load();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(apiSettings));
#if DEBUG
        // Trust ASP.NET Core development HTTPS certificate for local Windows/Android/iOS targets.
        builder.Services.AddSingleton(_ =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler);
        });
#else
        builder.Services.AddSingleton(_ => new HttpClient());
#endif
        builder.Services.AddSingleton<IApiClient, ApiClient>();
        builder.Services.AddSingleton<IAuthApiClient, AuthApiClient>();
        builder.Services.AddSingleton<ILicenseApiClient, LicenseApiClient>();
        builder.Services.AddSingleton<ILicenseAnchorStore, MauiLicenseAnchorStore>();
        builder.Services.AddSingleton<ILocalCredentialStore, MauiLocalCredentialStore>();
        builder.Services.AddSingleton<ILicenseService, LicenseService>();

        // ── Auth + Mock business services ─────────────────────────────────────

        builder.Services.AddSingleton<IClientDeviceInfo, MauiClientDeviceInfo>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddTransient<IBillingService, LocalBillingService>();
        builder.Services.AddTransient<AveroNova.App.UI.Services.Interfaces.ICompanyService, LocalCompanyService>();
        builder.Services.AddSingleton<IConnectivityService, MauiConnectivityService>();
        builder.Services.AddTransient<ICustomerService, LocalCustomerService>();
        builder.Services.AddTransient<IExpenseService, MockExpenseService>();
        builder.Services.AddTransient<IInventoryService, LocalInventoryService>();
        builder.Services.AddTransient<INotificationService, MockNotificationService>();
        builder.Services.AddTransient<IPaymentService, LocalPaymentService>();
        builder.Services.AddTransient<IProductService, LocalProductService>();
        builder.Services.AddTransient<IPurchaseService, MockPurchaseService>();
        builder.Services.AddTransient<IReturnService, MockReturnService>();
        builder.Services.AddTransient<ISettingsService, MockSettingsService>();
        builder.Services.AddTransient<ISubscriptionService, MockSubscriptionService>();
        builder.Services.AddSingleton<ISyncService, RegistrationSyncService>();
        builder.Services.AddTransient<IUserService, MockUserService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var localDb = scope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>();
            localDb.InitializeAsync().GetAwaiter().GetResult();
            var installation = scope.ServiceProvider.GetRequiredService<IInstallationService>();
            installation.EnsureInitializedAsync().GetAwaiter().GetResult();
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Local SQLite: {localDb.DatabasePath}");
            System.Diagnostics.Debug.WriteLine(
                $"[AveroNova] Installation: {installation.InstallationId} Status={installation.Status}");
            System.Diagnostics.Debug.WriteLine($"[AveroNova] API BaseUrl: {apiSettings.BaseUrl}");
        }

        return app;
    }
}
