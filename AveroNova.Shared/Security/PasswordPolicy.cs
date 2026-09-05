namespace AveroNova.Shared.Security;

public static class PasswordPolicy
{
    public const string RequirementMessage = "Password must be at least 6 characters and include uppercase, lowercase, number, and special character.";

    public static bool IsStrong(string? password)
        => !string.IsNullOrWhiteSpace(password)
           && password.Length >= 6
           && password.Any(char.IsUpper)
           && password.Any(char.IsLower)
           && password.Any(char.IsDigit)
           && password.Any(ch => !char.IsLetterOrDigit(ch));
}
