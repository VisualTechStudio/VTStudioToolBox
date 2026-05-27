using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VTStudioToolBox
{
    internal class ChangeLog
    {
        public const string Log = "[DevBuild] 0.1 (Patch01 Build.2601280407)\n「New」首次构建\n「Add」构建GUI\n「Add」测试控件\n\n" +
                                  "[Alpha] 0.1 (Patch02 Build.2601280547)\n「New」开发预发布标识符转入Alpha 即团队内部测试\n「Add」仪表盘、设置UI\n「Add」设置的关于和更新日志\n\n" +
                                  "[Alpha] 0.1 (Patch03 Build.2601281837)\n「Fix」侧边栏展开UI自适应\n「Remove」测试控件\n\n" +
                                  "[Alpha] 0.1 (Patch04 Build.2601282011)\n「New」仪表盘硬件系统信息\n\n" +
                                  "[Preview] 0.1 (Patch05 Build.2601282129)\n「New」开发预发布标识符转入Preview 即小范围内部测试\n「Add」更全面的仪表盘硬件系统信息\n「Fix」仪表盘硬件系统信息缓存加载失败\n\n" +
                                  "[Preview] 0.2 (Build.2601282210)\n「New」仪表盘新UI\n「Add」系统信息安装时间和运行时间\n「Add」硬件信息AMD系列CPU的基准频率\n「Add」硬件信息CPU线程数\n「Add」硬件信息内存插槽及频率信息\n「Add」硬件信息硬盘声卡网卡显示器信息\n「Fix」副标题的梦话\n「Fix」高DPI设备的UI显示问题\n「Fix」硬件信息显示器信息显示为通用即插即用监视器\n「Fix」窗口限制导致的内存溢出崩溃\n\n" +
                                  "[Preview] 0.2 (Patch01 Build.2601290105)\n「New」设置新UI\n「New」用户协议\n「New」参与开发的贡献者名单\n「New」开源协议\n「Fix」侧边栏宽度过大\n「Fix」Windows主题错误\n\n" +
                                  "[Preview] 0.2 (Patch02 Build.2601291830)\n「New」硬件信息从纯WMI方案转向使用Hardware.Info库\n「New」硬件信息添加内存DDR代数、频率、厂商、颗粒信息\n「New」硬件信息添加GPU驱动版本、驱动日期信息\n「Fix」高DPIUI自适应失效问题\n「Fix」高DPIUI自适应时闪屏问题\n\n" +
                                  "[Preview] 0.2 (Patch03 Build.2601291850)\n「Fix」UI问题\n\n" +
                                  "[Beta] 0.3 (Build.2601292250)\n「New」开发预发布标识符转入Beta 即面对公众的第一个测试版本\n「Add」关于的官方网站、GithubRepo、GNU跳转按钮\n\n" +
                                  "[Beta] 0.4 (Build.2602042302)\n「New」实用工具模块\n「Add」硬件检测分类（CPU-Z、Core Temp、AIDA64、HWiNFO、GPU-Z、FurMark、鲁大师）\n「Add」磁盘工具分类（CrystalDiskMark、CrystalDiskInfo、DiskGenius、SpaceSniffer）\n「Add」系统工具分类（Dism++、Geek Uninstaller、HEU KMS Activator）\n「Add」工具卡片设计和响应式布局\n「Add」图标提取功能，显示工具原生图标\n「New」性能优化\n「Remove」Hardware.Info库，改用直接WMI查询\n「Add」并行WMI查询，提升仪表盘加载速度\n「Add」硬件信息缓存机制\n「New」用户体验改进\n「Fix」因移除Hardware.Info库导致的集成显卡显存显示不准确问题\n「Fix」修复编译警告，使用空合并运算符提供默认值，避免null值转换警告\n\n";
    }
}
