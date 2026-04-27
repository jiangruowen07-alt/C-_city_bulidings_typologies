# CITY LAB v1.5

城市经济博弈模拟器。通过 Agent 之间的位置交换与对城市吸引子的偏好，模拟城市空间的自组织演化。

*Urban economic game simulator. Simulates self-organizing evolution of urban space through agent position swaps and preferences for city attractors.*

---

## 运行 / Run

需安装 [.NET 8 SDK](https://dotnet.microsoft.com/download)。

```bash
cd CityLab
dotnet run
```

或发布为可执行文件后运行 `CityLab.exe`。

**依赖 Dependencies**：Windows（WinForms）、.NET 8

---

## 功能概览 / Features

| 中文 | English |
|------|---------|
| **Agent 类型**：居民、企业、商店、咖啡馆、酒店、餐厅、诊所 | **Agent types**: Residential, Firm, Shop, Cafe, Hotel, Restaurant, Clinic |
| **城市吸引子**：交通(T)、公园(P)、道路(R)、水岸(W)、学校(S)、医疗(H)、政府(G) | **Attractors**: Transport, Park, Road, Waterfront, School, Healthcare, Government |
| **效用计算**：基于影响范围的加权求和，每个代理/吸引子有影响范围，代理的满意度 = 能影响到它的源（代理+吸引子）的贡献之和 | **Utility**: Influence-based weighted sum; each agent/attractor has influence range; agent satisfaction = sum of contributions from sources that can influence it |
| **交换规则**：Pareto、Greedy Total、Greedy Both、Greedy 1-sided | **Swap rules**: Pareto, Greedy Total, Greedy Both, Greedy 1-sided |
| **预设布局**：Grid、Radial、Organic、Linear、Polycentric、Superblock、Hybrid | **Layouts**: Grid, Radial, Organic, Linear, Polycentric, Superblock, Hybrid |
| **道路拓扑**：From Layout、Linear、Parallel、Cross、T-Junction、Loop | **Road topology**: From Layout, Linear, Parallel, Cross, T-Junction, Loop |

---

## 项目结构 / Project Structure

| 路径 Path | 说明 Description |
|-----------|-----------------|
| `CityLab/Program.cs` | 程序入口 / Application entry |
| `CityLab/CityModel.cs` | 模拟逻辑 / Simulation: agents, utility, swaps, distance fields, layouts |
| `CityLab/CityAppForm*.cs`、`CityGridView.cs` | WinForms 界面与画布 / UI and grid canvas |
| `CityLab/Config.cs` | 吸引子、偏好、颜色、布局、交换规则、道路拓扑 / Config |

---

## 配置 / Configuration

在 `CityLab/Config.cs` 中可修改 / Editable in `CityLab/Config.cs`:

| 符号 Symbol | 中文 | English |
|---------------|------|---------|
| `AttrDefs` | 吸引子键、显示名、颜色 | Attractor keys, names, colors |
| `DefaultPrefAttr` | 各 Agent 对吸引子的偏好 | Agent → attractor preferences |
| `DefaultPrefAgent` | 各 Agent 之间的邻近偏好 | Agent → agent proximity preferences |
| `SwapRuleOptions` | 可选的交换规则 | Swap rule options |
| `LayoutNames` | 预设布局列表 | Layout presets |
| `RoadTopologyNames` | 道路拓扑预设 | Road topology presets |

---

## 界面操作 / UI Controls

| 功能 Feature | 中文 | English |
|--------------|------|---------|
| **工具** | VIEW、ROAD、PARK、TRANS、WATER、SCHOOL、HEALTH、GOV，用于在网格上放置吸引子 | Tools to place attractors on the grid |
| **视图** | 公/私四档视图、单元满意度视图 | Public/private 4-bin view, cell satisfaction view |
| **运行** | 单步 / 连续运行 / 批量运行（RUN N + GO） | Step / continuous / batch run (RUN N + GO) |
| **偏好矩阵** | 可编辑 Agent 对吸引子、对其他 Agent 的偏好，以及影响范围矩阵（吸引子→代理、代理→代理） | Editable Agent→Attractor, Agent→Agent preferences, and influence range matrices (attr→agent, agent→agent) |
| **导出** | EXPORT PNG 将当前城市场地导出为 PNG 图片 | Export current city as PNG |
| **影响范围叠加** | 悬停 Agent 时显示能影响到它的源的最大范围 | Hover over agent to show max influence range from sources |
