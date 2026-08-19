using AveroNova.App.UI.Models;
using AveroNova.Domain.Constants;

namespace AveroNova.App.UI.ViewModels;

/// <summary>
/// Builds registration Step 3 plan cards from catalog data plus UI presentation.
/// Paid checkout is not implemented; only Free Trial is selectable.
/// </summary>
public static class RegistrationPlanCatalog
{
    public static IReadOnlyList<RegisterPlanOption> Create(IReadOnlyList<SubscriptionPlanModel>? catalog)
    {
        _ = catalog;
        return
        [
            CreateFreeTrial(),
            CreateComingSoon(
                SubscriptionPlanCodes.Pro,
                "Pro",
                "Advanced AveroNova tools designed for growing businesses that need more automation and control.",
                [
                    "Everything included in Free Trial",
                    "Advanced Billing / POS",
                    "Advanced Inventory",
                    "Advanced Reports",
                    "Multiple Users",
                    "Advanced Role & Permission Management",
                    "Cloud/Online Sync enhancements",
                    "Additional business automation"
                ]),
            CreateComingSoon(
                SubscriptionPlanCodes.Business,
                "Business",
                "A complete business management package for teams, growing operations, and multiple business locations.",
                [
                    "Everything in Pro",
                    "Multi-Branch Support",
                    "Branch Management",
                    "Advanced Inventory Management",
                    "Advanced Reports & Analytics",
                    "Multiple Users",
                    "Advanced Roles & Permissions",
                    "Centralized Business Management",
                    "Advanced Synchronization",
                    "Business-level controls"
                ]),
            CreateComingSoon(
                SubscriptionPlanCodes.Enterprise,
                "Enterprise",
                "Enterprise-level AveroNova capabilities for larger organizations with advanced operational and management requirements.",
                [
                    "Everything in Business",
                    "Multiple Locations / Branches",
                    "Enterprise Users & Permissions",
                    "Advanced Analytics & Reporting",
                    "Custom Workflows",
                    "Enterprise Integrations",
                    "Advanced Security & Controls",
                    "Custom Configuration",
                    "Enterprise Support",
                    "Future enterprise capabilities"
                ])
        ];
    }

    private static RegisterPlanOption CreateFreeTrial()
        => new()
        {
            Id = SubscriptionPlanCodes.FreeTrial,
            Name = "Free Trial",
            PriceText = "Free",
            ValidityText = "15-day free trial",
            Description = "Get started with AveroNova free for 15 days and explore the currently available ERP features before choosing a paid plan.",
            Badge = string.Empty,
            Status = "Available",
            PackageSummaryHeading = "Package Summary",
            FeatureSectionHeading = "Benefits",
            Features = CurrentFreeTrialBenefits
                .Select(text => new PlanFeatureItem { Text = text, IsIncluded = true })
                .ToList(),
            IsAvailable = true,
            IsComingSoon = false,
            IsSelected = false
        };

    private static RegisterPlanOption CreateComingSoon(
        string id,
        string name,
        string summary,
        IReadOnlyList<string> features)
        => new()
        {
            Id = id,
            Name = name,
            PriceText = "Coming Soon",
            ValidityText = "Not available yet",
            Description = summary,
            Badge = "Coming Soon",
            Status = "ComingSoon",
            PackageSummaryHeading = "Package Summary",
            FeatureSectionHeading = "Planned Benefits",
            Features = features.Select(text => new PlanFeatureItem { Text = text, IsIncluded = false }).ToList(),
            IsAvailable = false,
            IsComingSoon = true,
            IsSelected = false
        };

    /// <summary>
    /// Currently shipped Free Trial capabilities. Do not list future or unpaid-plan features here.
    /// </summary>
    private static readonly string[] CurrentFreeTrialBenefits =
    [
        "15-day free access",
        "No payment required",
        "Dashboard",
        "Company Management",
        "Customer Management",
        "Product Management",
        "Inventory",
        "Sales & Billing",
        "Purchase",
        "Payments",
        "Reports",
        "Users & Roles",
        "Offline-first support",
        "Automatic sync when internet is available"
    ];
}
