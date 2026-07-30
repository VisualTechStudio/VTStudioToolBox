using System.Threading;
using System.Threading.Tasks;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.Auth;

public interface IAuthService
{
    UserIdentity? CurrentUser { get; }
    bool IsLoggedIn { get; }

    Task<UserIdentity> LoginAsync(AuthProvider provider, CancellationToken ct = default);
    Task LogoutAsync();
}
