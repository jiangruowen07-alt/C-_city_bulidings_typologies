# CITY LAB v1.5

城市经济博弈模拟器。通过 Agent 之间的位置交换与对城市吸引子的偏好，模拟城市空间的自组织演化。

## 运行

```bash
python main.py
```

**依赖**：Python 3.x，仅使用标准库（tkinter）。

## 功能概览

- **Agent 类型**：居民(Resi)、企业(Firm)、商店(Shop)、咖啡馆(Cafe)、酒店(Hotel)、餐厅(Restaurant)、诊所(Clinic)
- **城市吸引子**：交通(T)、公园(P)、道路(R)、水岸(W)、学校(S)、医疗(H)、政府(G)
- **效用计算**：基于曼哈顿距离的加权求和 `Σ (偏好权重 / 距离)`
- **交换规则**：Pareto、Greedy Total、Greedy Both、Greedy 1-sided
- **预设布局**：Grid、Radial、Organic、Linear、Polycentric、Superblock、Hybrid

## 项目结构

| 文件 | 说明 |
|------|------|
| `main.py` | 入口，启动 40×40 网格与 6 格影响半径 |
| `model.py` | 模拟逻辑：Agent、效用、交换、距离场、布局生成 |
| `ui.py` | Tkinter 界面：画布、工具、偏好矩阵、图表 |
| `config.py` | 吸引子定义、Agent 偏好、颜色、布局名、交换规则选项 |

## 配置

在 `config.py` 中可修改：

- `ATTR_DEFS`：吸引子键、显示名、颜色
- `DEFAULT_PREF_ATTR`：各 Agent 对吸引子的偏好
- `DEFAULT_PREF_AGENT`：各 Agent 之间的邻近偏好
- `SWAP_RULE_OPTIONS`：可选的交换规则
- `LAYOUT_NAMES`：预设布局列表

## 界面操作

- **工具**：VIEW、ROAD、PARK、TRANS、WATER、SCHOOL、HEALTH、GOV，用于在网格上放置吸引子
- **视图**：公/私四档视图、单元贡献视图
- **运行**：单步 / 连续运行 / 批量运行
- **偏好矩阵**：可编辑 Agent 对吸引子及对其他 Agent 的偏好
