# GameCompare

一个读取本地 Playnite 游戏库、通过两两比较形成个人游戏排名的 Windows 桌面应用。

## 1. 产品目标

本程序解决两个核心问题：

1. 从 Playnite 中导入已经标记为 `Completed` 的游戏，形成个人已通关游戏列表。
2. 不要求用户直接给游戏打分，而是反复展示两款游戏，让用户选择更喜欢的一款，再根据比较记录计算个人排名。
3. 将每款游戏关联到 Obsidian Markdown 笔记，用熟悉的工具记录玩后感。

程序采用本地优先设计：游戏资料、比较记录和排名全部保存在本机，不依赖云端服务，也不修改 Playnite 数据。

## 2. 当前确认的信息

- Playnite 数据目录：`C:\Users\ray\AppData\Local\Playnite`
- Playnite 版本：`10.35.0.16422`
- Playnite 数据库格式版本：`4`
- Playnite 使用 LiteDB 4.x 保存游戏库数据
- 当前游戏总数：853
- 当前标记为 `Completed` 的游戏数：118
- `Completed` 与 `Beaten` 是两个独立状态；当前 `Beaten` 数量为 0
- 不需要实时监听 Playnite；由用户点击“从 Playnite 同步”按钮进行手动同步
- 第一版只读取 Playnite，绝不向 Playnite 数据库写入数据

以上数量只是开发时的基准数据，程序不能写死这些数值。

## 3. 第一版 MVP 范围

### 3.1 Playnite 手动同步

用户点击同步按钮后，程序应：

1. 自动检测或读取设置中的 Playnite 数据目录。
2. 检查 Playnite 是否正在运行。
3. 如果 Playnite 正在运行，提示用户关闭后重试，不强制结束进程。
4. 打开 `completionstatuses.db`，根据名称查找 `Completed` 状态 ID。
5. 打开 `games.db`，筛选 `CompletionStatusId` 对应的游戏。
6. 从其他数据库解析关联信息，例如：
   - `companies.db`：开发商和发行商
   - `platforms.db`：平台
   - `genres.db`：类型
   - `sources.db`：游戏来源
7. 从 `library\files` 解析封面文件。
8. 按 Playnite 游戏 ID 将数据更新到本程序自己的 SQLite 数据库。
9. 显示本次新增、更新、移出 `Completed` 列表和失败的数量。

同步时至少导入以下字段：

- Playnite 游戏 ID
- 游戏名称
- 排序名称
- 发行日期
- 开发商
- 发行商
- 平台
- 类型
- 来源
- 封面路径或封面缓存路径
- 游玩时间
- 最后游玩时间
- Playnite 用户评分（仅作为元数据，不参与本程序初始排名）
- 是否仍属于本次同步的 `Completed` 列表
- 最后同步时间

如果某款游戏以后不再是 `Completed`，不要删除它的历史比较记录；应将其标记为不在当前活动列表中。

### 3.2 游戏列表

游戏库页面采用“卡片化的单列列表”，而不是单纯封面墙。

每行至少展示：

- 小尺寸封面
- 游戏名称
- 发行年份或日期
- 开发商
- `Completed` 状态
- 已参与比较的次数
- 当前暂定排名（已有排名时）

首版提供：

- 按名称或开发商搜索
- 按名称、发行时间、同步时间和当前排名排序
- 空封面占位图
- 同步按钮及上次同步时间

### 3.3 两两比较

比较页面同时展示两款游戏：

- 两侧视觉权重相同
- 显示封面、名称、年份和开发商
- 用户可以选择左侧或右侧游戏
- 提供“记不清 / 跳过”选项
- 支持键盘快捷键：左、右、跳过
- 做出选择前不显示两款游戏的当前分数或排名，避免引导用户

每次选择都必须保存为一条不可丢失的原始比较记录。不要只保存最终分数。

### 3.4 排名页面

排名页面至少展示：

- 当前名次
- 游戏封面和名称
- 比较次数
- 排名是否仍不稳定
- 继续比较入口

当前使用自适应 Elo v2 形成即时排名。所有原始选择仍单独保存，程序启动时会用当前算法重新回放历史，因此以后仍可无损切换到 Bradley–Terry、TrueSkill 或其他算法。

### 3.5 Obsidian 玩后感

设置页可以选择一个已有的 Obsidian Vault，以及 Vault 内的游戏笔记子目录，默认使用
`GameCompare/Games`。游戏库中的每张卡片都提供“写玩后感”或“打开玩后感”按钮。

首次点击时，程序会：

1. 在指定子目录创建 UTF-8 Markdown 文件。
2. 写入 Playnite ID、名称、发行日期、开发商等 YAML 属性，以及基础玩后感模板。
3. 将 Vault 内的相对路径保存到本程序数据库。
4. 通过 `obsidian://open?path=...` 交给 Obsidian 打开。

已有 Markdown 文件永远不会被覆盖。如果笔记在配置的游戏笔记目录内被移动，程序会通过
`playnite_id` 属性重新发现并更新关联路径。笔记目录必须是 Vault 内的相对路径，不能通过
`..` 等方式写到 Vault 之外。

## 4. 暂不纳入第一版

- 不做 Playnite 实时监听
- 不向 Playnite 写入排名或评分
- 不开发内置 Markdown 编辑器
- 玩后感正文继续完全由 Obsidian 管理
- 不实现 Obsidian 插件、双向实时同步或附件管理
- 不开发手机端或云同步
- 不在第一版追求完整复刻 Obsidian、Steam 或 Playnite 的界面
- 不在第一阶段确定最终排名算法

游戏表中的可空 `NotePath` 字段保存 Vault 内的相对路径；Playnite 再同步不会清除这个关联。

## 5. 推荐技术方案

### 应用类型

Windows 本地桌面应用，界面采用网页式布局，但不做成依赖浏览器运行的纯网页。

### 技术栈

- 语言：C#
- 运行时：.NET 10
- 桌面宿主：WPF
- UI：Blazor Hybrid、Razor、HTML、CSS
- 本程序数据库：SQLite
- 数据访问：EF Core SQLite 或 Microsoft.Data.Sqlite
- Markdown：首版不使用；未来可使用 CodeMirror 6 + Markdig
- 测试：xUnit

选择该方案的原因：

- Playnite 和它的 SDK/LiteDB 都属于 .NET 生态，C# 集成最直接。
- Blazor Hybrid 可以使用适合卡片和双选界面的 HTML/CSS，同时保留完整本机文件访问能力。
- SQLite 不需要独立数据库服务，适合单用户本地应用。
- 相比 Electron，不需要引入明显偏重的运行环境。
- 相比 Tauri，不需要同时维护 TypeScript、Rust 和 .NET 数据读取代码。

## 6. 建议的代码结构

```text
GameCompare/
├── src/
│   ├── GameCompare.App/                 # WPF + Blazor Hybrid UI
│   ├── GameCompare.Core/                # 领域模型、排名接口、业务规则
│   ├── GameCompare.Playnite/            # Playnite 只读导入器
│   └── GameCompare.Persistence/         # SQLite、迁移和仓储
├── tests/
│   ├── GameCompare.Core.Tests/
│   └── GameCompare.Playnite.Tests/
├── README.md
└── GameCompare.sln
```

Playnite 读取逻辑必须与 UI 分离，这样可以使用复制出来的测试数据库进行测试，而不必依赖正在使用的真实游戏库。

## 7. 初步数据模型

### Game

- `Id`：本程序内部 ID
- `PlayniteId`：Playnite GUID，唯一索引
- `Name`
- `SortingName`
- `ReleaseDate`
- `Developers`
- `Publishers`
- `Platforms`
- `Genres`
- `Source`
- `CoverSourcePath`
- `CoverCachePath`
- `PlaytimeSeconds`
- `LastActivityAt`
- `PlayniteUserScore`
- `IsActiveCompleted`
- `LastSyncedAt`
- `NotePath`：为未来 Obsidian 集成预留，可空

开发商、平台和类型首版可以存为 JSON 数组；只有在查询需求明显增加时再拆成关联表。

### ComparisonEvent

- `Id`
- `LeftGameId`
- `RightGameId`
- `Outcome`：Left / Right / Skip
- `ComparedAt`
- `AlgorithmVersion`
- `SessionId`：可选，用于统计一轮比较

`Skip` 不能当作平局或胜负参与评分。

### RatingState

- `GameId`
- `Rating`
- `Uncertainty`：算法支持时使用
- `ComparisonCount`
- `Rank`
- `AlgorithmVersion`
- `UpdatedAt`

此表属于可重新生成的缓存。真正不可丢失的数据是 `ComparisonEvent`。

### SyncRun

- `Id`
- `StartedAt`
- `FinishedAt`
- `ImportedCount`
- `UpdatedCount`
- `DeactivatedCount`
- `FailedCount`
- `ErrorMessage`

## 8. 排名与配对原则

当前 118 款游戏共有 6,903 种两两组合，不应要求用户比较全部组合。

当前评分算法版本为 `elo-v2-adaptive-k`：

- 所有游戏初始分为 1500。
- Elo 预期胜率让正常结果产生较小变化、爆冷产生较大变化。
- 每个游戏使用独立动态 K 值：`K(n) = 16 + 32 / sqrt(n + 1)`。
- 新游戏的 K 值接近 48，可以更快找到位置；比较增多后 K 值逐渐接近 16，减少成熟排名抖动。
- 跳过会保留为原始记录，但不进入评分计算。
- 每次启动、同步或有效选择后，都能根据全部有效 `ComparisonEvent` 重建排名。

当前配对策略会评估全部候选组合，然后按三种模式主动采样：

- 60% 精排：优先预期胜率接近 50%，且不确定度较高的组合。
- 25% 覆盖：优先尚未充分参与比较的新游戏或低曝光游戏。
- 15% 校准：安排少量跨分数区间组合，避免局部排名形成彼此隔离的小圈子。
- 如果还有替代项，不重复最近 50 次出现过的同一组合。
- 如果游戏数量允许，最近两轮出现过的游戏会休息。
- 从得分最高的一小组候选中加权随机抽取，并随机交换左右位置，避免配对变得机械可预测。

后续仍可评估 Bradley–Terry、TrueSkill 或基于信息增益的贝叶斯模型。原始比较历史不依赖当前 Elo 实现。

排名算法和“下一组游戏如何选择”应定义成两个独立接口：

```csharp
public interface IRankingAlgorithm
{
    RankingResult Recalculate(IEnumerable<ComparisonEvent> events);
}

public interface IPairSelectionStrategy
{
    GamePair SelectNext(PairSelectionContext context);
}
```

## 9. 运行当前版本

前置条件：Windows 10 1809 或更高版本、.NET 10 Desktop Runtime，以及 Microsoft Edge WebView2 Runtime。

在项目目录执行：

```powershell
dotnet restore GameCompare.sln
dotnet run --project src\GameCompare.App\GameCompare.App.csproj
```

也可以在生成后直接运行：

```text
src\GameCompare.App\bin\Debug\net10.0-windows10.0.17763.0\GameCompare.App.exe
```

本次已经生成的 Release 版本位于：

```text
artifacts\GameCompare\GameCompare.App.exe
```

程序自己的数据库和设置位于 `%LocalAppData%\GameCompare`。首次同步前应完全关闭 Playnite。
Playnite 10 的 LiteDB 4 兼容性与安全边界记录在 [SECURITY.md](SECURITY.md)。

要启用玩后感，请先在“设置 → Obsidian 玩后感”中选择已有 Vault 的根目录。程序不会在安装目录
创建 Vault，也不会修改已有笔记正文。Windows 上应至少启动过一次 Obsidian，以便系统注册
`obsidian://` 协议。

## 10. 开发阶段与任务清单

### 阶段 0：项目骨架

- [x] 创建 .NET 10 解决方案和项目结构
- [x] 建立 WPF + Blazor Hybrid 主窗口
- [x] 建立依赖注入、日志和配置系统
- [x] 建立 SQLite 数据库和首版数据库结构
- [x] 创建基础导航：游戏库、偏好比较、我的排名、设置
- [x] 加入基础单元测试项目

### 阶段 1：Playnite 只读同步

- [x] 自动检测默认 Playnite 路径
- [x] 支持在设置中手动选择 Playnite 路径
- [x] 检测 Playnite 进程和数据库占用
- [x] 读取完成状态并动态找到 `Completed` ID
- [x] 读取游戏和关联元数据
- [x] 解析并显示封面
- [x] 将游戏数据写入本程序 SQLite
- [x] 正确处理新增、更新和移出 Completed 的游戏
- [x] 展示同步结果与错误信息
- [x] 使用数据库副本编写导入测试

### 阶段 2：游戏列表

- [x] 实现卡片化自适应列表
- [x] 实现搜索
- [x] 实现排序
- [x] 实现空封面和损坏封面的降级显示
- [x] 显示同步时间、比较次数和暂定排名
- [x] 验证当前 118 款 Completed 游戏能够全部导入

### 阶段 3：比较与基础排名

- [x] 实现下一组游戏选择器
- [x] 实现左右选择和跳过
- [ ] 实现键盘快捷键
- [x] 持久化每条 ComparisonEvent
- [x] 实现基础 Elo 算法
- [x] 实现排名重建
- [x] 实现排名页面
- [x] 重启程序后验证比较记录和排名不会丢失

### 阶段 4：配对质量与体验优化

- [x] 避免近期重复组合
- [x] 平衡不同游戏的曝光次数
- [x] 优先安排排名接近或不确定的游戏
- [x] 为新同步游戏安排校准比较
- [x] 升级为 60% 精排、25% 覆盖、15% 跨区校准的主动采样
- [x] 避免最近两轮连续出现同一款游戏
- [x] 使用自适应 K 值让新游戏快速定位、成熟游戏减少波动
- [x] 启动时自动用当前算法重建全部历史排名
- [x] 增加比较进度和排名稳定性提示
- [ ] 增加数据库备份与恢复
- [x] 完善首版异常处理和日志
- [ ] 发布打包

### 阶段 5：Obsidian 玩后感

- [x] 支持选择已有 Vault 和笔记子目录
- [x] 从游戏卡片创建或打开 Markdown 玩后感
- [x] 使用 Playnite ID 生成稳定关联并处理重名游戏
- [x] 防止路径越出 Vault
- [x] 保证已有笔记正文不被覆盖
- [x] 笔记移动后通过 YAML 中的 Playnite ID 重新发现
- [x] 使用 Obsidian URI 打开笔记
- [ ] 可选封面复制和自定义笔记模板

## 11. MVP 完成标准

满足以下条件即可认为第一版可用：

1. 能在 Windows 上直接启动。
2. 能从当前 Playnite 中只读导入全部 118 款 `Completed` 游戏。
3. 游戏名称、封面、发行日期和开发商能够正确展示。
4. 能连续进行左右二选一或跳过。
5. 每次有效选择都会持久保存。
6. 能根据选择生成可查看的暂定排名。
7. 关闭并重新打开程序后，游戏列表、比较记录和排名仍然存在。
8. Playnite 原始文件没有被修改。
9. 同步失败时能够给出清楚且可操作的错误提示。
10. 配置 Vault 后可以创建并再次打开玩后感，且用户正文不会被程序覆盖。

## 12. 后续再决定的问题

- 最终采用 Elo、Bradley–Terry、TrueSkill，还是组合方案
- 是否允许“平局”，目前首版只设计“跳过”
- 是否显示精确评分，还是只显示排名和稳定程度
- 游戏移出 `Completed` 后是否默认从排名页隐藏
- 是否加入按系列、平台和类型分别排名
- 是否加入撤销上一条比较
- 是否支持导出排名图片、CSV 或 Markdown
- 是否允许自定义 Obsidian 笔记模板和封面附件目录

## 13. 参考资料

- [Playnite：Working with game library](https://api.playnite.link/docs/tutorials/extensions/library.html)
- [Playnite：Game 数据模型](https://api.playnite.link/docs/api/Playnite.SDK.Models.Game.html)
- [Microsoft：Blazor Hybrid](https://learn.microsoft.com/aspnet/core/blazor/hybrid/?view=aspnetcore-10.0)
- [Microsoft：EF Core SQLite Provider](https://learn.microsoft.com/ef/core/providers/sqlite/)
- [Obsidian URI（后续功能）](https://help.obsidian.md/Extending%2BObsidian/Obsidian%2BURI)
