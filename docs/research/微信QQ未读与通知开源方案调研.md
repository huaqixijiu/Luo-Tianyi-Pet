# 微信与 QQ 未读消息开源方案调研

> 下文保留源码研究阶段结论；后续本机验证与 0.1.0.64 实现进度见 [验证报告](../validation/wechat-session-qq-preview-2026-09-12.md)。

日期：2026-09-12。范围：项目官方仓库、关键源码及官方文档，只读研究；未安装、执行、接入第三方机器人，未访问本机聊天数据库或正文。主工作区的 .63 安装保持不变。

## 用户已确认的产品行为

昵称、正文预览、新增通知计数共用一个详细提醒开关，三项一起启停，不增加正文或计数独立开关。关闭后保留来源提醒。预览仅限系统主动提供的通知文字，内存临时使用，不保存、上传或读聊天历史。通知次数不能标成 QQ／微信真实未读总数。本轮完成方案与边界确认，运行时正文和计数尚未实现。

## 源码核对结果

| 项目 | 做法与能取得的信息 | 对桌宠的意义 |
|---|---|---|
| [cluic/wxauto](https://github.com/cluic/wxauto/blob/main/wxauto/wxauto.py) | GetSessionAmont 从微信会话 UIA 控件提取新消息数字；GetNextNewMessage 会切换聊天再取消息列表 | 能读界面显示的未读数，但完整流程会操作微信，不能直接作为无干扰后台监听 |
| [FreeWisdom/wxauto-4.0](https://github.com/FreeWisdom/wxauto-4.0/blob/main/wxauto4/ui/sessionbox.py) | get_session 枚举会话子控件；SessionElement 从 Name 分行，以正则解析“[数字条]”；匹配失败直接返回 0 | 这是界面字段读取，不是系统全局未读 API。可以借鉴窄范围解析，但桌宠应把读取失败记为未知，不当成真实零未读 |
| [NapCatQQ](https://github.com/NapNeko/NapCatQQ/blob/main/packages/napcat-core/services/NodeIKernelRecentContactService.ts) | QQ 内部最近联系人服务定义 getMsgUnreadCount、getUnreadDetailsInfos；[监听接口](https://github.com/NapNeko/NapCatQQ/blob/main/packages/napcat-core/listeners/NodeIKernelRecentContactListener.ts) 有未读变化回调 | 数据路径比 Toast 完整，但依赖 QQ 内部服务，超出当前桌宠普通外部应用边界；接口声明不等于已经在本机验证返回值 |
| [Lagrange.Core](https://github.com/LagrangeDev/Lagrange.Core) | 自行实现 NTQQ 协议，作为账号消息客户端运行；当前主分支是 V2 | 能接收协议消息，不是读取已运行 QQ 桌面窗口的只读插件。本轮未证实它能同步桌面 QQ 的全局未读状态 |
| [WeChatFerry](https://github.com/lich0821/WeChatFerry) | 微信 Hook 框架，支持接收消息与查询数据库 | 能取得消息信息，但该路线涉及客户端内部能力，当前项目不采用 |
| [wechatauto-replica](https://github.com/fanyuantaier/wechatauto-replica/blob/main/README.md) | 项目说明通过进程内存取得解密材料，再读取本地会话和消息数据库；会话接口包含 unread | 可以绕开界面是否展开的限制，但会读取内存与聊天数据库，不符合现有约束；未运行验证其兼容性 |
| [NotiForward](https://github.com/gctuj/NotiForward) | Android 通知监听服务取得手机微信通知，再中转到 PC | 绕开 Windows 微信不发 Toast 的限制；仍只有通知摘要和通知事件，不能保证所有真实未读。需要手机配合 |

## 几个容易被名称误导的地方

wxauto 的“获取所有新消息”不等于静默读取全局未读。[旧版源码](https://github.com/cluic/wxauto/blob/main/wxauto/wxauto.py) 会显示微信、点击聊天入口并切换会话。微信 4.x 分支的 [监听说明](https://github.com/FreeWisdom/wxauto-4.0#监听稳定性优化建议) 说明控件可见性、窗口状态与版本影响结果，AddListenChat 会打开独立聊天子窗口。由此推断，整套接入可能干扰窗口或已读状态；只读会话字段能否在本机后台稳定暴露，仍需单独验证。此前测试的是托盘卡，不足以否定主会话列表的另一条路径。

NapCat 的 [GetRecentContact](https://github.com/NapNeko/NapCatQQ/blob/main/packages/napcat-onebot/action/user/GetRecentContact.ts) 实际调用内部最近会话快照，再按消息 ID 取内容；该公开动作的返回结构并未承诺完整未读总数。不能仅看到底层未读函数名就声称可直接取得准确数字。[框架入口](https://github.com/NapNeko/NapCatQQ/blob/main/packages/napcat-framework/napcat.ts) 加载 QQ wrapper 与 native 层，表明它走客户端内部接口。

NotiForward 的 [Android 服务源码](https://github.com/gctuj/NotiForward/blob/master/app/src/main/java/com/enthalpy/notiforward/NotificationForwardService.java) 属于 NotificationListenerService 路线。其现成架构通过公网中转并在 PC 存档，可选 AI 分类继续上传内容；不能把现成架构直接复制进本地、无聊天历史的桌宠。只能考虑未来独立的本地转发设计，且仍不能把通知数量冒充未读总数。项目宣传的“零风险”等绝对措辞未经本轮验证。

## 当前判断

QQ 继续使用已实测成功的 Windows Toast，最适合实现用户确认的昵称、预览、通知计数。微信的本地候选是会话列表公开 UIA 字段，但此前授权只覆盖托盘卡，不能把本次源码研究当成已授权遍历聊天列表或已实现微信未读读取。若后续扩大这一只读实验，应先约束字段、保留未知状态，验证后台／最小化／窗口未展开、99+、免打扰、群聊及“不切换会话、不改变已读”。

本轮没有找到可直接证明“普通权限、不碰客户端内部数据、不操作界面，同时对本机微信和 QQ 都提供准确全局未读总数”的通用库。这是本轮证据范围内的结论，不是声称所有技术路线都不可能。
