namespace DesktopHub.Infrastructure.Firebase.Models;

/// <summary>
/// One row from the listTenantUsers Cloud Function response. The Username
/// field is the server-decrypted display name (Cloud Functions hold the
/// AES-GCM key; the client only ever sees plaintext here, never the
/// ciphertext on the users/{user_id}/username_ct node).
/// </summary>
public sealed class TenantUserEntry
{
    public string UserId { get; init; } = "";
    public string Username { get; init; } = "";
    public bool IsAdmin { get; init; }
    public bool IsDev { get; init; }
    public bool IsEditor { get; init; }
    public string? LastSeen { get; init; }
}
