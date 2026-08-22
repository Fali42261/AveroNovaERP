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
using AveroNova.App.UI.Services.Interfaces;
using AveroNova.App.UI.Services.Local;
using AveroNova.App.UI.Services.Mock;
using AveroNova.App.UI.SubscriptionAccess;
using AveroNova.Application.Interfaces;
using AveroNova.Application.Interfaces.Repositories;
using AveroNova.Application.Services;
using AveroNova.Infrastructure.Persistence;
using AveroNova.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AveroNova.App.UI.Navigation;
using AveroNova.App.UI.Pages.Administration;
using AveroNova.App.UI.Pages.Billing;
using AveroNova.App.UI.Pages.Expenses;
using AveroNova.App.UI.Pages.Help;
using AveroNova.App.UI.Pages.Inventory;
using AveroNova.App.UI.Pages.Payments;
using AveroNova.App.UI.Pages.Products;
using AveroNova.App.UI.Pages.Purchases;
using AveroNova.App.UI.Pages.Reports;
using AveroNova.App.UI.Pages.Returns;
using AveroNova.App.UI.Pages.Settings;
using AveroNova.App.UI.Pages.Profile;
using AveroNova.App.UI.Pages.Subscription;
using AveroNova.App.UI.Pages.SyncCenter;

namespace AveroNova.App.UI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AveroNova.App.UI.Helpers.StartupLog.Write("CreateMauiApp start");
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>().ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        }).ConfigureMauiHandlers(handlers => AveroNova.App.UI.Helpers.NativeInputChrome.Register());

        builder.Services.AddDbContextFactory<AppDbContext>(options =>
        {
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "AveroNovaLocal.db");
            options.UseSqlite($"Data Source={dbPath}");
        });
        builder.Services.AddSingleton<LocalDatabaseInitializer>();
        builder.Services.AddSingleton<ISubscriptionAccessRepository, SubscriptionAccessRepository>();
        builder.Services.AddSingleton<ICompanySubscriptionService, CompanySubscriptionService>();
        builder.Services.AddSingleton<AveroNova.Application.Interfaces.IAccessControlService, AccessControlService>();
        builder.Services.AddSingleton<CurrentAccessService>();
        builder.Services.AddSingleton<TrialReminderPresenter>();
        builder.Services.AddSingleton<AppShell>();

        builder.Services.AddTransient<SplashPage>(); builder.Services.AddTransient<WelcomePage>(); builder.Services.AddTransient<LoginPage>(); builder.Services.AddTransient<RegisterPage>(); builder.Services.AddTransient<ForgotPasswordPage>(); builder.Services.AddTransient<ResetPasswordPage>();
        builder.Services.AddTransient<LoginViewModel>(); builder.Services.AddSingleton<RegisterViewModel>(); builder.Services.AddTransient<LoginFormView>(); builder.Services.AddTransient<CompanySetupPage>(); builder.Services.AddTransient<CompanySetupViewModel>(); builder.Services.AddTransient<MainPage>(); builder.Services.AddTransient<MainLayoutView>(); builder.Services.AddTransient<DashboardViewModel>(); builder.Services.AddTransient<DashboardView>(); builder.Services.AddTransient<ProfileViewModel>(); builder.Services.AddTransient<ProfileView>();

        builder.Services.AddTransient<DashboardPage>(); builder.Services.AddTransient<Func<DashboardPage>>(sp => () => sp.GetRequiredService<DashboardPage>());
        builder.Services.AddTransient<CompanyPageViewModel>(); builder.Services.AddTransient<CompanyPage>(); builder.Services.AddTransient<Func<CompanyPage>>(sp => () => sp.GetRequiredService<CompanyPage>());
        builder.Services.AddTransient<CustomersListPage>(); builder.Services.AddTransient<Func<CustomersListPage>>(sp => () => sp.GetRequiredService<CustomersListPage>()); builder.Services.AddTransient<CustomerFormPage>(); builder.Services.AddTransient<Func<CustomerFormPage>>(sp => () => sp.GetRequiredService<CustomerFormPage>()); builder.Services.AddTransient<CustomerViewPage>(); builder.Services.AddTransient<Func<CustomerViewPage>>(sp => () => sp.GetRequiredService<CustomerViewPage>()); builder.Services.AddTransient<CustomersViewModel>(); builder.Services.AddTransient<CustomerFormViewModel>(); builder.Services.AddTransient<CustomerViewViewModel>();
        builder.Services.AddTransient<ProductsListPage>(); builder.Services.AddTransient<Func<ProductsListPage>>(sp => () => sp.GetRequiredService<ProductsListPage>()); builder.Services.AddTransient<ProductFormPage>(); builder.Services.AddTransient<ProductViewPage>(); builder.Services.AddTransient<ProductsViewModel>(); builder.Services.AddTransient<ProductFormViewModel>(); builder.Services.AddTransient<ProductViewViewModel>();
        builder.Services.AddTransient<InventoryPage>(); builder.Services.AddTransient<Func<InventoryPage>>(sp => () => sp.GetRequiredService<InventoryPage>()); builder.Services.AddTransient<BillingListPage>(); builder.Services.AddTransient<Func<BillingListPage>>(sp => () => sp.GetRequiredService<BillingListPage>()); builder.Services.AddTransient<PurchasesListPage>(); builder.Services.AddTransient<Func<PurchasesListPage>>(sp => () => sp.GetRequiredService<PurchasesListPage>()); builder.Services.AddTransient<PaymentsListPage>(); builder.Services.AddTransient<Func<PaymentsListPage>>(sp => () => sp.GetRequiredService<PaymentsListPage>()); builder.Services.AddTransient<SalesReturnsListPage>(); builder.Services.AddTransient<Func<SalesReturnsListPage>>(sp => () => sp.GetRequiredService<SalesReturnsListPage>()); builder.Services.AddTransient<PurchaseReturnsListPage>(); builder.Services.AddTransient<Func<PurchaseReturnsListPage>>(sp => () => sp.GetRequiredService<PurchaseReturnsListPage>()); builder.Services.AddTransient<ExpensesListPage>(); builder.Services.AddTransient<Func<ExpensesListPage>>(sp => () => sp.GetRequiredService<ExpensesListPage>()); builder.Services.AddTransient<ReportsPage>(); builder.Services.AddTransient<Func<ReportsPage>>(sp => () => sp.GetRequiredService<ReportsPage>());
        builder.Services.AddTransient<UsersViewModel>(); builder.Services.AddTransient<UsersListPage>(); builder.Services.AddTransient<Func<UsersListPage>>(sp => () => sp.GetRequiredService<UsersListPage>()); builder.Services.AddTransient<UserFormPage>(); builder.Services.AddTransient<UserViewPage>(); builder.Services.AddTransient<RolesListPage>(); builder.Services.AddTransient<Func<RolesListPage>>(sp => () => sp.GetRequiredService<RolesListPage>()); builder.Services.AddTransient<PermissionsPage>(); builder.Services.AddTransient<Func<PermissionsPage>>(sp => () => sp.GetRequiredService<PermissionsPage>()); builder.Services.AddTransient<SubscriptionPage>(); builder.Services.AddTransient<Func<SubscriptionPage>>(sp => () => sp.GetRequiredService<SubscriptionPage>()); builder.Services.AddTransient<SyncCenterPage>(); builder.Services.AddTransient<Func<SyncCenterPage>>(sp => () => sp.GetRequiredService<SyncCenterPage>()); builder.Services.AddTransient<UsersRolesPage>(); builder.Services.AddTransient<Func<UsersRolesPage>>(sp => () => sp.GetRequiredService<UsersRolesPage>()); builder.Services.AddTransient<UserProfilePage>(); builder.Services.AddTransient<Func<UserProfilePage>>(sp => () => sp.GetRequiredService<UserProfilePage>()); builder.Services.AddTransient<SettingsPage>(); builder.Services.AddTransient<Func<SettingsPage>>(sp => () => sp.GetRequiredService<SettingsPage>()); builder.Services.AddTransient<HelpAboutPage>(); builder.Services.AddTransient<Func<HelpAboutPage>>(sp => () => sp.GetRequiredService<HelpAboutPage>());

        builder.Services.AddSingleton<IToastService, ToastService>(); builder.Services.AddSingleton<IAuthenticationService, LocalAuthenticationService>(); builder.Services.AddTransient<IDashboardService, DashboardService>(); builder.Services.AddTransient<IBillingService, LocalBillingService>(); builder.Services.AddSingleton<ICompanyRepository, CompanyRepository>(); builder.Services.AddSingleton<AveroNova.App.UI.Services.Interfaces.ICompanyService, LocalCompanyService>(); builder.Services.AddSingleton<IConnectivityService, DeviceConnectivityService>(); builder.Services.AddSingleton<ICustomerRepository, CustomerRepository>(); builder.Services.AddTransient<ICustomerService, LocalCustomerService>(); builder.Services.AddTransient<IExpenseService, MockExpenseService>(); builder.Services.AddTransient<IInventoryService, MockInventoryService>(); builder.Services.AddTransient<INotificationService, MockNotificationService>(); builder.Services.AddTransient<IPaymentService, LocalPaymentService>(); builder.Services.AddSingleton<IProductRepository, ProductRepository>(); builder.Services.AddTransient<IProductService, LocalProductService>(); builder.Services.AddTransient<IPurchaseService, LocalPurchaseService>(); builder.Services.AddTransient<IReturnService, MockReturnService>(); builder.Services.AddSingleton<ISettingsService, MockSettingsService>(); builder.Services.AddSingleton<ISubscriptionService, LocalSubscriptionService>(); builder.Services.AddTransient<ISyncService, MockSyncService>(); builder.Services.AddSingleton<ICompanyUserRepository, CompanyUserRepository>(); builder.Services.AddTransient<IUserService, LocalUserService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();
        AveroNova.App.UI.Helpers.StartupLog.Write("CreateMauiApp built");
        return app;
    }
}
