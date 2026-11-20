# Smart Learner

面向学生的桌面学习工作台：今日课表 + 知乎热榜 + 课程资料管理 + 「书僮」知识库问答 + 课件 AI 整理。

- 技术栈：C# / .NET 8 / WPF / MaterialDesignInXaml / CommunityToolkit.Mvvm
- 目标平台：Windows 10 1903+ / Windows 11（x64）
- 功能范围：[docs/01-功能清单.md](docs/01-功能清单.md)

## 环境

本机未安装 .NET SDK 时，仅需一次：

```powershell
powershell -ExecutionPolicy Bypass -File tools\install-dotnet.ps1
```

装到 `%USERPROFILE%\.dotnet`，不需要管理员权限。

## 构建与运行

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

## 目录结构

```
src/SmartLearner.App/
├─ App.xaml                 # 资源、主题、页面映射
├─ MainWindow.xaml          # 顶栏 + 侧边栏 + 内容区
├─ Models/                  # 设置、课程等数据模型
├─ Infrastructure/          # 路径、JSON 原子写、日志、DPAPI、网络探测
├─ Services/                # 设置服务、主题服务
├─ ViewModels/              # 页面 ViewModel
├─ Views/                   # 页面视图
└─ Themes/                  # 宣纸白 / 夜读墨 两套色板
```

## 数据目录

默认 `桌面/SmartLearner`：

```
settings.json      # 配置（固定放默认目录，便于找回）
courses.json       # 课表
keywords.json      # 关注关键词
cache/             # 推送缓存
Documents/         # 课程资料（每门课一个文件夹）
Database/          # 向量索引
logs/              # 按天日志
.trash/            # 删除回收站
```

## 当前进度

- [x] 外壳：顶栏、侧边栏、明暗主题、自适应窗口
- [x] 基础设施：路径初始化、JSON 原子写、日志脱敏、DPAPI、网络探测、全局异常
- [x] 设置页：数据目录、Access Secret、主题、缓存 TTL
- [x] 首页课表：添加课程弹窗、星期/时间选择、冲突校验、今日展示、`courses.json` 持久化、自动建课程文件夹
- [x] 课程文档：课程切换、文件夹浏览、导入 PDF/DOCX、PDF 预览（WebView2）、Markdown 笔记编辑、回收站
- [x] 整理：PDF/DOCX 抽取、分段调用百炼、Markdown → 带目录 HTML、源文编辑、导出到课程文件夹（需百炼 Key）
- [ ] 首页推送：热榜/关注、缓存
- [ ] 书僮：建库、索引、问答
