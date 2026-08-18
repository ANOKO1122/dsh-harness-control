# DeepSeek Harness 控制台

一个 Windows 小工具，用于一键启动 / 停止 / 重启 DeepSeek Harness。

## 功能

- 自动检测 DeepSeek Harness 安装目录（默认 `%USERPROFILE%\deepseek-harness`）
- 一键启动：打开新命令行窗口运行 `node --import tsx/esm apps/cli/src/bin.ts web`
- 一键停止：自动结束 Harness 相关 Node / CMD 进程
- 一键重启：先停止再启动

## 使用

直接运行 `bin/DeepSeekHarnessControl.exe`，或双击桌面快捷方式。

## 从源码构建

```powershell
.\build.ps1
```

构建依赖 Windows 自带的 .NET Framework C# 编译器：

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

输出到 `bin\DeepSeekHarnessControl.exe`。

## 目录

```text
├── bin/
│   └── DeepSeekHarnessControl.exe   # 预编译好的控制台程序
├── harness-app/
│   └── Program.cs                    # C# 源码
├── build.ps1                         # 一键重新编译脚本
└── README.md
```
