# Changelog

## [2.0.0] - 2026-10-14

### 界面与加载

- 重写天气页：实时天气、小时预报、十日预报与环境指标采用原生自适应布局，移除多层亚克力、光晕和阴影。
- 天气缓存读写移到后台；主天气先显示，空气质量独立更新；预报列表批量更新，刷新失败保留已有内容。
- 城市切换和页面离开时取消旧请求，防止迟到响应覆盖；修复快速搜索和目的地时区处理。
- 图片预览按窗口客户区居中并随窗口变化适配，修正居中留白下的滚轮缩放坐标。
- 视频预览重新计算窗口尺寸，下载和播放失败安全退出；完善 Win2D 控件事件解绑、重新加载和设备恢复。

### 安全与稳定性

- 媒体下载仅对受信任的 API HTTPS 来源发送 Bearer Token，外部图片与视频不再携带账号凭据。曾使用旧版本加载不可信外部媒体的用户建议撤销旧会话并重新登录。
- 修复旧房间缓存导入跨账号写入的竞态，以及账号切换后旧初始化等待不结束的问题。
- 修复搜索进入聊天缺少初始化、发送回显重复、历史跳转分页状态卡住，以及消息编辑/反应/附件更新未同步本地缓存的问题。
- 修复 SQLite DateTimeOffset 排序与保留期限查询，恢复消息年龄和数量清理；修复同批次更新/删除冲突及消息重新出现的问题。
- 修复订单预览与实际购物车不一致，保留已有订单支付入口；修复钱包迟到分页混入其他钱包的问题。
- 修复 Thinking 历史会话 ID 丢失与 AI 旧流式回复污染新会话的问题。
- 扫码登录生成和轮询切换到 `/stargate/auth/qr`，修复旧路由返回 `ROUTE_NOT_FOUND`。

### 验证与范围

- 本地 Debug x64 构建及 224 项测试通过；二维码生成和等待扫码状态已在线验证。
- 手机批准后的完整登录、真实订单/转账、GPU 重置与内存测量、ARM64 和发布包仍需独立验证。
- 版本统一为 `2.0.0`，程序集及 MSIX Identity 为 `2.0.0.0`。

### 新增

- **全局消息全文搜索**：在 ChatPage 顶部搜索框输入即同时搜索用户名与本地聊天记录。聊天结果按时间倒序展示（房间名 + 发送者 + 时间 + 含匹配子串的上下文摘要），点击跳转到 ChatDetailPage 并定位、短暂高亮该消息。基于每账号 SQLite 库内 FTS5 external-content 虚表（trigram 分词器 + 3 个同步触发器）；1–2 字查询（如「露营」）自动降级到 LIKE 兜底
- **回复引用可点击跳原消息**：聊天气泡里的引用块现在可点击，复用上述跳转定位机制

### 修复

- **聊天数据中心「按会话筛选」返回空**（1.1.9 已发布代码）：EF Core 的 SQLite provider 将 Guid 持久化为**大写** TEXT，而 `RoomId` 列没有 `COLLATE NOCASE`，原先把筛选参数规范化为小写后比较，匹配 0 行。参数改按大写绑定（`ChatAnalyticsRepository`）

### 改进

- 新增 EF Migration `20260913140000_AddChatMessagesFts`，为每账号聊天库增加 `chat_messages_fts` FTS5 虚表；随账号库文件整体删除，天然限域。迁移为幂等自愈式（先 DROP 再 CREATE），可修复任何历史遗留的索引状态
- FTS 建表与触发器统一改用 `_rowid_` 定位行：实体主键列 `RowId` 会遮蔽 SQLite 的隐式 `rowid`（标识符大小写不敏感），裸写 `rowid` 会读到 Guid 文本而非行号
- 触发器改为无条件镜像 `Content`，可搜索性完全由查询期谓词决定——与 FTS5 `rebuild` 的行为保持一致，并覆盖撤回消息（`messages.delete`）这类触发器无法表达的情况
- 新增仓储层 DB 测试 `ChatSearchRepositoryTests`（23 项）：在真实 SQLite 上重放迁移 DDL，覆盖 CJK 子串命中、旧行回填、触发器同步、解密恢复无残留 token、撤回/软删/非文本过滤、房间过滤大小写、迁移幂等与 `Down` 清理

### 性能

- **撤回探测走出二次复杂度**：新增部分表达式索引 `ix_chat_messages_retraction`，并把谓词里的 `COALESCE` 去掉（SQLite 按字面匹配表达式索引，包一层即失效且**不报错**）。原先每个候选命中都要解析房间里每条消息约 11 KB 的 `PayloadJson`，代价随群规模**平方增长**（单群 8000 条实测 16,337 ms → 12.35 ms）。测试对查询计划加断言，防止日后被静默改回
- **启用 `mmap_size=256MB`**（写连接与只读连接都设）。这是本工作负载上唯一有效的 pragma：单独实测 3.69x（广命中 FTS）、8.1x（LIKE 扫描）。**`cache_size` 与 `temp_store=MEMORY` 实测 0.99x / 1.00x，等于无效，故不设**
- 真实库上（最大群 530 条）实测：1 字查询 50.06 → 3.57 ms，2 字查询 4.06 → 0.27 ms

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
