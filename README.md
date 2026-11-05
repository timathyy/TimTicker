# TimTicker

[![Windows CI](https://github.com/timathyy/TimTicker/actions/workflows/windows-ci.yml/badge.svg)](https://github.com/timathyy/TimTicker/actions/workflows/windows-ci.yml)

> 把今天想做的事、真正花掉的时间，以及一天结束后的思考，放在同一个地方。

## 简介

TimTicker 是一款面向个人的 Windows 本地优先时间管理工具。它把任务池、每日计划、时间记录、完成度、复盘和日历回顾连接成一个完整的每日闭环：开始一天时明确计划，过程中记录实际行动，结束时回顾完成情况，再从日历中看到长期积累。

TimTicker 无需账号和网络，数据保存在本机 SQLite 数据库中。它适合希望认真管理任务与时间，同时不愿把个人计划、工作记录和复盘上传到云端的用户。

## 功能

### 今日计划：让一天从明确的选择开始

- 从任务池中挑选今天真正要完成的任务，而不是把整个待办列表都塞进日程。
- 随时添加、移除和调整今日任务顺序，计划改变时无需重建整份清单。
- 直接完成或重新打开任务，并自动统计今日完成数量与完成率。
- 清楚标记已经逾期的任务，帮助你优先处理被遗漏的事项。
- 一天结束时保存计划快照、最终完成度和复盘文字；需要修改历史记录时，也可以明确重新打开当天。

### 任务池：长期事项与今日计划彼此独立

- 创建、编辑、完成、重新打开和删除任务。
- 为任务填写标题、说明、创建日期、截止日期和优先级。
- 按状态筛选、按关键字搜索，并按创建时间、截止日期或优先级排序。
- 任务池负责保存“未来可能要做的事”，今日计划只负责表达“今天决定做什么”，长期安排不会被单日计划打乱。

### 多层文件夹：像整理文件一样整理任务

- 建立任意层级的任务文件夹，适合按项目、领域、客户或生活场景组织任务。
- 通过递归目录树和路径导航快速进入深层文件夹；选中目录时自动展开对应层级。
- 在当前目录中创建子文件夹和任务，也可以将任务移动到其他文件夹或根目录。
- 支持移动、重命名和删除文件夹，并阻止把文件夹移动到自身或其子目录中。
- 删除文件夹前会明确展示影响范围；确认后递归删除其中的子文件夹与任务，避免误以为只删除空壳目录。

### 自动任务文件夹：让周期事项自己出现

- 创建手动、每日、每周或每月生成任务的自动任务文件夹。
- 设置重复间隔、开始日期、任务说明和优先级，既能表达“每天一次”，也能表达“每两周一次”等节奏。
- 应用启动时自动补齐已经到期但尚未生成的任务，也可以随时手动触发生成。
- 自动任务与普通任务保存在同一套目录中；自动文件夹仍可包含普通任务和子文件夹。
- 使用标题占位符生成更自然的任务名称：
  - `%y`：年份
  - `%m`：月份
  - `%d`：日期
  - `%n`：该模板生成任务的序号
- 自动文件夹与普通文件夹混合显示，仅通过强调图标和“自动”标记区分，不需要维护两套任务系统。

### 时间轴：记录实际发生的事情

- 启动一条正在进行的时间记录，结束时自动计算持续时间。
- 也可以手工补录开始时间和结束时间，适合整理会议、通勤、休息等未即时记录的活动。
- 每条记录支持标题、说明，并可同时关联多个任务。
- 时间记录不必依附于任务：吃饭、运动、临时沟通等真实活动都可以独立记录。
- 自动校验时间范围并阻止相互重叠的记录，保证每日统计可信。
- 按时间顺序查看当天记录和累计用时，直观看到计划与实际投入之间的差异。

### 日历与历史回顾：让每天的数据形成长期记录

- 使用以周一开始的六周月历浏览过去和未来，支持上月、下月以及快速回到今天。
- 日期格展示计划完成数量、完成率、时间记录和当日是否已经结束。
- 点击任意日期即可查看当天计划、任务状态、时间轴、累计时长和复盘。
- 从历史详情直接进入编辑；选择今天时会返回“今天”页，历史日期则在独立窗口中处理。
- 已结束日期保存当时的计划与完成度快照，长期回顾不会因为后来修改任务而失去原貌。
- 没有计划、时间记录或复盘的日期保持为空，让真正有内容的日子更容易被看见。

### 可配置的一天：照顾跨过午夜的生活节奏

- 默认以次日 `06:00` 作为一天的结束时间，凌晨记录仍可归入前一个业务日。
- 可在设置中选择 `24:00`、次日 `02:00`、次日 `04:00` 或次日 `06:00`。
- 今日计划、任务完成日期、时间轴和日历统一遵循同一条业务日边界规则。

### 外观与本地存储

- 支持跟随系统、浅色和深色主题，主要页面、对话框、菜单和滚动条使用统一样式。
- 默认将数据保存在 `%LocalAppData%\TimTicker`，不会写入源码仓库或程序安装目录。
- 可在“设置 → 本地存储位置”中点击“浏览”，自行选择数据库、设置和日志的目标文件夹。
- 新存储位置在下次启动时生效。TimTicker 不会擅自删除、覆盖或搬迁旧数据；需要迁移时，可在退出应用后手动复制原目录中的 `Data` 文件夹。
- 应用启动时自动创建数据库并执行尚未应用的结构升级，无需用户手动维护 SQLite。
- 本地滚动日志用于排查问题，并限制单文件大小和保留数量；异常日志避免记录异常消息，以降低用户内容意外进入日志的风险。

## 用法

前往 [GitHub Releases](https://github.com/timathyy/TimTicker/releases) 下载适用于 Windows 10/11 x64 的版本：单 EXE 可直接运行，ZIP 版解压后运行 `TimTicker.App.exe`。两种版本均为自包含发布，无需预先安装 .NET；当前未进行代码签名，因此 Windows SmartScreen 可能显示安全提示。

推荐从一个简单的每日流程开始：

1. 在“任务”中记录待办事项，并按文件夹整理长期任务。
2. 在“今天”中选出当天计划，执行过程中使用时间轴记录实际活动。
3. 完成任务后结束当天、确认完成度并写下复盘。
4. 在“日历”中回看过去的计划、时间投入与复盘。

程序默认把数据库和日志保存在 `%LocalAppData%\TimTicker`。如果更改本地存储位置，请先记住原目录；选择新位置并退出 TimTicker 后，再按需复制旧目录中的 `Data` 文件夹，最后重新启动。

## 开发

### 环境与常用命令

- Windows 10 或 Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，版本由 `global.json` 固定为 `10.0.402`
- Visual Studio，或 Visual Studio Code 与支持 .NET 10/WPF 的开发环境

在仓库根目录执行：

```powershell
dotnet tool restore
dotnet restore TimTicker.slnx --disable-parallel
dotnet format TimTicker.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build TimTicker.slnx --configuration Release --no-restore
dotnet test TimTicker.slnx --configuration Release --no-build --no-restore
```

启动桌面应用：

```powershell
dotnet run --project src/TimTicker.App/TimTicker.App.csproj
```

重新生成 Windows 图标：

```powershell
& ./scripts/Convert-PngToIco.ps1 `
  -InputPath ./src/TimTicker.App/Assets/AppIcon.png `
  -OutputPath ./src/TimTicker.App/Assets/AppIcon.ico
```

### 解决方案结构与依赖方向

```text
TimTicker.slnx
├─ src/
│  ├─ TimTicker.App/              WPF 界面、ViewModel 与组合根
│  ├─ TimTicker.Application/      用例、接口与业务编排
│  ├─ TimTicker.Domain/           实体、值对象与领域规则
│  └─ TimTicker.Infrastructure/   SQLite、日志与系统服务
└─ tests/
   ├─ TimTicker.Domain.Tests/
   ├─ TimTicker.Application.Tests/
   └─ TimTicker.Infrastructure.Tests/
```

依赖方向固定为：

```text
TimTicker.App -> TimTicker.Application -> TimTicker.Domain
TimTicker.App -> TimTicker.Infrastructure -> TimTicker.Application
```

`TimTicker.Domain` 不得引用 UI、数据库、文件系统或其他操作系统相关实现。应用层定义用例和抽象，基础设施层实现持久化与系统服务，WPF 项目负责界面和依赖注入组合。

### 数据与迁移约定

默认运行时目录如下：

```text
%LocalAppData%\TimTicker\
├─ storage-location.json    本地存储位置配置
├─ Data\
│  ├─ timticker.db          SQLite 数据库
│  └─ settings.json         每日结束时间等本地设置
└─ Logs\
   └─ timticker-*.log       JSON Lines 结构化滚动日志
```

- 数据库时间点统一保存为 UTC，展示和业务日计算时转换为本机时区。
- 数据模型变更必须通过 EF Core migration，并补充从旧结构升级的测试。
- 应用启动时统一执行 migration；不得绕过迁移直接修改用户数据库结构。
- 更改本地存储位置不自动迁移用户数据，这是避免误覆盖和误删除的明确边界。

新增 migration：

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/TimTicker.Infrastructure `
  --output-dir Persistence/Migrations
```

### 代码与协作约定

- 保持 nullable 与隐式 using 开启，使用最新稳定 C# 语言版本，所有编译警告视为错误。
- NuGet 版本集中维护在 `Directory.Packages.props`；公共编译设置集中维护在 `Directory.Build.props`。
- 提交前必须通过全量格式检查、Release 构建和全部自动化测试。
- 不提交数据库、日志、测试输出、临时 staging、构建产物或发布包。
- 领域规则优先放入 `TimTicker.Domain`，用例编排放入 `TimTicker.Application`，避免在 View 或代码隐藏中复制业务逻辑。
- 删除、迁移或覆盖用户数据的操作必须有明确确认，并优先选择可恢复、可回退的方案。
- 异常日志不得主动写入任务标题、说明、复盘等用户内容。
- GitHub Actions 在 Windows runner 上执行工具还原、依赖还原、格式检查、Release 构建和全部测试。
- 对结构、数据兼容性或交付方式有长期影响的决定记录在 [`docs/adr/`](docs/adr/)；改变已接受的决定时，以新的 ADR 取代旧决定。

完整产品范围、业务规则、版本路线图与验收标准见 [`develop_tasks.md`](develop_tasks.md)。
