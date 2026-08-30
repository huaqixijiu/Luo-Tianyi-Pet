# 运行时素材

此目录保存已经确认用于程序运行的处理后素材。原始文件继续保存在 `候选素材_官方/`，不得被处理后文件覆盖。

## 当前文件

| 文件 | 用途 | 来源与处理 |
|---|---|---|
| `animations/processed/心律共鸣_享受音乐_无缝_0.5x.gif` | 音乐持续动画候选 | 来自官方“享受音乐”GIF；跳过前 4 帧一次性文字入场，将文字稳定的第 4～9 帧按 `4,5,6,7,8,9,8,7,6,5` 往返闭环，并将每帧 100 ms 调整为 200 ms；转换参数和来源哈希见同名 `.meta.json` |
| `animations/processed/心律共鸣_共鸣之声_倒放.gif` | 降低系统音量 | 来自官方“共鸣之声”GIF；保持帧时长并颠倒帧顺序 |
| `animations/runtime/guoyue-headpat.atlas.png` | 正式摸头反应 | 来自“天依游学记·国乐季 · 摸摸”官方动态 GIF；保留 10 帧及每帧 100 ms 时序，运行时 ID 为 `guoyue-headpat` |
| `animations/runtime/startup-*.atlas.png` | 四段启动时间问候 | 来自收藏集“早安”、十周年“干饭/摸鱼”和代号洛天依“眠了”官方 PNG；运行时通过程序动效叠加浮动、弹跳或呼吸 |
| `animations/runtime/resonance-awake-pop.atlas.png` | Windows 解锁/电源恢复 | 来自心律共鸣“苏醒”官方 PNG；运行时叠加一次放大回弹 |
| `animations/runtime/*.atlas.png` | WPF 实际播放图集 | 由选定 GIF、动态 WebP 或 PNG 的完整 RGBA 帧确定性生成 |
| `manifests/animations.json` | 运行时动画清单 | 保存帧尺寸、帧时长、循环、显示尺寸、透明边界、来源与图集 SHA-256 |

运行 `tools/animation_tools/compile_animation_atlases.py` 可以根据 `config/animation-sources.json` 重建图集和清单。处理后素材同样通过 Git LFS 管理。

运行 `tools/animation_tools/make_seamless_gif.py` 可以从源 GIF 的稳定帧区间重建往返闭环。本项目的“享受音乐”保留完整英文文字，但不再在每轮循环重新播放文字入场。

十二周年 QQ GIF 在原文件内部重复写入同一个短动作 13～20 次。运行时配置通过 `maximumFrames` 只保留首个完整周期，并用 `resizeWidth` / `resizeHeight` 生成 240×240 图集帧；原始 300×300 GIF 不修改。播放器可对有限图集使用反向时间线，用于“偷看”和“闪亮登场”的边缘躲藏。
