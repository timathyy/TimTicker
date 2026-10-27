# TimeTicker

TimeTicker 是一款面向个人的 Windows 本地日历与时间记录应用。项目当前处于 `v0.1.0` 工程基础建设阶段。

当前界面提供“今天、任务、日历、设置”四个主入口，并内置浅色与深色主题。整体视觉采用低对比表面、圆角控件和紫色强调色。

## 开发环境

- Windows 10 或 Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，当前由 `global.json` 固定到 `10.0.402`
- Visual Studio Code，或支持 .NET 10 与 WPF 的 Visual Studio

## 还原、构建与测试

在仓库根目录执行：

```powershell
dotnet restore TimeTicker.slnx
dotnet build TimeTicker.slnx --configuration Release --no-restore
dotnet test TimeTicker.slnx --configuration Release --no-build --no-restore
```

启动桌面应用：

```powershell
dotnet run --project src/TimeTicker.App/TimeTicker.App.csproj
```

## 解决方案结构

```text
TimeTicker.slnx
├─ src/
│  ├─ TimeTicker.App/              WPF 界面与组合根
│  ├─ TimeTicker.Application/      用例、接口与业务编排
│  ├─ TimeTicker.Domain/           实体、值对象与领域规则
│  └─ TimeTicker.Infrastructure/   持久化、日志与系统服务
└─ tests/
   ├─ TimeTicker.Domain.Tests/
   ├─ TimeTicker.Application.Tests/
   └─ TimeTicker.Infrastructure.Tests/
```

依赖方向固定为：

```text
TimeTicker.App -> TimeTicker.Application -> TimeTicker.Domain
TimeTicker.App -> TimeTicker.Infrastructure -> TimeTicker.Application
```

`TimeTicker.Domain` 不得引用 UI、数据库或操作系统相关项目。NuGet 版本统一在 `Directory.Packages.props` 中管理，公共编译设置统一在 `Directory.Build.props` 中管理。

## 开发约定

- 提交前运行 Release 构建和全部测试。
- 保持 nullable 与隐式 using 开启；编译警告视为错误。
- 不提交本地数据库、日志、测试输出、构建产物或发布包。
- 数据模型变更必须通过 EF Core migration，并配套升级测试。
- 影响架构或用户数据格式的决定记录到 `docs/adr/`。

完整产品范围、版本任务与完成定义见 [`develop_tasks.md`](develop_tasks.md)。
