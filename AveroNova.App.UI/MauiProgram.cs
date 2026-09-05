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
using AveroNova.App.UI.Services.Security;
using AveroNova.App.UI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
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
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureMauiHandlers(handlers =>
            {
                AveroNova.App.UI.Helpers.NativeInputChrome.Register();
            });

        builder.Services.AddSingleton<App>();
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IMainContentNavigator, MainContentNavigator>();

        builder.Services.AddTransient<SplashPage>();
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ForgotPasswordPage>();
        builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<LoginFormView>();
        builder.Services.AddTransient<CompanySetupPage>();
        builder.Services.AddTransient<CompanySetupViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<MainLayoutView>();
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<DashboardView>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<ProfileView>();

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
        builder.Services.AddTransient<PurchaseFormPage>();
        builder.Services.AddTransient<Func<PurchaseFormPage>>(sp => () => sp.GetRequiredService<PurchaseFormPage>());
        builder.Services.AddTransient<PurchaseViewPage>();
        builder.Services.AddTransient<Func<PurchaseViewPage>>(sp => () => sp.GetRequiredService<PurchaseViewPage>());
        builder.Services.AddTransient<SuppliersListPage>();
        builder.Services.AddTransient<Func<SuppliersListPage>>(sp => () => sp.GetRequiredService<SuppliersListPage>());
        builder.Services.AddTransient<SupplierFormPage>();
        builder.Services.AddTransient<Func<SupplierFormPage>>(sp => () => sp.GetRequiredService<SupplierFormPage>());
        builder.Services.AddTransient<PaymentsListPage>();
        builder.Services.AddTransient<Func<PaymentsListPage>>(sp => () => sp.GetRequiredService<PaymentsListPage>());
        builder.Services.AddTransient<SalesReturnsListPage>();
        builder.Services.AddTransient<Func<SalesReturnsListPage>>(sp => () => sp.GetRequiredService<SalesReturnsListPage>());
        builder.Services.AddTransient<SalesReturnFormPage>();
        builder.Services.AddTransient<Func<SalesReturnFormPage>>(sp => () => sp.GetRequiredService<SalesReturnFormPage>());
        builder.Services.AddTransient<PurchaseReturnsListPage>();
        builder.Services.AddTransient<Func<PurchaseReturnsListPage>>(sp => () => sp.GetRequiredService<PurchaseReturnsListPage>());
        builder.Services.AddTransient<PurchaseReturnFormPage>();
        builder.Services.AddTransient<Func<PurchaseReturnFormPage>>(sp => () => sp.GetRequiredService<PurchaseReturnFormPage>());
        builder.Services.AddTransient<ExpensesListPage>();
        builder.Services.AddTransient<Func<ExpensesListPage>>(sp => () => sp.GetRequiredService<ExpensesListPage>());
        builder.Services.AddTransient<ExpenseFormPage>();
        builder.Services.AddTransient<Func<ExpenseFormPage>>(sp => () => sp.GetRequiredService<ExpenseFormPage>());
        builder.Services.AddTransient<ExpenseViewPage>();
        builder.Services.AddTransient<Func<ExpenseViewPage>>(sp => () => sp.GetRequiredService<ExpenseViewPage>());
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<Func<ReportsPage>>(sp => () => sp.GetRequiredService<ReportsPage>());
        builder.Services.AddTransient<UsersListPage>();
        builder.Services.AddTransient<Func<UsersListPage>>(sp => () => sp.GetRequiredService<UsersListPage>());
        builder.Services.AddTransient<UserFormPage>();
        builder.Services.AddTransient<Func<UserFormPage>>(sp => () => sp.GetRequiredService<UserFormPage>());
        builder.Services.AddTransient<UserViewPage>();
        builder.Services.AddTransient<Func<UserViewPage>>(sp => () => sp.GetRequiredService<UserViewPage>());
        builder.Services.AddTransient<RolesListPage>();
        builder.Services.AddTransient<Func<RolesListPage>>(sp => () => sp.GetRequiredService<RolesListPage>());
        builder.Services.AddTransient<RoleFormPage>();
        builder.Services.AddTransient<Func<RoleFormPage>>(sp => () => sp.GetRequiredService<RoleFormPage>());
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

        var localDbPath = Path.Combine(FileSystem.AppDataDirectory, "AveroNovaLocal.db");
        builder.Services.AddDbContextFactory<LocalAppDbContext>(options => options.UseSqlite($"Data Source={localDbPath}"));
        builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<LocalAppDbContext>>().CreateDbContext());
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
        builder.Services.AddSingleton(_ =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
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
        builder.Services.AddSingleton<IClientDeviceInfo, MauiClientDeviceInfo>();
        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddTransient<IBillingService, LocalBillingService>();
        builder.Services.AddTransient<ICompanyService, LocalCompanyService>();
        builder.Services.AddSingleton<IConnectivityService, MauiConnectivityService>();
        builder.Services.AddTransient<ICustomerService, LocalCustomerService>();
        builder.Services.AddTransient<IExpenseService, LocalExpenseService>();
        builder.Services.AddTransient<IInventoryService, LocalInventoryService>();
        builder.Services.AddSingleton<INotificationService, LocalNotificationService>();
        builder.Services.AddSingleton<IPaymentSyncService, PaymentSyncService>();
        builder.Services.AddSingleton<IProcurementSyncService, ProcurementSyncService>();
        builder.Services.AddTransient<IPaymentService, LocalPaymentService>();
        builder.Services.AddTransient<IProductService, LocalProductService>();
        builder.Services.AddTransient<IPurchaseService, LocalPurchaseService>();
        builder.Services.AddTransient<ISupplierService, LocalSupplierService>();
        builder.Services.AddTransient<IReturnService, LocalReturnService>();
        builder.Services.AddTransient<ISettingsService, LocalSettingsService>();
        builder.Services.AddTransient<ISubscriptionService, LocalSubscriptionService>();
        builder.Services.AddSingleton<ISyncService, RegistrationSyncService>();
        builder.Services.AddTransient<IUserService, LocalUserService>();

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
            System.Diagnostics.Debug.WriteLine($"[AveroNova] Installation: {installation.InstallationId} Status={installation.Status}");
            System.Diagnostics.Debug.WriteLine($"[AveroNova] API BaseUrl: {apiSettings.BaseUrl}");
        }
        return app;
    }
}
