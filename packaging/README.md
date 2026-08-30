# MSIX 包身份准备

Windows `UserNotificationListener` 只有在应用具有 package identity、包清单声明
`uap3:userNotificationListener`，并由用户明确授权后才能读取通知来源。

`Package.appxmanifest.template` 只保存已经确认的最小能力声明，不是可直接安装的包。
正式生成 MSIX 前还需要：

1. 生成三项包图标；
2. 选择开发签名证书或无签名开发包策略；
3. 将 Release 自包含输出放入包布局；
4. 验证发布者、包名和应用 ID 一致；
5. 在用户明确同意后安装测试包，再从设置页点击“授权访问”。

免安装 EXE 继续可用，但设置页会明确显示“需要 MSIX 包身份”，不会尝试绕过系统授权。
