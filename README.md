# SolarWin

**Solar Network** 的 Windows 桌面客户端。基于 WinUI 3 与 Windows App SDK，对接官方 API 网关，提供登录、聊天、帖子、探索、网盘、通知、钱包、天气与个人中心等能力。

| 项 | 说明 |
|----|------|
| 当前版本 | **1.1.0** |
| 产品显示名 | Solar Network |
| 解决方案 | `SolarWin.slnx` |
| 目标平台 | Windows 10 1809+（最低 10.0.17763） |
| API | `https://api.solian.app` |
| User-Agent | `SolarWin/1.1` |

---

## 版本 1.1.0 亮点

- **帖子流**：可靠加载公共帖 / 时间线；修复 publisher 空布尔导致整页解析失败
- **聊天贴纸**：发送 `:{prefix}+{slug}:` 并按图显示（lookup / 批量解析）
- **壁纸**：设置页可选图片，调节透明度与模糊强度，**玻璃 / 磨砂**质感
- **导航图标**：符号字体 + 主题前景色，自包含发布后图标稳定显示
- **托盘**：`icon.ico` 任务栏 / 托盘图标；关闭或最小化到托盘
- **聊天通知**：后台收到私聊/群聊消息时系统通知 + 托盘气泡
- **寻思**：人格机器人对话（SSE 流式）
- **天气**：玻璃拟态仪表盘 UI
- **多账号**：本地保存与切换 PasswordVault token 槽

完整变更见 [CHANGELOG.md](CHANGELOG.md)。

---

## 功能一览

| 模块 | 能力 |
|------|------|
| 登录 | Padlock 挑战应答、多因素、设备码 / 扫码、通行密钥相关流程 |
| 会话 | PasswordVault 存 Token；启动恢复；401 刷新；登出清理 |
| 首页 | 签到、状态、趣味 API（运势 / IP / 回顾等） |
| 聊天 | 房间列表缓存、消息同步、回复 / 反应、图片与语音、贴纸面板、WebSocket 实时 |
| 帖子 | 公共流与时间线、发帖（含图片附件）、详情 / 回复 / 反应 / 转发 |
| 探索 | 贴纸包、出版者、精选等内容发现 |
| 寻思 | 模型 / 人格对话，流式输出 |
| 天气 | 城市搜索、小时 / 日预报、空气质量等 |
| 网盘 | DysonFS 浏览、建夹、上传下载（进度） |
| 通知 | 列表、未读角标、全部已读 |
| 钱包 | 余额与流水 |
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
| 其它 | NAudio（语音）、QRCoder、H.NotifyIcon（托盘） |
| 打包 | MSIX（可选）/ 解包自包含 |
| 发布 | Release：单文件自包含；Debug：关闭 AOT 便于调试 |
| 架构 | x86 / x64 / ARM64 |

---

## 仓库结构

```
.
├── SolarWin.slnx
├── README.md
├── CHANGELOG.md
├── docs/
│   ├── api-contract.md           # 后端 API 说明（Drive 等）
│   └── swagger.md                # OpenAPI 摘录
├── SolarWin.Tests/               # 纯逻辑单元测试
└── SolarWin/
    ├── App.xaml(.cs)             # 启动、DI、登录门控
    ├── Views/                    # 页面（Shell + 业务页）
    ├── ViewModels/
    ├── Services/                 # 认证、API、Token、Toast、托盘、通知
    ├── Models/
    ├── Helpers/                  # JSON、壁纸、主题、Dyson 图片等
    ├── Controls/
    ├── Assets/                   # icon.ico、商店徽标等
    ├── Package.appxmanifest      # Identity Version = 1.1.0.0
    └── SolarWin.csproj           # Version = 1.1.0
```

分层示意：

```
Views (XAML)
    → ViewModels (ObservableObject / RelayCommand)
        → Services (IAuthService, ISolarApiClient, ITokenStorage, …)
            → HTTPS → api.solian.app
```

---

## 架构说明

### 启动与导航

1. `App` 构建 DI，调用 `IAuthService.InitializeAsync()` 尝试恢复会话。
2. **未登录** → `LoginPage`；**已登录** → `ShellPage`（左侧 `NavigationView` + 内容 `Frame`）。
3. 主导航：首页、聊天、帖子、探索、寻思、天气、文件、通知、钱包、我的；设置从 NavigationView 齿轮进入。

### 认证

`AuthService` 对齐官方 Web 客户端 Padlock 流程：

1. 创建 challenge → 轮询 factors → 提交密码 → `authorization_code` 换 Token  
2. 支持 Device Code / 扫码等桌面登录路径  
3. Access / Refresh Token 仅写入 **PasswordVault**，不落日志  
4. `GetTokenAsync` 在过期时自动 refresh  

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
| Messager | `/messager/*` | 聊天房间、消息、同步、已读、实时 |
| Sphere | `/sphere/*` | 帖子、时间线、贴纸、出版者、探索 |
| Ring | `/ring/*` | 通知列表与未读数 |
| Drive | `/drive/*` | 网盘文件与上传下载 |
| Wallet | `/wallet/*` | 钱包与流水 |
| Personality | `/personality/*` | 寻思对话 |

更完整的网关契约见 [`docs/api-contract.md`](docs/api-contract.md) 与 [`docs/swagger.md`](docs/swagger.md)。

### 依赖注入（摘要）

- **Singleton**：`ITokenStorage`、`ISolarApiClient`、`IAuthService`、`IToastService`、`ITrayService`、`MainViewModel`、`ChatViewModel`、`PostsViewModel`、`DysonFileImageLoader`、`IChatDataCache` 等  
- **Transient**：多数业务 `*ViewModel`  
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
dotnet build SolarWin/SolarWin.csproj -c Debug -p:Platform=x64

# 运行
dotnet run --project SolarWin/SolarWin.csproj -c Debug -p:Platform=x64
```

或用 Visual Studio 打开 `SolarWin.slnx`，平台选 **x64**，F5 启动。

### 测试

```powershell
dotnet test SolarWin.Tests/SolarWin.Tests.csproj -c Debug
```

### 发布

```powershell
# 示例：x64 Release（单文件自包含）
dotnet publish SolarWin/SolarWin.csproj -c Release -r win-x64 -p:Platform=x64
```

发布配置位于 `SolarWin/Properties/PublishProfiles/`（`win-x64` / `win-x86` / `win-arm64`）。

| 配置 | 行为 |
|------|------|
| Debug | 关闭 AOT / ReadyToRun / Trim，便于迭代 |
| Release | 单文件自包含 CoreCLR（默认关闭 AOT/裁剪，兼容反射 JSON） |

---

## 配置与安全

- **API 基址**写在 `SolarApiClient.BaseUrl`（默认 `https://api.solian.app`）。  
- **Token** 仅通过 `PasswordVaultTokenStorage` 持久化，请勿在日志或配置中明文写入密钥。  
- 本地偏好（主题、壁纸、托盘等）存于 `%LOCALAPPDATA%\SolarWin\` 或 ApplicationData。  
- 壁纸文件缓存在 `%LOCALAPPDATA%\SolarWin\wallpaper\`。  
- Device Code 登录若使用公开 `client_id`，注意不要提交私有 client secret。  
- 本仓库不包含服务端；后端行为以 Solar Network 网关与文档为准。

---

## 开发约定

- 新页面：`Views/*Page.xaml` + `ViewModels/*ViewModel`，在 `ServiceCollectionExtensions` 注册，并在 `ShellPage` 导航表中挂上 tag。  
- 新 API：优先在 `ISolarApiClient` / `SolarApiClient` 增加强类型方法，DTO 放 `Models/`。  
- JSON 统一走 `JsonDefaults.Options`，避免各处自定义序列化选项不一致。  
- 导航图标优先 `FontIcon` + `SymbolThemeFontFamily` + 主题前景色，勿依赖系统未保证的字形。  
- UI 线程与后台：跨线程更新 UI 使用 `App.DispatcherQueue`。  

---

## 版本号维护

| 位置 | 字段 |
|------|------|
| `SolarWin/SolarWin.csproj` | `Version` / `AssemblyVersion` / `FileVersion` / `InformationalVersion` |
| `SolarWin/Package.appxmanifest` | `Identity@Version`（四段，如 `1.1.0.0`） |
| 设置页 | 读取 InformationalVersion / 程序集版本 |
| HTTP | `User-Agent: SolarWin/1.1` |

发版时同步改上述位置与 `CHANGELOG.md`、`README.md`。

---

## 许可证

见仓库根目录 [`LICENSE.txt`](LICENSE.txt)。
