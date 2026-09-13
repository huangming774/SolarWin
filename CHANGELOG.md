# Changelog

本文件记录 SolarWin（Solar Network Windows 客户端）的版本变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循语义化版本。

---

## [1.2.0] — 2026-08-02

### 新增

- **瑞幸咖啡点餐**：集成 MCP 协议驱动瑞幸 API，完整菜单浏览、规格定制、加购下单流程；Token 安全存储于 Windows PasswordVault
- **GPU 图片渲染控件**（`GpuImage` / `FastWin2DImage`）：D3D11SCOT 硬件加速解码缩略图，解决大图列表滚动掉帧
- **视频预览助手**（`VideoPreviewHelper` / C++ `VideoThumbnailer`）：Native 侧提取视频关键帧，配合 `VideoMediaCache` 缓存
- **聊天数据分析**：`ChatDataCenterPage` 活动热力图 / 词云 / 消息趋势 / 成员贡献排行；`LinkPreviewCard` 链接预览
- **代码片段卡片**（`CodeSnippetCard`）：Markdown 代码块语法高亮展示
- **聊天本地持久化**：SQLite WAL 模式写入泵（`ChatWritePump`），聊天记录离线可用；本地 RoomStore 缓存
- **聊天 MLS 端到端加密（RFC 9420）**
  - Rust OpenMLS v0.8.1 原生桥接库（`SolarWin.Mls.Native`，Windows x64），密码套件 `MLS_128_DHKEMX25519_AES128GCM_SHA256_Ed25519`
  - 每账号独立 SQLCipher 加密数据库；密钥与 Ed25519 签名私钥由 Windows DPAPI 按用户保护
  - 建群、Welcome / External Commit 加群、成员增删、消息加解密；设备 KeyPackage 库存与轮换
  - 加密房间禁止上传新媒体（附件 / 语音 / 贴纸），防止明文泄露；非 x64 平台 fail-closed
  - **聊天加密设置页**：房间内开启加密、查看成员设备、手动重置 / 重新引导群组
- **AI 对话页**：`AiPage` 重构，聊天式 AI 交互界面
- **托盘关闭修复**：解决 `H.NotifyIcon` 第二窗口导致进程无法退出的问题

### 修复

- **崩溃处理**：UI Dispatcher 损坏时不再维持托盘进程；未处理异常写入 `unhandled-exceptions.log`
- **Native DLL 打包**：`SolarWin.Native.dll` 现作为 `Content` 正确打入 Build / Publish / MSIX 输出，不再依赖额外 Copy Target
- **聊天消息**：`publisher.gatekept_follows` 等字段 `null` 导致整表反序列化失败；新增 `FlexibleBoolConverter` / `FlexibleInt32Converter`

### 变更

- 应用版本升至 **1.2.0**（程序集 / MSIX Identity `1.2.0.0`）
- **GitHub Actions CI/CD** 全面重写：并行 Build + Test_job → 便携 ZIP → MSIX（含 MSIX_PFX_BASE64 签名 secrets）→ SHA256SUMS → Release upload；tag push 自动发版
- `MessageItemViewModel`、`ChatDetailViewModel`、`PostsViewModel` 等核心 ViewModel 重构，消息状态与操作分离
- `ImagePreviewHelper` / `DysonFileImageLoader` 大幅增强，支持多规格缩略图流水线

---

## [1.1.0] — 2026-07-22

### 新增

- **壁纸**：设置页可选本地图片，调节透明度、模糊强度；可勾选「玻璃」或「磨砂」质感；偏好持久化
- **聊天贴纸**：贴纸面板、按包加载、发送 `:{prefix}+{slug}:`；气泡内图片渲染（单条 / 批量 lookup）
- **聊天消息通知**：后台或其它页时，新消息可走系统通知 + 托盘气泡（当前会话不重复弹）
- **寻思**：人格 / 模型对话页，支持 SSE 流式回复
- **天气页**：玻璃拟态仪表盘 UI
- **深度链接**：`solian://` 协议注册与跳转（用户 / 聊天 / 设置等）
- **多账号**：本地账号列表切换与移除
- **探索 / 出版者 / 用户主页** 等 Sphere 相关页面

### 修复

- **帖子加载为空**：`publisher.gatekept_follows` 等 API `null` 布尔导致整表反序列化失败；增加 `FlexibleBoolConverter` / `FlexibleInt32Converter`
- **帖子源**：优先稳定 `/sphere/posts`，并保留时间线 / 精选回退
- **导航图标不显示**：为 `FontIcon` 指定 `SymbolThemeFontFamily` 与主题前景色；壁纸开启时侧栏半透明底
- **托盘图标**：使用 `Assets/icon.ico` 作为托盘与窗口图标

### 变更

- 应用版本升至 **1.1.0**（程序集 / MSIX Identity `1.1.0.0`）
- HTTP `User-Agent` 更新为 `SolarWin/1.1`
- 聊天数据缓存、消息列表与实时同步体验改进
- README 与功能说明同步至当前能力

---

## [1.0.x] — 更早

- 初始 WinUI 3 客户端：Padlock 登录、Shell 导航、聊天 / 网盘 / 通知 / 资料基础能力
- Token PasswordVault 存储与会话恢复
- MSIX / 自包含发布骨架

---

[1.1.0]: https://github.com/ # 可按实际上游仓库填写
