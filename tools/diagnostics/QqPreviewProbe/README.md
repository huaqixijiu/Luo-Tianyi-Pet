# QQ 统一详情诊断

用户已授权系统通知预览后的定向诊断。以 .NET 10 SDK 构建本目录并设置 EnableNetFrameworkBuild=true，使用 Windows PowerShell 运行 run.ps1，复用已安装桌宠的包身份和既有通知权限。不开权限提示，不安装包。

使用正式 WindowsMessageNotificationSource 对最近一条 QQ 系统通知分别关闭／开启详情进行解析（通过反射调用本仓库自身方法），仅记录标题／预览长度和是否有通知标识，不保存任何文字或历史；随后监听 120 秒新通知事件。现有 Toast 的解析不算用户新消息完整界面验收。
