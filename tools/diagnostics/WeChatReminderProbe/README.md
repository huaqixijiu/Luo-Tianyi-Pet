# 微信正式来源匿名诊断

以 .NET 10 SDK 构建本目录（EnableNetFrameworkBuild=true），运行 net48 输出的 WeChatReminderProbe.exe。无需 MSIX 身份或提权；只调用本仓库正式 WeChatSessionReader 和 WindowsWeChatSessionNotificationSource。输出一次窗口状态／条目数量，再监听 120 秒，事件只输出标题与摘要长度及标识是否存在，不输出原文，不移动鼠标或窗口。运行时未保存结果，若需留证仅保存这些标量输出。
