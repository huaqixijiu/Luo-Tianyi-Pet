# QQ 后台通知只读探针

验证“QQ 在后台、鼠标不悬停托盘”时是否产生带独立标题的 Windows Toast。使用现有桌宠包身份和用户已授予的通知权限，不申请新权限、不改通知设置、不发送测试消息。

构建：用户级 .NET 10 SDK 执行 `dotnet build tools/diagnostics/QqBackgroundProbe/QqBackgroundProbe.csproj -c Release`。通过 Windows PowerShell 的 `Invoke-CommandInDesktopPackage`，用已安装 `LuoTianyiPet.Dev` 的 PackageFamilyName、AppId `App` 启动构建出的 `QqBackgroundProbe.exe`；不要以管理员运行。它没有可见窗口，不需要停止正式桌宠。

输出仅包含事件数量、相对时间、文本元素数、标题字符数、公开卡片是否存在及前台 QQ 布尔值；不写通知 ID、原始昵称、消息正文或其它应用名称。至少两个独立文本元素才读取第一个标题；其余文本元素值始终不读取。QQ 卡片读取复用正式的窄范围解析器，不遍历主界面。

构建输出目录 `result.txt` 出现 `READY` 后，让测试者从另一个账号发送普通私聊；QQ 保持后台、鼠标离开托盘。`Baseline=True` 是启动前已有通知，不能作为新消息验收。只有 `Baseline=False` 且 `QqForeground=False`、同一阶段无展开卡片，才能证明自动详情来源。事件数不等于未读总数。

探针最多运行五分钟；在构建输出目录创建 `stop` 文件可提前结束，重测前移除该文件。诊断完成后删除输出或仅保留匿名化的验证记录，不保存聊天内容。最终还必须启动正式桌宠并验证实际显示，不能把探针成功当成桌宠已经显示。
