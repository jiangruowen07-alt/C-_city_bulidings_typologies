# CITY LAB v1.5 - Tkinter UI
# CityUI: layout, canvas, views, panels, chart, events

import tkinter as tk
from tkinter import filedialog, messagebox

from PIL import Image, ImageDraw

from config import (
    ATTR_DEFS,
    AGENT_COLORS,
    UI_COLORS,
    PP_BIN_COLORS,
    PP_AGENT_RANK,
    PP_ATTR_RANK,
    LAYOUT_NAMES,
    ROAD_TOPOLOGY_NAMES,
    SWAP_RULE_OPTIONS,
    TOOLS,
    ORIGINAL_CITYLAB_ATTR_DEFS,
    ORIGINAL_CITYLAB_TYPE_LABELS,
    ORIGINAL_CITYLAB_AGENT_COLORS,
    ORIGINAL_CITYLAB_TOOLS,
    ORIGINAL_CITYLAB_PP_AGENT_RANK,
    ORIGINAL_CITYLAB_PP_ATTR_RANK,
)
from model import Agent, CityModel
from export_csharp import export_to_csharp


class CityUI:
    def __init__(self, root, model: CityModel):
        self.root, self.m = root, model
        self.running = False
        self.current_tool = "None"
        self.cell = 15

        self.colors = dict(UI_COLORS)
        self._refresh_colors()

        self.attr_item = {}
        self.agent_item = {}
        self.show_view = False
        self.view_mode = "pubpriv"
        self.zone_item = {}

        self.pp_bin_colors = dict(PP_BIN_COLORS)
        self._refresh_pp_ranks()
        self.view_agent_first = True

        self.contrib_cache = [[0.0 for _ in range(self.m.h)] for _ in range(self.m.w)]
        self.contrib_min = 0.0
        self.contrib_max = 1.0

        self.util_history = []
        self.chart_max_points = 240
        self.chart_padding = 10
        self.contrib_rebuild_interval = 30  # 降低 contrib 缓存刷新频率以减轻卡顿

        self.show_reach_overlay = True
        self._hover_cell = (-1, -1)
        self._hover_after = None
        self._reach_tag = "reach"
        self._reach_center_tag = "reach_center"
        self._contrib_tooltip = None
        self._contrib_tooltip_lbl = None

        self.swap_mode_var = tk.StringVar(value=self.m.swap_mode)
        self.reset_target_int = None

        self.run_n_var = tk.StringVar(value="2000")
        self._batch_running = False
        self._batch_prev_running = False
        self._batch_remaining = 0
        self._batch_dirty = set()
        self.batch_chunk = 1200

        self.build_layout()
        self.build_matrices()

        self.canvas.bind("<Button-1>", self.on_canvas_click)
        self.canvas.bind("<Motion>", self.on_canvas_motion)
        self.canvas.bind("<Leave>", self.on_canvas_leave)
        self.root.bind("<Configure>", lambda _e: self.resize_canvas())
        self.chart_canvas.bind("<Configure>", lambda _e: self.draw_chart())

        self.root.after(0, self.boot)
        self.loop()

    def _refresh_colors(self):
        """Update colors from current schema (standard or original city lab)."""
        self.colors.clear()
        self.colors.update(UI_COLORS)
        if self.m.use_original_citylab:
            self.colors.update(ORIGINAL_CITYLAB_AGENT_COLORS)
            for k, _name, col in ORIGINAL_CITYLAB_ATTR_DEFS:
                self.colors[k] = col
        else:
            self.colors.update(AGENT_COLORS)
            for k, _name, col in ATTR_DEFS:
                self.colors[k] = col

    def _refresh_pp_ranks(self):
        """Update public/private rank mappings for current schema."""
        if self.m.use_original_citylab:
            self.pp_agent_rank = dict(ORIGINAL_CITYLAB_PP_AGENT_RANK)
            self.pp_attr_rank = dict(ORIGINAL_CITYLAB_PP_ATTR_RANK)
        else:
            self.pp_agent_rank = dict(PP_AGENT_RANK)
            self.pp_attr_rank = dict(PP_ATTR_RANK)

    def _get_tools(self):
        """Get tools for current schema."""
        return ORIGINAL_CITYLAB_TOOLS if self.m.use_original_citylab else TOOLS

    def _rebuild_tool_buttons(self):
        """Rebuild infrastructure tool buttons for current schema."""
        for w in self.tool_frame.winfo_children():
            w.destroy()
        self.tool_buttons = {}
        tools = self._get_tools()
        for i, (label, key) in enumerate(tools):
            b = tk.Button(self.tool_frame, text=label, command=lambda k=key: self.set_tool(k),
                          bg="#ffffff" if key == "None" else UI_COLORS["panel_bg"],
                          fg="#000" if key == "None" else "white", relief="groove")
            b.grid(row=0, column=i, sticky="ew", padx=2, pady=2)
            self.tool_buttons[key] = b
            self.tool_frame.grid_columnconfigure(i, weight=1)

    def _is_canvas_under_pointer(self, e) -> bool:
        w = self.root.winfo_containing(e.x_root, e.y_root)
        return w is self.canvas

    def build_layout(self):
        self.root.title("CITY LAB v1.5 - Public/Private (4 bins) + Cell Satisfaction")
        self.root.configure(bg=UI_COLORS["panel_bg"])

        app = tk.Frame(self.root, bg=UI_COLORS["panel_bg"])
        app.pack(fill="both", expand=True, padx=20, pady=20)

        self.grid_container = tk.Frame(app, bg="#000000", highlightbackground=UI_COLORS["border"], highlightthickness=1, bd=0)
        self.grid_container.pack(side="left", fill="both", expand=True)

        self.canvas = tk.Canvas(self.grid_container, bg="#000000", highlightthickness=0)
        self.canvas.pack(expand=True)

        panel_wrap = tk.Frame(app, bg=UI_COLORS["panel_bg"], width=440)
        panel_wrap.pack(side="right", fill="y", padx=(20, 0))
        panel_wrap.pack_propagate(False)

        vsb = tk.Scrollbar(panel_wrap, orient="vertical")
        vsb.pack(side="right", fill="y")
        panel_canvas = tk.Canvas(panel_wrap, bg=UI_COLORS["panel_bg"], highlightthickness=0, yscrollcommand=vsb.set)
        panel_canvas.pack(side="left", fill="both", expand=True)
        vsb.config(command=panel_canvas.yview)

        panel = tk.Frame(panel_canvas, bg=UI_COLORS["panel_bg"])
        self.panel = panel
        panel_window = panel_canvas.create_window((0, 0), window=panel, anchor="nw")

        def _panel_on_configure(_e=None):
            panel_canvas.configure(scrollregion=panel_canvas.bbox("all"))
            panel_canvas.itemconfigure(panel_window, width=panel_canvas.winfo_width())

        panel.bind("<Configure>", _panel_on_configure)
        panel_canvas.bind("<Configure>", _panel_on_configure)

        def _on_mousewheel(e):
            panel_canvas.yview_scroll(int(-1 * (e.delta / 120)), "units")
        panel_canvas.bind_all("<MouseWheel>", _on_mousewheel)

        header = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        header.pack(fill="x", pady=(0, 10))
        tk.Label(header, text="CITY LAB v1.5", fg="white", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 14, "bold")).pack(side="left")
        self.status_dot = tk.Canvas(header, width=14, height=14, bg=UI_COLORS["panel_bg"], highlightthickness=0)
        self.status_dot.pack(side="right")
        self._dot_id = self.status_dot.create_oval(2, 2, 12, 12, fill="#ef4444", outline="")

        self.run_btn = tk.Button(panel, text="START SIMULATION", command=self.toggle_run,
                                 bg="#ffffff", fg="#000000", font=("Consolas", 10, "bold"),
                                 relief="flat", padx=10, pady=10)
        self.run_btn.pack(fill="x", pady=(0, 10))

        row = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        row.pack(fill="x", pady=(0, 10))
        mkbtn = lambda p, t, cmd: tk.Button(p, text=t, command=cmd, bg=UI_COLORS["panel_bg"], fg="white", relief="groove")
        mkbtn(row, "STEP", self.step_once).pack(side="left", expand=True, fill="x", padx=(0, 6))
        mkbtn(row, "RESET", self.reset).pack(side="left", expand=True, fill="x", padx=6)
        mkbtn(row, "CLEAR", self.clear).pack(side="left", expand=True, fill="x", padx=(6, 0))

        exp_row = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        exp_row.pack(fill="x", pady=(0, 10))
        tk.Button(exp_row, text="EXPORT PNG", command=self.export_city_jpg,
                  bg=UI_COLORS["panel_bg"], fg="white", relief="groove").pack(side="left", fill="x", expand=True, padx=(0, 4))
        tk.Button(exp_row, text="EXPORT C#", command=self.export_csharp,
                  bg=UI_COLORS["panel_bg"], fg="white", relief="groove").pack(side="left", fill="x", expand=True, padx=(4, 0))

        rn = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        rn.pack(fill="x", pady=(0, 12))
        tk.Label(rn, text="RUN N STEPS", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(side="left")
        self.run_n_entry = tk.Entry(rn, textvariable=self.run_n_var, width=10,
                                    bg=UI_COLORS["box_bg"], fg="white", relief="flat",
                                    insertbackground="white", font=("Consolas", 10), justify="center")
        self.run_n_entry.pack(side="left", padx=8)
        tk.Button(rn, text="GO", command=self.run_n_steps,
                  bg="#ffffff", fg="#000000", relief="groove").pack(side="left", padx=2)
        self.batch_status_lbl = tk.Label(rn, text="", fg="#666", bg=UI_COLORS["panel_bg"], font=("Consolas", 9))
        self.batch_status_lbl.pack(side="right")

        tk.Label(panel, text="Evolution Speed (Steps/Frame)", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        sp = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        sp.pack(fill="x", pady=(6, 12))
        self.speed_var = tk.IntVar(value=150)
        self.speed_lbl = tk.Label(sp, text="150", fg="white", bg=UI_COLORS["panel_bg"], font=("Consolas", 10))
        self.speed_lbl.pack(side="right")
        tk.Scale(panel, from_=1, to=1000, orient="horizontal", variable=self.speed_var,
                 showvalue=False, command=lambda _v: self.speed_lbl.config(text=str(self.speed_var.get())),
                 bg=UI_COLORS["panel_bg"], fg="white", troughcolor="#333", highlightthickness=0
                 ).pack(fill="x", pady=(0, 14))

        tk.Label(panel, text="Swap Rule (compare behaviors)", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        rf = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        rf.pack(fill="x", pady=(6, 12))

        def _set_mode():
            self.m.swap_mode = self.swap_mode_var.get()

        for lab, val in SWAP_RULE_OPTIONS:
            tk.Radiobutton(
                rf, text=lab, value=val, variable=self.swap_mode_var, command=_set_mode,
                bg=UI_COLORS["panel_bg"], fg="white", selectcolor="#111",
                activebackground=UI_COLORS["panel_bg"], activeforeground="white",
                font=("Consolas", 9)
            ).pack(anchor="w")

        tk.Label(panel, text="Urban Typologies", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        lf = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        lf.pack(fill="x", pady=(6, 12))
        self.layout_buttons = {}
        for idx, name in enumerate(LAYOUT_NAMES):
            b = tk.Button(lf, text=name.upper(), command=lambda n=name: self.set_layout(n),
                          bg=UI_COLORS["panel_bg"], fg="white", relief="groove")
            r, c = divmod(idx, 2)
            if name == "Hybrid":
                b.grid(row=r, column=0, columnspan=2, sticky="ew", padx=2, pady=2)
            else:
                b.grid(row=r, column=c, sticky="ew", padx=2, pady=2)
            self.layout_buttons[name] = b
        lf.grid_columnconfigure(0, weight=1)
        lf.grid_columnconfigure(1, weight=1)

        tk.Label(panel, text="Road Topology", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        rtf = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        rtf.pack(fill="x", pady=(6, 12))
        self.road_topology_buttons = {}
        for idx, name in enumerate(ROAD_TOPOLOGY_NAMES):
            b = tk.Button(rtf, text=name.upper(), command=lambda n=name: self.set_road_topology(n),
                          bg="#ffffff" if name == "From Layout" else UI_COLORS["panel_bg"],
                          fg="#000" if name == "From Layout" else "white", relief="groove")
            r, c = divmod(idx, 3)
            b.grid(row=r, column=c, sticky="ew", padx=2, pady=2)
            self.road_topology_buttons[name] = b
        for c in range(3):
            rtf.grid_columnconfigure(c, weight=1)

        tk.Label(panel, text="Infrastructure Tools", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        self.tool_frame = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        self.tool_frame.pack(fill="x", pady=(6, 12))
        self._rebuild_tool_buttons()

        self.pp_btn = tk.Button(panel, text="PUBLIC / PRIVATE VIEW (4 BINS)",
                                command=self.toggle_pubpriv_view,
                                bg=UI_COLORS["panel_bg"], fg="white", relief="groove")
        self.pp_btn.pack(fill="x", pady=(0, 10))

        self.contrib_btn = tk.Button(panel, text="CELL SATISFACTION VIEW",
                                     command=self.toggle_contrib_view,
                                     bg=UI_COLORS["panel_bg"], fg="white", relief="groove")
        self.contrib_btn.pack(fill="x", pady=(0, 10))

        self.reach_btn = tk.Button(panel, text="SHOW INFLUENCE OVERLAY (HOVER)",
                                   command=self.toggle_reach_overlay,
                                   bg="#ffffff", fg="#000000", relief="groove")
        self.reach_btn.pack(fill="x", pady=(0, 10))

        self.original_citylab_btn = tk.Button(panel, text="原始city lab game (Original City Lab Game)",
                                             command=self.toggle_original_citylab,
                                             bg=UI_COLORS["panel_bg"], fg="white", relief="groove")
        self.original_citylab_btn.pack(fill="x", pady=(0, 10))

        tk.Label(panel, text="Legend (Color + Shape)", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        self.legend_frame = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        self.legend_frame.pack(fill="x", pady=(6, 12))
        self.build_legend(self.legend_frame)

        tk.Label(panel, text="Influence Matrices (Rules 1 & 2)", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        tk.Label(panel, text="A) Agent → Attractor", fg="#666", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", pady=(6, 2))
        self.matrix_attr_container = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        self.matrix_attr_container.pack(fill="x", pady=(0, 10))
        tk.Label(panel, text="B) Agent → Agent", fg="#666", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", pady=(0, 2))
        self.matrix_agent_container = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        self.matrix_agent_container.pack(fill="x", pady=(0, 10))
        tk.Label(panel, text="C) Attractor influence range (attr→agent)", fg="#666", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", pady=(0, 2))
        self.matrix_influence_attr_container = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        self.matrix_influence_attr_container.pack(fill="x", pady=(0, 6))
        tk.Label(panel, text="D) Agent influence range (agent→agent)", fg="#666", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", pady=(0, 2))
        self.matrix_influence_agent_container = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        self.matrix_influence_agent_container.pack(fill="x", pady=(0, 12))

        tk.Label(panel, text="Value Trend (Total Utility)", fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w")
        cc = tk.Frame(panel, bg=UI_COLORS["box_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        cc.pack(fill="x", pady=(6, 12))
        self.chart_canvas = tk.Canvas(cc, height=150, bg=UI_COLORS["box_bg"], highlightthickness=0)
        self.chart_canvas.pack(fill="x", padx=8, pady=8)

        self.iter_val = tk.StringVar(value="0")
        self.util_val = tk.StringVar(value="0")
        self.acc_val = tk.StringVar(value="0")
        self.rej_val = tk.StringVar(value="0")
        self.sac_val = tk.StringVar(value="0")
        sf = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        sf.pack(fill="x", pady=(8, 0))
        self._stat_box(sf, "Iterations", self.iter_val).pack(side="left", expand=True, fill="x", padx=(0, 6))
        self._stat_box(sf, "Total Utility", self.util_val).pack(side="left", expand=True, fill="x", padx=(6, 0))
        sf2 = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        sf2.pack(fill="x", pady=(8, 0))
        self._stat_box(sf2, "Accepted", self.acc_val).pack(side="left", expand=True, fill="x", padx=(0, 6))
        self._stat_box(sf2, "Rejected", self.rej_val).pack(side="left", expand=True, fill="x", padx=(6, 0))
        sf3 = tk.Frame(panel, bg=UI_COLORS["panel_bg"])
        sf3.pack(fill="x", pady=(8, 0))
        self._stat_box(sf3, "Sacrificed", self.sac_val).pack(side="left", expand=True, fill="x")

    def build_legend(self, parent):
        for w in parent.winfo_children():
            w.destroy()

        def row_item(color, text, shape="rect", outline=None):
            line = tk.Frame(parent, bg=UI_COLORS["box_bg"])
            line.pack(fill="x", padx=10, pady=4)
            ic = tk.Canvas(line, width=18, height=18, bg=UI_COLORS["box_bg"], highlightthickness=0)
            ic.pack(side="left")
            outline = outline if outline is not None else ""
            if shape == "rect":
                ic.create_rectangle(2, 2, 16, 16, fill=color, outline=outline)
            else:
                ic.create_oval(2, 2, 16, 16, fill=color, outline=outline)
            tk.Label(line, text=text, fg="#ddd", bg=UI_COLORS["box_bg"], font=("Consolas", 9)).pack(side="left", padx=8)

        tk.Label(parent, text="Attractors", fg="#aaa", bg=UI_COLORS["box_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(8, 2))
        attr_defs = ORIGINAL_CITYLAB_ATTR_DEFS if self.m.use_original_citylab else ATTR_DEFS
        for k, name, _col in attr_defs:
            row_item(self.colors.get(k, "#fff"), name, shape="rect")
        tk.Label(parent, text="Agents", fg="#aaa", bg=UI_COLORS["box_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(10, 2))
        if self.m.use_original_citylab:
            agent_items = [("residential", "Residential"), ("office", "Office"), ("shop", "Shop"), ("cafe", "Cafe")]
        else:
            agent_items = [("Resi", "Residential"), ("Firm", "Company"), ("Shop", "Retail"), ("Cafe", "Cafe"),
                          ("Hotel", "Hotel"), ("Restaurant", "Restaurant"), ("Clinic", "Clinic")]
        for k, name in agent_items:
            row_item(self.colors.get(k, "#fff"), name, shape="oval", outline="#fff")
        tk.Label(parent, text="Public / Private View (4 bins)", fg="#aaa", bg=UI_COLORS["box_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(10, 2))
        row_item(self.pp_bin_colors["white"], "Bin 1: Public (white)", shape="rect", outline="#111")
        row_item(self.pp_bin_colors["lgray"], "Bin 2: Semi-public (light gray)", shape="rect", outline="#111")
        row_item(self.pp_bin_colors["dgray"], "Bin 3: Semi-private (dark gray)", shape="rect", outline="#111")
        row_item(self.pp_bin_colors["black"], "Bin 4: Private (black)", shape="rect", outline="#111")
        tk.Label(parent, text="Cell Satisfaction View", fg="#aaa", bg=UI_COLORS["box_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(10, 2))
        row_item("#777777", "Grayscale = normalized satisfaction (min→max)", shape="rect", outline="#111")
        tk.Label(parent, text="Influence Overlay (hover agent/attractor)", fg="#aaa", bg=UI_COLORS["box_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(10, 2))
        row_item("", "Agent: solid = this agent's influence range on others", shape="rect", outline="#fff")
        row_item("", "Attractor: dashed = this attractor's influence range", shape="rect", outline="#777")

    def _stat_box(self, parent, title, var):
        box = tk.Frame(parent, bg=UI_COLORS["panel_bg"], highlightbackground=UI_COLORS["border"], highlightthickness=1)
        tk.Label(box, text=title.upper(), fg="#888", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 9, "bold")).pack(anchor="w", padx=10, pady=(8, 0))
        tk.Label(box, textvariable=var, fg="white", bg=UI_COLORS["panel_bg"],
                 font=("Consolas", 16, "bold")).pack(anchor="w", padx=10, pady=(0, 8))
        return box

    def build_matrices(self):
        for w in self.matrix_attr_container.winfo_children():
            w.destroy()
        for w in self.matrix_agent_container.winfo_children():
            w.destroy()
        for w in self.matrix_influence_attr_container.winfo_children():
            w.destroy()
        for w in self.matrix_influence_agent_container.winfo_children():
            w.destroy()
        self.matrix_entries = {}
        self._build_matrix_table(parent=self.matrix_attr_container, table="attr", cols=self.m.attr_keys)
        self._build_matrix_table(parent=self.matrix_agent_container, table="agent", cols=self.m.type_labels)
        self._build_influence_attr_table(parent=self.matrix_influence_attr_container)
        self._build_influence_agent_table(parent=self.matrix_influence_agent_container)

    def _build_matrix_table(self, parent, table: str, cols):
        tk.Label(parent, text="", bg="#111", fg="#888",
                 font=("Consolas", 8, "bold"), width=10).grid(row=0, column=0, sticky="nsew")
        for j, c in enumerate(cols, start=1):
            tk.Label(parent, text=c, bg="#111", fg="#888",
                     font=("Consolas", 8, "bold"), width=6).grid(row=0, column=j, sticky="nsew")
        for i, row in enumerate(self.m.type_labels, start=1):
            tk.Label(parent, text=row, bg=UI_COLORS["box_bg"], fg="#aaa",
                     font=("Consolas", 8, "bold"), width=10, anchor="w").grid(row=i, column=0, sticky="nsew")
            for j, col in enumerate(cols, start=1):
                ent = tk.Entry(parent, width=6, justify="center",
                               bg=UI_COLORS["box_bg"], fg="white", relief="flat",
                               insertbackground="white", font=("Consolas", 9))
                ent.insert(0, str(self.m.pref_attr[row][col] if table == "attr" else self.m.pref_agent[row][col]))
                ent.grid(row=i, column=j, sticky="nsew", padx=1, pady=1)
                ent.bind("<Return>", lambda e, t=table, r=row, c=col: self.on_matrix_change(t, r, c))
                ent.bind("<FocusOut>", lambda e, t=table, r=row, c=col: self.on_matrix_change(t, r, c))
                self.matrix_entries[(table, row, col)] = ent
        for j in range(len(cols) + 1):
            parent.grid_columnconfigure(j, weight=1)

    def _build_influence_attr_table(self, parent):
        """吸引子影响范围：行=吸引子类型，列=代理类型"""
        tk.Label(parent, text="", bg="#111", fg="#888",
                 font=("Consolas", 8, "bold"), width=6).grid(row=0, column=0, sticky="nsew")
        for j, t in enumerate(self.m.type_labels, start=1):
            tk.Label(parent, text=t[:4], bg="#111", fg="#888",
                     font=("Consolas", 7, "bold"), width=5).grid(row=0, column=j, sticky="nsew")
        for i, k in enumerate(self.m.attr_keys, start=1):
            tk.Label(parent, text=k, bg=UI_COLORS["box_bg"], fg="#aaa",
                     font=("Consolas", 8, "bold"), width=6, anchor="w").grid(row=i, column=0, sticky="nsew")
            for j, t in enumerate(self.m.type_labels, start=1):
                ent = tk.Entry(parent, width=5, justify="center",
                               bg=UI_COLORS["box_bg"], fg="white", relief="flat",
                               insertbackground="white", font=("Consolas", 8))
                ent.insert(0, str(int(self.m.influence_range_attr.get(k, {}).get(t, self.m.reach))))
                ent.grid(row=i, column=j, sticky="nsew", padx=1, pady=1)
                self.matrix_entries[("influence_attr", k, t)] = ent
                ent.bind("<Return>", lambda e, kk=k, tt=t: self.on_influence_change("attr", kk, tt))
                ent.bind("<FocusOut>", lambda e, kk=k, tt=t: self.on_influence_change("attr", kk, tt))
        for j in range(len(self.m.type_labels) + 1):
            parent.grid_columnconfigure(j, weight=1)

    def _build_influence_agent_table(self, parent):
        """代理影响范围：行=源代理类型，列=目标代理类型"""
        tk.Label(parent, text="", bg="#111", fg="#888",
                 font=("Consolas", 8, "bold"), width=6).grid(row=0, column=0, sticky="nsew")
        for j, t in enumerate(self.m.type_labels, start=1):
            tk.Label(parent, text=t[:4], bg="#111", fg="#888",
                     font=("Consolas", 7, "bold"), width=5).grid(row=0, column=j, sticky="nsew")
        for i, src in enumerate(self.m.type_labels, start=1):
            tk.Label(parent, text=src, bg=UI_COLORS["box_bg"], fg="#aaa",
                     font=("Consolas", 8, "bold"), width=6, anchor="w").grid(row=i, column=0, sticky="nsew")
            for j, tgt in enumerate(self.m.type_labels, start=1):
                ent = tk.Entry(parent, width=5, justify="center",
                               bg=UI_COLORS["box_bg"], fg="white", relief="flat",
                               insertbackground="white", font=("Consolas", 8))
                ent.insert(0, str(int(self.m.influence_range_agent.get(src, {}).get(tgt, self.m.reach))))
                ent.grid(row=i, column=j, sticky="nsew", padx=1, pady=1)
                self.matrix_entries[("influence_agent", src, tgt)] = ent
                ent.bind("<Return>", lambda e, s=src, t=tgt: self.on_influence_change("agent", s, t))
                ent.bind("<FocusOut>", lambda e, s=src, t=tgt: self.on_influence_change("agent", s, t))
        for j in range(len(self.m.type_labels) + 1):
            parent.grid_columnconfigure(j, weight=1)

    def _refresh_reset_target_to_current(self):
        self.reset_target_int = int(self.m.stats.get("totalUtility", 0.0))

    def on_matrix_change(self, table, row, col):
        ent = self.matrix_entries[(table, row, col)]
        try:
            v = float(ent.get())
        except ValueError:
            v = 0.0
            ent.delete(0, "end")
            ent.insert(0, "0")
        if table == "attr":
            self.m.pref_attr[row][col] = v
        else:
            self.m.pref_agent[row][col] = v
        self.m.calc_total_utility()
        self._refresh_reset_target_to_current()
        self.update_stats()
        self.sample_chart(force=True)
        if self.show_view and self.view_mode == "contrib":
            self.rebuild_contrib_cache()
            self.refresh_view_full()

    def on_influence_change(self, table, row_key, col_key):
        key = ("influence_attr", row_key, col_key) if table == "attr" else ("influence_agent", row_key, col_key)
        ent = self.matrix_entries.get(key)
        if ent is None:
            return

        def _parse_int(e, fallback):
            try:
                v = int(float(e.get()))
            except ValueError:
                v = int(fallback)
            v = max(1, v)
            e.delete(0, "end")
            e.insert(0, str(v))
            return v

        v = _parse_int(ent, self.m.reach)
        if table == "attr":
            self.m.influence_range_attr.setdefault(row_key, {})[col_key] = v
        else:
            self.m.influence_range_agent.setdefault(row_key, {})[col_key] = v
        self.m.calc_total_utility()
        self._refresh_reset_target_to_current()
        self.update_stats()
        self.sample_chart(force=True)
        if self.show_view and self.view_mode == "contrib":
            self.rebuild_contrib_cache()
            self.refresh_view_full()
        if self.show_reach_overlay and self._hover_cell != (-1, -1):
            hx, hy = self._hover_cell
            a = self.m.grid[hx][hy]
            if a is not None:
                self.draw_reach_overlay(hx, hy)

    def rebuild_contrib_cache(self):
        w, h = self.m.w, self.m.h
        vals = []
        for x in range(w):
            for y in range(h):
                a = self.m.grid[x][y]
                v = self.m.get_utility(a, x, y) if a is not None else 0.0
                self.contrib_cache[x][y] = v
                vals.append(v)
        self.contrib_min = min(vals) if vals else 0.0
        self.contrib_max = max(vals) if vals else 1.0

    def _pubpriv_fill_at(self, x: int, y: int) -> str:
        a = self.m.grid[x][y]
        k = self.m.attr_grid[x][y] if hasattr(self.m, "attr_grid") else None
        if self.view_agent_first and a is not None:
            rank = self.pp_agent_rank.get(a.type, "black")
            return self.pp_bin_colors.get(rank, "#000000")
        if k is not None:
            rank = self.pp_attr_rank.get(k, "black")
            return self.pp_bin_colors.get(rank, "#000000")
        if (not self.view_agent_first) and a is not None:
            rank = self.pp_agent_rank.get(a.type, "black")
            return self.pp_bin_colors.get(rank, "#000000")
        return self.pp_bin_colors["black"]

    def _contrib_fill_at(self, x: int, y: int) -> str:
        v = self.contrib_cache[x][y]
        vmin, vmax = self.contrib_min, self.contrib_max
        if abs(vmax - vmin) < 1e-9:
            t = 0.5
        else:
            t = (v - vmin) / (vmax - vmin)
            t = max(0.0, min(1.0, t))
        g = int(255 * t)
        return f"#{g:02x}{g:02x}{g:02x}"

    def view_fill_at(self, x: int, y: int) -> str:
        if self.view_mode == "contrib":
            return self._contrib_fill_at(x, y)
        return self._pubpriv_fill_at(x, y)

    def update_view_cell(self, x: int, y: int):
        item = self.zone_item.get((x, y))
        if item:
            self.canvas.itemconfig(item, fill=self.view_fill_at(x, y))

    def refresh_view_full(self):
        for x in range(self.m.w):
            for y in range(self.m.h):
                self.update_view_cell(x, y)

    def set_view_layer_visible(self, on: bool):
        self.show_view = bool(on)
        if not self.show_view:
            self._hide_contrib_tooltip()
        self.canvas.itemconfig("zone", state=("normal" if self.show_view else "hidden"))
        self.canvas.itemconfig("dyn", state=("hidden" if self.show_view else "normal"))
        self.canvas.tag_raise(self._reach_tag)
        self.canvas.tag_raise(self._reach_center_tag)

    def toggle_pubpriv_view(self):
        if self.show_view and self.view_mode == "pubpriv":
            self.set_view_layer_visible(False)
            self.pp_btn.config(bg=UI_COLORS["panel_bg"], fg="white")
            self._hide_contrib_tooltip()
            return
        self.view_mode = "pubpriv"
        self._hide_contrib_tooltip()
        self.set_view_layer_visible(True)
        self.pp_btn.config(bg="#ffffff", fg="#000000")
        self.contrib_btn.config(bg=UI_COLORS["panel_bg"], fg="white")
        self.refresh_view_full()

    def toggle_contrib_view(self):
        if self.show_view and self.view_mode == "contrib":
            self.set_view_layer_visible(False)
            self.contrib_btn.config(bg=UI_COLORS["panel_bg"], fg="white")
            self._hide_contrib_tooltip()
            return
        self.view_mode = "contrib"
        self.set_view_layer_visible(True)
        self.contrib_btn.config(bg="#ffffff", fg="#000000")
        self.pp_btn.config(bg=UI_COLORS["panel_bg"], fg="white")
        self.rebuild_contrib_cache()
        self.refresh_view_full()

    def toggle_reach_overlay(self):
        self.show_reach_overlay = not self.show_reach_overlay
        self.reach_btn.config(
            bg="#ffffff" if self.show_reach_overlay else UI_COLORS["panel_bg"],
            fg="#000000" if self.show_reach_overlay else "white"
        )
        if not self.show_reach_overlay:
            self.clear_reach_overlay()

    def toggle_original_citylab(self):
        """Switch between standard and Original City Lab Game schema."""
        if self._batch_running:
            return
        if self.m.use_original_citylab:
            self.m.switch_to_standard()
        else:
            self.m.switch_to_original_citylab()
        self._refresh_colors()
        self._refresh_pp_ranks()
        self._rebuild_tool_buttons()
        self.build_legend(self.legend_frame)
        self.build_matrices()
        self.original_citylab_btn.config(
            bg="#ffffff" if self.m.use_original_citylab else UI_COLORS["panel_bg"],
            fg="#000000" if self.m.use_original_citylab else "white"
        )
        self.set_tool("None")
        self.util_history.clear()
        self.rebuild_all()

    def clear_reach_overlay(self):
        self.canvas.delete(self._reach_tag)
        self.canvas.delete(self._reach_center_tag)

    def on_canvas_leave(self, _e=None):
        if self._hover_after is not None:
            try:
                self.root.after_cancel(self._hover_after)
            except Exception:
                pass
            self._hover_after = None
        self._hover_cell = (-1, -1)
        self.clear_reach_overlay()
        self._hide_contrib_tooltip()

    def on_canvas_motion(self, e):
        if not self.show_reach_overlay and not (self.show_view and self.view_mode == "contrib"):
            return
        if not self._is_canvas_under_pointer(e):
            self.on_canvas_leave()
            return
        if self._hover_after is not None:
            return
        self._hover_after = self.root.after(15, lambda: self._handle_hover(e))

    def _show_contrib_tooltip(self, text: str, root_x: int, root_y: int):
        """在 cell contribution 模式下显示满意度 tooltip"""
        if self._contrib_tooltip is None:
            self._contrib_tooltip = tk.Toplevel(self.root)
            self._contrib_tooltip.wm_overrideredirect(True)
            self._contrib_tooltip.wm_geometry("+0+0")
            self._contrib_tooltip.configure(bg="#2a2a2a", highlightthickness=0)
            self._contrib_tooltip_lbl = tk.Label(
                self._contrib_tooltip, text="", fg="white", bg="#2a2a2a",
                font=("Consolas", 10), padx=8, pady=4
            )
            self._contrib_tooltip_lbl.pack()
        self._contrib_tooltip_lbl.config(text=text)
        offset = 12
        self._contrib_tooltip.wm_geometry(f"+{root_x + offset}+{root_y + offset}")
        self._contrib_tooltip.deiconify()

    def _hide_contrib_tooltip(self):
        """隐藏 cell contribution 的 tooltip"""
        if self._contrib_tooltip is not None:
            try:
                self._contrib_tooltip.withdraw()
            except Exception:
                pass

    def _handle_hover(self, e):
        self._hover_after = None
        if not self._is_canvas_under_pointer(e):
            self.on_canvas_leave()
            return
        x = int(e.x // self.cell)
        y = int(e.y // self.cell)
        if not (0 <= x < self.m.w and 0 <= y < self.m.h):
            self.on_canvas_leave()
            return
        if (x, y) == self._hover_cell:
            return
        self._hover_cell = (x, y)
        a = self.m.grid[x][y]

        # Cell contribution 模式：悬停于代理格时显示满意度
        if self.show_view and self.view_mode == "contrib":
            if a is not None:
                u = self.m.get_utility(a, x, y)
                self._show_contrib_tooltip(f"Utility: {u:.2f}", e.x_root, e.y_root)
            else:
                self._hide_contrib_tooltip()

        if self.show_reach_overlay:
            self.draw_reach_overlay(x, y)

    def _diamond_poly_pixels(self, cx, cy, r):
        s = self.cell
        pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy), (cx, cy - r)]
        poly = []
        for gx, gy in pts:
            poly.extend([gx * s + s / 2, gy * s + s / 2])
        return poly

    def draw_reach_overlay(self, cx, cy):
        self.clear_reach_overlay()
        a = self.m.grid[cx][cy]
        attr_type = self.m.attr_grid[cx][cy] if hasattr(self.m, "attr_grid") else None

        if a is not None:
            # 悬停代理：显示该代理自身的影响范围（能影响多远内的其他代理）
            ra = max(int(self.m.influence_range_agent.get(a.type, {}).get(t, self.m.reach))
                     for t in self.m.type_labels)
            if ra > 0:
                poly_a = self._diamond_poly_pixels(cx, cy, ra)
                self.canvas.create_line(*poly_a, fill="#ffffff", width=3, tags=self._reach_tag)
            s = self.cell
            self.canvas.create_rectangle(
                cx * s + 1, cy * s + 1, (cx + 1) * s - 1, (cy + 1) * s - 1,
                outline="#ffffff", width=2, fill="", tags=self._reach_center_tag
            )
        elif attr_type is not None:
            # 悬停吸引子：显示该吸引子对各类代理的影响范围（取最大）
            r = max(int(self.m.influence_range_attr.get(attr_type, {}).get(t, self.m.reach))
                    for t in self.m.type_labels)
            if r > 0:
                poly = self._diamond_poly_pixels(cx, cy, r)
                self.canvas.create_line(*poly, fill="#aaaaaa", width=2, dash=(4, 3), tags=self._reach_tag)
            s = self.cell
            self.canvas.create_rectangle(
                cx * s + 1, cy * s + 1, (cx + 1) * s - 1, (cy + 1) * s - 1,
                outline="#aaaaaa", width=2, fill="", tags=self._reach_center_tag
            )
        else:
            return

        self.canvas.tag_raise(self._reach_tag)
        self.canvas.tag_raise(self._reach_center_tag)

    def boot(self):
        self.set_layout_buttons("Grid")
        self.set_road_topology_buttons("From Layout")
        self.set_tool("None")
        self.m.utility_bias = 0.0
        self.m.apply_layout("Grid")
        self.m.calc_total_utility()
        self.reset_target_int = int(self.m.stats["totalUtility"])
        self.rebuild_all()

    def resize_canvas(self):
        cw, ch = self.grid_container.winfo_width(), self.grid_container.winfo_height()
        if cw <= 1 or ch <= 1:
            return
        size = max(200, min(cw, ch) - 20)
        new_cell = max(4, size // self.m.w)
        new_w, new_h = self.m.w * new_cell, self.m.h * new_cell
        cur_w = int(float(self.canvas.cget("width"))) if self.canvas.cget("width") else 0
        cur_h = int(float(self.canvas.cget("height"))) if self.canvas.cget("height") else 0
        if new_cell == self.cell and new_w == cur_w and new_h == cur_h:
            return
        self.cell = new_cell
        self.canvas.config(width=new_w, height=new_h)
        self.draw_static_grid()
        self.redraw_all_items_positions()
        self._hover_cell = (-1, -1)
        self.clear_reach_overlay()

    def set_layout_buttons(self, name):
        for k, b in self.layout_buttons.items():
            b.config(bg="#ffffff" if k == name else UI_COLORS["panel_bg"], fg="#000" if k == name else "white")

    def set_road_topology_buttons(self, name):
        for k, b in self.road_topology_buttons.items():
            b.config(bg="#ffffff" if k == name else UI_COLORS["panel_bg"], fg="#000" if k == name else "white")

    def set_layout(self, name):
        if self._batch_running:
            return
        self.set_layout_buttons(name)
        self.m.utility_bias = 0.0
        self.m.apply_layout(name)
        self.m.calc_total_utility()
        self.reset_target_int = int(self.m.stats["totalUtility"])
        self.util_history.clear()
        self.rebuild_all()

    def set_road_topology(self, name):
        if self._batch_running:
            return
        self.set_road_topology_buttons(name)
        self.m.utility_bias = 0.0
        self.m.apply_road_topology(name)
        self.m.rebuild_attr_distance_fields()
        self.m.reset(target_int=self.reset_target_int)
        self.m.calc_total_utility()
        self.reset_target_int = int(self.m.stats["totalUtility"])
        self.util_history.clear()
        self.rebuild_all()

    def set_tool(self, key):
        self.current_tool = key
        for k, b in self.tool_buttons.items():
            b.config(bg="#ffffff" if k == key else UI_COLORS["panel_bg"], fg="#000" if k == key else "white")

    def toggle_run(self):
        if self._batch_running:
            return
        self.running = not self.running
        if self.running:
            self.run_btn.config(text="PAUSE SIMULATION", bg="#ef4444", fg="white")
            self.status_dot.itemconfig(self._dot_id, fill="#22c55e")
        else:
            self.run_btn.config(text="START SIMULATION", bg="#ffffff", fg="#000000")
            self.status_dot.itemconfig(self._dot_id, fill="#ef4444")

    def step_once(self):
        if self._batch_running:
            return
        result = self.m.step()
        swapped, a1, a2, old1, old2 = result[:5]
        for a in (result[5] if len(result) > 5 else []):
            itm = self.agent_item.pop(id(a), None)
            if itm:
                self.canvas.delete(itm)
        if self.m.stats["steps"] % 20 == 0:
            self.m.calc_total_utility()
            self.sample_chart()
        if swapped:
            self.move_agent_item(a1)
            self.move_agent_item(a2)
            if self.show_view and self.view_mode == "pubpriv":
                if old1:
                    self.update_view_cell(*old1)
                if old2:
                    self.update_view_cell(*old2)
        if self.show_view and self.view_mode == "contrib" and (self.m.stats["steps"] % self.contrib_rebuild_interval == 0):
            self.rebuild_contrib_cache()
            self.refresh_view_full()
        self.update_stats()

    def reset(self):
        if self._batch_running:
            return
        if self.reset_target_int is None:
            self.m.utility_bias = 0.0
            self.m.reset()
            self.m.calc_total_utility()
            self.reset_target_int = int(self.m.stats["totalUtility"])
        else:
            self.m.reset(target_int=self.reset_target_int)
        self.util_history.clear()
        self.rebuild_all()

    def clear(self):
        if self._batch_running:
            return
        self.m.utility_bias = 0.0
        self.m.clear()
        self.m.calc_total_utility()
        self.reset_target_int = int(self.m.stats["totalUtility"])
        self.util_history.clear()
        self.rebuild_all()

    def export_city_jpg(self):
        """Export the rectangular city canvas as a PNG image (programmatic render, no screen capture)."""
        if self._batch_running:
            return
        if self.show_view and self.view_mode == "contrib":
            self.rebuild_contrib_cache()
        path = filedialog.asksaveasfilename(
            defaultextension=".png",
            filetypes=[("PNG Image", "*.png"), ("All Files", "*.*")],
            initialfile="city_export.png",
        )
        if not path:
            return
        try:
            cell = max(self.cell, 8)
            wpx = self.m.w * cell
            hpx = self.m.h * cell
            img = Image.new("RGB", (wpx, hpx), "#000000")
            draw = ImageDraw.Draw(img)
            grid_color = self.colors.get("grid", "#1c1c1c")

            if self.show_view:
                for x in range(self.m.w):
                    for y in range(self.m.h):
                        fill = self.view_fill_at(x, y)
                        draw.rectangle(
                            [x * cell, y * cell, (x + 1) * cell - 1, (y + 1) * cell - 1],
                            fill=fill, outline=fill
                        )
            else:
                draw_order = self.m._get_attr_draw_order()
                for k in draw_order:
                    lst = self.m.attractors.get(k, [])
                    col = self.colors.get(k, "#ffffff")
                    for (x, y) in lst:
                        x1, y1 = x * cell + 1, y * cell + 1
                        x2, y2 = (x + 1) * cell - 1, (y + 1) * cell - 1
                        draw.rectangle([x1, y1, x2, y2], fill=col, outline=col)
                for a in self.m.agents:
                    cx = a.x * cell + cell / 2
                    cy = a.y * cell + cell / 2
                    r = cell * 0.35
                    draw.ellipse(
                        [cx - r, cy - r, cx + r, cy + r],
                        fill=self.colors.get(a.type, "#ffffff"),
                        outline="#ffffff", width=max(1, int(cell / 15))
                    )

            for i in range(self.m.w + 1):
                px = i * cell
                draw.line([(px, 0), (px, hpx)], fill=grid_color, width=1)
            for j in range(self.m.h + 1):
                py = j * cell
                draw.line([(0, py), (wpx, py)], fill=grid_color, width=1)

            img.save(path, "PNG")
        except Exception as e:
            messagebox.showerror("Export Error", str(e))

    def export_csharp(self):
        """Export game logic as C# code for Grasshopper (Rhino)."""
        if self._batch_running:
            return
        if len(self.m.agents) < 2:
            messagebox.showwarning("Export C#", "Need at least 2 agents. Run RESET first.")
            return
        path = filedialog.asksaveasfilename(
            defaultextension=".cs",
            filetypes=[("C# Script", "*.cs"), ("All Files", "*.*")],
            initialfile="CityLab_Grasshopper.cs",
        )
        if not path:
            return
        try:
            code = export_to_csharp(self.m)
            with open(path, "w", encoding="utf-8") as f:
                f.write(code)
            messagebox.showinfo("Export C#", f"Exported to:\n{path}\n\nPaste into Grasshopper C# component for Rhino analysis.")
        except Exception as e:
            messagebox.showerror("Export C# Error", str(e))

    def run_n_steps(self):
        if self._batch_running:
            return
        try:
            n = int(float(self.run_n_var.get()))
        except ValueError:
            n = 0
        n = max(0, n)
        if n <= 0:
            return
        self._batch_prev_running = bool(self.running)
        if self.running:
            self.running = False
            self.run_btn.config(text="START SIMULATION", bg="#ffffff", fg="#000000")
            self.status_dot.itemconfig(self._dot_id, fill="#ef4444")
        self._batch_running = True
        self._batch_remaining = n
        self._batch_dirty = set()
        self.batch_status_lbl.config(text=f"{n} left")
        self._batch_tick()

    def _batch_tick(self):
        if not self._batch_running:
            return
        chunk = min(self.batch_chunk, self._batch_remaining)
        for _ in range(chunk):
            result = self.m.step()
            swapped, _a1, _a2, old1, old2 = result[:5]
            for a in (result[5] if len(result) > 5 else []):
                itm = self.agent_item.pop(id(a), None)
                if itm:
                    self.canvas.delete(itm)
            if swapped and self.show_view and self.view_mode == "pubpriv":
                if old1:
                    self._batch_dirty.add(old1)
                if old2:
                    self._batch_dirty.add(old2)
        self._batch_remaining -= chunk
        if self.m.stats["steps"] % 20 == 0:
            self.m.calc_total_utility()
        for a in self.m.agents:
            item = self.agent_item.get(id(a))
            if item:
                self.canvas.coords(item, *self.agent_oval(a.x, a.y))
        if self.show_view:
            if self.view_mode == "pubpriv":
                if len(self._batch_dirty) > (self.m.w * self.m.h) * 0.30:
                    self.refresh_view_full()
                else:
                    for (cx, cy) in self._batch_dirty:
                        self.update_view_cell(cx, cy)
                self._batch_dirty.clear()
            else:
                self.rebuild_contrib_cache()
                self.refresh_view_full()
        self.update_stats()
        self.sample_chart()
        if self._batch_remaining > 0:
            self.batch_status_lbl.config(text=f"{self._batch_remaining} left")
            self.root.after(1, self._batch_tick)
        else:
            self._batch_running = False
            self.batch_status_lbl.config(text="done")
            if self._batch_prev_running:
                self.running = True
                self.run_btn.config(text="PAUSE SIMULATION", bg="#ef4444", fg="white")
                self.status_dot.itemconfig(self._dot_id, fill="#22c55e")

    def draw_static_grid(self):
        c, s = self.canvas, self.cell
        c.delete("static")
        wpx, hpx = self.m.w * s, self.m.h * s
        for i in range(self.m.w + 1):
            x = i * s
            c.create_line(x, 0, x, hpx, fill=self.colors["grid"], width=1, tags="static")
        for j in range(self.m.h + 1):
            y = j * s
            c.create_line(0, y, wpx, y, fill=self.colors["grid"], width=1, tags="static")

    def cell_rect(self, x, y, pad=1):
        s = self.cell
        return (x * s + pad, y * s + pad, x * s + s - pad, y * s + s - pad)

    def agent_oval(self, x, y):
        s = self.cell
        cx, cy = x * s + s / 2, y * s + s / 2
        r = s * 0.35
        return (cx - r, cy - r, cx + r, cy + r)

    def rebuild_all(self):
        self.m._remove_agents_on_attractors()
        self.canvas.delete("dyn")
        self.canvas.delete("zone")
        self.clear_reach_overlay()
        self.attr_item.clear()
        self.agent_item.clear()
        self.zone_item.clear()
        self.draw_static_grid()
        if self.show_view and self.view_mode == "contrib":
            self.rebuild_contrib_cache()
        for x in range(self.m.w):
            for y in range(self.m.h):
                item = self.canvas.create_rectangle(
                    x * self.cell, y * self.cell, (x + 1) * self.cell, (y + 1) * self.cell,
                    fill=self.view_fill_at(x, y), outline="", tags="zone"
                )
                self.zone_item[(x, y)] = item
        self.canvas.itemconfig("zone", state=("normal" if self.show_view else "hidden"))
        # 吸引子重叠时保留数量较少的
        draw_order = self.m._get_attr_draw_order()
        for k in draw_order:
            lst = self.m.attractors.get(k, [])
            col = self.colors.get(k, "#ffffff")
            for (x, y) in lst:
                item = self.canvas.create_rectangle(*self.cell_rect(x, y), fill=col, outline="", tags="dyn")
                self.attr_item[(x, y)] = (k, item)
        for a in self.m.agents:
            item = self.canvas.create_oval(*self.agent_oval(a.x, a.y),
                                           fill=self.colors.get(a.type, "#ffffff"),
                                           outline="#ffffff", width=1, tags="dyn")
            self.agent_item[id(a)] = item
        self.canvas.itemconfig("dyn", state=("hidden" if self.show_view else "normal"))
        self.update_stats()
        self.sample_chart(force=True)
        self.canvas.tag_raise(self._reach_tag)
        self.canvas.tag_raise(self._reach_center_tag)

    def redraw_all_items_positions(self):
        for (x, y), (_k, item) in self.attr_item.items():
            self.canvas.coords(item, *self.cell_rect(x, y))
        for a in self.m.agents:
            item = self.agent_item.get(id(a))
            if item:
                self.canvas.coords(item, *self.agent_oval(a.x, a.y))
        for (x, y), item in self.zone_item.items():
            self.canvas.coords(item, x * self.cell, y * self.cell, (x + 1) * self.cell, (y + 1) * self.cell)
        self.canvas.tag_raise(self._reach_tag)
        self.canvas.tag_raise(self._reach_center_tag)

    def move_agent_item(self, a: Agent):
        item = self.agent_item.get(id(a))
        if item:
            self.canvas.coords(item, *self.agent_oval(a.x, a.y))

    def update_stats(self):
        self.iter_val.set(f"{self.m.stats['steps']:,}")
        self.util_val.set(f"{int(self.m.stats['totalUtility']):,}")
        self.acc_val.set(f"{self.m.stats['accepted']:,}")
        self.rej_val.set(f"{self.m.stats['rejected']:,}")
        self.sac_val.set(f"{self.m.stats['sacrificed']:,}")

    def sample_chart(self, force=False):
        if force or (self.m.stats["steps"] % 20 == 0):
            v = float(self.m.stats["totalUtility"])
            if force or len(self.util_history) == 0 or self.util_history[-1] != v:
                self.util_history.append(v)
                if len(self.util_history) > self.chart_max_points:
                    self.util_history = self.util_history[-self.chart_max_points:]
            self.draw_chart()

    def draw_chart(self):
        cv = self.chart_canvas
        w, h = cv.winfo_width(), cv.winfo_height()
        cv.delete("all")
        if w < 40 or h < 40:
            return
        pad = self.chart_padding
        label_h = 14
        x0, y0 = pad, pad + label_h
        x1, y1 = w - pad, h - pad - label_h
        if x1 <= x0 + 10 or y1 <= y0 + 10:
            return
        fx0, fy0, fx1, fy1 = pad, pad, w - pad, h - pad
        cv.create_rectangle(fx0, fy0, fx1, fy1, outline="#333", width=1)
        data = self.util_history
        if len(data) < 2:
            cv.create_text(w / 2, h / 2, text="(run simulation to see trend)", fill="#666", font=("Consolas", 9))
            return
        vmin, vmax = min(data), max(data)
        if abs(vmax - vmin) < 1e-9:
            vmax = vmin + 1.0
        for i in range(1, 4):
            yy = y0 + (y1 - y0) * i / 4.0
            cv.create_line(x0, yy, x1, yy, fill="#111", width=1)
        cv.create_text(fx0 + 6, fy0 + 4, text=f"{int(vmax):,}", fill="#888", font=("Consolas", 8), anchor="nw")
        cv.create_text(fx0 + 6, fy1 - 4, text=f"{int(vmin):,}", fill="#888", font=("Consolas", 8), anchor="sw")
        cv.create_text(fx1 - 6, fy0 + 4, text=f"{int(data[-1]):,}", fill="#fff", font=("Consolas", 8, "bold"), anchor="ne")
        n = len(data)
        dx = (x1 - x0) / (n - 1)
        pts = []
        for i, v in enumerate(data):
            t = (v - vmin) / (vmax - vmin)
            x = x0 + i * dx
            y = y1 - t * (y1 - y0)
            pts.extend([x, y])
        cv.create_line(*pts, fill="#ffffff", width=2)
        lx, ly = pts[-2], pts[-1]
        cv.create_oval(lx - 3, ly - 3, lx + 3, ly + 3, fill="#ffffff", outline="")

    def on_canvas_click(self, e):
        if self._batch_running:
            return
        if self.current_tool == "None":
            return
        x = int(e.x // self.cell)
        y = int(e.y // self.cell)
        if not (0 <= x < self.m.w and 0 <= y < self.m.h):
            return
        tool = self.current_tool
        pos = (x, y)
        if tool not in self.m.attractors:
            return
        if pos in self.m.attractors[tool]:
            self.m.attractors[tool].remove(pos)
            info = self.attr_item.pop(pos, None)
            if info:
                self.canvas.delete(info[1])
        else:
            for k in list(self.m.attractors.keys()):
                if pos in self.m.attractors[k]:
                    self.m.attractors[k].remove(pos)
            info = self.attr_item.pop(pos, None)
            if info:
                self.canvas.delete(info[1])
            self.m.attractors[tool].append(pos)
            item = self.canvas.create_rectangle(*self.cell_rect(x, y), fill=self.colors.get(tool, "#fff"),
                                                outline="", tags="dyn")
            self.attr_item[pos] = (tool, item)
            a = self.m.grid[x][y]
            if a is not None:
                itm = self.agent_item.pop(id(a), None)
                if itm:
                    self.canvas.delete(itm)
                self.m.agents.remove(a)
        removed = self.m.rebuild_attr_distance_fields()
        for a in removed:
            itm = self.agent_item.pop(id(a), None)
            if itm:
                self.canvas.delete(itm)
        self.m.update_grid()
        if self.show_view:
            if self.view_mode == "pubpriv":
                self.update_view_cell(x, y)
            else:
                self.rebuild_contrib_cache()
                self.refresh_view_full()
        self.m.calc_total_utility()
        self._refresh_reset_target_to_current()
        self.update_stats()
        self.sample_chart(force=True)
        self.canvas.tag_raise(self._reach_tag)
        self.canvas.tag_raise(self._reach_center_tag)

    def loop(self):
        if self.running and (not self._batch_running):
            speed = int(self.speed_var.get())
            swapped_agents = []
            for _ in range(speed):
                result = self.m.step()
                swapped, a1, a2, old1, old2 = result[:5]
                for a in (result[5] if len(result) > 5 else []):
                    itm = self.agent_item.pop(id(a), None)
                    if itm:
                        self.canvas.delete(itm)
                if swapped:
                    swapped_agents.append((a1, a2, old1, old2))
            if self.m.stats["steps"] % 20 == 0:
                self.m.calc_total_utility()
                self.sample_chart()
            dirty = set() if (self.show_view and self.view_mode == "pubpriv") else None
            for a1, a2, old1, old2 in swapped_agents:
                self.move_agent_item(a1)
                self.move_agent_item(a2)
                if dirty is not None:
                    if old1:
                        dirty.add(old1)
                    if old2:
                        dirty.add(old2)
            if self.show_view:
                if self.view_mode == "pubpriv" and dirty is not None:
                    for (cx, cy) in dirty:
                        self.update_view_cell(cx, cy)
                else:
                    if self.m.stats["steps"] % self.contrib_rebuild_interval == 0:
                        self.rebuild_contrib_cache()
                        self.refresh_view_full()
            self.update_stats()
            if self.show_reach_overlay:
                self.canvas.tag_raise(self._reach_tag)
                self.canvas.tag_raise(self._reach_center_tag)
        self.root.after(16, self.loop)
