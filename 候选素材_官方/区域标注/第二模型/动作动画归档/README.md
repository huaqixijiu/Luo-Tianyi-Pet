# 第二模型动作动画归档

本目录保存“晶蓝礼服版”动作的轻量动态预览。用户提供的 720×720、145 帧透明 PNG 母版位于相邻的
`动作动画png/`；母版保留在本地但不直接进入 Git，正式运行使用 `assets/animations/runtime/` 下的
256×256 PNG 图集。

| 编号 | 动作 | 触发区域 | 运行时 ID |
|---|---|---|---|
| 01 | 捂嘴 | Mouth | `crystal-cover-mouth` |
| 02 | 手比心 | LeftHand / RightHand | `crystal-hand-heart` |
| 03 | 摸腿脚 | LeftFoot / RightFoot | `crystal-touch-leg` |
| 04 | 捂肚子 | OtherBody | `crystal-hold-belly` |
| 05 | 摸摸头 | HeadAndHair | `crystal-headpat` |
| 06 | 遮眼睛 | LeftEye / RightEye | `crystal-cover-eyes` |
| 07 | 捏脸 | Face | `crystal-pinch-cheeks` |

Chest 与 LowerBodySensitiveArea 当前没有对应动作，点击后保持待机；第一模型不开放区域互动。

重新生成命令：

```powershell
python tools/animation_tools/prepare_crystal_interactions.py --root .
python tools/animation_tools/compile_animation_atlases.py --root .
```

来源帧哈希、透明处理策略、图集哈希与预览哈希记录在
`assets/animations/processed/晶蓝礼服_互动动作.meta.json`。
