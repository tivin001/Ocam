# OcamLauncher

#### 介绍
oCam启动器 - 自动屏蔽广告窗口并优化窗体位置及大小的启动工具

#### 功能特点
- **自动启动oCam**：一键启动oCam录屏软件
- **屏蔽广告窗口**：自动检测并关闭oCam的购买页面广告窗口
- **优化窗体位置**：自动将oCam窗口调整到桌面右侧200像素位置，垂直居中
- **优化窗体大小**：自动将oCam窗口调整到合适尺寸
- **进程监控**：持续监控oCam进程，进程退出时自动关闭启动器
- **兼容性**：支持Windows xp及以上系统

#### 软件架构
```
OcamLauncher/
├── Program.cs              # 程序入口，负责启动oCam和初始化窗口控制
├── AdWindowKiller.cs       # 窗口管理类，处理广告窗口屏蔽和窗口位置调整
├── OcamLauncher.csproj     # 项目文件（.NET Framework 3.5）
├── oCam.ico               # 应用程序图标
└── App/                   # oCam程序目录（需手动放置安装后的oCam）
    └── oCam.exe
```

#### 技术实现
- 使用Win32 API进行窗口操作（SetWinEventHook、EnumWindows、SetWindowPos等）
- 双模式广告检测：事件钩子模式（首选）和轮询模式（备选）

#### 安装教程

1.  下载或克隆本项目到本地
2.  将oCam录屏软件安装后的程序放入项目根目录的`App`文件夹中
3.  使用Visual Studio打开`OcamLauncher.sln`解决方案文件
#### 使用说明

1.  双击运行`OcamLauncher.exe`
2.  程序将自动启动oCam录屏软件
3.  oCam窗口会自动调整到桌面右侧200像素位置，垂直居中显示
4.  如果出现购买页面广告窗口，程序会自动关闭它
5.  关闭oCam后，启动器也会自动退出

#### 编译要求
- .NET Framework 3.5

#### 参与贡献

1.  Fork 本仓库
2.  新建 Feat_xxx 分支
3.  提交代码
4.  新建 Pull Request

#### 许可证
本项目基于MIT许可证开源

#### 特技
- 使用Windows事件钩子实现高效广告窗口检测
- 智能进程监控和自动退出机制
