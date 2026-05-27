# MWL + MPPS 标准工作流实施规划

## 1. 目标

本规划用于把当前项目从“模拟工作列表 + 本地检查会话 + PACS 发送”升级为“标准 MWL 拉单 + MPPS 状态上报”的 DICOM 工作流学习工程。

本轮目标不是建设完整 RIS，而是在保留当前查看器、曝光控制台、模拟采集和 PACS 回看闭环的基础上，补齐标准 Modality Workflow 的关键环节：

1. 通过 DICOM MWL 获取待执行检查。
2. 基于选中的 Scheduled Procedure Step 创建本地检查会话。
3. 在检查开始时发送 MPPS N-CREATE。
4. 在检查完成或中止时发送 MPPS N-SET。
5. 对失败、重试、补发和审计建立最小可追踪闭环。

## 2. 当前基础与切入点

当前仓库已经具备以下可复用基础：

1. 工作列表加载入口：src/DicomViewer.Application/Abstractions/IWorklistService.cs
2. 当前工作列表模型：src/DicomViewer.Domain/Entities/ImagingOrder.cs
3. 检查会话状态：src/DicomViewer.Domain/Entities/ExamSession.cs
4. 控制台业务编排：src/DicomViewer.Application/Services/ExamWorkflowService.cs
5. 控制台快照：src/DicomViewer.Application/Models/ConsoleSnapshot.cs
6. PACS 通信配置：src/DicomViewer.Application/Models/PacsConfiguration.cs
7. 当前模拟工作列表来源：src/DicomViewer.Infrastructure/Worklist/MockWorklistService.cs

因此，最合理的切入方式不是新增独立 RIS 子系统，而是沿现有控制台主链路替换工作列表来源，并把 MPPS 状态上报嵌入现有状态机。

## 3. 范围边界

本规划纳入范围：

1. DICOM Modality Worklist SCU 查询。
2. DICOM MPPS SCU N-CREATE / N-SET。
3. MWL 字段到本地订单与会话模型的映射。
4. MPPS 状态与本地会话状态的映射。
5. SQLite 持久化、失败重试、审计与 UI 状态反馈。

本规划暂不纳入范围：

1. 完整 RIS 功能，例如预约、登记、排班、收费、报告。
2. 真正的设备 SDK 或硬件驱动接入。
3. 多实例、多系列采集增强。
4. Storage Commitment、UPS、HL7 ORM/ORU 等扩展流程。

## 4. 标准工作流目标链路

建议按以下业务链路实现：

1. 控制台输入查询条件并执行 MWL 查询。
2. 用户从 MWL 结果中选择一条 Scheduled Procedure Step。
3. 系统将 MWL 项映射为本地 ImagingOrder，并创建 ExamSession。
4. 联锁通过后，准备曝光。
5. 首次进入实际采集前，发送 MPPS N-CREATE，状态为 IN PROGRESS。
6. 曝光完成、DICOM 生成成功后，发送 MPPS N-SET，状态为 COMPLETED。
7. 如果检查取消、异常中断或失败终止，发送 MPPS N-SET，状态为 DISCONTINUED。
8. 本地持久化 MPPS 事务和发送结果，支持失败补发和审计追踪。

## 5. 领域模型调整建议

### 5.1 ImagingOrder 扩展

当前 ImagingOrder 更接近本地演示订单，需要扩展为既能承接模拟数据，也能承接 MWL 结果的统一模型。

建议至少新增以下字段：

1. AccessionNumber
2. RequestedProcedureId
3. ScheduledProcedureStepId
4. StudyInstanceUid
5. Modality
6. ScheduledStationAeTitle
7. ScheduledStartDateTime
8. ReferringPhysicianName
9. PatientSex
10. PatientBirthDate
11. RequestedProcedureDescription
12. SourceType，取值如 Mock 或 Mwl

其中，OrderId 可以继续保留作为 UI 和本地持久化主键，但不应再承担所有上游业务标识的语义。

### 5.2 ExamSession 扩展

当前 ExamSession 仅覆盖本地检查执行态，不足以承载 MPPS 生命周期。

建议增加：

1. MppsInstanceUid
2. MppsStatus
3. MppsCreatedAtUtc
4. MppsLastSentAtUtc
5. MppsLastError
6. ScheduledStepIdSnapshot
7. AccessionNumberSnapshot

设计原则：

1. 选中 MWL 项创建会话时，对关键上游字段做快照，避免上游变更影响本地执行语义。
2. MPPS SOP Instance UID 必须在首次 N-CREATE 成功前后都可恢复，不能只保存在内存中。

### 5.3 新增 MPPS 事务模型

建议新增独立模型，例如 MppsTransaction 或 PerformedProcedureStepRecord，用于记录：

1. SessionId
2. SopInstanceUid
3. TransactionType，取值如 Create 或 Set
4. StatusPayloadSummary
5. AttemptCount
6. LastAttemptAtUtc
7. LastResult
8. LastError
9. IsPendingRetry

这个模型的目的不是替代 ExamSession，而是为失败补发和审计追踪提供独立落点。

## 6. 应用层接口调整建议

### 6.1 Worklist 查询接口升级

当前 IWorklistService 只有无参数 LoadAsync，不足以支持标准 MWL 查询。

建议调整为：

1. 新增 MwlQueryCriteria 模型。
2. IWorklistService 支持按条件查询。
3. 保留 Mock 实现，用于离线演示和测试。

建议接口方向：

```csharp
public interface IWorklistService
{
    Task<IReadOnlyList<ImagingOrder>> QueryAsync(MwlQueryCriteria criteria, CancellationToken cancellationToken = default);
}
```

MwlQueryCriteria 至少建议包含：

1. PatientId
2. PatientName
3. AccessionNumber
4. Modality
5. ScheduledDateFrom
6. ScheduledDateTo
7. ScheduledStationAeTitle

### 6.2 新增 MPPS 服务接口

建议新增应用层抽象：

```csharp
public interface IMppsService
{
    Task<MppsSubmitResult> CreateInProgressAsync(ExamSession session, CancellationToken cancellationToken = default);
    Task<MppsSubmitResult> CompleteAsync(ExamSession session, CancellationToken cancellationToken = default);
    Task<MppsSubmitResult> DiscontinueAsync(ExamSession session, string reason, CancellationToken cancellationToken = default);
}
```

这样可以把 MPPS 组包和网络发送封装在 Infrastructure 内，而在 Application 层只关心时机、结果和状态推进。

### 6.3 新增配置存储抽象

当前 PacsConfiguration 只覆盖 PACS 与本地 C-MOVE 接收配置，不足以描述 MWL 与 MPPS。

建议拆分或扩展为 WorkflowConfiguration，至少包括：

1. LocalAeTitle
2. MwlCalledAeTitle
3. MwlHost
4. MwlPort
5. MppsCalledAeTitle
6. MppsHost
7. MppsPort
8. StationAeTitle
9. StationName
10. ProcedureStationClass
11. OutputDirectory

如果短期不想大改 UI，可以先在现有 PacsConfiguration 中增补字段，待稳定后再重构为独立配置模型。

## 7. Application 编排落点

ExamWorkflowService 应继续作为主编排入口，但要加入两个新的关键动作。

### 7.1 选择 MWL 项时

在 SelectOrder 对应路径中：

1. 记录所选 MWL 项的关键字段快照。
2. 创建带有待上报 MPPS 状态的 ExamSession。
3. 审计记录明确写出 MWL 来源、Accession Number 和 Scheduled Procedure Step ID。

### 7.2 执行曝光前

在从 Ready 进入真实采集前：

1. 检查当前会话是否已完成 MPPS N-CREATE。
2. 未完成则先调用 IMppsService.CreateInProgressAsync。
3. 若 N-CREATE 失败，则阻止曝光或按配置进入“允许继续但待补发”模式。

学习项目建议默认采用“阻止曝光并提示失败”的严格模式，后续再扩展补发策略。

### 7.3 曝光成功后

在 DICOM 生成成功后：

1. 调用 IMppsService.CompleteAsync。
2. 将 ExposureResult 和 MPPS 完成结果一起写入会话和审计记录。
3. 若 MPPS Completed 失败，将会话标记为待补发状态，不影响本地回看闭环。

### 7.4 曝光中断或取消时

增加显式取消或失败终止路径：

1. 生成可读的终止原因。
2. 调用 IMppsService.DiscontinueAsync。
3. 持久化中止状态和失败说明。

## 8. Infrastructure 实现建议

### 8.1 MWL 客户端

新增 DicomMwlWorklistService，放在 Infrastructure 的 Worklist 或 Dicom 目录中。

职责：

1. 使用 fo-dicom 发送 C-FIND 到 Modality Worklist Information Model - FIND。
2. 组装查询数据集。
3. 解析返回数据集并映射为 ImagingOrder。
4. 统一处理网络异常、超时和空结果。

### 8.2 MPPS 客户端

新增 DicomMppsService。

职责：

1. 生成 MPPS SOP Instance UID。
2. 构建 N-CREATE 所需数据集。
3. 构建 N-SET Completed 或 Discontinued 数据集。
4. 返回标准化结果对象，而不是把 fo-dicom 细节泄漏到 Application 层。

### 8.3 本地持久化

建议新增以下持久化对象：

1. WorkflowConfiguration
2. MwlQueryHistory，可选
3. MppsTransactionRecord

至少应保证：

1. 会话重启后可看到最近一次 MPPS 状态。
2. 失败的 MPPS 事务可以重新发送。
3. 审计日志能够串起 MWL 订单、会话和 MPPS 事务。

## 9. WPF 界面调整建议

控制台页面建议新增三个区域：

1. MWL 查询条件区
2. MWL 结果列表区
3. MPPS 状态与失败提示区

最小交互流程：

1. 用户输入日期范围、患者或检查号。
2. 点击查询后展示 MWL 结果。
3. 选中一条结果创建会话。
4. 执行联锁检查。
5. 曝光时显示 MPPS Started 状态。
6. 结束后显示 MPPS Completed 或 Discontinued 状态。

ConsoleSnapshot 也应新增必要字段，例如：

1. 当前工作列表来源
2. MWL 查询摘要
3. MPPS 状态文本
4. MPPS 最近结果
5. 待补发标识

## 10. 数据映射最低要求

第一版至少应打通以下映射：

1. MWL Patient ID -> ImagingOrder.PatientId
2. MWL Patient Name -> ImagingOrder.PatientName
3. MWL Accession Number -> ImagingOrder.AccessionNumber
4. MWL Requested Procedure ID -> ImagingOrder.RequestedProcedureId
5. MWL Scheduled Procedure Step ID -> ImagingOrder.ScheduledProcedureStepId
6. MWL Modality -> ImagingOrder.Modality
7. MWL Scheduled Station AE Title -> ImagingOrder.ScheduledStationAeTitle
8. MWL Scheduled Procedure Step Start DateTime -> ImagingOrder.ScheduledStartDateTime
9. MWL Scheduled Procedure Step Description 或 Requested Procedure Description -> ImagingOrder.ProcedureDescription
10. MWL Body Part Examined -> ImagingOrder.BodyPart

MPPS 第一版至少应写出：

1. Performed Procedure Step Status
2. Performed Station AE Title
3. Performed Station Name
4. Performed Procedure Step Start DateTime
5. Performed Procedure Step End DateTime
6. Performed Series Sequence 的最小引用信息
7. Accession Number
8. Study Instance UID
9. Patient ID
10. Patient Name

## 11. 分阶段实施顺序

### 阶段一：模型与接口对齐

目标：把现有“模拟工作列表”升级为“可承接标准 MWL 的骨架”。

产出：

1. 扩展 ImagingOrder
2. 新增 MwlQueryCriteria
3. 新增 IMppsService
4. 扩展 ExamSession 与 ConsoleSnapshot
5. 扩展配置模型和持久化表结构

验收标准：

1. 不接真实 MWL 服务器时，MockWorklistService 仍可运行。
2. 控制台可以展示扩展后的字段。
3. 现有测试可继续通过或按新接口完成迁移。

### 阶段二：MWL 查询接入

目标：替换模拟工作列表入口，接入真实 DICOM MWL 查询。

产出：

1. DicomMwlWorklistService
2. 查询条件 UI
3. 返回结果映射
4. 查询失败提示与审计

验收标准：

1. 能从指定 MWL 服务查询到订单。
2. 结果可创建本地会话。
3. 空结果、超时、网络错误均有可读反馈。

### 阶段三：MPPS Started 接入

目标：在实际执行前发出 IN PROGRESS。

产出：

1. DicomMppsService.CreateInProgressAsync
2. MPPS SOP Instance UID 持久化
3. Started 失败策略

验收标准：

1. 曝光前能成功发送 MPPS N-CREATE。
2. 失败时界面与审计可见。
3. 应用重启后仍可恢复当前会话的 MPPS 标识。

### 阶段四：MPPS Completed / Discontinued

目标：补齐完整结束态。

产出：

1. CompleteAsync
2. DiscontinueAsync
3. 会话取消或失败终止入口
4. 待补发和重试机制

验收标准：

1. 成功曝光后可发送 Completed。
2. 中断场景可发送 Discontinued。
3. 失败事务支持重试。

### 阶段五：回归与文档固化

目标：把标准工作流正式纳入项目基线。

产出：

1. 单元测试与集成测试补齐
2. 配置说明文档
3. 运行前提说明
4. 异常恢复说明

验收标准：

1. 形成可复现的本地演示环境。
2. 文档可指导他人完成联调。
3. 现有查看器、PACS 发送、回看闭环不回退。

## 12. 测试重点

至少新增以下测试：

1. MWL 查询条件映射测试
2. MWL 返回数据到 ImagingOrder 的映射测试
3. SelectOrder 后关键字段快照测试
4. MPPS Started 成功与失败测试
5. MPPS Completed 成功与失败测试
6. MPPS Discontinued 场景测试
7. MPPS 失败补发测试
8. 配置持久化和会话恢复测试

## 13. 推荐的开工顺序

如果下一步直接进入编码，建议按以下顺序推进：

1. 先改模型和接口，不先写网络层。
2. 再让 MockWorklistService 适配新字段，保证 UI 不断。
3. 再引入 DicomMwlWorklistService。
4. 再接 MPPS Started。
5. 最后补 Completed / Discontinued 和失败补发。

这样可以避免一开始就被远端环境、协议细节和 UI 改动同时卡住。

## 14. 结论

对当前项目来说，标准方案不等于“先做 RIS”，而是“先把控制台升级为标准 DICOM Modality Workflow 参与方”。

只要 MWL 和 MPPS 接上，你这个项目的学习价值就会从“本地模拟链路”提升到“标准检查执行工作流”。后面如果还要扩展 RIS，再在这条主链路之外补预约、登记、排班和报告即可。