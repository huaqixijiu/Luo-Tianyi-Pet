# Windows 安装与包身份

QQ / 微信来源提醒使用 Windows `UserNotificationListener`。微软要求调用方同时满足：

- 以 MSIX 安装并取得 package identity；
- 清单声明 `uap3:userNotificationListener` 和桌面应用所需的 `runFullTrust`；
- 使用者在桌宠“设置 → 通知”中亲自批准 Windows 权限。

因此便携 ZIP 和直接运行的普通 EXE 可以使用动画、音乐和文件功能，但永远不能开启通知监听。
这不是 QQ / 微信安装路径差异，也不能通过扫描进程、聊天数据库或窗口内容安全补救。

## 给其他 Windows 11 电脑测试

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_sideload_bundle.ps1
```

输出位于 `artifacts/sideload/release/`：

- `LuoTianyiPet-Installer-<version>-win-x64.zip`；
- 对应的 SHA-256 文件。

测试者完整解压后双击“安装洛天依桌宠.cmd”。脚本先校验 MSIX、公钥 CER 的 SHA-256 和
签名者指纹；首次电脑会显示一次 UAC，只把公开开发证书加入
`LocalMachine\TrustedPeople`，随后回到当前登录用户安装 MSIX。桌宠本体不会以管理员权限运行。
安装完成后仍要由使用者在设置页点击“授权访问”。

测试包不包含 PFX 私钥或证书密码。自签名证书只适合受控测试，证书过期、签名不一致、包被替换、
试图降级或文件不完整时安装器都会停止。

## 面向公众正式分发

所有普通 Windows 11 电脑都能直接安装且不导入测试证书，需要以下二选一：

1. 提交 Microsoft Store，由商店使用与 Partner Center 身份一致的证书签名；
2. 使用 Windows 已信任的生产代码签名证书签署 MSIX。当前脚本支持受信任 CA 签发且可由
   PFX 提供的代码签名证书；Azure Artifact Signing/Trusted Signing 需要另接其远程签名客户端。

仓库已支持第二条路径，证书和密码文件必须位于仓库外：

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_msix.ps1 `
  -Version 1.0.0.0 `
  -SigningMode Production `
  -ProductionCertificatePath D:\secrets\luotianyi-production.pfx `
  -ProductionCertificatePasswordPath D:\secrets\luotianyi-production-password.txt `
  -ProductionIdentityName LuoTianyiPet `
  -ProductionPublisherDisplayName 洛天依桌宠
```

脚本从 PFX 读取真实发布者 Subject 并写入清单，拒绝没有私钥或空密码；发布目录只输出签名
MSIX 与 SHA-256，不复制 PFX、密码或开发 CER。若选择 Microsoft Store，正式包名和 Publisher
必须改为 Partner Center 分配值，不能自行猜测。

## 单独构建开发 MSIX

```powershell
powershell -ExecutionPolicy Bypass -File tools\packaging\build_msix.ps1
```

脚本会创建 .NET 10 x64 自包含布局、生成/复用本机开发证书、打包签名并校验清单能力、关键文件、
签名和 SHA-256。PFX、随机密码和临时发布布局仅位于 Git 忽略的 `artifacts/msix/private/` 与
`artifacts/msix/staging/`。构建脚本本身不安装证书、不注册应用、不申请通知权限。
