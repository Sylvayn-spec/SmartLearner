# Smart Learner

面向学生的 Windows 桌面学习工作台：课程资料、Markdown 笔记、课件 AI 整理、基于知识库的「书僮」问答，以及知乎热榜与关注推送。

- 技术栈：C# 12 / .NET 8 / WPF / MaterialDesignInXaml / CommunityToolkit.Mvvm
- 目标平台：Windows 10 1903+ / Windows 11（x64）
- 功能清单：[docs/01-功能清单.md](docs/01-功能清单.md)
- 30 秒演示：[dist/demo.mp4](dist/demo.mp4)（下载页：[dist/index.html](dist/index.html)）

![首页](docs/screenshots/home-tiles.png)

## 能做什么

- **首页**：今日课程、课程/关键词统计、知乎热榜与「我的关注」，每周一自动刷新，也可手动拉取。
- **课程**：每门课自动创建 `Note` / `TextBook` 两个目录；笔记统一进 Note，导入资料统一进 TextBook；支持文件夹浏览、PDF 预览、Markdown 渲染预览与源码编辑、回收站。
- **整理**：抽取 PDF / DOCX 文本，调用百炼整理成中文知识点；结果带目录渲染、可编辑、可导出 `.md` 到指定课程；支持全屏查看（Esc 退出）。
- **书僮**：勾选资料建库（切块 + 向量化，扫描版 PDF 自动 OCR），基于课程知识库回答并标注来源；支持多会话保存、批量删除、复制纯文本、全屏对话。
- **问问知乎**：输入 `问问知乎:[问题]` 直接调用知乎搜索，以卡片展示最多 3 篇相关文章，点击跳转原文。
- **设置**：知乎 Access Key 与阿里百炼 API Key 独立存储（DPAPI 加密），模型名可改；数据根目录可迁移。

| 整理结果全屏 | 书僮与知乎卡片 |
| --- | --- |
| ![整理](docs/screenshots/organize-fullscreen.png) | ![书僮](docs/screenshots/assistant-normal.png) |

| Markdown 预览 | 下载页 |
| --- | --- |
| ![Markdown](docs/screenshots/md-preview.png) | ![下载页](docs/screenshots/landing.png) |

## 下载与安装

已提供自包含安装包（不需要预装 .NET）：

```
dist/SmartLearner-Setup-1.0.0.exe   # 55.7 MB
SHA256: e1c4e3e28143b198bce498a00c0d889c3fb953dd70eaa23c385385e861750fc9
```

安装向导支持自定义目录、创建桌面快捷方式、快速启动栏、安装后启动；卸载不会删除用户数据。

把 `index.html`、安装包、`demo.mp4`、`preview.png` 放在同一目录即可作为下载页托管，详见 [`dist/安装说明.txt`](dist/安装说明.txt)。

## 从源码构建

本机未安装 .NET SDK 时，仅需一次：

```powershell
powershell -ExecutionPolicy Bypass -File tools\install-dotnet.ps1
```

装到 `%USERPROFILE%\.dotnet`，不需要管理员权限。

```bat
build-and-run.cmd     :: 构建并启动
run.cmd               :: 直接启动（需已构建）
```

或手动：

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"
& "$env:DOTNET_ROOT\dotnet.exe" build SmartLearner.sln
& "$env:DOTNET_ROOT\dotnet.exe" src\SmartLearner.App\bin\Debug\net8.0-windows\SmartLearner.App.dll
```

## 打包发布

```powershell
# 1. 自包含发布（win-x64，无需目标机安装 .NET）
& "$env:USERPROFILE\.dotnet\dotnet.exe" publish src\SmartLearner.App\SmartLearner.App.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false `
    -o artifacts\publish

# 2. 用 Inno Setup 6 编译安装包
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer.iss
# 产物：dist\SmartLearner-Setup-1.0.0.exe
```

图标由 `tools/make-icon.py` 生成；演示视频由 `tools/gen-promo.py` 生成（2 倍画布 + 浮点仿射裁剪，避免缩放抖动）。

## 数据目录

默认 `桌面/SmartLearner`（可在设置中修改根目录）：

```
settings.json          # 配置（固定放默认目录，便于找回）
courses.json           # 课程
keywords.json          # 关注关键词
AccessKey/
├─ zhihu.json          # 知乎 Key（DPAPI 加密）
└─ bailian.json        # 百炼 Key（DPAPI 加密）
Preference/
└─ preference.json     # 推送偏好
cache/                 # 知乎周缓存
chat/                  # 对话记录，每个会话一个 <id>.json
Documents/<课程>/
├─ Note/               # 笔记
└─ TextBook/           # 导入的资料
Database/<课程>/
├─ chunks.json         # 文本片段
└─ vectors.bin         # float32 向量
logs/                  # 按天日志（脱敏）
.trash/                # 删除回收站
```

## 目录结构

```
src/SmartLearner.App/
├─ App.xaml                 # 资源、主题、页面映射
├─ MainWindow.xaml          # 顶栏 + 侧边栏 + 内容区
├─ Models/                  # 设置、课程、知乎、知识库、对话
├─ Infrastructure/          # 路径、JSON 原子写、日志、DPAPI、网络探测
├─ Services/                # 设置、课程、文档、知乎、百炼、知识库、对话、整理
├─ ViewModels/              # 页面 ViewModel
├─ Views/                   # 页面视图与管理对话弹窗
└─ Themes/                  # 宣纸白 / 夜读墨 两套色板

dist/                       # 安装包、下载页、演示视频
packaging/                  # 安装向导说明与中文语言文件
tools/                      # 安装脚本、图标与视频生成脚本
```

## 配置

首次使用到「设置」填写：

- 知乎开放平台 Access Key（热榜、关注、`问问知乎`）
- 阿里百炼 API Key（整理、书僮、OCR）
- 可选模型：对话 `qwen-flash`、向量 `text-embedding-v4`、视觉 `qwen-vl-ocr`

Key 只保存在本机，使用 Windows DPAPI 加密，不进入代码与日志。

## 已知限制

- 仅支持 Windows x64；PDF / Markdown 预览依赖 Microsoft Edge WebView2 运行时（Win11 自带）。
- 扫描版 PDF 依赖 OCR，视觉模型按页计费；纯图片、无文字层的页面可能识别失败。
- 安装包未做代码签名，首次运行可能出现 SmartScreen 提示。
- PDF 文本抽取使用 iText7（AGPL）；如需闭源分发，请替换该依赖或取得商业授权。
- 知乎与百炼接口受各自平台配额、模型权限影响，Key 过期时应用会提示并引导到设置页。

## 功能清单

- [x] 外壳：顶栏、侧边栏、明暗主题、自适应窗口、全屏与 Esc 退出
- [x] 基础设施：路径初始化、JSON 原子写、日志脱敏、DPAPI、网络探测、全局异常
- [x] 设置页：数据目录、知乎 / 百炼 Key、模型名、主题、缓存 TTL
- [x] 首页：今日课程、添加/管理课程、冲突校验、知乎热榜与关注、手动/每周刷新
- [x] 课程：Note / TextBook 归档、导入、PDF 预览、Markdown 渲染与编辑、回收站
- [x] 整理：文本抽取、百炼整理、目录渲染、导出 `.md`、全屏查看
- [x] 书僮：建库、向量索引、OCR、问答与来源、多会话持久化、批量删除、复制
- [x] 问问知乎：格式识别、最多 3 篇卡片、点击打开原文
- [x] 发布：自包含安装包、桌面快捷方式、下载页与 30 秒演示视频
