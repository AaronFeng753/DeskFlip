<p align="center">
<img src="/src/DeskFlip/Assets/screen.png" height="150">
</p>

<h2 align="center"> DeskFlip </h2>

<h3 align="center"> 鼠标手势工具：摩擦屏幕边缘，切换 Windows 11 虚拟桌面 </h3>

<p align="center">
<a href=https://github.com/AaronFeng753/DeskFlip/releases/latest><img src="https://img.shields.io/github/v/release/aaronfeng753/deskflip?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&style=flat-square&color=brightgreen"></a>
<img src="https://img.shields.io/badge/%E6%94%AF%E6%8C%81-Windows%2011%20x64-blue?logo=Windows&style=flat-square">
</p>

#### [📜English README](https://github.com/AaronFeng753/DeskFlip/blob/main/README.md)

# [💾下载最新版本 (Windows 11 x64)](https://github.com/AaronFeng753/DeskFlip/releases/latest)

单个绿色便携 `DeskFlip.exe`，免安装、无需 .NET 运行时，放到任意位置直接运行。

界面语言：English、简体中文。

# DeskFlip 是什么？

### 一个鼠标手势托盘小工具：把鼠标光标移到屏幕左/右边缘，沿边竖直来回滑动（像在"摩擦"边缘），即可切换到上一个/下一个虚拟桌面。无需按任何快捷键。

### ✨功能特性：
- 🖱无按钮、无快捷键：摩擦屏幕边缘即可切换桌面。
- 🎯灵敏度可调：触发区宽度、单段位移、摩擦次数等均可设置。
- 🖥智能全屏检测：全屏应用运行时自动禁用手势。
- 🚫按进程禁用：指定程序运行时绝不触发。
- 🌗深色模式设置窗口，贴合 Windows 11 风格。
- 🚀可选开机自启动。
- 🔋轻量：安静驻留系统托盘，资源占用几乎为零。

# 为什么做这个软件

我在 Windows 上唯一用到的鼠标手势就是切换虚拟桌面。我曾经在用的手势软件早已无法在 Windows 11 上工作，网上能找到的开源替代品也都停更多年。所以我自己开发了这个软件，免费开源发布。

# 使用方法

- 在屏幕**左**边缘竖直来回滑动（下-上）→ 切换到左侧虚拟桌面；**右**边缘同理。
- 技巧：把光标甩到边缘撞边停住，然后沿边竖直滑动。
- **双击**托盘图标打开设置；**右键**托盘图标可暂停手势或退出。
- 首次运行会自动打开一次设置窗口。
- 到最左/最右桌面时同向摩擦无效果（与 `Win+Ctrl+←/→` 快捷键行为一致，不循环）。

# 隐私政策🙈🙉🙊

```
1. 本软件从不联网。没有服务器，没有遥测，没有更新检查。

2. 所有设置保存在本地 %AppData%\DeskFlip\settings.json。

总之，我们不收集你的任何数据。
```

# 关于杀毒软件警告

DeskFlip 通过全局鼠标钩子观察光标位置，并通过向 Windows 发送标准 `Win+Ctrl+←/→` 快捷键来切换桌面。从启发式特征上看这与键盘记录器/自动化工具相似，因此未签名的程序可能被 Windows SmartScreen 或杀毒软件警告，这属于误报。全部源代码已在此公开，欢迎审查。

# [📄许可证](https://github.com/AaronFeng753/DeskFlip/blob/main/LICENSE)

#### DeskFlip 是自由开源软件，采用 [GNU AGPLv3](https://github.com/AaronFeng753/DeskFlip/blob/main/LICENSE) 许可证。

# 💝致谢💝：
- 应用图标：[Screen icons created by Magnific - Flaticon](https://www.flaticon.com/free-icons/screen)
