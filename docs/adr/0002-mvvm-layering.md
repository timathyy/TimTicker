# ADR 0002：采用 MVVM 与分层依赖方向

- 状态：已接受
- 日期：2026-10-28

## 背景

应用同时包含 WPF 交互、任务与计划规则、SQLite 持久化和 Windows 系统集成。如果界面直接操作数据库，业务规则会难以测试，也会让后续页面和存储变化互相影响。

## 决策

解决方案保持以下依赖方向：

```text
TimTicker.App -> TimTicker.Application -> TimTicker.Domain
TimTicker.App -> TimTicker.Infrastructure -> TimTicker.Application
```

- `TimTicker.Domain` 保存实体、值对象和领域规则，不引用 UI、EF Core、日志或操作系统 API；
- `TimTicker.Application` 保存用例、端口接口、DTO 和业务编排，只依赖 Domain；
- `TimTicker.Infrastructure` 实现 Application 定义的端口，承载 EF Core、文件、日志和系统服务；
- `TimTicker.App` 保存 WPF View、ViewModel、导航和组合根，负责注册具体实现；
- ViewModel 不直接持有 DbContext，也不把 WPF 类型传入 Application 或 Domain；
- 跨页面或跨存储的操作通过 Application 用例完成，界面状态通过可绑定属性和命令暴露；
- Domain 与 Application 优先使用单元测试，Infrastructure 使用临时 SQLite 文件进行集成测试。

## 影响

该结构增加了接口和映射代码，但能隔离 UI、业务规则和持久化细节。简单的纯界面状态可以留在 App 层，不要求为每个控件事件创建用例；一旦操作会修改业务数据或跨越外部边界，就必须进入 Application 层。
