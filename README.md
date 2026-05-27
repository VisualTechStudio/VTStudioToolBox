# VTStudioToolBox

A developer's toolkit on Windows - 集成系统信息检测与硬件工具管理的专业Windows工具箱

## 功能特性

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
├── Helpers/                   # 辅助工具类
│   ├── FileCacheManager.cs     # 文件缓存管理器
│   └── SystemInfo.cs           # 系统信息数据模型
├── Models/                    # 业务数据模型
│   └── Projectltem.cs          # 项目项抽象模型
├── Properties/                # 项目配置
│   ├── PublishProfiles/        # MSIX发布配置
│   │   ├── win-arm64.pubxml
│   │   ├── win-x64.pubxml
│   │   └── win-x86.pubxml
│   └── launchSettings.json     # 开发环境启动设置
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
├── Views/                     # UI页面
│   ├── DashboardPage.xaml     # 仪表盘页面
│   ├── DashboardPage.xaml.cs
│   ├── UtilitiesPage.xaml     # 工具中心页面
│   ├── UtilitiesPage.xaml.cs
│   ├── SettingsPage.xaml      # 设置页面
│   └── SettingsPage.xaml.cs
├── App.xaml                   # 应用入口定义
├── App.xaml.cs                # 应用生命周期管理
├── MainWindow.xaml            # 主窗口布局
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

采用 MVVM Lite 架构模式：

View Layer: MainWindow → DashboardPage → UtilitiesPage → SettingsPage
    ↓ Data Binding / Events
ViewModel Layer: 业务逻辑直接在Code-Behind实现，轻量化设计
    ↓ Method Calls
Model Layer: SystemInfo(DTO) + FileCacheManager(数据持久化)
    ↓ WMI / Registry / DirectX
System Layer: Windows Management Instrumentation、Windows Registry、DirectX Graphics Infrastructure

## 快速开始

### 环境要求

组件 | 最低版本 | 推荐版本
----- | -------- | --------
Windows OS | 10 1809 (10.0.17763.0) | 11 22H2+
.NET SDK | 8.0.100 | 8.0.300+
Visual Studio | 2022 17.4 | 2022 17.10+
Windows App SDK | 1.6 | 1.6.250108002

### 安装依赖

```powershell
# 安装 .NET 8 SDK
winget install Microsoft.DotNet.SDK.8

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

# 运行发布版本
./publish/x64/VTStudioToolBox.exe
```


## 核心模块详解

### 1. 应用入口 (App.xaml.cs)

职责：应用生命周期管理、全局主题配置

```csharp
public partial class App : Application
{
    private Window? m_window;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();
        
        // 全局深色主题配置
        if (m_window.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = ElementTheme.Dark;
        }
        
        m_window.Activate();
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

职责：系统信息采集、缓存管理、UI展示

数据采集流程：

LoadSystemInfoWithCacheAsync()
    ↓
检查缓存 (FileCacheManager.Get<SystemInfo>)
    ↓
缓存存在 → 立即显示缓存 → 后台刷新数据 → 更新缓存+UI
缓存不存在 → 显示加载中 → 采集系统信息 → 更新缓存+UI


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

## 技术栈

### 核心框架

组件 | 版本 | 用途
----- | ---- | ----
Microsoft.WindowsAppSDK | 1.6.250108002 | WinUI 3框架、Windows API封装
.NET | 8.0 | 运行时、基础类库
WinUI 3 | 1.6 | UI框架、控件库

### 第三方依赖

包名 | 版本 | 用途
----- | ---- | ----
SharpDX | 4.2.0 | DirectX API访问、GPU信息获取
SharpDX.Direct2D1 | 4.2.0 | Direct2D绑定
SharpDX.Direct3D11 | 4.2.0 | Direct3D 11绑定
System.Management | 10.0.2 | WMI查询支持
Microsoft.Management.Infrastructure | 3.0.0 | WMI管理基础设施
System.Drawing.Common | 8.0.0 | GDI+图像操作、图标提取

### 系统依赖

- WMI (Windows Management Instrumentation)：硬件信息采集
- Windows Registry：显卡显存信息读取
- DXGI (DirectX Graphics Infrastructure)：GPU信息获取

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

### 并行优化

- WMI查询采用 Task.Run() 并行执行
- 设置10秒超时防止长时间阻塞
- 显示器信息单独后台加载，不阻塞主流程

### 异常处理

- 所有WMI查询都有独立的try-catch包裹
- 单个查询失败不影响其他信息采集
- 缓存读取失败静默降级到实时采集

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
Copyright (C) 2024 VTStudio

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
