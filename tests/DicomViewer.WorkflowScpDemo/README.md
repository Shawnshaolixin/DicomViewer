# MWL / MPPS 本地 SCP 测试项目

这个测试项目用于把本地 `workflow-data.json` 里的登记数据暴露成一个简单的 RIS 工作流服务：

- MWL：外部 SCU 可以通过 C-FIND 查询 worklist
- MPPS：外部 SCU 可以通过 N-CREATE / N-SET 更新执行状态

## 运行

```bash
dotnet run --project ./tests/DicomViewer.WorkflowScpDemo -- --host 127.0.0.1 --port 11112 --ae RIS_SCP --data ./tests/DicomViewer.WorkflowScpDemo/workflow-data.json
```

默认参数：

- Host：`127.0.0.1`
- Port：`11112`
- Called AE Title：`RIS_SCP`
- 数据文件：项目目录下的 `workflow-data.json`

## 数据说明

`workflow-data.json` 中有两部分：

1. `worklistItems`：本地已登记的检查单
2. `mppsRecords`：收到的 MPPS 实例与最新状态

当外部 modality/SCU 发起：

1. MWL C-FIND：服务按 `PatientID`、`PatientName`、`AccessionNumber`、`Modality`、`ScheduledStationAETitle`、`ScheduledProcedureStepStartDate` 过滤
2. MPPS N-CREATE：按 `ScheduledProcedureStepID` / `AccessionNumber` / `StudyInstanceUID` 匹配本地 worklist，并把状态写成 `IN PROGRESS`
3. MPPS N-SET：按 `SOP Instance UID` 更新为 `COMPLETED` 或 `DISCONTINUED`

## 如何接到当前 DicomViewer

把 WPF 应用里的 MWL / MPPS 配置指向这个 SCP：

- `MwlHost=127.0.0.1`
- `MwlPort=11112`
- `MwlCalledAeTitle=RIS_SCP`
- `MppsHost=127.0.0.1`
- `MppsPort=11112`
- `MppsCalledAeTitle=RIS_SCP`

这样当前仓库里的 `DicomMwlWorklistService` 和 `DicomMppsService` 就能直接对这个测试项目做联调。
