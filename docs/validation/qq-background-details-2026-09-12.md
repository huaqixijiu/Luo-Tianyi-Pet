# QQ 后台自动详情实机验证（2026-09-12）

## 结论与边界

本机 QQ 9.9.31.49738 的普通私聊会主动产生 Windows Toast，提供独立的昵称标题；无需悬停托盘。0.1.0.60 的安装版存在通知对象跨线程和空图标两个故障，导致自动详情没有到达显示层。0.1.0.61 已修复并安装，最终真实私聊显示仍在核对。

Windows Toast 的事件次数不等于真实未读总数。本轮只确认自动昵称来源；精确未读数仍只能在 QQ 原生托盘卡已展开且公开结构可靠时补充。没有读取消息正文、缓存、数据库或私有接口，没有自动移动鼠标。

## 不悬停托盘的真实私聊

- 探针使用已授权桌宠包身份，通知权限 Allowed，QQ 不在前台。
- 首次快照的已有 QQ 通知不计入新消息验收。
- 用户确认收到私聊后，在第 48.0、50.3、51.8 秒检测到三条新的 QQ Toast，均有两个独立文本元素，首个标题长度为 4。
- 该阶段 QQ 托盘卡不存在，公开卡片读取次数为 0，因此标题来自系统通知。
- 第一次用户测试时正式桌宠未运行，不能把探针成功视为程序已经显示。随后启动 0.1.0.60，用户反馈仍未显示昵称，由此继续定位安装版故障。
- 后续用户展开卡片产生的样本与上面的自动通知阶段分开，不计入后台成功证据。

## 可复现的两个故障及修复

1. .NET Framework 4.8 的 UI 线程取得 `UserNotificationListener.Current` 后，将该对象用于线程池轮询，实测抛出 `0x8001010E`（RPC_E_WRONG_THREAD）。旧代码吞掉异常，界面权限仍显示 Allowed，只剩任务栏来源回退。改为权限检查和每轮后台轮询分别在自身线程取得监听器，不跨 COM apartment 复用。
2. 实测 QQ 的 `DisplayInfo.GetLogo` 返回 null，昵称标题仍有效。旧代码在 `OpenReadAsync` 空引用，连同详情一起丢掉；现把空图标视为可选字段缺失，保留昵称，界面使用已有来源字母图标。
3. 单文本元素可能是正文，现先检查至少两个文本元素才读取首项，而不是先读后丢弃。

修复后在 net48 实机直接检查：`TitleLength=4; LogoNull=True; MTA done count=10; Production event TitleLength=4; Production done; STA second count=10`。只记录长度和数量。正式解析器已经能在无图标条件下向消费方派发详情，最终仍需真实新消息端到端验收。

## 验证与发布

- 双目标 Release 编译无警告、无错误；Core 516、Animation 37、Windows 127 各两套，共 1360/1360 通过。
- 双目标真实 WPF 通知 QA 各 34 项通过；设置说明更新为自动读取系统通知标题，展开托盘仅用于补充未读数。已查看新的 WPF 设置页渲染图。
- 0.1.0.61 签名 Valid，覆盖安装后 Windows 包状态 Ok、正式进程响应；安装 EXE 与验证发布目录哈希一致。
- MSIX SHA-256：`9a3822ff952374f398ce1a8636ea056bf97c4ef8728822a63941208d9820af68`。
- 本轮不发送他人消息；测试私聊由用户自己从另一账号发送。临时探针已退出。

## 官方接口依据

微软的 [Notification listener 文档](https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/notification-listener) 说明 `GetNotificationsAsync(NotificationKinds.Toast)` 提供当前系统应用通知，可通过通知对象访问应用元数据和结构化文本。本机 QQ 是否产生该通知、标题是否可用，以本页实测为准。
