---
description: "Use when reviewing code, reviewing changes, PR review, code inspection, security/quality assessment in C#/.NET repositories. Enforce checks for structure/readability, naming, complexity, duplication, error handling, boundary validation, resource management, concurrency safety, and testability. Output must include findings list, risk level, and actionable fixes."
name: "Code Review General Rules"
---
# 代码检视通用规则

适用场景：代码评审、分支差异评审、PR 审查、质量/安全检查。

## 1) 代码结构与可读性

- 检查命名是否表达意图（类型、方法、变量、布尔标志）。
- 检查函数/方法复杂度是否过高（过长、分支过多、嵌套过深）。
- 检查是否存在重复逻辑（可提取公共方法、策略、组件）。
- 对发现的问题给出可落地重构建议（拆分方法、提炼对象、消除魔法值）。

## 2) 错误处理

- 标记异常吞没（catch 后忽略、仅打印无上下文、无重抛策略）。
- 标记返回值语义不一致（同类错误在不同路径返回不同状态/结构）。
- 标记边界条件缺失（null、空字符串、范围、溢出、非法状态）。
- 建议明确错误契约：异常类型、错误码、HTTP 状态码、日志上下文。

## 3) 资源管理

- 检查连接、流、文件句柄、DbContext、HttpResponse 等是否正确释放。
- 标记可能的内存泄露风险（事件未解绑、缓存无上限、长期持有大对象）。
- 检查 async/await 与 IDisposable/IAsyncDisposable 使用是否正确。
- 建议使用 using/await using、连接池策略、超时与取消令牌。

## 4) 并发风险

- 标记竞态条件（共享可变状态无同步）。
- 标记锁粒度问题（过粗导致阻塞，过细导致一致性问题）。
- 标记线程安全问题（非线程安全集合、静态可变状态、双检锁错误）。
- 建议使用原子操作、不可变对象、并发集合或明确的同步策略。

## 5) 可测试性

- 标记依赖耦合过重（直接 new 外部依赖、静态时间/IO/网络调用）。
- 标记难以 mock 的设计（缺少接口抽象、隐藏副作用、全局状态）。
- 建议通过依赖注入、端口适配器、时间提供器、分层分离提升可测性。

## 输出格式要求

- 先给“问题清单”，按风险从高到低排序。
- 每个问题必须包含：
  - 位置（文件 + 行号）。
  - 风险等级（高/中/低）。
  - 影响说明（可能导致的故障或安全后果）。
  - 可执行修改建议（具体到改法，不只给原则）。
- 若未发现问题，明确写“未发现阻断性问题”，并补充残余风险或测试缺口。

## 审查边界

- 以“行为回归、可靠性、安全性、可维护性、可测试性”为优先。
- 摘要应简短，重点放在可验证的问题证据与修复建议。
