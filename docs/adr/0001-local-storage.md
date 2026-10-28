# ADR 0001：使用本地 SQLite 作为主数据存储

- 状态：已接受
- 日期：2026-10-28

## 背景

TimTicker 是单用户、离线优先的 Windows 桌面应用。首版数据应留在本机，应用需要支持事务、关系约束、可重复升级以及后续的备份与恢复。引入独立数据库服务会增加安装和维护成本，普通 JSON 文件则难以可靠处理关系、并发写入和 schema 演进。

## 决策

- 使用 SQLite 单文件数据库作为主数据存储；
- 使用 EF Core 负责数据访问和 migration；
- 数据库固定存放在 `%LocalAppData%\TimTicker\Data\timticker.db`；
- 应用启动时按顺序应用尚未执行的 migration，migration 必须保持向前兼容并接受自动化测试；
- `TimTicker.Infrastructure` 拥有 DbContext、SQLite 配置和 migration，其他层不直接依赖 SQLite；
- 数据库、日志和备份均不写入安装目录或源码仓库；
- 首版不提供云同步，也不把 SQLite 文件放进由第三方同步盘实时同步的目录。

## 影响

优点：部署简单、无需后台服务、事务与约束能力完整，数据文件也便于备份。代价是同一数据库不支持多设备实时协作；schema 变更必须通过 migration，备份与恢复还需要处理 WAL、占用和版本兼容问题。
