# R6ConfigBridge

Windows 本地彩虹六号配置迁移工具：国际服 ⇄ 腾讯 WeGame 国服。MVP 0.1，非官方项目，与 Ubisoft／腾讯无隶属或背书关系。

## 下载与运行

从本仓库 Releases 下载 Windows x64 ZIP，解压后双击 `R6ConfigBridge.exe`。也可使用仓库 `dist/windows/R6ConfigBridge.exe`。

C# + WinForms + .NET Framework 4.x，不是 WinUI 3 或 Native AOT。没有浏览器外壳、开机启动项或后台服务；关闭窗口即退出。当前图标版 EXE 为 48,128 字节；运行时内存与完整运行环境成本尚未测量，不能以 EXE 大小代替内存数据。

## 用法

1. 确认两服账号配置，支持自动发现标准 Documents 路径及手动选择 GameSettings.ini。多账号不猜选。
2. 两栏对照，按类别／字段搜索或仅显示差异，逐项勾选。
3. 选择迁移方向，预览所有所选修改。筛选隐藏的勾选项也包含在选择中。
4. 正常退出两服后确认应用，工具先备份再写入并回读校验，不会替用户关闭游戏。
5. 启动目标服核对效果，退出重启后检查是否保留；云同步可能覆盖本地结果，工具不更改启动器同步选项。

备份保存在 EXE 旁 `backups/`，保留 `.ini` 和同名 `.ini.xml`；恢复前也备份当前状态。若配置在预览后变化，或迁移后又被游戏修改，工具拒绝直接覆盖。

## 能力及未完成项目

- 已在一台 Windows 实机加载两份各 158 字段的配置，其中 132 项在迁移白名单。
- 涵盖鼠标／倍率灵敏度、按住或切换行为、手柄、显示、画质、声音及辅助功能；内部硬件、设备 ID、网络字段不默认迁移。
- **具体动作键位、鼠标侧键绑定尚未实现。** INI 的按住／切换行为不等于完整键位。
- 核心 14 项检查通过；真实配置副本的双向迁移、非白名单保留和逐字节恢复通过。
- **尚未对真实游戏设置执行迁移，也未验收游戏内生效、重启保留或云同步行为。** 不保证两服同名枚举的语义完全相同。
- 尚未验证所有版本、显示缩放、多账号与不同编码。UTF-16 配置不支持，重复字段拒绝；不提供任意 Unicode 值编辑。
- 程序未签名，不保证反作弊兼容或零封号风险。不注入游戏、不读写游戏内存、不修改游戏程序。

## 源码与构建

- `App.cs`：WinForms 界面及集成 smoke 模式。
- `Core.cs`：INI 解析、白名单、备份／应用／恢复。
- `Tests.cs`：核心行为测试。
- `assets/`：原创双向箭头图标（SVG／PNG／多尺寸 ICO）。
- `build.cmd`：先构建并运行测试，再编译 Windows x64 EXE，内嵌图标。
- `dist/windows/`：已验证的 Windows 编译产物快照。

Windows 项目目录运行：

```bat
build.cmd
```

使用系统 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`，无 NuGet 依赖。生成根目录 Tests.exe 与 R6ConfigBridge.exe；不自动更新 dist 快照。macOS 不能原生构建／运行本 WinForms 程序。

副本 smoke 测试：

```bat
R6ConfigBridge.exe --smoke C:\R6ConfigBridgeTest\smoke-new
```

仅使用新的专用目录。该模式读取真实配置，但只向测试副本写入；会保留私人样本、备份和窗口截图，不要提交它们。不代表用户桌面交互或游戏内验收已通过。
