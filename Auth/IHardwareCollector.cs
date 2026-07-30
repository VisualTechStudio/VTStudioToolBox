using VTStudioToolBox.Models;

namespace VTStudioToolBox.Auth;

public interface IHardwareCollector
{
    HardwareInfo Collect();
}
