# Calculator

Windows 极简轻量计算器：圆角按钮、低饱和配色、浅色 / 深色主题。

一个单文件级的 WPF 桌面计算器，不依赖 Visual Studio 工程文件。直接 `csc` 编译成单一 exe，双击即可使用。

## 截图与体验

- 无边框窗口 + 自定义标题栏
- 圆角按键，按键按类型上色（数字 / 运算符 / 等号 / 功能）
- 浅色、深色主题，关闭后自动记住
- 滑出历史面板，保留最近 30 条计算，可点击召回
- 结果过长时自动缩小字号
- 窗口可拖拽调大小，标题栏可一键还原默认尺寸

键盘布局：

```
C    ←    %    ÷
7    8    9    ×
4    5    6    −
1    2    3    +
±    0    .    =
```

## 计算规则

与常见桌面计算器一致：**即时运算**。每按一个新运算符，会先算完上一步待定运算。

- `2 + 3 × 4 =` 得到 `20`（不是先乘后加）
- 连续按 `=` 会重复上一次运算（`5 + =` → `10` → `15`）
- `+` / `-` 后的 `%` 按左侧的百分比取：`200 + 10%` = `220`
- `×` / `÷` 后的 `%` 按当前值除以 100：`200 × 10%` = `20`
- 除以 0 显示「不能除以 0」，再输入数字会自动清除错误
- 显示最多 12 位有效数字，整数部分按千分位加逗号

## 快捷键

| 按键 | 作用 |
|------|------|
| `0`–`9` / 小键盘 | 输入数字 |
| `+` `-` `*` `/` | 四则运算 |
| `Enter` 或 `=` | 等于 |
| `Backspace` | 退格 |
| `Esc` 或 `C` | 清除 |
| `.` | 小数点 |
| `F9` 或 `±` | 正负号 |
| `Shift + 5` | 百分号 |
| `Ctrl + C` | 复制当前结果 |
| `Ctrl + V` | 粘贴数字（支持千分位逗号、全角数字） |
| 双击显示区 | 复制结果 |

## 编译

环境：Windows + .NET Framework 4.x（系统自带）+ Roslyn `csc.exe`。

仓库里的 `build.bat` 会：

1. 如果还没有 `calc.ico`，先编译并运行 `MakeIcon.cs` 生成图标
2. 把 `CalcEngine.cs` 和 `App.cs` 编译成无控台窗口程序
3. 输出到桌面 `D:\Users\Administrator\Desktop\轻量计算器.exe`

如果你的 Visual Studio Build Tools 路径不一样，先改 `build.bat` 里的 `CSC` 变量，再把最后的 `/out:` 改成自己的目录。

手动编译示例（路径按机器修改）：

```bat
set CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe
set FX=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
set WPF=%FX%\WPF

"%CSC%" /nologo /langversion:latest /codepage:65001 /target:winexe /optimize+ ^
  /lib:"%FX%" ^
  /r:"%WPF%\PresentationCore.dll" ^
  /r:"%WPF%\PresentationFramework.dll" ^
  /r:"%WPF%\WindowsBase.dll" ^
  /r:"%FX%\System.Xaml.dll" ^
  /win32icon:calc.ico ^
  /out:MiniCalc.exe CalcEngine.cs App.cs
```

## 运行测试

`Tests.cs` 是一份控制台测试，覆盖加减乘除、百分号、连续等号、除零、粘贴、千分位等。

```bat
"%CSC%" /nologo /langversion:latest /target:exe /out:Tests.exe CalcEngine.cs Tests.cs
Tests.exe
```

全部通过时会打印 `OK`。

## 文件说明

| 文件 | 作用 |
|------|------|
| `App.cs` | 窗口、主题、历史、键盘与鼠标 |
| `CalcEngine.cs` | 计算引擎（即时运算、格式化、粘贴） |
| `MakeIcon.cs` | 生成 `calc.ico` |
| `Tests.cs` | 引擎单元测试 |
| `build.bat` | 一键编译 |

## 许可

仅供个人使用与学习。
