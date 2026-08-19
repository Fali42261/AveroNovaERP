namespace AveroNova.Domain.Constants
{
    public static class SubscriptionPlanCodes
    {
        public const string FreeTrial = "FreeTrial";
        public const string Pro = "Pro";
        public const string Business = "Business";
        public const string Enterprise = "Enterprise";
    }

    public static class SubscriptionMessages
    {
        public const string FreeTrialExpired = "Your free trial has expired.";
        public const string FreeTrialExpiredAccess =
            "Your free trial has expired. Please continue with a subscription to access AveroNova.";
        public const string FreeTrialExpiresTomorrow = "Your Free Trial expires tomorrow.";
        public const string PaidSubscriptionComingSoon = "Paid subscription options will be available soon.";
        public const string ModuleNotIncluded = "This module is not included in your company subscription.";
        public const string PermissionDenied = "You do not have permission to access this module.";
        public const string CompanyContextRequired = "A company context is required.";
        public const string UserNotInCompany = "You do not belong to this company.";
    }

    public static class SubscriptionModules
    {
        public const string Dashboard = "dashboard";
        public const string Company = "company";
        public const string Customers = "customers";
        public const string Products = "products";
        public const string Inventory = "inventory";
        public const string Sales = "sales";
        public const string Purchase = "purchase";
        public const string Payments = "payments";
        public const string Reports = "reports";
        public const string Settings = "settings";
        public const string Expenses = "expenses";

        public static IReadOnlyList<string> Catalog { get; } =
        [
            Dashboard,
            Company,
            Customers,
            Products,
            Inventory,
            Sales,
            Purchase,
            Payments,
            Reports,
            Settings,
            Expenses
        ];

        public static string DisplayName(string moduleKey) => moduleKey switch
        {
            Dashboard => "Dashboard",
            Company => "Company",
            Customers => "Customers",
            Products => "Products",
            Inventory => "Inventory",
            Sales => "Sales",
            Purchase => "Purchase",
            Payments => "Payments",
            Reports => "Reports",
            Settings => "Settings",
            Expenses => "Expenses",
            _ => moduleKey
        };
    }
}
