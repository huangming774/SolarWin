# Changelog

本文件记录 SolarWin（Solar Network Windows 客户端）的版本变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循语义化版本。

---

## [1.1.9] — 2026-09-13

### 新增

- **瑞幸咖啡点餐**：集成 MCP 协议驱动瑞幸 API，完整菜单浏览、规格定制、加购下单流程
  - 全量菜单扫描（loop-until-dry）、中文 / 拼音模糊匹配搜索
  - 规格 / 属性定制（`OrderCustomizeState`）、确认下单与取消订单
  - 瑞幸 Token 安全存储于 Windows PasswordVault
- **恒星计划**：会员订阅状态与管理页（`StellarProgramPage`），档位（Stellar / Nova 等）展示与订阅管理
- **天气城市定位排序**（`GeoCityRanking`）：城市搜索结果按地理位置智能排序
- **视觉资产更新**：应用图标、托盘图标、启动画面与商店徽标全套更新

### 变更

- **钱包**：页面与 ViewModel 大幅重构（`WalletViewModel` +588 行），流水展示增强
- **帖子**：`PostsViewModel` / `PostsPage` 增强，详情页交互改进
- **实时通话**：`LiveKitRealtimeCallService` 改进
- **API 客户端**：`SolarApiClient` 扩展（+265 行），支持恒星计划等新接口
- 新增测试：`LuckinJsonTests` / `LuckinMenuSweepTests` / `LuckinProductAttrsTests` / `GeoCityRankingTests`，MLS 契约测试更新
- 应用版本升至 **1.1.9**（程序集 / MSIX Identity `1.1.9.0`）

---

## [1.1.7] — 2026-08-02

### 新增

- **端到端加密（MLS / RFC 9420）**：聊天房间可开启 MLS 加密，密码套件 `MLS_128_DHKEMX25519_AES128GCM_SHA256_Ed25519`
  - 新增 Rust 原生桥接库 `SolarWin.Mls.Native`（OpenMLS v0.8.1，Windows x64），负责建群、Welcome / 外部 Commit 加群、成员增删、消息加解密等全部密码学操作
  - 每账号独立 SQLCipher 加密数据库；32 字节数据库密钥与 Ed25519 签名私钥由 Windows DPAPI 按当前用户保护
  - 持久化稳定设备 ID，随每个 Padlock MLS 请求发送
  - KeyPackage 库存管理、引导建群、Welcome / Commit 分发、GroupInfo / ratchet-tree 发布、按设备成员管理
  - 入站消息（REST / 同步 / WebSocket）有序处理、解密成功后才 ACK
- **聊天加密设置页**：房间内开启加密、查看成员设备、手动重置 / 重新引导群组

### 安全行为

- 密文以 `chat.mls.v2` 走普通消息发送接口，明文内容永不进入 HTTP 请求体
- 解密失败或引擎不可用时绝不回退为明文发送
- 加密房间内禁止上传新附件 / 语音 / 贴纸等新媒体（尚无加密媒体管线，防止明文泄露）；消息中已有的附件 ID 仍受 MLS 认证保护
- 服务端不允许加密房间切回明文，「关闭加密」控件在 UI 上禁用
- 非 Windows x64 平台 fail-closed：未提供原生库前 MLS 不启用

### 变更

- 新增 MLS 协议契约测试与原生集成测试（`MlsContractTests` / `MlsNativeIntegrationTests`）
- 集成说明见 `docs/mls-integration.md`

---

## [1.1.6] — 2026-08-02

### 新增

- **聊天数据中心**（`ChatDataCenterPage`）：活动热力图 / 词云 / 消息趋势 / 成员贡献排行；`LinkPreviewCard` 链接预览；`CodeSnippetCard` 代码片段语法高亮
- **GPU 图片渲染控件**（`GpuImage` / `FastWin2DImage`）：D3D11 硬件加速解码缩略图，解决大图列表滚动掉帧
- **视频预览**（`VideoPreviewHelper` + C++ `SolarWin.Native` / `VideoThumbnailer`）：Native 侧提取视频关键帧，配合 `VideoMediaCache` 缓存
- **AI 对话页**：`AiPage` 重构为聊天式 AI 交互界面
- **LeasedLruCache**：可租约 LRU 缓存，支撑多规格缩略图流水线

### 修复

- **托盘关闭**：解决 `H.NotifyIcon` 第二窗口导致进程无法退出的问题
- **崩溃处理**：UI Dispatcher 损坏时不再维持托盘进程；未处理异常写入 `unhandled-exceptions.log`
- **Native DLL 打包**：`SolarWin.Native.dll` 现作为 `Content` 正确打入 Build / Publish / MSIX 输出，不再依赖额外 Copy Target
- **聊天消息**：`publisher.gatekept_follows` 等字段 `null` 导致整表反序列化失败；新增 `FlexibleBoolConverter` / `FlexibleInt32Converter`

### 变更

- **GitHub Actions CI/CD** 全面重写：并行 Build + Test → 便携 ZIP → MSIX（含 `MSIX_PFX_BASE64` 签名 secrets）→ SHA256SUMS → Release upload；tag push 自动发版
- `MessageItemViewModel`、`ChatDetailViewModel`、`PostsViewModel` 等核心 ViewModel 重构，消息状态与操作分离
- `ImagePreviewHelper` / `DysonFileImageLoader` 大幅增强，支持多规格缩略图流水线

---

## [1.1.2] — 2026-07-25

### 新增

- **聊天本地持久化**：SQLite WAL 模式单写泵（`ChatWritePump`），聊天记录离线可用
  - EF Core Migrations（可回滚）：消息表 + 房间表
  - `ChatLocalStore` / `RoomLocalStore` 本地缓存，`ChatMessageMapper` / `ChatRoomMapper` 领域映射
  - `SqliteWalConnectionInterceptor` 保证 WAL 连接语义

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
