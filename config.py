# CITY LAB v1.5 - Configuration & Constants
# Attractor definitions, default preferences, UI colors

# -------------------- ATTRACTOR DEFINITIONS --------------------
# (key, display_name, color)
ATTR_DEFS = [
    ("T", "Transport", "#ffffff"),
    ("P", "Park", "#4d8047"),
    ("R", "Road", "#333333"),
    ("W", "Waterfront", "#89adcd"),
    ("S", "School", "#7a507e"),
    ("H", "Healthcare", "#1a2747"),
    ("G", "Government", "#f97316"),
]

# -------------------- AGENT TYPES --------------------
TYPE_LABELS = [
    "Resi", "Firm", "Shop", "Cafe", "Hotel", "Restaurant", "Clinic",
]

# -------------------- DEFAULT PREFERENCES (Agent → Attractor) --------------------
DEFAULT_PREF_ATTR = {
    "Resi": {"T": 3.5, "P": 5.0, "R": -2.0, "W": 4.5, "S": 4.0, "H": 2.0, "G": 2.5},
    "Firm": {"T": 5.0, "P": 1.0, "R": 2.0, "W": 1.0, "S": 0.5, "H": 1.0, "G": 3.0},
    "Shop": {"T": 5.0, "P": 4.0, "R": 2.0, "W": 2.0, "S": 1.0, "H": 1.0, "G": 1.5},
    "Cafe": {"T": 2.0, "P": 5.0, "R": -1.0, "W": 4.0, "S": 2.0, "H": 1.0, "G": 1.0},
    "Hotel": {"T": 4.0, "P": 3.0, "R": 1.0, "W": 4.0, "S": 1.0, "H": 2.0, "G": 1.5},
    "Restaurant": {"T": 3.0, "P": 4.5, "R": 0.5, "W": 4.0, "S": 1.0, "H": 1.0, "G": 1.0},
    "Clinic": {"T": 2.5, "P": 2.0, "R": -0.5, "W": 1.0, "S": 2.0, "H": 5.0, "G": 2.0},
}

# -------------------- DEFAULT PREFERENCES (Agent → Agent) --------------------
DEFAULT_PREF_AGENT = {
    "Resi": {"Resi": 2.0, "Firm": -1.0, "Shop": 3.0, "Cafe": 4.0, "Hotel": 1.5, "Restaurant": 3.5, "Clinic": 2.5},
    "Firm": {"Resi": 1.0, "Firm": 3.0, "Shop": 4.0, "Cafe": 2.0, "Hotel": 2.0, "Restaurant": 2.5, "Clinic": 1.5},
    "Shop": {"Resi": 2.0, "Firm": 4.0, "Shop": 1.0, "Cafe": 3.0, "Hotel": 2.5, "Restaurant": 4.0, "Clinic": 1.5},
    "Cafe": {"Resi": 4.0, "Firm": 2.0, "Shop": 3.0, "Cafe": -1.0, "Hotel": 2.0, "Restaurant": 4.0, "Clinic": 1.0},
    "Hotel": {"Resi": 1.0, "Firm": 2.0, "Shop": 3.0, "Cafe": 2.0, "Hotel": -1.0, "Restaurant": 4.0, "Clinic": 1.5},
    "Restaurant": {"Resi": 3.0, "Firm": 2.0, "Shop": 3.5, "Cafe": 4.0, "Hotel": 3.0, "Restaurant": -1.0, "Clinic": 1.0},
    "Clinic": {"Resi": 2.5, "Firm": 1.0, "Shop": 1.0, "Cafe": 1.0, "Hotel": 1.0, "Restaurant": 0.5, "Clinic": -1.0},
}

# -------------------- PUBLIC/PRIVATE VIEW (4 bins) --------------------
PP_BIN_COLORS = {
    "white": "#ffffff",
    "lgray": "#cccccc",
    "dgray": "#444444",
    "black": "#000000",
}

PP_AGENT_RANK = {
    "Shop": "white",
    "Cafe": "white",
    "Restaurant": "white",
    "Clinic": "lgray",
    "Hotel": "lgray",
    "Firm": "dgray",
    "Resi": "black",
}

PP_ATTR_RANK = {
    "P": "white",
    "W": "white",
    "R": "white",
    "T": "lgray",
    "S": "dgray",
    "H": "dgray",
    "G": "dgray",
}

# -------------------- UI COLORS --------------------
AGENT_COLORS = {
    "Resi": "#063f76",
    "Firm": "#b26d5d",
    "Shop": "#d3b09d",
    "Cafe": "#a99f83",
    "Hotel": "#698e6c",
    "Restaurant": "#ebead8",
    "Clinic": "#8ba3c7",
}

UI_COLORS = {
    "bg": "#0f0f0f",
    "panel_bg": "#0a0a0a",
    "grid": "#1c1c1c",
    "box_bg": "#050505",
    "border": "#333333",
}

# -------------------- LAYOUT NAMES --------------------
LAYOUT_NAMES = ["Grid", "Radial", "Organic", "Linear", "Polycentric", "Superblock", "Hybrid"]

# -------------------- ROAD TOPOLOGY PRESETS --------------------
ROAD_TOPOLOGY_NAMES = ["From Layout", "Linear", "Parallel", "Cross", "T-Junction", "Loop"]

# -------------------- SWAP RULE OPTIONS --------------------
SWAP_RULE_OPTIONS = [
    ("PARETO (no one worse)", "pareto"),
    ("GREEDY TOTAL (u1+u2 up)", "greedy_total"),
    ("GREEDY BOTH (both up)", "greedy_both"),
    ("GREEDY 1-SIDED (a1 up)", "greedy_1"),
]

# -------------------- TOOLS (label, attractor_key) --------------------
TOOLS = [
    ("VIEW", "None"),
    ("ROAD", "R"),
    ("PARK", "P"),
    ("TRANS", "T"),
    ("WATER", "W"),
    ("SCHOOL", "S"),
    ("HEALTH", "H"),
    ("GOV", "G"),
]
