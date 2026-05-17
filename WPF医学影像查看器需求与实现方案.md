# WPF 医学影像查看器需求与实现方案

## 1. 项目目标

本项目面向 WPF 开发场景，建设一个桌面端医学影像查看器工程骨架，目标包括：

1. 支持后续接入 DICOM 文件导入、序列组织与二维阅片。
2. 形成清晰的分层架构，便于逐步扩展窗宽窗位、测量、缓存与 PACS 对接。
3. 先交付可运行的 WPF 壳工程和核心代码骨架，保证项目可直接继续开发。

## 2. 本期需求范围

### 2.1 功能范围

1. WPF 主界面框架
2. 左侧序列列表
3. 中央视口占位区
4. 右侧检查与视口信息面板
5. 顶部工具栏与底部状态栏
6. 分层领域模型
7. 应用服务层与基础设施层接口骨架
8. 可运行的样例数据工作区

### 2.2 后续扩展范围

1. fo-dicom 接入
2. 本地 DICOM 文件导入
3. 实际像素解码与渲染
4. 窗宽窗位实时调整
5. 缩放、平移、旋转、翻转
6. 测量与标注
7. 缓存、日志、配置持久化
8. 多视口、MPR、PACS

## 3. 解决方案结构

当前目录已创建以下解决方案结构：

1. src/DicomViewer.Wpf
2. src/DicomViewer.Application
3. src/DicomViewer.Domain
4. src/DicomViewer.Infrastructure
5. src/DicomViewer.Rendering
6. src/DicomViewer.Shared
7. tests/DicomViewer.Tests

依赖方向如下：

1. Wpf -> Application, Rendering, Shared
2. Application -> Domain, Rendering, Shared
3. Domain -> Shared
4. Infrastructure -> Application, Domain, Rendering, Shared
5. Rendering -> Domain, Shared

## 4. 模块职责

### 4.1 Wpf

负责主窗口、数据绑定、命令交互和工作区展示。

### 4.2 Application

负责工作区编排、序列切换、切片浏览和工具状态管理。

### 4.3 Domain

负责 Patient、Study、Series、ImageInstance 等核心模型以及 WindowLevel、PixelSpacing、ViewTransform 等值对象。

### 4.4 Infrastructure

负责外部数据源接入。本期提供样例数据实现，后续可替换为真实 DICOM 读取服务。

### 4.5 Rendering

负责视口渲染请求和视图描述结果。本期提供占位渲染描述，后续可替换为真实位图渲染管线。

## 5. 已落地实现

当前版本已经完成：

1. .NET/WPF 解决方案初始化
2. 分层项目及引用关系
3. 根目录需求与架构文档
4. 主窗口阅片器布局
5. 样例患者、检查、序列和切片数据
6. DICOM 目录导入与元数据分组
7. 单帧灰度图像的真实像素显示
8. 窗宽窗位预设与实时调整
9. 视口缩放状态控制
10. 工作区服务与序列切换逻辑
11. 工具模式切换和切片导航命令
12. 占位渲染结果展示

## 6. 当前运行效果

运行 WPF 项目后，可看到：

1. 左侧序列列表
2. 中间阅片器主视口可在真实 DICOM 数据下显示单帧灰度图
3. 右侧患者、检查、视图和开发说明面板
4. 顶部工具栏，可切换示例工具模式并浏览切片
5. 顶部目录导入输入框，可加载真实 DICOM 目录元数据
5. 底部状态栏，展示当前工作区状态

这不是完整 DICOM 查看器，而是为后续真实影像功能准备好的可编译骨架。

## 7. 下一阶段建议

建议按照下面顺序继续推进：

1. 在 Rendering 中实现灰度像素到位图的转换
2. 在 Wpf 中加入自定义 ImageViewport 控件
3. 在 Application 中补充窗宽窗位、缩放和平移用例
4. 在 Domain 中增加测量与标注模型
5. 在 Infrastructure 中增加日志、配置和缓存服务

## 8. 运行方式

在当前目录执行：

```powershell
dotnet build DicomViewer.slnx
dotnet run --project src/DicomViewer.Wpf/DicomViewer.Wpf.csproj
```

## 9. 交付说明

本次交付满足“把文档形成一份文件，放到当前目录，并在当前目录实现它”的要求，具体体现为：

1. 根目录新增本文件作为正式文档。
2. 当前目录已生成可运行的 WPF 医学影像查看器工程骨架。
3. 代码结构已与文档中的分层设计保持一致。