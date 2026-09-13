# PawnIO 模块说明

## 获取 PawnIO 模块

要使用 PawnIO 读取 CPU 电压，需要下载对应 CPU 品牌的模块。

### 下载步骤

1. 访问 PawnIO.Modules 发布页面：
   https://github.com/namazso/PawnIO.Modules/releases/latest

2. 下载最新的发布包（例如：PawnIO.Modules-0.2.11.zip）

3. 解压下载的文件

4. 根据你的 CPU 品牌，将对应模块复制到 `Assets/` 目录：
   - **Intel CPU** → 复制 `IntelMSR.amx` 或 `IntelMSR.bin`
   - **AMD CPU** → 复制 `RyzenSMU.amx` 或 `RyzenSMU.bin`

### 安装 PawnIO 驱动

如果 PawnIO 驱动未安装，请访问：
https://pawnio.eu

下载并运行安装程序。

## 支持的模块格式

- `.amx` - PawnIO 模块格式
- `.bin` - 二进制格式（两种格式均支持）

## 支持的 CPU

| 品牌 | 模块文件 | 读取方式 |
|------|----------|----------|
| Intel | IntelMSR.amx/.bin | MSR_IA32_PERF_STATUS (0x198) |
| AMD | RyzenSMU.amx/.bin | SMU 电压命令 |

## 故障排除

1. 确保 PawnIO 驱动已安装
2. 确保对应模块已放置在 `Assets/` 目录
3. 检查日志输出中的错误信息
