using System.Threading.Tasks;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.Auth;

public interface IAnalyticsService
{
    void TrackAppLaunch(HardwareInfo hardware);
    void TrackToolUsage(string toolName);
    Task FlushAsync();
}
