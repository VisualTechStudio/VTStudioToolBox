<div align="center">
             <h1>VTStudioToolBox</h1>
             A developer's toolkit on Windows - 集成系统信息检测与硬件工具管理的专业Windows工具箱
             <br>
             <br>
             <img src="https://img.shields.io/github/release/VisualTechStudio/VTStudioToolBox" />
             <img src="https://img.shields.io/github/stars/VisualTechStudio/VTStudioToolBox" />
             <br>
             <br>
             <a href="README_en_US.md">English Readme</a>
</div>

## 功能特性

### 用户认证系统

支持通过本地 Loopback 回调在系统默认浏览器中完成第三方登录：

平台 | 协议 | 说明
---- | ---- | ----
GitHub | OAuth 2.0 + client_secret | 用户头像、邮箱
Microsoft | OAuth 2.0 + PKCE | 用户头像（Graph API）、邮箱
Steam | OpenID 2.0 | 用户头像、昵称

- 登录状态本地持久化，重启自动恢复
- 侧边栏底部显示用户头像与登录状态
- 支持一键退出登录

### 系统信息检测模块

检测类别 | 检测项 | 实现方式
--------- | ------ | --------
处理器 | CPU型号、核心数、线程数、主频 | WMI Win32_Processor
内存 | 容量、类型(DDR4/DDR5)、频率、品牌颗粒 | WMI Win32_PhysicalMemory
显卡 | 型号、显存容量、驱动版本/日期 | WMI + DirectX + Registry
主板 | 制造商、型号 | WMI Win32_BaseBoard
存储 | 硬盘型号、容量 | WMI Win32_DiskDrive
网络 | 物理网卡列表 | WMI Win32_NetworkAdapter
音频 | 音频设备列表 | WMI Win32_SoundDevice
显示器 | 显示器型号 | WMI Win32_PnPEntity
系统 | OS版本、安装时间、开机时长、计算机名 | WMI Win32_OperatingSystem

### 实时硬件监控模块

检测类别 | 检测项 | 实现方式
--------- | ------ | --------
CPU | 实时频率、使用率、温度、电压、功耗 | Performance Counter + LibreHardwareMonitorLib + ACPI Thermal Zone
GPU | 核心频率、显存频率、使用率、温度、电压、功耗 | LibreHardwareMonitorLib
风扇 | 转速（CPU/GPU/Mid） | ASUS ATKACPI驱动 / LibreHardwareMonitorLib
内存 | 使用率 | LibreHardwareMonitorLib
硬盘 | 占用空间 | LibreHardwareMonitorLib

传感器数据采集采用三层回退策略：
1. **LibreHardwareMonitorLib** — 优先读取（GPU数据全部正常，部分AMD平台CPU数据受限）
2. **HWiNFO64共享内存** — 若HWiNFO运行中，从其共享内存读取
3. **Windows API回退** — CPU频率使用Performance Counter "Actual Frequency"（实时频率），温度使用ACPI热区

ASUS设备风扇读取通过 `\\.\ATKACPI` 内核驱动（IOCTL `0x0022240C`）实现，使用DSTS方法读取设备状态，支持CPU Fan、GPU Fan、Mid Fan三个风扇通道。

### 网络检测模块

检测协议 | 检测项 | 实现方式
--------- | ------ | --------
RFC 3489 | NAT类型（Open Internet / Full Cone / Restricted Cone / Port Restricted Cone / Symmetric） | STUN UDP绑定测试 + CHANGE-REQUEST
RFC 5780 | 映射行为（Endpoint Independent / Address Dependent / Address and Port Dependent） | STUN UDP绑定测试 + OTHER-ADDRESS + 多步状态机
RFC 5780 | 过滤行为（Endpoint Independent / Address Dependent / Address and Port Dependent） | CHANGE-REQUEST change IP/port 组合测试

- 共享STUN服务器选择：两种协议共用同一服务器下拉框，一次点击同时执行两项检测
- 并行检测：RFC 3489与RFC 5780通过Task.WhenAll并行执行，缩短总耗时
- 支持9个预置STUN服务器，支持手动输入自定义服务器地址
- DNS-over-HTTPS解析，支持系统DNS回退

### 主题系统

模式 | 说明
---- | ----
跟随系统 | 自动检测 Windows 浅色/深色模式，实时跟随切换
深色模式 | 手动固定深色主题
浅色模式 | 手动固定浅色主题

- 通过 `Windows.UI.ViewManagement.UISettings.ColorValuesChanged` 监听系统主题变化
- 侧边栏、页面、Flyout 均支持主题热切换
- 标题栏按钮颜色自动适配

### 隐私与数据

- 硬件信息采集仅收集 CPU/GPU/RAM/OS 型号，**不采集**磁盘序列号、MAC 地址、主机名、用户名
- 匿名设备 GUID 持久化存储于本地
- 用户体验改进计划（预留，默认关闭）

### 性能优化特性

- 智能缓存机制：首次检测后缓存至 %LOCALAPPDATA%\VTStudioToolBox\Cache，缓存有效期24小时
- 并行查询：多WMI查询并行执行，减少信息采集耗时
- 后台刷新：缓存数据静默更新，不阻塞UI线程
- 秒开体验：已缓存数据立即显示，无需等待

### 工具集成中心

分类 | 工具名称 | 功能描述
----- | -------- | --------
CPU检测 | CPU-Z | 处理器详细信息、缓存、主板信息
系统检测 | AIDA64 | 全面硬件检测、稳定性测试
硬件监控 | HWiNFO | 实时传感器监控、日志记录
显卡检测 | GPU-Z | GPU规格、显存、BIOS版本
温度监控 | Core Temp | CPU核心温度实时监控
显卡测试 | FurMark | GPU压力测试、稳定性验证
磁盘性能 | CrystalDiskMark | 磁盘读写速度测试
磁盘健康 | CrystalDiskInfo | SSD/HDD健康状态、SMART信息
分区管理 | DiskGenius | 磁盘分区、数据恢复
空间分析 | SpaceSniffer | 磁盘空间可视化分析
系统维护 | Dism++ | Windows镜像维护、优化
软件卸载 | Geek Uninstaller | 强力卸载、残留清理
硬件检测 | 鲁大师 | 国产硬件检测工具
显示信息 | MonitorInfo | 显示器参数检测
系统激活 | HEU KMS Activator | Windows/Office激活

## 项目架构

### 目录结构

```
VTStudioToolBox/
├── Assets/                     # 应用资源
│   ├── Fonts/                   # 自定义字体
│   ├── LockScreenLogo.scale-200.png
│   ├── SplashScreen.scale-200.png
│   ├── Square150x150Logo.scale-200.png
│   ├── Square44x44Logo.scale-200.png
│   ├── Square44x44Logo.targetsize-24_altform-unplated.png
│   ├── StoreLogo.png
│   ├── Wide310x150Logo.scale-200.png
│   ├── dwg.png
│   ├── kpdw.png
│   └── xcy.png
├── Auth/                      # 认证模块
│   ├── IAuthService.cs          # 认证服务接口
│   ├── IHardwareCollector.cs    # 硬件采集接口
│   ├── IAnalyticsService.cs     # 数据上报接口
│   └── AuthManager.cs           # OAuth/OpenID 认证实现
├── Helpers/                   # 辅助工具类
│   ├── DashboardSettings.cs    # 仪表盘设置（刷新间隔等）
│   ├── DnsResolver.cs          # DNS-over-HTTPS解析器
│   ├── EulaHelper.cs           # EULA协议管理
│   ├── FileCacheManager.cs     # 文件缓存管理器
│   ├── FirewallHelper.cs       # Windows防火墙规则管理
│   ├── LanguageHelper.cs       # 多语言管理
│   ├── Logger.cs               # 文件日志记录器
│   ├── SystemInfo.cs           # 系统信息数据模型
│   ├── ThemeHelper.cs          # 主题管理（含跟随系统）
│   └── WindowHelper.cs         # 窗口辅助类
├── Models/                    # 业务数据模型
│   ├── AnalyticsEvent.cs       # 分析事件模型
│   ├── HardwareInfo.cs         # 硬件信息模型
│   ├── Projectltem.cs          # 项目项抽象模型
│   ├── ToolInfo.cs             # 工具信息模型
│   └── UserIdentity.cs         # 用户身份模型
├── Network/                   # 网络检测模块
│   ├── NatType.cs              # NAT类型/映射行为/过滤行为枚举
│   ├── StunAttribute.cs        # STUN属性解析（RFC 3489/5389）
│   ├── StunClient.cs           # RFC 3489经典NAT类型检测客户端
│   ├── Stun5780Client.cs       # RFC 5780 NAT行为发现状态机
│   ├── StunMessage.cs          # STUN消息序列化/反序列化
│   └── StunServer.cs           # STUN服务器地址解析
├── Services/                  # 业务服务
│   ├── AnalyticsService.cs     # 异步数据上报（Channel队列）
│   ├── AsusFanReader.cs        # ASUS ATKACPI风扇读取
│   ├── HardwareCollector.cs    # 硬件信息采集（WMI）
│   ├── HardwareMonitorService.cs  # 硬件传感器监控（LibreHardwareMonitorLib）
│   └── HwInfoReader.cs         # HWiNFO64共享内存读取
├── Strings/                   # 多语言资源
│   ├── zh-CN.json              # 简体中文
│   ├── zh-TW.json              # 繁体中文（台湾）
│   ├── zh-HK.json              # 繁体中文（香港）
│   ├── ja-JP.json              # 日语
│   ├── ko-KR.json              # 韩语
│   ├── ko-KP.json              # 朝鲜语
│   ├── ru-RU.json              # 俄语
│   ├── th-TH.json              # 泰语
│   └── zh-CN-meow.json         # 喵体中文
├── Tools/                     # 第三方工具集
│   ├── AIDA64/
│   ├── CoreTemp/
│   ├── CrystalDiskInfo/
│   ├── CrystalDiskMark/
│   ├── Dism++/
│   ├── FurMark/
│   ├── GPUZ/
│   ├── Geek Uninstaller/
│   ├── hwinfo/
│   └── color/
├── ViewModels/                # 视图模型
│   └── UserViewModel.cs        # 用户状态视图模型
├── Views/                     # UI页面
│   ├── AdbCache.cs             # ADB设备缓存
│   ├── AndroidPage.xaml        # Android设备管理页面
│   ├── AndroidPage.xaml.cs
│   ├── DashboardPage.xaml      # 仪表盘页面
│   ├── DashboardPage.xaml.cs
│   ├── MacOSPage.xaml          # Hackintosh页面
│   ├── MacOSPage.xaml.cs
│   ├── NetworkPage.xaml        # 网络检测页面（RFC 3489 + RFC 5780）
│   ├── NetworkPage.xaml.cs
│   ├── SettingsPage.xaml       # 设置页面
│   ├── SettingsPage.xaml.cs
│   ├── UtilitiesPage.xaml      # 工具中心页面
│   └── UtilitiesPage.xaml.cs
├── App.xaml                   # 应用入口定义
├── App.xaml.cs                # 应用生命周期 + DI配置
├── MainWindow.xaml            # 主窗口布局（含侧边栏头像）
├── MainWindow.xaml.cs         # 主窗口逻辑
├── CacheMaanager.cs           # 缓存管理接口(预留)
├── ChangeLog.cs               # 变更日志常量
├── cfg.cs                     # 应用配置常量
├── LICENSE                    # GPLv3许可证
├── Package.appxmanifest       # MSIX打包清单
├── README.md                  # 项目文档
└── VTStudioToolBox.csproj     # MSBuild项目配置
```

### 架构设计

采用 MVVM + 依赖注入架构模式：

View Layer: MainWindow → DashboardPage → NetworkPage → UtilitiesPage → AndroidPage → SettingsPage (含仪表盘刷新间隔设置)
    ↓ x:Bind / Data Binding / Events
ViewModel Layer: UserViewModel (INotifyPropertyChanged)
    ↓ DI / Method Calls
Service Layer: AuthManager (OAuth/OpenID) + HardwareCollector (WMI) + HardwareMonitorService (LibreHardwareMonitorLib) + AsusFanReader (ATKACPI) + HwInfoReader (HWiNFO64共享内存) + AnalyticsService (Channel队列)
    ↓ Interface Isolation
Model Layer: UserIdentity + HardwareInfo + AnalyticsEvent + SystemInfo(DTO) + FileCacheManager
    ↓ WMI / Registry / DirectX / UDP / HTTP / Performance Counter / ACPI / ATKACPI
System Layer: Windows Management Instrumentation、Windows Registry、DirectX Graphics Infrastructure、STUN Protocol (RFC 3489/5780)、Performance Counter、ACPI Thermal Zone、ASUS ATKACPI Driver

## 快速开始

### 环境要求

组件 | 最低版本 | 推荐版本
----- | -------- | --------
Windows OS | 10 1809 (10.0.17763.0) | 11 22H2+
.NET SDK | 10.0.100 | 10.0.400+
Visual Studio | 2022 17.4 | 2022 17.10+
Windows App SDK | 1.6 | 1.6.250108002

### 安装依赖

```powershell
# 安装 .NET 10 SDK
winget install Microsoft.DotNet.SDK.10

# 安装 Windows App SDK 运行时
winget install Microsoft.WindowsAppSDK
```


### 编译构建

```powershell
# 克隆仓库
git clone https://github.com/Notepad233/VTStudioToolBox.git
cd VTStudioToolBox

# 恢复依赖
dotnet restore

# 开发构建 (x64)
dotnet build --configuration Debug --platform x64

# 发布构建 (x64)
dotnet publish --configuration Release --platform x64 --self-contained true --output ./publish/x64

# 发布构建 (x86)
dotnet publish --configuration Release --platform x86 --self-contained true --output ./publish/x86

# 发布构建 (ARM64)
dotnet publish --configuration Release --platform ARM64 --self-contained true --output ./publish/arm64
```


### 运行应用

```powershell
# 开发模式运行
dotnet run --configuration Debug --platform x64

# 运行发布版本（需要管理员权限，用于读取硬件传感器）
./publish/x64/VTStudioToolBox.exe
```

> **注意**：应用通过 `app.manifest` 请求管理员权限（`requireAdministrator`），LibreHardwareMonitorLib 和 ASUS ATKACPI 驱动需要管理员权限才能读取硬件传感器数据。


## 核心模块详解

### 1. 应用入口 (App.xaml.cs)

职责：应用生命周期管理、全局主题配置

```csharp
public partial class App : Application
{
    internal Window? m_window;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Logger.Init();
        LanguageHelper.Initialize();
        ThemeHelper.Initialize();

        // 依赖注入注册
        Services = ConfigureServices();

        m_window = new MainWindow();
        // ...
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<HardwareCollector>();
        services.AddSingleton<IHardwareCollector>(sp => sp.GetRequiredService<HardwareCollector>());
        services.AddSingleton<IAuthService, AuthManager>();
        services.AddSingleton<IAnalyticsService, AnalyticsService>(...);
        services.AddSingleton<UserViewModel>();
        return services.BuildServiceProvider();
    }
}
```


### 2. 主窗口 (MainWindow.xaml.cs)

职责：窗口管理、导航路由、EULA协议处理

核心功能：
- 沉浸式标题栏：ExtendsContentIntoTitleBar = true + 自定义Grid作为标题栏
- 亚克力效果：DesktopAcrylicBackdrop 实现毛玻璃效果
- 响应式窗口：DPI感知的窗口尺寸调整
- 导航路由：_pageRoutes 字典管理页面映射

```csharp
private readonly Dictionary<string, Type> _pageRoutes = new()
{
    ["dashboard"] = typeof(DashboardPage),
    ["utilities"] = typeof(UtilitiesPage),
    ["settings"] = typeof(SettingsPage),
};
```


### 3. 仪表盘页面 (DashboardPage.xaml.cs)

职责：系统信息采集、缓存管理、实时硬件监控、UI展示

布局结构：
- 左上：设备型号卡片
- 左侧：硬件信息卡片（制造商、主板、型号、CPU、内存、GPU、硬盘、网卡、声卡、显示器）
- 右上：系统信息卡片（计算机名、系统版本、安装时间、运行时间）
- 右下：系统使用率卡片（CPU频率/使用率/温度/电压/功耗、GPU核心/显存/使用率/温度/电压/功耗、内存使用率、风扇转速）

数据采集流程：

LoadSystemInfoWithCacheAsync()
    ↓
检查缓存 (FileCacheManager.Get<SystemInfo>)
    ↓
缓存存在 → 立即显示缓存 → 后台刷新数据 → 更新缓存+UI
缓存不存在 → 显示加载中 → 采集系统信息 → 更新缓存+UI

实时监控流程：

InitHardwareMonitorAsync() (异步，不阻塞UI)
    ↓
初始化 HardwareMonitorService (静态单例，页面切换不销毁)
    ↓
启动 DispatcherTimer (默认2秒，可在设置中调整: 100ms/200ms/500ms/1000ms/2000ms/5000ms)
    ↓
UpdateSensorData() → 读取CPU/GPU/风扇/内存/硬盘传感器数据 → 更新UI

WMI并行查询优化：

```csharp
var tasks = new List<Task>
{
    Task.Run(() => GetOSInfo(info)),
    Task.Run(() => GetComputerSystemInfo(info)),
    Task.Run(() => GetCPUInfo(info)),
    Task.Run(() => GetRAMInfo(info)),
    // ... 其他查询
};
Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(10));
```


### 4. 工具中心 (UtilitiesPage.xaml.cs)

职责：工具图标加载、进程启动、错误处理

图标加载机制：

```csharp
private void LoadIconFromPath(Image imageControl, string toolPath)
{
    if (File.Exists(toolPath))
    {
        using var icon = Icon.ExtractAssociatedIcon(toolPath);
        using var bitmap = icon.ToBitmap();
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        // ... 绑定到Image控件
    }
}
```


### 5. 缓存管理器 (Helpers/FileCacheManager.cs)

职责：基于文件的JSON缓存系统

技术特性：
- 存储路径：%LOCALAPPDATA%\VTStudioToolBox\Cache\{key}.json
- 序列化：System.Text.Json，CamelCase命名策略
- 过期策略：基于时间的自动过期清理
- 异常处理：静默失败，不影响主流程

### 6. 网络检测模块 (Network/)

职责：NAT类型检测与NAT行为发现

RFC 3489 经典NAT类型检测流程：

StunClient.QueryAsync()
    ↓
Test I: Binding Request → 获取MAPPED-ADDRESS + CHANGED-ADDRESS
    ↓
Test II: CHANGE-REQUEST (change IP+port) → OpenInternet / FullCone
    ↓
Test I #2: Binding Request to CHANGED-ADDRESS → 检测Symmetric NAT
    ↓
Test III: CHANGE-REQUEST (change port) → RestrictedCone / PortRestrictedCone

RFC 5780 NAT行为发现流程：

Stun5780Client.QueryAsync()
    ↓
Binding Test: Binding Request → 获取XOR-MAPPED-ADDRESS + OTHER-ADDRESS
    ↓
Filtering Test II: CHANGE-REQUEST (change IP+port) → EndpointIndependent Filtering
    ↓
Filtering Test III: CHANGE-REQUEST (change port) → AddressDependent / AddressAndPortDependent Filtering
    ↓
Mapping Test II: Binding Request to (OTHER_ADDRESS, server_port) → EndpointIndependent Mapping
    ↓
Mapping Test III: Binding Request to OTHER_ADDRESS → AddressDependent / AddressAndPortDependent Mapping

技术特性：
- 双协议并行检测：Task.WhenAll同时执行RFC 3489和RFC 5780
- STUN消息兼容：自动识别RFC 3489（MagicCookie=0）和RFC 5389（MagicCookie=0x2112A442）
- XOR地址解码：支持XOR-MAPPED-ADDRESS和OTHER-ADDRESS属性解析
- DNS-over-HTTPS：通过Cloudflare DoH解析服务器地址，支持系统DNS回退
- 防火墙管理：自动创建Windows防火墙规则允许UDP流量

## 技术栈

### 核心框架

组件 | 版本 | 用途
----- | ---- | ----
Microsoft.WindowsAppSDK | 1.6.250108002 | WinUI 3框架、Windows API封装
.NET | 10.0 | 运行时、基础类库
WinUI 3 | 1.6 | UI框架、控件库

### 第三方依赖

包名 | 版本 | 用途
----- | ---- | ----
Microsoft.Extensions.DependencyInjection | 10.0.0-preview.5 | 依赖注入容器
LibreHardwareMonitorLib | 0.9.6 | 硬件传感器监控（CPU/GPU/风扇/内存/硬盘）
SharpDX | 4.2.0 | DirectX API访问、GPU信息获取
SharpDX.Direct2D1 | 4.2.0 | Direct2D绑定
SharpDX.Direct3D11 | 4.2.0 | Direct3D 11绑定
System.Diagnostics.PerformanceCounter | 10.0.10 | CPU实时频率读取
System.Management | 10.0.2 | WMI查询支持
Microsoft.Management.Infrastructure | 3.0.0 | WMI管理基础设施
System.Drawing.Common | 10.0.0 | GDI+图像操作、图标提取

### 系统依赖

- WMI (Windows Management Instrumentation)：硬件信息采集
- Windows Registry：显卡显存信息读取、ASUS固件版本检测
- DXGI (DirectX Graphics Infrastructure)：GPU信息获取
- Performance Counter：CPU实时频率读取
- ACPI Thermal Zone (MSAcpi_ThermalZoneTemperature)：CPU温度读取
- ASUS ATKACPI驱动 (ATKWMIACPIIO)：ASUS设备风扇转速读取
- HWiNFO64共享内存：备选传感器数据源

## 开发指南

### 添加新工具

1. 将工具文件放入 Tools/{ToolName}/ 目录
2. 在 VTStudioToolBox.csproj 中添加Content引用

```xml
<Content Include="Tools\MyTool\mytool.exe">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```


3. 在 UtilitiesPage.xaml 中添加工具按钮和图标
4. 在 UtilitiesPage.xaml.cs 中添加图标加载和启动方法

### 扩展系统信息

1. 在 Helpers/SystemInfo.cs 中添加新字段
2. 在 DashboardPage.xaml.cs 中添加WMI查询方法
3. 在 DashboardPage.xaml 中添加展示控件

### 主题定制

```csharp
// 在 App.xaml.cs 中修改主题
rootElement.RequestedTheme = ElementTheme.Light;   // 浅色主题
rootElement.RequestedTheme = ElementTheme.Dark;    // 深色主题
rootElement.RequestedTheme = ElementTheme.Default; // 跟随系统
```


## 性能优化

### 缓存策略

- 缓存有效期：24小时（可在 FileCacheManager.Set() 中调整）
- 缓存键名：SystemInfo
- 缓存位置：%LOCALAPPDATA%\VTStudioToolBox\Cache\SystemInfo.json
- 仪表盘设置：%LOCALAPPDATA%\VTStudioToolBox\dashboard.json（刷新间隔、警告抑制）

### 并行优化

- WMI查询采用 Task.Run() 并行执行
- 设置10秒超时防止长时间阻塞
- 显示器信息单独后台加载，不阻塞主流程
- 硬件监控初始化异步执行，不阻塞UI线程
- 硬件监控服务使用静态单例，页面切换时复用，避免重复初始化

### 异常处理

- 所有WMI查询都有独立的try-catch包裹
- 单个查询失败不影响其他信息采集
- 缓存读取失败静默降级到实时采集
- 传感器读取失败时显示"--"，不影响其他传感器数据

## CI/CD

### 构建配置

平台目标：
- x64（主平台）
- x86（兼容旧系统）
- ARM64（ARM设备）

发布配置：
- SelfContained = true（自包含部署）
- PublishReadyToRun = true（Release模式）
- PublishTrimmed = true（Release模式）

### 自动化脚本示例

```powershell
# build.ps1 - 自动化构建脚本
param(
    [string]$Configuration = "Release",
    [string[]]$Platforms = @("x64", "x86", "ARM64")
)

foreach ($platform in $Platforms) {
    Write-Host "Building $platform..."
    dotnet publish --configuration $Configuration --platform $platform `
        --self-contained true `
        --output "./publish/$platform"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $platform"
        exit 1
    }
}

Write-Host "All builds completed successfully"
```


## 许可证

本项目采用 GNU General Public License v3.0 开源许可证。

VTStudioToolBox - A developer's toolkit on Windows
Copyright (C) 2016-2026 VisualTechStudio

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

详见 LICENSE 文件。

## 贡献指南

### 贡献流程

1. Fork 项目：创建个人仓库副本
2. 创建分支：git checkout -b feature/your-feature
3. 提交变更：git commit -m "feat: add your feature"
4. 推送分支：git push origin feature/your-feature
5. 创建PR：提交Pull Request

### 代码规范

- 命名约定：PascalCase（类、方法），camelCase（变量、参数）
- 注释规范：XML文档注释（公共API）
- 格式规范：遵循 .editorconfig 配置
- 异常处理：所有外部调用必须有try-catch

### 提交信息规范

<type>(<scope>): <description>

[optional body]

[optional footer]

Type：
- feat：新功能
- fix：Bug修复
- docs：文档更新
- refactor：代码重构
- perf：性能优化
- test：测试相关
- chore：构建/工具变更
