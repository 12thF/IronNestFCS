# IronNestFCS

[Demo Video](https://www.bilibili.com/video/BV1xc7F6WEET/)

[Iron Nest: Heavy Turret Simulator](https://store.steampowered.com/app/4300500/) 的 [MelonLoader](https://melonwiki.xyz/) Mod，为游戏中的重型炮塔加入一套自动化**火控系统（Fire Control System, FCS）**：在地图上点选目标，Mod 会自动解算弹道、采购/装填炮弹、调整炮塔方向与仰角，并完成确认与击发的全套流程。

> 基于游戏 Demo 版本开发，使用 IL2CPP + MelonLoader。

## 功能

- **一键打击**：点击地图右侧的目标按钮（T1~T4），自动为该目标入队一次完整的打击任务。
- **战术雷达**：每 3 秒自动扫描战场，识别敌方实体并将其坐标映射到地图标记上；支持 `Numpad 0` 一键扫荡所有存活目标。
- **双炮管任务调度**：任务进入队列后由调度器自动派给空闲炮管，一管炮打完一发自动拉取下一个任务，两管炮并行作业。
- **自动弹道解算**：读取目标的方向角与距离，自动设定装药、弹种并解算所需仰角。
- **多弹种支持**：AP / HCHE / HE / STAR / SMK，可通过控制台旁的按钮选择当前弹种；弹仓缺弹时自动到采购台购买。
- **自动击发（可选）**：通过面板上的 `Auto Fire` 开关切换是否自动完成最后的击发动作。
- **最大装药（可选）**：通过 `Max Charge` 开关可强制使用 6 号装药，适用于追求极限射程的场景。
- **状态面板**：IMGUI 窗口实时显示两管炮的当前任务、目标参数、预估仰角/装药与待派发任务数。
- **热重载开发**：火控逻辑独立成可卸载的程序集，开发时改完代码按 **F9** 即可在不重启游戏的情况下重新加载。
- **自定义唱片机**（附带的独立 Mod）：扫描 `UserData/CustomRecords/` 下的音频文件（.mp3/.wav/.flac），用其内嵌封面与音频替换游戏内的 RecordDisk。

## 键盘快捷键

| 按键 | 功能 |
| --- | --- |
| `F9` | 热重载火控逻辑（开发用） |
| `Numpad 0` | 自动扫荡：将所有雷达扫到的存活目标依次入队 |
| `Numpad 1-4` | 分别击发 T1-T4 目标（等价于点击地图右侧按钮） |

## 架构

工程拆分为四个程序集，核心是为**热重载**服务的宿主 / 逻辑分离设计：

| 项目 | 角色 | 说明 |
| --- | --- | --- |
| `IronNestFCS` | **宿主 Mod** | 稳定加载、永不重载。负责首次加载 Logic、监听 F9 触发热重载、监控制台修改、转发生命周期回调。 |
| `IronNestFCS.Abstractions` | **契约** | 仅含 `IFcsModule` 接口。只加载一份，是唯一能安全跨 `AssemblyLoadContext` 边界传递的类型。 |
| `IronNestFCS.Logic` | **火控逻辑** | 所有高频改动的火控代码：弹道解算、任务调度、炮塔/炮管操控、UI。被装进可回收的 ALC，按 F9 卸载并重载。 |
| `IronNestFCS.CustomRecords` | **独立 Mod** | 与火控无关的场景装饰，扫描自定义音频文件并替换游戏内唱片机的音轨与贴图。 |

### Logic 内部结构

```
IronNestFCS.Logic/
├── FcsModule.cs          # 程序集入口，组装 FSC + FcsWindow + TacticalRadar，实现 IFcsModule
├── FSC.cs                # 火控核心：任务调度、协程编排、炮塔预约、两把协程锁
├── FcsWindow.cs          # IMGUI 状态面板（任务进度、队列、弹道参数）
├── TacticalRadar.cs      # 战术雷达：扫描敌方实体、敌我识别、存活判定、地图标记对接
├── FcsSceneInteractor.cs # 场景交互：3D 按钮（弹种/T1-T4/AutoFire/MaxCharge）、点击注册、按键快捷键分发
├── ClickRaycaster.cs     # 自定义射线点击检测（走新 Input System，不依赖游戏交互组件）
└── FCS/
    ├── ArtilleryTask.cs      # 任务数据类（目标ID/角度/距离/弹种/进度状态）
    ├── BallisticCalculator.cs # 弹道计算器硬件抽象：拨盘、计算按钮、里程表
    ├── GunSystem.cs          # 单管炮硬件抽象：弹仓、装填、仰角杆、开火状态
    ├── Turret.cs             # 炮塔硬件抽象：方向角旋控
    ├── TriggerConsole.cs     # 击发确认台抽象：五步确认 + 左右扳机
    ├── PurchaseDeck.cs       # 采购台抽象：弹药/装药购买
    ├── MapTable.cs           # 地图桌抽象：目标标记读取、世界坐标映射
    └── CoroutineLock.cs      # 协程互斥锁（主线程协作式调度，无真正并发）
```

### 热重载关键设计

Logic 程序集从内存字节加载（不锁住磁盘 dll），装进 `isCollectible` 的 `AssemblyLoadContext`；重载时先 `Shutdown`（撤销 Harmony 补丁、停止协程、清空 IL2CPP 引用）再卸载旧 ALC，最后从磁盘重新加载新版本。

详见 `LogicReloader.cs` 与 `FSC.cs` 中的注释。关键约束：
- 不要在 Logic 中注册新的 IL2CPP 类型（同一类型进程内只能注册一次）
- 所有对 IL2CPP 对象的引用在 Shutdown 时清空
- 每次实例用独立的 Harmony 实例；Shutdown 时 `UnpatchSelf`
- 协程必须登记到 `_runningCoroutines`，卸载时 `MelonCoroutines.Stop` 全部停掉

### 任务调度与并发控制

火控任务流程走 Unity 协程（MelonCoroutines），全程在主线程协作式调度，无真正并发，因此用简单的 `CoroutineLock`（一个 `bool`）实现互斥：

- **`_turretLock`**：炮塔方向角锁，炮塔维度资源独立，任务后台抢占后一直到击发完成才归还，全程与装填/升降角并行。
- **`_deskLock`**：控制台互斥锁，保护弹道计算器、确认开关台、采购台三组全局唯一的硬件串行使用。

防死锁规则：凡同时需要两把锁处，一律"先 turret 后 desk"，无环故不死锁。

### 点击检测方案

不用 `MonoBehaviour.OnMouseDown`（需注册 IL2CPP 类型）、不用游戏自带的 `LookAtTarget`（外部硬挂不可靠）、不用 IMGUI 按钮（单 pass controlID 错位），而是自写 `ClickRaycaster`：每帧走新 Input System 读鼠标左键，从主相机射线命中已注册的 BoxCollider 后触发回调——纯逻辑、可热重载、不依赖游戏交互组件。

## 构建与安装

### 前置条件

- 已安装 **.NET 6 SDK**（见 [global.json](global.json)）。
- 游戏本体，并已为其安装 **MelonLoader**（IL2CPP）。

### 配置游戏路径

各 `.csproj` 通过 `GameDir` 属性定位游戏目录下的 MelonLoader 程序集。请把以下两个文件里的 `GameDir` 改成你本机的游戏安装路径：

- [IronNestFCS/IronNestFCS.csproj](IronNestFCS/IronNestFCS.csproj)
- [IronNestFCS.Logic/IronNestFCS.Logic.csproj](IronNestFCS.Logic/IronNestFCS.Logic.csproj)
- [IronNestFCS.CustomRecords/IronNestFCS.CustomRecords.csproj](IronNestFCS.CustomRecords/IronNestFCS.CustomRecords.csproj)

```xml
<GameDir>你的路径\IRON NEST Heavy Turret Simulator Demo</GameDir>
```

### 构建

```bash
dotnet build IronNestFCS.sln -c Release
```

各程序集的输出位置：

- **宿主 Mod**（`IronNestFCS.dll`）：放入游戏的 `Mods/` 目录，由 MelonLoader 自动加载。
- **火控逻辑**（`IronNestFCS.Logic.dll`）：输出到 `UserData/IronNestFCS/`（不放进 `Mods/`，由宿主在运行时反射加载）。
- **契约**（`IronNestFCS.Abstractions.dll`）：放入 `UserLibs/`，确保宿主与逻辑共用同一份接口。
- **自定义唱片机**（`IronNestFCS.CustomRecords.dll`）：放入 `Mods/`。将带内嵌封面的音频文件（.mp3/.wav/.flac）放入 `UserData/CustomRecords/` 即可。

> `IronNestFCS.Logic.csproj` 默认已把 `OutputPath` 指向 `$(GameDir)\UserData\IronNestFCS\`，构建即就位，改完代码进游戏按 F9 即可生效。

### 第三方依赖

| 依赖 | 用途 | 说明 |
| --- | --- | --- |
| [CSCore](https://github.com/filoe/cscore) | 音频解码 (mp3/wav/flac) | 仅 CustomRecords 使用，纯托管库 |
| [TagLibSharp](https://github.com/mono/taglib-sharp) | 音频标签/封面读取 | 仅 CustomRecords 使用，纯托管库 |

## 使用

1. 启动已安装 MelonLoader 与本 Mod 的游戏。
2. 进入包含炮塔与地图桌的关卡场景。若火控面板提示 `Waiting for scene...`，按 **F9** 在当前场景重新绑定。
3. 在控制台旁的按钮上选择弹种（默认 HE），并按需开启 `Auto Fire` 和 `Max Charge`。
4. 拖动地图上的目标标记 (1~4) 到目标位置，或等待战术雷达自动定位敌方实体。
5. 点击地图右侧的目标按钮（T1~T4）下达打击任务，Mod 会自动完成解算、装填、瞄准与击发。
6. 左上角面板实时显示两管炮的任务进度与队列情况，右上角雷达显示存活/已摧毁目标。
7. 按 `Numpad 0` 可一键扫荡所有雷达探测到的存活目标。

### 开发热重载

修改 `IronNestFCS.Logic` 内的代码后，重新构建该项目（dll 会直接输出到游戏的 `UserData/IronNestFCS/`），切回游戏按 **F9** 即可加载新逻辑，无需重启游戏。

## 贡献

欢迎提交 Issue 和 Pull Request。

- 发现 Bug、有功能建议或疑问，请[提交 Issue](../../issues)。
- 改进代码请[提交 Pull Request](../../pulls)。改动火控逻辑时请留意 `FSC.cs` 中关于热重载与协程的约定（不要在 Logic 中注册新的 IL2CPP 类型、协程必须登记以便卸载时停止、跨 ALC 只能传递 `IFcsModule`）。

## 许可

本项目基于 MIT 许可证开源。详见 [LICENSE](LICENSE)。

## 免责声明

本项目为非官方的第三方 Mod，与游戏开发商无关。仅供学习与单机娱乐使用，使用风险自负。
