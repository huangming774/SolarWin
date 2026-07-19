# SolarWin

**Solar Network** 的 Windows 桌面客户端。基于 WinUI 3 与 Windows App SDK，对接官方 API 网关，提供登录、聊天、网盘、通知与个人中心等能力。

| 项 | 说明 |
|----|------|
| 产品显示名 | Solar Network |
| 解决方案 | `SolarWin.slnx` |
| 目标平台 | Windows 10 1809+（最低 10.0.17763） |
| API | `https://api.solian.app` |

---

## 功能

- **登录**：Padlock 挑战应答（账号密码）；可选 OAuth 2.0 Device Code
- **会话**：Token 存入 Windows PasswordVault；启动自动恢复；401 刷新；登出清理
- **首页**：账号状态、每日签到等
- **聊天**：房间列表、消息、同步、已读、附件展示
- **网盘（DysonFS）**：目录浏览、建夹、重命名、回收、直传上传/下载（带进度）
- **通知**：列表、未读角标、全部已读
- **个人资料 / 设置**：资料查看与编辑、主题等
- **动态（Posts）**：页面占位，业务待扩展

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 语言 / 运行时 | C# / .NET 10（`net10.0-windows10.0.26100.0`） |
| UI | WinUI 3 + Windows App SDK 1.8 |
| 架构 | MVVM（CommunityToolkit.Mvvm） |
| DI / HTTP | `Microsoft.Extensions.DependencyInjection`、`IHttpClientFactory` |
| UI 辅助 | CommunityToolkit.WinUI |
| 打包 | MSIX |
| 发布 | Release：Native AOT + 自包含裁剪；Debug：关闭 AOT 便于调试 |
| 架构 | x86 / x64 / ARM64 |

---

## 仓库结构

```
.
├── SolarWin.slnx                 # 解决方案
├── docs/
│   └── api-contract.md           # 后端 API 契约（Drive 等）
├── README.md
└── SolarWin/
    ├── App.xaml(.cs)             # 启动、DI、登录门控
    ├── Views/                    # 页面（Shell + 业务页）
    ├── ViewModels/               # MVVM
    ├── Services/                 # 认证、API、Token、Toast
    ├── Models/                   # API DTO
    ├── Helpers/                  # JSON、头像、云文件 URL、主题
    ├── Controls/
    ├── Assets/
    └── Package.appxmanifest
```

分层示意：

```
Views (XAML)
    → ViewModels (ObservableObject / RelayCommand)
        → Services (IAuthService, ISolarApiClient, ITokenStorage)
            → HTTPS → api.solian.app
```

---

## 架构说明

### 启动与导航

1. `App` 构建 DI，调用 `IAuthService.InitializeAsync()` 尝试恢复会话。
2. **未登录** → `LoginPage`；**已登录** → `ShellPage`（`NavigationView` + 内容 `Frame`）。
3. Shell 导航：首页、聊天、动态、网盘、通知、资料、设置。

### 认证

`AuthService` 对齐官方 Web 客户端 Padlock 流程：

1. 创建 challenge → 轮询 factors → 提交密码 → `authorization_code` 换 Token  
2. 支持 Device Code 登录（桌面场景）  
3. Access / Refresh Token 仅写入 **PasswordVault**，不落日志  
4. `GetTokenAsync` 在过期时自动 refresh  

### HTTP 客户端

`SolarApiClient`（`User-Agent: SolarWin/1.0`）：

- 自动附加 Bearer  
- **401** → refresh 后重试  
- **429** → 遵循 `Retry-After`  
- **5xx** → 最多 3 次重试  
- JSON：`snake_case` + 宽松 Instant / Guid / 枚举转换（见 `Helpers/JsonDefaults`）

主要业务路径：

| 模块 | 前缀 | 能力 |
|------|------|------|
| Padlock / Passport | `/padlock/*`、`/passport/*` | 登录、账号、资料、状态、签到 |
| Messager | `/messager/*` | 聊天房间、消息、同步、已读 |
| Ring | `/ring/*` | 通知列表与未读数 |
| Drive | `/drive/*` | 网盘文件与上传下载 |

更完整的网关契约见 [`docs/api-contract.md`](docs/api-contract.md)。

### 依赖注入（摘要）

- **Singleton**：`ITokenStorage`、`ISolarApiClient`、`IAuthService`、`IToastService`、`MainViewModel`、`ChatViewModel`（列表跨页缓存）
- **Transient**：各业务 `*ViewModel`
- 命名 `HttpClient`：BaseAddress 指向网关，超时 10 分钟（适配大文件）

---

## 环境要求

- Windows 10 1809+ 或 Windows 11  
- [.NET 10 SDK](https://dotnet.microsoft.com/download)  
- 建议使用 Visual Studio 2022/2026（含 WinUI / Windows App SDK 工作负载），或仅用 `dotnet` CLI  

---

## 构建与运行

```powershell
# 克隆后进入仓库根目录
dotnet restore SolarWin/SolarWin.csproj
dotnet build SolarWin/SolarWin.csproj -c Debug

# 运行（按本机架构自动选择 RID）
dotnet run --project SolarWin/SolarWin.csproj -c Debug
```

或用 Visual Studio 打开 `SolarWin.slnx`，平台选 **x64**，F5 启动。

### 发布

```powershell
# 示例：x64 Release（Native AOT + 自包含）
dotnet publish SolarWin/SolarWin.csproj -c Release -r win-x64
```

发布配置位于 `SolarWin/Properties/PublishProfiles/`（`win-x64` / `win-x86` / `win-arm64`）。

| 配置 | 行为 |
|------|------|
| Debug | 关闭 AOT / ReadyToRun / Trim，便于迭代 |
| Release | `PublishAot` + `PublishTrimmed` + `SelfContained` |

---

## 配置与安全

- **API 基址**写在 `SolarApiClient.BaseUrl`（默认 `https://api.solian.app`）。  
- **Token** 仅通过 `PasswordVaultTokenStorage` 持久化，请勿在日志或配置中明文写入密钥。  
- Device Code 登录若使用公开 `client_id`，注意不要提交私有 client secret。  
- 本仓库不包含服务端；后端行为以 Solar Network 网关与 `docs/api-contract.md` 为准。

---

## 开发约定

- 新页面：`Views/*Page.xaml` + `ViewModels/*ViewModel`，在 `ServiceCollectionExtensions` 注册，并在 `ShellPage` 导航表中挂上 tag。  
- 新 API：优先在 `ISolarApiClient` / `SolarApiClient` 增加强类型方法，DTO 放 `Models/`。  
- JSON 统一走 `JsonDefaults.Options`，避免各处自定义序列化选项不一致。  
- UI 线程与后台：跨线程更新 UI 使用 `App.DispatcherQueue`。  

---

## 许可证

未指定许可证。若对外开源，请在仓库根目录补充 `LICENSE` 文件。
