# MWL / MPPS SCP + WebAPI Demo

这个 Demo 在一个进程中同时提供：

- fo-dicom 5.2.6 的 MWL/MPPS SCP（C-FIND / N-CREATE / N-SET）
- ASP.NET Core WebAPI（设备发现目录与审计日志查询）

## 运行

```bash
dotnet run --project ./tests/DicomViewer.WorkflowScpDemo -- \
  --host 127.0.0.1 \
  --port 11112 \
  --ae RIS_SCP \
  --data ./workflow-data.json \
  --db ./workflow-discovery.db \
  --http-url http://127.0.0.1:5200
```

默认参数：

- Host：`127.0.0.1`
- Port：`11112`
- Called AE Title：`RIS_SCP`
- 数据文件：`workflow-data.json`
- SQLite：`workflow-discovery.db`
- WebAPI：`http://127.0.0.1:5200`

## 设备发现与审计设计

- 连接与协议事件在 SCP 热路径仅进入内存缓存 + Channel 队列
- `DeviceDiscoveryAuditPipeline` 后台异步批量刷盘到 SQLite
- 设备目录保存在 `device_catalog` 表，审计日志保存在 `dicom_audit_logs` 表
- 默认只记录结构化关键字段（IP/端口/AE/Event/状态/简要详情），不落完整 DICOM Dataset

捕获字段（尽可能）包括：

- Remote IP
- Remote Port
- Calling AE Title
- Called AE Title
- FirstSeenUtc / LastSeenUtc

## WebAPI

- `GET /api/devices?enabledOnly=true|false`：设备目录
- `GET /api/device-options`：UI 下拉简化选项
- `PATCH /api/devices/{id}`：更新 `displayName` / `remark` / `isEnabled`
- `GET /api/audit-logs?fromUtc=&toUtc=&eventType=&deviceKey=&skip=&take=`：审计日志查询

## 数据说明

`workflow-data.json` 中：

1. `worklistItems`：本地已登记检查单（C-FIND 返回）
2. `mppsRecords`：收到 MPPS 后的最新状态

## 如何接到当前 DicomViewer

把 WPF 配置指向 SCP：

- `MwlHost=127.0.0.1`
- `MwlPort=11112`
- `MwlCalledAeTitle=RIS_SCP`
- `MppsHost=127.0.0.1`
- `MppsPort=11112`
- `MppsCalledAeTitle=RIS_SCP`
