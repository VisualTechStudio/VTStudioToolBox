namespace VTStudioToolBox.Models;

public enum AuthProvider
{
    GitHub,
    Microsoft,
    Google,
    Steam
}

public sealed record UserIdentity
{
    public AuthProvider Provider { get; init; }
    public string UserId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public string Email { get; init; } = "";
}
