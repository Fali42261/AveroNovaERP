using AveroNova.Shared.Security;
using Xunit;

namespace AveroNova.OfflineAuth.Tests;

public sealed class PasswordPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Ab1!")]
    [InlineData("abcdef!")]
    [InlineData("ABCDEF1!")]
    [InlineData("Abcdef!")]
    [InlineData("Abcdef1")]
    public void WeakPasswordsAreRejected(string? password) => Assert.False(PasswordPolicy.IsStrong(password));

    [Theory]
    [InlineData("Abc1!x")]
    [InlineData("Strong@123")]
    public void StrongPasswordsAreAccepted(string password) => Assert.True(PasswordPolicy.IsStrong(password));
}
