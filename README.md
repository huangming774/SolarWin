# SolarWin

**Solar Network** 的 Windows 桌面客户端。基于 WinUI 3 与 Windows App SDK，对接官方 API 网关，提供登录、聊天（含 MLS 端到端加密与实时通话）、帖子、探索、寻思、AI、天气、网盘、通知、钱包、恒星计划、瑞幸咖啡点餐与个人中心等能力。

| 项 | 说明 |
|----|------|
| 当前版本 | **1.1.9** |
| 产品显示名 | Solar Network |
| 解决方案 | `SolarWin.slnx` |
| 目标平台 | Windows 10 1809+（最低 10.0.17763）；MLS 加密仅 x64 |
| API | `https://api.solian.app` |
| User-Agent | `SolarWin/1.1` |

---

## 版本 1.1.9 亮点

- **聊天 MLS 端到端加密（RFC 9420）**：Rust OpenMLS 原生桥接，密码套件 `MLS_128_DHKEMX25519_AES128GCM_SHA256_Ed25519`；每账号独立 SQLCipher 加密数据库，密钥由 Windows DPAPI 保护；明文永不进入 HTTP 请求体，解密失败不回退明文
- **瑞幸咖啡点餐**：集成 MCP 协议驱动瑞幸 API，菜单浏览、规格定制、加购下单全流程；Token 安全存储于 PasswordVault
- **实时通话**：基于 LiveKit 的语音通话与屏幕共享（GPU 捕获），来电接听 / 拒绝
- **聊天数据中心**：活动热力图、词云、消息趋势、成员贡献排行
- **聊天本地持久化**：SQLite WAL 写入泵，聊天记录离线可用
- **GPU 图片渲染**：`GpuImage` / `FastWin2DImage` 硬件加速解码缩略图，大图列表不再掉帧
- **视频预览**：C++ `VideoThumbnailer` 提取关键帧 + `VideoMediaCache` 缓存
- **AI 对话页**：聊天式 AI 交互界面
- **GitHub Actions CI/CD**：并行构建 + 测试 → 便携 ZIP / MSIX（签名）→ SHA256 → Release，tag push 自动发版

完整变更见 [CHANGELOG.md](CHANGELOG.md)。

---

## 功能一览

| 模块 | 能力 |
|------|------|
| 登录 | Padlock 挑战应答、多因素、设备码 / 扫码、WebAuthn、验证码、注册 / 找回、社交登录 |
| 会话 | PasswordVault 存 Token；多账号切换；启动恢复；401 刷新；登出清理 |
| 首页 | 签到、状态、趣味 API（运势 / IP / 回顾等） |
| 聊天 | 房间列表缓存、消息同步、回复 / 反应、图片与语音、贴纸、WebSocket 实时、SQLite 离线记录 |
| 加密 | 房间内开启 MLS 加密、查看成员设备、重置 / 重新引导群组（见 `docs/mls-integration.md`） |
| 通话 | LiveKit 语音通话、屏幕共享、来电通知（接听 / 拒绝） |
| 数据中心 | 聊天活动热力图、词云、消息趋势、成员贡献、链接预览卡片 |
| 帖子 | 公共流与时间线、发帖（含图片附件）、详情 / 回复 / 反应 / 转发、领域（Realm） |
| 探索 | 贴纸包、出版者、精选等内容发现 |
| 寻思 / AI | 人格对话（SSE 流式）、聊天式 AI 页面 |
| 天气 | 城市搜索（地理位置排序）、小时 / 日预报、空气质量 |
| 网盘 | DysonFS 浏览、建夹、上传下载（进度） |
| 通知 | 列表、未读角标、全部已读、后台消息系统通知 |
| 钱包 | 余额与流水 |
| 恒星计划 | 会员订阅状态与管理 |
| 点餐 | 瑞幸咖啡：菜单浏览、规格定制、加购、真实下单（MCP 协议） |
| 资料 / 安全 | 资料编辑、设备 / 会话 / MFA / 密钥等 Padlock 能力 |
| 设置 | 主题、壁纸、托盘与通知开关、深度链接 `solian://`、多账号、缓存清理 |

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 语言 / 运行时 | C# / .NET 10（`net10.0-windows10.0.26100.0`） |
| UI | WinUI 3 + Windows App SDK 1.8 |
| 架构 | MVVM（CommunityToolkit.Mvvm） |
| DI / HTTP | `Microsoft.Extensions.DependencyInjection`、`IHttpClientFactory` |
| 持久化 | EF Core SQLite（WAL + 单写泵）、OfflineCache |
| 加密 | Rust OpenMLS v0.8.1（`SolarWin.Mls.Native`）、SQLCipher、DPAPI |
| 通话 | LiveKit（`Livekit.Rtc.Dotnet`）、Windows.Graphics.Capture |
| 图形 | Win2D、LiveCharts 2（数据中心图表） |
| MCP | `ModelContextProtocol.AspNetCore`（瑞幸点餐桥接） |
| 其它 | NAudio（语音）、QRCoder、H.NotifyIcon（托盘） |
| Native | C++ `SolarWin.Native`（视频缩略图）、Rust `SolarWin.Mls.Native`（MLS） |
| 打包 | MSIX（可选）/ 解包自包含 |
| 发布 | Release：单文件自包含；Debug：关闭 AOT 便于调试 |
| 架构 | x86 / x64 / ARM64（MLS 加密仅 x64，其它平台 fail-closed） |

---

## 仓库结构

```
.
├── SolarWin.slnx
├── README.md / CHANGELOG.md
├── docs/
│   ├── api-contract.md           # 后端 API 说明（Drive 等）
│   ├── mls-integration.md        # MLS 端到端加密集成说明
│   └── swagger.md                # OpenAPI 摘录
├── .github/workflows/            # CI/CD（构建 / 测试 / MSIX / Release）
├── SolarWin/                     # 主项目（WinUI 3）
│   ├── App.xaml(.cs)             # 启动、DI、登录门控、全局异常
│   ├── Views/                    # 页面（Shell + 业务页）
│   ├── ViewModels/
│   ├── Services/                 # 认证、API、MLS、LiveKit 通话、瑞幸 MCP、托盘、通知等
│   ├── Models/
│   ├── Helpers/                  # JSON、壁纸、主题、Dyson 图片、离线缓存等
│   ├── Controls/                 # GpuImage、热力图、词云、代码卡片、链接预览等
│   ├── Assets/                   # icon.ico、商店徽标等
│   ├── Package.appxmanifest
│   └── SolarWin.csproj
├── SolarWin.Native/              # C++ 视频缩略图（VideoThumbnailer）
├── SolarWin.Mls.Native/          # Rust OpenMLS 桥接库（Windows x64）
└── SolarWin.Tests/               # 纯逻辑单元测试（含 MLS 契约测试）
```

分层示意：

```
Views (XAML)
    → ViewModels (ObservableObject / RelayCommand)
        → Services (IAuthService, ISolarApiClient, IMlsClientService,
                    IRealtimeCallService, IMcpBridgeService, …)
            → HTTPS → api.solian.app
            → Native（Rust MLS / C++ 缩略图）
```

---

## 架构说明

### 启动与导航

1. `App` 构建 DI，调用 `IAuthService.InitializeAsync()` 尝试恢复会话。
2. **未登录** → `LoginPage`；**已登录** → `ShellPage`（左侧 `NavigationView` + 内容 `Frame`）。
3. 主导航：首页、聊天、数据中心、帖子、探索、寻思、天气、AI、文件、通知、恒星计划、钱包、点餐、我的；设置从 NavigationView 齿轮进入。
4. 未处理异常写入 `unhandled-exceptions.log`；UI Dispatcher 损坏时确保进程退出而非残留托盘。

### 认证

`AuthService` 对齐官方 Web 客户端 Padlock 流程：

1. 创建 challenge → 轮询 factors → 提交密码 → `authorization_code` 换 Token
2. 支持 Device Code / 扫码 / WebAuthn / 社交登录等桌面登录路径
3. Access / Refresh Token 仅写入 **PasswordVault**，不落日志；多账号分槽存储
4. `GetTokenAsync` 在过期时自动 refresh

### MLS 端到端加密

- 密码学操作全部在 `SolarWin.Mls.Native`（Rust OpenMLS）内完成，C# 侧只做编排
- 每账号独立 SQLCipher 数据库；数据库密钥与 Ed25519 签名私钥由 Windows DPAPI 按用户保护
- 密文以 `chat.mls.v2` 走普通消息接口；入站消息有序处理、解密成功后才 ACK
- 解密失败或引擎不可用时绝不回退为明文；加密房间禁止上传新媒体；非 x64 平台 fail-closed
- 详见 [`docs/mls-integration.md`](docs/mls-integration.md)

### 瑞幸点餐（MCP）

`LuckinMcpService` 通过 MCP（Model Context Protocol）桥接瑞幸 API：全量菜单扫描、模糊搜索（中文 / 拼音）、规格与属性定制、门店与订单管理；瑞幸 Token 存于 PasswordVault。

### HTTP 客户端

`SolarApiClient`（`User-Agent: SolarWin/1.1`）：

- 自动附加 Bearer
- **401** → refresh 后重试
- **429** → 遵循 `Retry-After`
- **5xx** → 最多 3 次重试
- JSON：`snake_case` + 宽松 Instant / Guid / 枚举 / 布尔转换（见 `Helpers/JsonDefaults`、`FlexibleJsonConverters`）

主要业务路径：

| 模块 | 前缀 | 能力 |
|------|------|------|
| Padlock / Passport | `/padlock/*`、`/passport/*` | 登录、账号、资料、状态、签到、社交 |
| Messager | `/messager/*` | 聊天房间、消息、同步、已读、实时、MLS |
| Sphere | `/sphere/*` | 帖子、时间线、贴纸、出版者、领域、探索 |
| Ring | `/ring/*` | 通知列表与未读数 |
| Drive | `/drive/*` | 网盘文件与上传下载 |
| Wallet | `/wallet/*` | 钱包与流水 |
| Personality | `/personality/*` | 寻思 / AI 对话 |

更完整的网关契约见 [`docs/api-contract.md`](docs/api-contract.md) 与 [`docs/swagger.md`](docs/swagger.md)。

### 依赖注入（摘要）

- **Singleton**：`ITokenStorage`、`ISolarApiClient`、`IAuthService`、`IToastService`、`ITrayService`、`IMlsClientService`、`IRealtimeCallService`、`IMcpBridgeService`、`MainViewModel`、`ChatViewModel`、`DysonFileImageLoader`、`IChatDataCache` 等
- **Transient**：多数业务 `*ViewModel`
- 命名 `HttpClient`：BaseAddress 指向网关，超时 10 分钟（适配大文件）

---

## 环境要求

- Windows 10 1809+ 或 Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022/2026，需含以下工作负载：
  - .NET 桌面开发（WinUI / Windows App SDK）
  - C++ 桌面开发（构建 `SolarWin.Native`）
- Rust 工具链（仅修改 `SolarWin.Mls.Native` 时需要；仓库内含预编译 DLL）

> 注意：解决方案含 C++ 项目（`.vcxproj`），`dotnet build` 无法直接构建完整解决方案，请使用 Visual Studio 或 VS 的 `MSBuild`。

---

## 构建与运行

```powershell
# 使用 Visual Studio 打开 SolarWin.slnx，平台选 x64，F5 启动

# 或使用 VS MSBuild（在「Developer PowerShell for VS」中）
msbuild SolarWin.slnx /t:Restore /p:Configuration=Debug /p:Platform=x64
msbuild SolarWin.slnx /p:Configuration=Debug /p:Platform=x64
```

### 测试

```powershell
# 先 x64 重建测试项目，避免跑到旧 DLL
msbuild SolarWin.Tests/SolarWin.Tests.csproj /t:Rebuild /p:Configuration=Debug /p:Platform=x64
dotnet test SolarWin.Tests/SolarWin.Tests.csproj -c Debug -p:Platform=x64 --no-build
```

### 发布

```powershell
# 示例：x64 Release（单文件自包含）
msbuild SolarWin/SolarWin.csproj /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64
```

发布配置位于 `SolarWin/Properties/PublishProfiles/`（`win-x64` / `win-x86` / `win-arm64`）。CI 上 tag push 会自动构建便携 ZIP 与签名 MSIX 并发布 Release。

| 配置 | 行为 |
|------|------|
| Debug | 关闭 AOT / ReadyToRun / Trim，便于迭代 |
| Release | 单文件自包含 CoreCLR（默认关闭 AOT/裁剪，兼容反射 JSON） |

---

## 配置与安全

- **API 基址**写在 `SolarApiClient.BaseUrl`（默认 `https://api.solian.app`）。
- **Solar Token** 仅通过 `PasswordVaultTokenStorage` 持久化；**瑞幸 Token** 同样存于 PasswordVault。请勿在日志或配置中明文写入密钥。
- MLS 数据库密钥与签名私钥由 **DPAPI** 按用户保护；密文不落明文日志。
- 本地偏好（主题、壁纸、托盘等）存于 `%LOCALAPPDATA%\SolarWin\` 或 ApplicationData。
- 壁纸文件缓存在 `%LOCALAPPDATA%\SolarWin\wallpaper\`；聊天记录 SQLite 持久化到用户数据目录。
- Device Code 登录若使用公开 `client_id`，注意不要提交私有 client secret。
- 本仓库不包含服务端；后端行为以 Solar Network 网关与文档为准。

---

## 开发约定

- 新页面：`Views/*Page.xaml` + `ViewModels/*ViewModel`，在 `ServiceCollectionExtensions` 注册，并在 `ShellPage` 导航表中挂上 tag。
- 新 API：优先在 `ISolarApiClient` / `SolarApiClient` 增加强类型方法，DTO 放 `Models/`。
- JSON 统一走 `JsonDefaults.Options`，避免各处自定义序列化选项不一致。
- SQLite 访问遵循既有约束：严格限域、可回滚 Migration、单写泵、UI 不同步等待。
- 导航图标优先 `FontIcon` + `SymbolThemeFontFamily` + 主题前景色，勿依赖系统未保证的字形。
- UI 线程与后台：跨线程更新 UI 使用 `App.DispatcherQueue`。

---

## 版本号维护

| 位置 | 字段 |
|------|------|
| `SolarWin/SolarWin.csproj` | `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion` |
| `SolarWin/Package.appxmanifest` | `Identity@Version`（四段，如 `1.1.9.0`） |
| 设置页 | 读取 InformationalVersion / 程序集版本 |
| HTTP | `User-Agent: SolarWin/1.1` |

发版时同步改上述位置与 `CHANGELOG.md`、`README.md`。

---

## 许可证

见仓库根目录 [`LICENSE.txt`](LICENSE.txt)。
