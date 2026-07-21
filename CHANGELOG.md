# Changelog

本文件记录 SolarWin（Solar Network Windows 客户端）的版本变更。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号遵循语义化版本。

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
