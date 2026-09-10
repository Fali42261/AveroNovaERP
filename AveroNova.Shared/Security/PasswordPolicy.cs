namespace AveroNova.Shared.Security;

public static class PasswordPolicy
{
    public const string RequirementMessage = "Password must be 10 to 128 characters and include uppercase, lowercase, number, and special character.";

    public static bool IsStrong(string? password)
        => !string.IsNullOrWhiteSpace(password)
           && password.Length is >= 10 and <= 128
           && password.Any(char.IsUpper)
           && password.Any(char.IsLower)
           && password.Any(char.IsDigit)
           && password.Any(ch => !char.IsLetterOrDigit(ch) && !char.IsWhiteSpace(ch));
}
