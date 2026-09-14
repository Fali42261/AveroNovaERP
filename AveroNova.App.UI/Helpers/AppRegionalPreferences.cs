using System.Globalization;
using Microsoft.Maui.Storage;

namespace AveroNova.App.UI.Helpers;

public static class AppRegionalPreferences
{
    public const string LanguageKey = "regional.language";
    public const string DateFormatKey = "regional.date_format";
    public const string CurrencyCodeKey = "regional.currency_code";
    public const string CurrencySymbolKey = "regional.currency_symbol";
    public const string TimeZoneKey = "regional.time_zone";

    public static string Language => Preferences.Default.Get(LanguageKey, "en");
    public static string DateFormat => Preferences.Default.Get(DateFormatKey, "dd MMM yyyy");
    public static string CurrencyCode => Preferences.Default.Get(CurrencyCodeKey, "INR");
    public static string CurrencySymbol => Preferences.Default.Get(CurrencySymbolKey, "₹");
    public static string TimeZone => Preferences.Default.Get(TimeZoneKey, "Asia/Kolkata");

    public static void Apply(string language, string dateFormat, string currencyCode, string currencySymbol, string timeZone, bool persist)
    {
        language = string.IsNullOrWhiteSpace(language) ? "en" : language;
        dateFormat = string.IsNullOrWhiteSpace(dateFormat) ? "dd MMM yyyy" : dateFormat;
        currencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "INR" : currencyCode;
        currencySymbol = string.IsNullOrWhiteSpace(currencySymbol) ? "₹" : currencySymbol;
        timeZone = string.IsNullOrWhiteSpace(timeZone) ? "Asia/Kolkata" : timeZone;

        if (persist)
        {
            Preferences.Default.Set(LanguageKey, language);
            Preferences.Default.Set(DateFormatKey, dateFormat);
            Preferences.Default.Set(CurrencyCodeKey, currencyCode);
            Preferences.Default.Set(CurrencySymbolKey, currencySymbol);
            Preferences.Default.Set(TimeZoneKey, timeZone);
        }

        var cultureName = language switch
        {
            "hi" => "hi-IN",
            "ur" => "ur-IN",
            _ => "en-IN"
        };

        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
        catch (CultureNotFoundException)
        {
            var culture = CultureInfo.GetCultureInfo("en-IN");
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }
    }

    public static void ApplyStored() => Apply(Language, DateFormat, CurrencyCode, CurrencySymbol, TimeZone, persist: false);

    public static string FormatDate(DateTime value) => value.ToString(DateFormat, CultureInfo.CurrentCulture);
}
