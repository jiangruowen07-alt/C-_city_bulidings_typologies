# CITY LAB - C# Export for Grasshopper
# Exports game logic as C# code for Rhino/Grasshopper analysis


def _escape_cs_string(s):
    return s.replace("\\", "\\\\").replace('"', '\\"')


def _dict_to_cs(d, indent=2):
    """Convert Python dict to C# Dictionary initializer."""
    lines = []
    for k, v in d.items():
        if isinstance(v, dict):
            inner = _dict_to_cs(v, indent + 2)
            lines.append(f'{{"{_escape_cs_string(k)}", {inner}}}')
        elif isinstance(v, (int, float)):
            lines.append(f'{{"{_escape_cs_string(k)}", {v}}}')
        else:
            lines.append(f'{{"{_escape_cs_string(k)}", "{_escape_cs_string(str(v))}"}}')
    return "new Dictionary<string, object> {\n" + ",\n".join("  " * indent + l for l in lines) + "\n" + "  " * (indent - 1) + "}"


def export_to_csharp(model) -> str:
    """
    Export CityModel game logic as C# code for Grasshopper C# component.
    Returns a complete C# script that can be pasted into Grasshopper.
    """
    w, h = model.w, model.h
    reach = model.reach

    # Serialize attractors
    attr_str = "var attractors = new Dictionary<string, List<Tuple<int,int>>> {\n"
    for k in model.attr_keys:
        pts = model.attractors.get(k, [])
        pts_str = ", ".join(f"Tuple.Create({x},{y})" for x, y in pts)
        attr_str += f'  {{"{k}", new List<Tuple<int,int>> {{ {pts_str} }}}},\n'
    attr_str += "};\n"

    # Serialize pref_attr
    pa_lines = []
    for t in model.type_labels:
        inner = ", ".join(f'{{"{k}", {model.pref_attr[t].get(k, 0.0)}}}' for k in model.attr_keys)
        pa_lines.append(f'  {{"{t}", new Dictionary<string, double> {{ {inner} }}}}')
    pref_attr_str = "var prefAttr = new Dictionary<string, Dictionary<string, double>> {\n" + ",\n".join(pa_lines) + "\n};\n"

    # Serialize pref_agent
    pg_lines = []
    for t in model.type_labels:
        inner = ", ".join(f'{{"{tt}", {model.pref_agent[t].get(tt, 0.0)}}}' for tt in model.type_labels)
        pg_lines.append(f'  {{"{t}", new Dictionary<string, double> {{ {inner} }}}}')
    pref_agent_str = "var prefAgent = new Dictionary<string, Dictionary<string, double>> {\n" + ",\n".join(pg_lines) + "\n};\n"

    # Serialize agents
    agents_str = "var agents = new List<Agent> {\n"
    for a in model.agents:
        agents_str += f'  new Agent("{a.type}", {a.x}, {a.y}),\n'
    agents_str += "};\n"

    # Serialize influence range matrices
    inf_attr_lines = []
    for k in model.attr_keys:
        inner = ", ".join(f'{{"{t}", {model.influence_range_attr.get(k, {}).get(t, reach)}}}' for t in model.type_labels)
        inf_attr_lines.append(f'  {{"{k}", new Dictionary<string, int> {{ {inner} }}}}')
    influence_attr_str = "var influenceRangeAttr = new Dictionary<string, Dictionary<string, int>> {\n" + ",\n".join(inf_attr_lines) + "\n};\n"

    inf_agent_lines = []
    for s in model.type_labels:
        inner = ", ".join(f'{{"{t}", {model.influence_range_agent.get(s, {}).get(t, reach)}}}' for t in model.type_labels)
        inf_agent_lines.append(f'  {{"{s}", new Dictionary<string, int> {{ {inner} }}}}')
    influence_agent_str = "var influenceRangeAgent = new Dictionary<string, Dictionary<string, int>> {\n" + ",\n".join(inf_agent_lines) + "\n};\n"

    code = '''// CITY LAB - Game Logic for Grasshopper (Rhino)
// Generated C# script - paste into Grasshopper C# component
// Inputs: W (int), H (int), Steps (int), SwapMode (string)
// Outputs: A=agents as points, B=agent types, C=total utility, D=stats

using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

// --- Agent ---
public class Agent {
  public string Type;
  public int X, Y;
  public Agent(string type, int x, int y) { Type = type; X = x; Y = y; }
  public Point3d ToPoint(double scale) { return new Point3d(X * scale, Y * scale, 0); }
}

// --- CityModel (core logic) ---
public class CityModelGH {
  public int W, H, Reach;
  public Dictionary<string, List<Tuple<int,int>>> Attractors;
  public Dictionary<string, Dictionary<string, double>> PrefAttr;
  public Dictionary<string, Dictionary<string, double>> PrefAgent;
  public Dictionary<string, Dictionary<string, int>> InfluenceRangeAttr, InfluenceRangeAgent;
  public List<Agent> Agents;
  public List<string> AttrKeys;
  public Agent[,] Grid;
  public double[][][] DistAttr;
  public string[,] AttrGrid;
  public string SwapMode = "pareto";
  public double TotalUtility;
  public int Steps, Accepted, Rejected, Sacrificed;
  static Random _rnd = new Random();

  public CityModelGH(int w, int h, int reach,
    Dictionary<string, List<Tuple<int,int>>> attractors,
    Dictionary<string, Dictionary<string, double>> prefAttr,
    Dictionary<string, Dictionary<string, double>> prefAgent,
    Dictionary<string, Dictionary<string, int>> influenceRangeAttr,
    Dictionary<string, Dictionary<string, int>> influenceRangeAgent,
    List<Agent> agents,
    string swapMode,
    List<string> attrKeys) {
    W = w; H = h; Reach = reach;
    Attractors = attractors;
    PrefAttr = prefAttr;
    PrefAgent = prefAgent;
    InfluenceRangeAttr = influenceRangeAttr;
    InfluenceRangeAgent = influenceRangeAgent;
    Agents = agents;
    AttrKeys = attrKeys ?? attractors.Keys.ToList();
    SwapMode = swapMode;
    Grid = new Agent[w, h];
    DistAttr = new double[AttrKeys.Count][][];
    AttrGrid = new string[w, h];
    RebuildDistanceFields();
    UpdateGrid();
    CalcTotalUtility();
  }

  bool IsAttr(int x, int y) { return x >= 0 && x < W && y >= 0 && y < H && AttrGrid[x, y] != null; }

  void UpdateGrid() {
    for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) Grid[i, j] = null;
    foreach (var a in Agents) if (a.X >= 0 && a.X < W && a.Y >= 0 && a.Y < H) Grid[a.X, a.Y] = a;
  }

  void RebuildDistanceFields() {
    for (int ki = 0; ki < AttrKeys.Count; ki++) {
      var k = AttrKeys[ki];
      if (!Attractors.ContainsKey(k)) continue;
      var dist = new double[W][];
      for (int i = 0; i < W; i++) { dist[i] = new double[H]; for (int j = 0; j < H; j++) dist[i][j] = 1e9; }
      var q = new Queue<Tuple<int,int>>();
      foreach (var p in Attractors[k]) {
        int ax = p.Item1, ay = p.Item2;
        if (ax >= 0 && ax < W && ay >= 0 && ay < H) { dist[ax][ay] = 0; q.Enqueue(p); }
      }
      while (q.Count > 0) {
        var t = q.Dequeue();
        int x = t.Item1, y = t.Item2;
        double nd = dist[x][y] + 1;
        if (x > 0 && nd < dist[x-1][y]) { dist[x-1][y] = nd; q.Enqueue(Tuple.Create(x-1, y)); }
        if (x < W-1 && nd < dist[x+1][y]) { dist[x+1][y] = nd; q.Enqueue(Tuple.Create(x+1, y)); }
        if (y > 0 && nd < dist[x][y-1]) { dist[x][y-1] = nd; q.Enqueue(Tuple.Create(x, y-1)); }
        if (y < H-1 && nd < dist[x][y+1]) { dist[x][y+1] = nd; q.Enqueue(Tuple.Create(x, y+1)); }
      }
      DistAttr[ki] = dist;
    }
    for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) AttrGrid[i, j] = null;
    foreach (var kv in Attractors)
      foreach (var p in kv.Value)
        if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
          AttrGrid[p.Item1, p.Item2] = kv.Key;
  }

  double GetUtility(Agent agent, int x, int y) {
    var pa = PrefAttr.ContainsKey(agent.Type) ? PrefAttr[agent.Type] : new Dictionary<string, double>();
    var pg = PrefAgent.ContainsKey(agent.Type) ? PrefAgent[agent.Type] : new Dictionary<string, double>();
    string myType = agent.Type;
    double u = 0;
    for (int ki = 0; ki < AttrKeys.Count; ki++) {
      var k = AttrKeys[ki];
      double md = DistAttr[ki][x][y];
      int rAttr = (InfluenceRangeAttr.ContainsKey(k) && InfluenceRangeAttr[k].ContainsKey(myType)) ? InfluenceRangeAttr[k][myType] : Reach;
      if (md <= rAttr && pa.ContainsKey(k)) u += pa[k] / Math.Max(md, 1);
    }
    foreach (var nb in Agents) {
      if (nb.X == x && nb.Y == y) continue;
      int dist = Math.Abs(nb.X - x) + Math.Abs(nb.Y - y);
      int rAgent = (InfluenceRangeAgent.ContainsKey(nb.Type) && InfluenceRangeAgent[nb.Type].ContainsKey(myType)) ? InfluenceRangeAgent[nb.Type][myType] : Reach;
      if (dist <= rAgent && pg.ContainsKey(nb.Type)) u += pg[nb.Type] / Math.Max(dist, 1);
    }
    return u;
  }

  public bool Step() {
    Steps++;
    if (Agents.Count < 2) return false;
    int i1 = _rnd.Next(Agents.Count);
    int i2 = _rnd.Next(Agents.Count - 1);
    if (i2 >= i1) i2++;
    var a1 = Agents[i1]; var a2 = Agents[i2];
    if (IsAttr(a1.X, a1.Y)) { Agents.Remove(a1); UpdateGrid(); return false; }
    if (IsAttr(a2.X, a2.Y)) { Agents.Remove(a2); UpdateGrid(); return false; }
    double u1o = GetUtility(a1, a1.X, a1.Y), u2o = GetUtility(a2, a2.X, a2.Y);
    double u1n = GetUtility(a1, a2.X, a2.Y), u2n = GetUtility(a2, a1.X, a1.Y);
    bool accept = false;
    if (SwapMode == "pareto") accept = (u1n >= u1o && u2n >= u2o) && (u1n > u1o || u2n > u2o);
    else if (SwapMode == "greedy_total") accept = (u1n + u2n) > (u1o + u2o);
    else if (SwapMode == "greedy_both") accept = u1n > u1o && u2n > u2o;
    else if (SwapMode == "greedy_1") accept = u1n > u1o;
    else accept = (u1n >= u1o && u2n >= u2o) && (u1n > u1o || u2n > u2o);
    if (accept) {
      int x1 = a1.X, y1 = a1.Y, x2 = a2.X, y2 = a2.Y;
      a1.X = x2; a1.Y = y2; a2.X = x1; a2.Y = y1;
      Grid[x1, y1] = a2; Grid[x2, y2] = a1;
      Accepted++;
      CalcTotalUtility();
      return true;
    }
    Rejected++;
    return false;
  }

  public void RunSteps(int n) { for (int i = 0; i < n; i++) Step(); }

  void CalcTotalUtility() {
    TotalUtility = 0;
    foreach (var a in Agents) TotalUtility += GetUtility(a, a.X, a.Y);
  }

  public List<Point3d> GetAgentPoints(double scale) {
    var pts = new List<Point3d>();
    foreach (var a in Agents) pts.Add(a.ToPoint(scale));
    return pts;
  }
  public List<string> GetAgentTypes() { return Agents.Select(a => a.Type).ToList(); }
}

// --- Grasshopper script body ---
// Inputs: add params W(int), H(int), Steps(int), SwapMode(str) to C# component, or use defaults below
int w = ''' + str(w) + ''';
int h = ''' + str(h) + ''';
int steps = 500;
string swapMode = "''' + model.swap_mode + '''";
double scale = 1.0;
// Uncomment to use Grasshopper inputs:
// if (x != null) w = (int)(double)x;
// if (y != null) h = (int)(double)y;
// if (z != null) steps = (int)(double)z;

''' + attr_str + pref_attr_str + pref_agent_str + influence_attr_str + influence_agent_str + agents_str + '''
var attrKeys = new List<string> { ''' + ", ".join(f'"{k}"' for k in model.attr_keys) + ''' };

var model = new CityModelGH(w, h, ''' + str(reach) + ''', attractors, prefAttr, prefAgent, influenceRangeAttr, influenceRangeAgent, agents, swapMode, attrKeys);
model.RunSteps(steps);

A = model.GetAgentPoints(scale);
B = model.GetAgentTypes();
C = model.TotalUtility;
D = string.Format("Steps={0} Accepted={1} Rejected={2} Utility={3:F1}", model.Steps, model.Accepted, model.Rejected, model.TotalUtility);
'''
    return code


def export_core_logic_only() -> str:
    """
    Export only the core algorithm (Agent, CityModelGH, GetUtility, Step)
    without runtime data - for use as a reusable class library in Grasshopper.
    """
    return '''// CITY LAB - Core Logic Only (no runtime data)
// Paste into Grasshopper C# component as a reference / class definition
// Use: create CityModelGH with your attractors, prefs, agents; call Step() or RunSteps(n)

using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;

public class Agent {
  public string Type;
  public int X, Y;
  public Agent(string type, int x, int y) { Type = type; X = x; Y = y; }
  public Point3d ToPoint(double scale) { return new Point3d(X * scale, Y * scale, 0); }
}

public class CityModelGH {
  public int W, H, Reach;
  public Dictionary<string, List<Tuple<int,int>>> Attractors;
  public Dictionary<string, Dictionary<string, double>> PrefAttr;
  public Dictionary<string, Dictionary<string, double>> PrefAgent;
  public Dictionary<string, Dictionary<string, int>> InfluenceRangeAttr, InfluenceRangeAgent;
  public List<Agent> Agents;
  public Agent[,] Grid;
  public double[][][] DistAttr;
  public string[,] AttrGrid;
  public string SwapMode = "pareto";
  public double TotalUtility;
  public int Steps, Accepted, Rejected, Sacrificed;
  static Random _rnd = new Random();

  public CityModelGH(int w, int h, int reach,
    Dictionary<string, List<Tuple<int,int>>> attractors,
    Dictionary<string, Dictionary<string, double>> prefAttr,
    Dictionary<string, Dictionary<string, double>> prefAgent,
    Dictionary<string, Dictionary<string, int>> influenceRangeAttr,
    Dictionary<string, Dictionary<string, int>> influenceRangeAgent,
    List<Agent> agents,
    string swapMode) {
    W = w; H = h; Reach = reach;
    Attractors = attractors;
    PrefAttr = prefAttr;
    PrefAgent = prefAgent;
    InfluenceRangeAttr = influenceRangeAttr;
    InfluenceRangeAgent = influenceRangeAgent;
    Agents = agents;
    SwapMode = swapMode;
    Grid = new Agent[w, h];
    DistAttr = new double[attractors.Count][][];
    AttrGrid = new string[w, h];
    RebuildDistanceFields();
    UpdateGrid();
    CalcTotalUtility();
  }

  bool IsAttr(int x, int y) { return x >= 0 && x < W && y >= 0 && y < H && AttrGrid[x, y] != null; }

  void UpdateGrid() {
    for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) Grid[i, j] = null;
    foreach (var a in Agents) if (a.X >= 0 && a.X < W && a.Y >= 0 && a.Y < H) Grid[a.X, a.Y] = a;
  }

  void RebuildDistanceFields() {
    var attrKeys = Attractors.Keys.ToList();
    for (int ki = 0; ki < attrKeys.Count; ki++) {
      var k = attrKeys[ki];
      var dist = new double[W][];
      for (int i = 0; i < W; i++) { dist[i] = new double[H]; for (int j = 0; j < H; j++) dist[i][j] = 1e9; }
      var q = new Queue<Tuple<int,int>>();
      foreach (var p in Attractors[k]) {
        int ax = p.Item1, ay = p.Item2;
        if (ax >= 0 && ax < W && ay >= 0 && ay < H) { dist[ax][ay] = 0; q.Enqueue(p); }
      }
      while (q.Count > 0) {
        var t = q.Dequeue();
        int x = t.Item1, y = t.Item2;
        double nd = dist[x][y] + 1;
        if (x > 0 && nd < dist[x-1][y]) { dist[x-1][y] = nd; q.Enqueue(Tuple.Create(x-1, y)); }
        if (x < W-1 && nd < dist[x+1][y]) { dist[x+1][y] = nd; q.Enqueue(Tuple.Create(x+1, y)); }
        if (y > 0 && nd < dist[x][y-1]) { dist[x][y-1] = nd; q.Enqueue(Tuple.Create(x, y-1)); }
        if (y < H-1 && nd < dist[x][y+1]) { dist[x][y+1] = nd; q.Enqueue(Tuple.Create(x, y+1)); }
      }
      DistAttr[ki] = dist;
    }
    for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) AttrGrid[i, j] = null;
    foreach (var kv in Attractors)
      foreach (var p in kv.Value)
        if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
          AttrGrid[p.Item1, p.Item2] = kv.Key;
  }

  public double GetUtility(Agent agent, int x, int y) {
    var pa = PrefAttr.ContainsKey(agent.Type) ? PrefAttr[agent.Type] : new Dictionary<string, double>();
    var pg = PrefAgent.ContainsKey(agent.Type) ? PrefAgent[agent.Type] : new Dictionary<string, double>();
    string myType = agent.Type;
    double u = 0;
    var attrKeys = Attractors.Keys.ToList();
    for (int ki = 0; ki < attrKeys.Count; ki++) {
      var k = attrKeys[ki];
      double md = DistAttr[ki][x][y];
      int rAttr = (InfluenceRangeAttr.ContainsKey(k) && InfluenceRangeAttr[k].ContainsKey(myType)) ? InfluenceRangeAttr[k][myType] : Reach;
      if (md <= rAttr && pa.ContainsKey(k)) u += pa[k] / Math.Max(md, 1);
    }
    foreach (var nb in Agents) {
      if (nb.X == x && nb.Y == y) continue;
      int dist = Math.Abs(nb.X - x) + Math.Abs(nb.Y - y);
      int rAgent = (InfluenceRangeAgent.ContainsKey(nb.Type) && InfluenceRangeAgent[nb.Type].ContainsKey(myType)) ? InfluenceRangeAgent[nb.Type][myType] : Reach;
      if (dist <= rAgent && pg.ContainsKey(nb.Type)) u += pg[nb.Type] / Math.Max(dist, 1);
    }
    return u;
  }

  public bool Step() {
    Steps++;
    if (Agents.Count < 2) return false;
    int i1 = _rnd.Next(Agents.Count);
    int i2 = _rnd.Next(Agents.Count - 1);
    if (i2 >= i1) i2++;
    var a1 = Agents[i1]; var a2 = Agents[i2];
    if (IsAttr(a1.X, a1.Y)) { Agents.Remove(a1); UpdateGrid(); return false; }
    if (IsAttr(a2.X, a2.Y)) { Agents.Remove(a2); UpdateGrid(); return false; }
    double u1o = GetUtility(a1, a1.X, a1.Y), u2o = GetUtility(a2, a2.X, a2.Y);
    double u1n = GetUtility(a1, a2.X, a2.Y), u2n = GetUtility(a2, a1.X, a1.Y);
    bool accept = false;
    if (SwapMode == "pareto") accept = (u1n >= u1o && u2n >= u2o) && (u1n > u1o || u2n > u2o);
    else if (SwapMode == "greedy_total") accept = (u1n + u2n) > (u1o + u2o);
    else if (SwapMode == "greedy_both") accept = u1n > u1o && u2n > u2o;
    else if (SwapMode == "greedy_1") accept = u1n > u1o;
    else accept = (u1n >= u1o && u2n >= u2o) && (u1n > u1o || u2n > u2o);
    if (accept) {
      int x1 = a1.X, y1 = a1.Y, x2 = a2.X, y2 = a2.Y;
      a1.X = x2; a1.Y = y2; a2.X = x1; a2.Y = y1;
      Grid[x1, y1] = a2; Grid[x2, y2] = a1;
      Accepted++;
      CalcTotalUtility();
      return true;
    }
    Rejected++;
    return false;
  }

  public void RunSteps(int n) { for (int i = 0; i < n; i++) Step(); }

  void CalcTotalUtility() {
    TotalUtility = 0;
    foreach (var a in Agents) TotalUtility += GetUtility(a, a.X, a.Y);
  }

  public List<Point3d> GetAgentPoints(double scale) {
    var pts = new List<Point3d>();
    foreach (var a in Agents) pts.Add(a.ToPoint(scale));
    return pts;
  }
  public List<string> GetAgentTypes() { return Agents.Select(a => a.Type).ToList(); }
}
'''
