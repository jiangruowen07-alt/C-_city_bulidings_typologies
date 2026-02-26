# CITY LAB v1.5 - Simulation Model
# Agent, CityModel: utility, swap rules, layouts, distance fields

import random
import math
from dataclasses import dataclass
from collections import deque

from config import (
    ATTR_DEFS,
    TYPE_LABELS,
    DEFAULT_PREF_ATTR,
    DEFAULT_PREF_AGENT,
    ROAD_TOPOLOGY_NAMES,
)


@dataclass
class Agent:
    type: str
    x: int
    y: int


class CityModel:
    """
    Utility = Σ (Weight / ManhattanDistance) for attractors + nearby agents.
    Swap rules: pareto, greedy_total, greedy_both, greedy_1.
    """
    def __init__(self, w=40, h=40, reach=6):
        self.w, self.h, self.reach = w, h, reach

        self.attr_keys = [k for (k, _name, _col) in ATTR_DEFS]
        self.attractors = {k: [] for k in self.attr_keys}
        self.type_labels = list(TYPE_LABELS)

        self.agents = []
        self.grid = [[None for _ in range(h)] for _ in range(w)]
        self.stats = {
            "steps": 0,
            "rawUtility": 0.0,
            "totalUtility": 0.0,
            "accepted": 0,
            "rejected": 0,
            "sacrificed": 0,
        }
        self.utility_bias = 0.0
        self.swap_mode = "pareto"
        self.road_topology = ROAD_TOPOLOGY_NAMES[0]  # default: Linear

        self.reach_agent_by_type = {t: int(reach) for t in self.type_labels}
        self.reach_attr_by_type = {t: int(reach) for t in self.type_labels}

        # Deep copy default prefs so UI can edit in place
        self.pref_attr = {t: dict(DEFAULT_PREF_ATTR.get(t, {})) for t in self.type_labels}
        self.pref_agent = {t: dict(DEFAULT_PREF_AGENT.get(t, {})) for t in self.type_labels}
        self._ensure_pref_tables_complete()

        INF = 10**9
        self.dist_attr = {k: [[INF for _ in range(self.h)] for _ in range(self.w)] for k in self.attr_keys}
        self.attr_grid = [[None for _ in range(self.h)] for _ in range(self.w)]

    def _ensure_pref_tables_complete(self):
        for t in self.type_labels:
            self.pref_attr.setdefault(t, {})
            for k in self.attr_keys:
                self.pref_attr[t].setdefault(k, 0.0)
        for t in self.type_labels:
            self.pref_agent.setdefault(t, {})
            for tt in self.type_labels:
                self.pref_agent[t].setdefault(tt, 0.0)
        for t in self.type_labels:
            for table_name in ("reach_agent_by_type", "reach_attr_by_type"):
                table = getattr(self, table_name, {})
                try:
                    v = int(table.get(t, self.reach))
                except Exception:
                    v = int(self.reach)
                table[t] = max(1, v)
                setattr(self, table_name, table)

    def _get_attr_draw_order(self):
        """吸引子重叠时保留数量较少的：先绘制数量多的，后绘制数量少的。"""
        counts = {k: len([p for p in self.attractors.get(k, []) if 0 <= p[0] < self.w and 0 <= p[1] < self.h])
                  for k in self.attr_keys}
        others = [k for k in self.attr_keys if k != "R"]
        others_sorted = sorted(others, key=lambda k: -counts.get(k, 0))
        return ["R"] + others_sorted

    def rebuild_attr_distance_fields(self):
        INF = 10**9
        self.attr_grid = [[None for _ in range(self.h)] for _ in range(self.w)]
        draw_order = self._get_attr_draw_order()
        for k in draw_order:
            lst = self.attractors.get(k, [])
            for (ax, ay) in lst:
                if 0 <= ax < self.w and 0 <= ay < self.h:
                    self.attr_grid[ax][ay] = k

        for k in self.attr_keys:
            dist = [[INF for _ in range(self.h)] for _ in range(self.w)]
            q = deque()
            for (ax, ay) in self.attractors.get(k, []):
                if 0 <= ax < self.w and 0 <= ay < self.h:
                    dist[ax][ay] = 0
                    q.append((ax, ay))
            while q:
                x, y = q.popleft()
                nd = dist[x][y] + 1
                if x > 0 and nd < dist[x - 1][y]:
                    dist[x - 1][y] = nd
                    q.append((x - 1, y))
                if x < self.w - 1 and nd < dist[x + 1][y]:
                    dist[x + 1][y] = nd
                    q.append((x + 1, y))
                if y > 0 and nd < dist[x][y - 1]:
                    dist[x][y - 1] = nd
                    q.append((x, y - 1))
                if y < self.h - 1 and nd < dist[x][y + 1]:
                    dist[x][y + 1] = nd
                    q.append((x, y + 1))
            self.dist_attr[k] = dist
        return self._remove_agents_on_attractors()

    def _remove_agents_on_attractors(self):
        """移除所有位于吸引子格上的代理，以保留吸引子为主。返回被移除的代理列表。"""
        removed = []
        for a in list(self.agents):
            if self.is_attr(a.x, a.y):
                self.agents.remove(a)
                removed.append(a)
        if removed:
            self.update_grid()
        return removed

    def clear(self):
        self.attractors = {k: [] for k in self.attr_keys}
        self.rebuild_attr_distance_fields()
        self.reset()


    def is_attr(self, x, y):
        return self.attr_grid[x][y] is not None

    def update_grid(self):
        self.grid = [[None for _ in range(self.h)] for _ in range(self.w)]
        for a in self.agents:
            self.grid[a.x][a.y] = a

    def _random_agent_type(self):
        r = random.random()
        if r < 0.55:
            return "Resi"
        if r < 0.70:
            return "Firm"
        if r < 0.82:
            return "Shop"
        if r < 0.90:
            return "Cafe"
        if r < 0.95:
            return "Hotel"
        if r < 0.985:
            return "Restaurant"
        return "Clinic"

    def _get_attr_cells(self):
        """从 attractors 字典直接构建吸引子格集合，确保与数据源一致。"""
        cells = set()
        for lst in self.attractors.values():
            for (ax, ay) in lst:
                if 0 <= ax < self.w and 0 <= ay < self.h:
                    cells.add((ax, ay))
        return cells

    def _reset_agents_once(self):
        attr_cells = self._get_attr_cells()
        self.agents.clear()
        slots = [(x, y) for x in range(self.w) for y in range(self.h) if (x, y) not in attr_cells]
        random.shuffle(slots)
        for x, y in slots:
            if (x, y) in attr_cells:
                continue
            self.agents.append(Agent(self._random_agent_type(), x, y))
        self.stats["steps"] = 0
        self.stats["accepted"] = 0
        self.stats["rejected"] = 0
        self.stats["sacrificed"] = 0
        self.update_grid()
        self.calc_total_utility()

    def lock_total_utility_int(self, target_int: int):
        target_int = int(target_int)
        raw = float(self.stats.get("rawUtility", 0.0))
        bias = float(target_int - int(raw))
        for _ in range(6):
            cur = int(raw + bias)
            if cur == target_int:
                break
            bias += 1.0 if cur < target_int else -1.0
        self.utility_bias = bias
        self.stats["totalUtility"] = raw + self.utility_bias

    def reset(self, target_int=None):
        self._reset_agents_once()
        if target_int is not None:
            self.lock_total_utility_int(int(target_int))

    def apply_layout(self, typ: str):
        self.attractors = {k: [] for k in self.attr_keys}
        cx, cy = self.w // 2, self.h // 2

        if typ == "Grid":
            for i in range(self.w):
                for j in range(self.h):
                    if i % 8 == 0 or j % 8 == 0:
                        self.attractors["R"].append((i, j))
            # T(交通)最多2个
            t1 = (max(1, min(self.w - 2, cx - 6)), max(1, min(self.h - 2, cy - 6)))
            t2 = (max(1, min(self.w - 2, cx + 6)), max(1, min(self.h - 2, cy + 6)))
            t3 = (max(1, min(self.w - 2, cx - 6)), max(1, min(self.h - 2, cy + 6)))
            t4 = (max(1, min(self.w - 2, cx + 6)), max(1, min(self.h - 2, cy - 6)))
            self.attractors["T"] = [t1, t2]
            # 公园、学校、医疗：原 t3,t4 转为 P、S 以补足种类
            px1, py1 = max(1, min(self.w - 2, cx - 6)), max(1, min(self.h - 2, cy - 6))
            px2, py2 = max(1, min(self.w - 2, cx + 6)), max(1, min(self.h - 2, cy + 6))
            self.attractors["P"] = [(px1, py1), (px2, py2), t3]
            self.attractors["S"] = [(max(0, cx - 8), cy), (min(self.w - 1, cx + 8), cy), t4]
            self.attractors["H"] = [(cx, max(0, cy - 8)), (cx, min(self.h - 1, cy + 8))]
            self.attractors["W"] = [(i, self.h - 1) for i in range(self.w)]
            self.attractors["G"] = [(cx, cy), (cx, max(0, cy - 10))]

        elif typ == "Radial":
            # 根据网格尺寸缩放半径，确保小网格也有完整吸引子环
            scale = min(self.w, self.h) / 40.0
            r1, r2, r3, r4, r5 = 8 * scale, 10 * scale, 14 * scale, 16 * scale, 18 * scale
            tol = max(0.5, 0.6 * scale)
            for i in range(self.w):
                for j in range(self.h):
                    d = math.hypot(i - cx, j - cy)
                    if abs(d - r1) < tol or abs(d - r4) < tol or abs(i - cx) < 0.6 or abs(j - cy) < 0.6:
                        self.attractors["R"].append((i, j))
                    if d < max(2, 3 * scale):
                        self.attractors["P"].append((i, j))
                    if abs(d - r2) < tol:
                        self.attractors["S"].append((i, j))
                    if abs(d - r3) < tol:
                        self.attractors["H"].append((i, j))
                    if abs(d - r5) < tol:
                        self.attractors["W"].append((i, j))
            # T(交通)最多2个：中心 + 一角；其余转为 G、S、H
            self.attractors["T"] = [(cx, cy), (0, 0)]
            self.attractors["G"] = [(cx, max(0, cy - 1)), (cx, min(self.h - 1, cy + 1)),
                                    (self.w - 1, self.h - 1), (0, self.h - 1)]
            self.attractors["S"] = self.attractors.get("S", []) + [(self.w - 1, 0)]

        elif typ == "Organic":
            for _ in range(10):
                x, y = random.randrange(self.w), random.randrange(self.h)
                for _ in range(18):
                    self.attractors["R"].append((x, y))
                    x = max(0, min(self.w - 1, x + random.randrange(3) - 1))
                    y = max(0, min(self.h - 1, y + random.randrange(3) - 1))
                self.attractors["P"].append((x, y))
            for _ in range(6):
                self.attractors["S"].append((random.randrange(self.w), random.randrange(self.h)))
            for _ in range(4):
                self.attractors["H"].append((random.randrange(self.w), random.randrange(self.h)))
            for _ in range(2):
                self.attractors["T"].append((random.randrange(self.w), random.randrange(self.h)))
            for _ in range(3):
                self.attractors["G"].append((random.randrange(self.w), random.randrange(self.h)))
            x, y = random.randrange(self.w), self.h - 2
            for _ in range(self.w * 2):
                self.attractors["W"].append((x, y))
                x = max(0, min(self.w - 1, x + random.choice([-1, 0, 1])))
                y = max(self.h // 2, min(self.h - 1, y + random.choice([-1, 0, 1])))

        elif typ == "Linear":
            for i in range(self.w):
                for yy in (cy - 1, cy + 1):
                    if 0 <= yy < self.h:
                        self.attractors["R"].append((i, yy))
                for yy in (cy - 5, cy + 5):
                    if 0 <= yy < self.h:
                        self.attractors["P"].append((i, yy))
                sy = cy - 8 if 0 <= cy - 8 < self.h else max(0, cy - 2)
                hy = cy + 8 if 0 <= cy + 8 < self.h else min(self.h - 1, cy + 2)
                if i % 12 == 0:
                    self.attractors["S"].append((i, sy))
                if i % 12 == 6:
                    self.attractors["H"].append((i, hy))
            self.attractors["T"] = [(0, cy), (self.w - 1, cy)]
            self.attractors["W"] = [(i, 0) for i in range(self.w)]
            self.attractors["G"] = [(self.w // 4, cy), (self.w // 2, cy), (3 * self.w // 4, cy)]

        elif typ == "Polycentric":
            # 根据网格尺寸缩放中心点，确保小网格也有有效吸引子
            base_hubs = [(10, 10), (30, 10), (10, 30), (30, 30), (20, 20)]
            hubs = [(max(1, min(self.w - 2, int(hx * self.w / 40))),
                     max(1, min(self.h - 2, int(hy * self.h / 40)))) for hx, hy in base_hubs]
            # T(交通)最多2个：对角两角；G 需有独立位置避免被 S/H 覆盖
            self.attractors["T"] = [(0, 0), (self.w - 1, self.h - 1)]
            self.attractors["G"] = [(0, cy), (self.w - 1, cy)]
            for idx, (hx, hy) in enumerate(hubs):
                self.attractors["G"].append((hx, hy))
                for dx in range(-3, 4):
                    for xx, yy in [(hx + dx, hy - 3), (hx + dx, hy + 3), (hx - 3, hy + dx), (hx + 3, hy + dx)]:
                        if 0 <= xx < self.w and 0 <= yy < self.h:
                            self.attractors["R"].append((xx, yy))
                for xx, yy in [(hx, hy + 1), (hx, hy - 1)]:
                    if 0 <= xx < self.w and 0 <= yy < self.h:
                        self.attractors["P"].append((xx, yy))
                if idx % 2 == 0:
                    self.attractors["S"].append((hx, hy))
                else:
                    self.attractors["H"].append((hx, hy))
            self.attractors["W"] = [(i, self.h - 1) for i in range(self.w)]

        elif typ == "Superblock":
            for i in range(self.w):
                for j in range(self.h):
                    if i % 14 == 0 or j % 14 == 0:
                        self.attractors["R"].append((i, j))
                    if (i + 7) % 14 == 0 and (j + 7) % 14 == 0:
                        for di in (-1, 0, 1):
                            for dj in (-1, 0, 1):
                                xx, yy = i + di, j + dj
                                if 0 <= xx < self.w and 0 <= yy < self.h:
                                    self.attractors["P"].append((xx, yy))
            for i in range(7, self.w, 14):
                for j in range(7, self.h, 14):
                    self.attractors["S"].append((i, j))
            # S 需有独立位置避免被 T/H/G 覆盖（街区中心外）
            sx1 = max(1, min(self.w - 2, cx - 7))
            sy1 = max(1, min(self.h - 2, cy - 7))
            self.attractors["S"].extend([(sx1, cy), (cx, sy1)])
            for i in range(7, self.w, 28):
                for j in range(7, self.h, 28):
                    self.attractors["H"].append((i + 3 if i + 3 < self.w else i, j))
            # T(交通)最多2个，街区中心取前2个；其余转为 G、H 补足
            block_centers = [(i, j) for i in range(7, self.w, 14) for j in range(7, self.h, 14)
                            if 0 <= i < self.w and 0 <= j < self.h]
            self.attractors["T"] = block_centers[:2] if len(block_centers) >= 2 else block_centers + [(cx, cy)]
            self.attractors["W"] = [(self.w - 1, j) for j in range(self.h)]
            self.attractors["G"] = [(cx, cy), (cx + 1 if cx + 1 < self.w else cx, cy)]
            for pos in block_centers[2:4]:
                self.attractors["G"].append(pos)
            for pos in block_centers[4:]:
                self.attractors["H"].append(pos)

        elif typ == "Hybrid":
            for i in range(self.w):
                for j in range(self.h):
                    if i < cx:
                        if random.random() < 0.05:
                            self.attractors["R"].append((i, j))
                    else:
                        if i % 6 == 0 or j % 6 == 0:
                            self.attractors["R"].append((i, j))
            self.attractors["T"] = [(cx, cy), (self.w - 1, cy)]
            self.attractors["P"] = [(i, i) for i in range(min(self.w, self.h)) if i % 2 == 0]
            self.attractors["S"] = [(max(0, cx - 10), max(0, cy - 6)), (min(self.w - 1, cx + 6), min(self.h - 1, cy + 10))]
            self.attractors["H"] = [(max(0, cx - 6), min(self.h - 1, cy + 10)), (min(self.w - 1, cx + 10), max(0, cy - 6))]
            self.attractors["W"] = [(0, j) for j in range(self.h)]
            self.attractors["G"] = [(cx, cy), (min(self.w - 1, cx + 12), cy)]

        self.apply_road_topology(self.road_topology)
        self._ensure_all_attractors_present()
        self.rebuild_attr_distance_fields()
        self.reset()

    def apply_road_topology(self, typ: str):
        """仅替换道路(R)吸引子，其它吸引子保持不变。"""
        if typ not in ROAD_TOPOLOGY_NAMES:
            return
        self.road_topology = typ
        cx, cy = self.w // 2, self.h // 2
        self.attractors["R"] = []

        if typ == "Linear":
            # Single road: horizontal center line
            for i in range(self.w):
                self.attractors["R"].append((i, cy))

        elif typ == "Parallel":
            # 3 parallel horizontal roads
            for i in range(self.w):
                for yy in (cy - self.h // 6, cy, cy + self.h // 6):
                    if 0 <= yy < self.h:
                        self.attractors["R"].append((i, yy))

        elif typ == "Cross":
            # Horizontal + vertical crossing at center
            for i in range(self.w):
                self.attractors["R"].append((i, cy))
            for j in range(self.h):
                self.attractors["R"].append((cx, j))

        elif typ == "T-Junction":
            # Horizontal top bar + vertical stem down
            for i in range(self.w):
                self.attractors["R"].append((i, cy))
            for j in range(cy, self.h):
                self.attractors["R"].append((cx, j))

        elif typ == "Loop":
            # Rectangular loop around center
            margin = max(2, min(self.w, self.h) // 6)
            x1, x2 = max(0, cx - margin), min(self.w - 1, cx + margin)
            y1, y2 = max(0, cy - margin), min(self.h - 1, cy + margin)
            for i in range(x1, x2 + 1):
                self.attractors["R"].append((i, y1))
                self.attractors["R"].append((i, y2))
            for j in range(y1 + 1, y2):
                self.attractors["R"].append((x1, j))
                self.attractors["R"].append((x2, j))

    def _ensure_all_attractors_present(self):
        """确保每种吸引子至少有一个有效位置，避免部分城市布局吸引子不全。"""
        cx, cy = self.w // 2, self.h // 2
        fallbacks = {
            "T": (cx, cy),
            "P": (max(0, cx - 1), cy),
            "R": (0, cy),
            "W": (0, self.h - 1),
            "S": (cx, max(0, cy - 1)),
            "H": (min(self.w - 1, cx + 1), cy),
            "G": (cx, min(self.h - 1, cy + 1)),
        }
        for k in self.attr_keys:
            lst = self.attractors.get(k, [])
            valid = [(x, y) for (x, y) in lst if 0 <= x < self.w and 0 <= y < self.h]
            if valid:
                self.attractors[k] = valid
            elif k in fallbacks:
                fx, fy = fallbacks[k]
                fx = max(0, min(self.w - 1, fx))
                fy = max(0, min(self.h - 1, fy))
                self.attractors[k] = [(fx, fy)]
            else:
                self.attractors[k] = [(cx, cy)]

    def get_utility(self, agent: Agent, x, y):
        pa = self.pref_attr[agent.type]
        pg = self.pref_agent[agent.type]
        u = 0.0
        reach_attr = int(self.reach_attr_by_type.get(agent.type, self.reach))
        reach_agent = int(self.reach_agent_by_type.get(agent.type, self.reach))

        for k in self.attr_keys:
            md = self.dist_attr[k][x][y]
            if md <= reach_attr:
                u += pa[k] / max(md, 1)

        r = reach_agent
        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                if dx == 0 and dy == 0:
                    continue
                nx, ny = x + dx, y + dy
                dist = abs(dx) + abs(dy)
                if dist <= r and 0 <= nx < self.w and 0 <= ny < self.h:
                    nb = self.grid[nx][ny]
                    if nb is not None:
                        u += pg[nb.type] / max(dist, 1)
        return u

    def step(self):
        self.stats["steps"] += 1
        n = len(self.agents)
        if n < 2:
            return (False, None, None, None, None, [])

        i1 = random.randrange(n)
        i2 = random.randrange(n - 1)
        if i2 >= i1:
            i2 += 1
        a1, a2 = self.agents[i1], self.agents[i2]
        if self.is_attr(a1.x, a1.y):
            self.agents.remove(a1)
            self.update_grid()
            return (False, None, None, None, None, [a1])
        if self.is_attr(a2.x, a2.y):
            self.agents.remove(a2)
            self.update_grid()
            return (False, None, None, None, None, [a2])

        u1_old = self.get_utility(a1, a1.x, a1.y)
        u2_old = self.get_utility(a2, a2.x, a2.y)
        u1_new = self.get_utility(a1, a2.x, a2.y)
        u2_new = self.get_utility(a2, a1.x, a1.y)

        mode = getattr(self, "swap_mode", "pareto")
        if mode == "pareto":
            accept = (u1_new >= u1_old and u2_new >= u2_old) and (u1_new > u1_old or u2_new > u2_old)
        elif mode == "greedy_total":
            accept = (u1_new + u2_new) > (u1_old + u2_old)
        elif mode == "greedy_both":
            accept = (u1_new > u1_old) and (u2_new > u2_old)
        elif mode == "greedy_1":
            accept = u1_new > u1_old
        else:
            accept = (u1_new >= u1_old and u2_new >= u2_old) and (u1_new > u1_old or u2_new > u2_old)

        if accept:
            if (u1_new < u1_old) or (u2_new < u2_old):
                self.stats["sacrificed"] += 1
            x1, y1 = a1.x, a1.y
            x2, y2 = a2.x, a2.y
            a1.x, a1.y = x2, y2
            a2.x, a2.y = x1, y1
            self.grid[x1][y1] = a2
            self.grid[x2][y2] = a1
            self.stats["accepted"] += 1
            return (True, a1, a2, (x1, y1), (x2, y2), [])
        self.stats["rejected"] += 1
        return (False, None, None, None, None, [])

    def calc_total_utility(self):
        raw = sum(self.get_utility(a, a.x, a.y) for a in self.agents)
        self.stats["rawUtility"] = raw
        self.stats["totalUtility"] = raw + float(self.utility_bias)
