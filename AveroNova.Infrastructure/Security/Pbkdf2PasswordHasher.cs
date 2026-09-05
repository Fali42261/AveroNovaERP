using AveroNova.Application.Interfaces.Security;

namespace AveroNova.Infrastructure.Security;

/// <summary>
/// Server adapter over the shared PBKDF2 hasher. Never log passwords or hashes.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly Shared.Security.Pbkdf2PasswordHasher _inner = new();

    public string HashPassword(string password) => _inner.HashPassword(password);

    public bool VerifyPassword(string password, string passwordHash) => _inner.VerifyPassword(password, passwordHash);
}
