# CITY LAB v1.5

城市经济博弈模拟器。通过 Agent 之间的位置交换与对城市吸引子的偏好，模拟城市空间的自组织演化。

*Urban economic game simulator. Simulates self-organizing evolution of urban space through agent position swaps and preferences for city attractors.*

---

## 运行 / Run

```bash
pip install -r requirements.txt
python main.py
```

**依赖 Dependencies**：Python 3.x、tkinter、Pillow（用于导出 PNG / for PNG export）

---

## 功能概览 / Features

| 中文 | English |
|------|---------|
| **Agent 类型**：居民、企业、商店、咖啡馆、酒店、餐厅、诊所 | **Agent types**: Residential, Firm, Shop, Cafe, Hotel, Restaurant, Clinic |
| **城市吸引子**：交通(T)、公园(P)、道路(R)、水岸(W)、学校(S)、医疗(H)、政府(G) | **Attractors**: Transport, Park, Road, Waterfront, School, Healthcare, Government |
| **效用计算**：基于曼哈顿距离的加权求和 `Σ (偏好权重 / 距离)` | **Utility**: Weighted sum by Manhattan distance `Σ (weight / distance)` |
| **交换规则**：Pareto、Greedy Total、Greedy Both、Greedy 1-sided | **Swap rules**: Pareto, Greedy Total, Greedy Both, Greedy 1-sided |
| **预设布局**：Grid、Radial、Organic、Linear、Polycentric、Superblock、Hybrid | **Layouts**: Grid, Radial, Organic, Linear, Polycentric, Superblock, Hybrid |
| **道路拓扑**：From Layout、Linear、Parallel、Cross、T-Junction、Loop | **Road topology**: From Layout, Linear, Parallel, Cross, T-Junction, Loop |

---

## 项目结构 / Project Structure

| 文件 File | 说明 Description |
|-----------|-----------------|
| `main.py` | 入口，启动 40×40 网格与 6 格影响半径 / Entry point, 40×40 grid, reach=6 |
| `model.py` | 模拟逻辑：Agent、效用、交换、距离场、布局生成 / Simulation: agents, utility, swaps, distance fields, layouts |
| `ui.py` | Tkinter 界面：画布、工具、偏好矩阵、图表、导出 / UI: canvas, tools, preference matrices, chart, export |
| `config.py` | 吸引子定义、Agent 偏好、颜色、布局名、交换规则、道路拓扑 / Attractors, preferences, colors, layouts, swap rules, road topology |
| `requirements.txt` | 依赖：Pillow / Dependencies: Pillow |

---

## 配置 / Configuration

在 `config.py` 中可修改 / Editable in `config.py`:

| 变量 Variable | 中文 | English |
|---------------|------|---------|
| `ATTR_DEFS` | 吸引子键、显示名、颜色 | Attractor keys, display names, colors |
| `DEFAULT_PREF_ATTR` | 各 Agent 对吸引子的偏好 | Agent → attractor preferences |
| `DEFAULT_PREF_AGENT` | 各 Agent 之间的邻近偏好 | Agent → agent proximity preferences |
| `SWAP_RULE_OPTIONS` | 可选的交换规则 | Swap rule options |
| `LAYOUT_NAMES` | 预设布局列表 | Layout presets |
| `ROAD_TOPOLOGY_NAMES` | 道路拓扑预设 | Road topology presets |

---

## 界面操作 / UI Controls

| 功能 Feature | 中文 | English |
|--------------|------|---------|
| **工具** | VIEW、ROAD、PARK、TRANS、WATER、SCHOOL、HEALTH、GOV，用于在网格上放置吸引子 | Tools to place attractors on the grid |
| **视图** | 公/私四档视图、单元贡献视图 | Public/private 4-bin view, cell contribution view |
| **运行** | 单步 / 连续运行 / 批量运行（RUN N STEPS + GO） | Step / continuous / batch run (RUN N STEPS + GO) |
| **偏好矩阵** | 可编辑 Agent 对吸引子、对其他 Agent 的偏好，以及各类型的 Reach（曼哈顿距离） | Editable Agent→Attractor, Agent→Agent preferences, and Reach per type |
| **导出** | EXPORT PNG 将当前城市场地导出为 PNG 图片（程序化渲染，支持普通视图与公私有/贡献度视图） | Export current city as PNG (programmatic render, supports all view modes) |
| **Reach 叠加** | 悬停 Agent 时显示其影响半径（实线=对 Agent，虚线=对吸引子） | Hover over agent to show influence radius (solid=agents, dashed=attractors) |
