# MSIX 包身份准备

Windows `UserNotificationListener` 只有在应用具有 package identity、包清单声明
`uap3:userNotificationListener`，并由用户明确授权后才能读取通知来源。

`Package.appxmanifest.template` 保存已经确认的最小能力声明。仓库提供可重复的构建脚本：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_msix.ps1
```

脚本会：

1. 从正式“十二周年·抱抱”运行时图集的稳定无文字帧生成三项天依蓝包图标；
2. 创建 .NET 10 x64 自包含发布布局；
3. 首次运行时生成仅供本机测试的随机密码开发证书；
4. 用微软 `winapp` CLI 创建并签名 MSIX；
5. 校验清单能力、关键包文件、签名存在性和 SHA-256；
6. 将包、公钥证书与哈希写入 `artifacts/msix/release/`。

`artifacts/` 被 Git 忽略，PFX 私钥和随机密码只保存在
`artifacts/msix/private/`，不得提交或分享。脚本设置 `WINAPP_CLI_TELEMETRY_OPTOUT=1`。

正式安装测试前还需要：

1. 由用户确认将 `LuoTianyiPet.Dev.cer` 信任到当前用户的 `TrustedPeople`；
2. 由用户确认安装生成的 `.msix`；
3. 从安装后的桌宠设置页点击“授权访问”；
4. 用不含隐私内容的测试消息验证 QQ 和微信来源。

免安装 EXE 继续可用，但设置页会明确显示“需要 MSIX 包身份”，不会尝试绕过系统授权。
