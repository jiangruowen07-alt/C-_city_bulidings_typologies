// CITY LAB v1.5 - Simulation model

using System;
using System.Collections.Generic;
using System.Linq;

namespace CityLab;

public sealed class Agent
{
    public string Type;
    public int X, Y;
    /// <summary>与 <see cref="BuildingShapeCatalog"/> 中该代理映射功能类下 10 种预形对应，0..9。</summary>
    public int BuildingPatternSlot;

    public Agent(string type, int x, int y)
    {
        Type = type;
        X = x;
        Y = y;
    }
}

public sealed class SimulationStats
{
    public int Steps;
    public double RawUtility;
    public double TotalUtility;
    public int Accepted;
    public int Rejected;
    public int Sacrificed;
}

public readonly record struct StepResult(
    bool Swapped,
    Agent? A1,
    Agent? A2,
    (int x, int y)? Old1,
    (int x, int y)? Old2,
    IReadOnlyList<Agent> Removed);

public sealed class CityModel
{
    private const int InfDist = 1_000_000_000;
    private static readonly string[] StandardAttrKeys = ["T", "P", "R", "W", "S", "H", "G"];

    public int W { get; }
    public int H { get; }
    public int Reach { get; set; }
    public bool UseOriginalCitylab { get; private set; }

    public List<string> AttrKeys { get; private set; } = [];
    public Dictionary<string, List<(int x, int y)>> Attractors { get; private set; } = new();
    public List<string> TypeLabels { get; private set; } = [];
    public List<Agent> Agents { get; } = [];
    public Agent?[,] Grid { get; private set; }
    public SimulationStats Stats { get; } = new();
    public double UtilityBias;
    public string SwapMode { get; set; } = "pareto";
    public string CurrentLayout { get; private set; } = "Grid";
    public string RoadTopology { get; set; } = Config.RoadTopologyNames[0];

    /// <summary>为 true 时，RESET/初始代理仅从建筑四类 Resi/Firm/Shop/Cafe 中抽取（仅标准城市场景）。</summary>
    public bool BuildingFourAgentMode { get; set; }

    public Dictionary<string, int> ReachAgentByType { get; } = new();
    public Dictionary<string, int> ReachAttrByType { get; } = new();
    public Dictionary<string, Dictionary<string, int>> InfluenceRangeAttr { get; } = new();
    public Dictionary<string, Dictionary<string, int>> InfluenceRangeAgent { get; } = new();
    public Dictionary<string, Dictionary<string, double>> PrefAttr { get; } = new();
    public Dictionary<string, Dictionary<string, double>> PrefAgent { get; } = new();

    public Dictionary<string, int[,]> DistAttr { get; } = new();
    public string?[,] AttrGrid { get; private set; }

    private readonly Random _rng;

    public CityModel(int w = 40, int h = 40, int reach = 6, Random? rng = null)
    {
        W = w;
        H = h;
        Reach = reach;
        _rng = rng ?? new Random();
        Grid = new Agent[w, h];
        AttrGrid = new string[w, h];
        UseOriginalCitylab = false;

        AttrKeys = Config.AttrDefs.Select(t => t.Key).ToList();
        foreach (var k in AttrKeys)
            Attractors[k] = [];
        TypeLabels = [..Config.TypeLabels];

        foreach (var t in TypeLabels)
        {
            ReachAgentByType[t] = reach;
            ReachAttrByType[t] = reach;
        }

        foreach (var k in AttrKeys)
        {
            InfluenceRangeAttr[k] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAttr[k][t] = reach;
        }
        foreach (var s in TypeLabels)
        {
            InfluenceRangeAgent[s] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAgent[s][t] = reach;
        }
        var transportKey = UseOriginalCitylab ? "transport" : "T";
        foreach (var t in TypeLabels)
        {
            if (InfluenceRangeAttr.TryGetValue(transportKey, out var row))
                row[t] = 20;
        }
        EnsureInfluenceTablesComplete();

        foreach (var t in TypeLabels)
        {
            PrefAttr[t] = new Dictionary<string, double>(Config.DefaultPrefAttr.GetValueOrDefault(t) ?? new Dictionary<string, double>());
            PrefAgent[t] = new Dictionary<string, double>(Config.DefaultPrefAgent.GetValueOrDefault(t) ?? new Dictionary<string, double>());
        }
        EnsurePrefTablesComplete();

        foreach (var k in AttrKeys)
            DistAttr[k] = new int[W, H];
    }

    private void EnsurePrefTablesComplete()
    {
        foreach (var t in TypeLabels)
        {
            PrefAttr.TryAdd(t, new Dictionary<string, double>());
            foreach (var k in AttrKeys)
                PrefAttr[t].TryAdd(k, 0.0);
        }
        foreach (var t in TypeLabels)
        {
            PrefAgent.TryAdd(t, new Dictionary<string, double>());
            foreach (var tt in TypeLabels)
                PrefAgent[t].TryAdd(tt, 0.0);
        }
        foreach (var t in TypeLabels)
        {
            var v = Math.Max(1, ReachAgentByType.GetValueOrDefault(t, Reach));
            ReachAgentByType[t] = v;
        }
        foreach (var t in TypeLabels)
        {
            var v = Math.Max(1, ReachAttrByType.GetValueOrDefault(t, Reach));
            ReachAttrByType[t] = v;
        }
    }

    private void EnsureInfluenceTablesComplete()
    {
        foreach (var k in AttrKeys)
        {
            InfluenceRangeAttr.TryAdd(k, new Dictionary<string, int>());
            foreach (var t in TypeLabels)
            {
                var v = InfluenceRangeAttr[k].GetValueOrDefault(t, Reach);
                InfluenceRangeAttr[k][t] = Math.Max(1, v);
            }
        }
        foreach (var s in TypeLabels)
        {
            InfluenceRangeAgent.TryAdd(s, new Dictionary<string, int>());
            foreach (var t in TypeLabels)
            {
                var v = InfluenceRangeAgent[s].GetValueOrDefault(t, Reach);
                InfluenceRangeAgent[s][t] = Math.Max(1, v);
            }
        }
    }

    public IReadOnlyList<string> GetAttrDrawOrder()
    {
        var roadKey = UseOriginalCitylab ? "road" : "R";
        var counts = AttrKeys.ToDictionary(
            k => k,
            k => Attractors.GetValueOrDefault(k)?.Count(p => p.x >= 0 && p.x < W && p.y >= 0 && p.y < H) ?? 0);
        var others = AttrKeys.Where(k => k != roadKey).OrderByDescending(k => counts.GetValueOrDefault(k, 0)).ToList();
        var result = new List<string> { roadKey };
        result.AddRange(others);
        return result;
    }

    public IReadOnlyList<Agent> RebuildAttrDistanceFields()
    {
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                AttrGrid[x, y] = null;

        var drawOrder = GetAttrDrawOrder();
        foreach (var k in drawOrder)
        {
            foreach (var (ax, ay) in Attractors.GetValueOrDefault(k) ?? [])
            {
                if (ax >= 0 && ax < W && ay >= 0 && ay < H)
                    AttrGrid[ax, ay] = k;
            }
        }

        foreach (var k in AttrKeys)
        {
            var dist = new int[W, H];
            for (var x = 0; x < W; x++)
                for (var y = 0; y < H; y++)
                    dist[x, y] = InfDist;
            var q = new Queue<(int x, int y)>();
            foreach (var (ax, ay) in Attractors.GetValueOrDefault(k) ?? [])
            {
                if (ax < 0 || ax >= W || ay < 0 || ay >= H) continue;
                dist[ax, ay] = 0;
                q.Enqueue((ax, ay));
            }
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                var nd = dist[x, y] + 1;
                if (x > 0 && nd < dist[x - 1, y]) { dist[x - 1, y] = nd; q.Enqueue((x - 1, y)); }
                if (x < W - 1 && nd < dist[x + 1, y]) { dist[x + 1, y] = nd; q.Enqueue((x + 1, y)); }
                if (y > 0 && nd < dist[x, y - 1]) { dist[x, y - 1] = nd; q.Enqueue((x, y - 1)); }
                if (y < H - 1 && nd < dist[x, y + 1]) { dist[x, y + 1] = nd; q.Enqueue((x, y + 1)); }
            }
            DistAttr[k] = dist;
        }
        return RemoveAgentsOnAttractors();
    }

    /// <summary>Remove agents that stand on attractor cells (rebuild / click).</summary>
    public IReadOnlyList<Agent> RemoveAgentsOnAttractors()
    {
        var removed = new List<Agent>();
        foreach (var a in Agents.ToList())
        {
            if (IsAttr(a.X, a.Y))
            {
                Agents.Remove(a);
                removed.Add(a);
            }
        }
        if (removed.Count > 0)
            UpdateGrid();
        return removed;
    }

    public void Clear()
    {
        foreach (var k in AttrKeys)
            Attractors[k] = [];
        RebuildAttrDistanceFields();
        Reset();
    }

    public void SwitchToOriginalCitylab()
    {
        UseOriginalCitylab = true;
        AttrKeys = Config.OriginalCitylabAttrDefs.Select(t => t.Key).ToList();
        TypeLabels = [..Config.OriginalCitylabTypeLabels];
        foreach (var k in AttrKeys)
            Attractors[k] = [];
        foreach (var t in TypeLabels)
        {
            PrefAttr[t] = new Dictionary<string, double>(Config.OriginalCitylabDefaultPrefAttr.GetValueOrDefault(t) ?? new Dictionary<string, double>());
            PrefAgent[t] = new Dictionary<string, double>(Config.OriginalCitylabDefaultPrefAgent.GetValueOrDefault(t) ?? new Dictionary<string, double>());
        }
        EnsurePrefTablesComplete();
        foreach (var k in AttrKeys)
        {
            InfluenceRangeAttr[k] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAttr[k][t] = Reach;
        }
        foreach (var s in TypeLabels)
        {
            InfluenceRangeAgent[s] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAgent[s][t] = Reach;
        }
        foreach (var t in TypeLabels)
            InfluenceRangeAttr["transport"][t] = 20;
        EnsureInfluenceTablesComplete();
        DistAttr.Clear();
        foreach (var k in AttrKeys)
            DistAttr[k] = new int[W, H];
        ApplyLayout(CurrentLayout);
    }

    public void SwitchToStandard()
    {
        UseOriginalCitylab = false;
        AttrKeys = Config.AttrDefs.Select(t => t.Key).ToList();
        TypeLabels = [..Config.TypeLabels];
        foreach (var k in AttrKeys)
            Attractors[k] = [];
        foreach (var t in TypeLabels)
        {
            PrefAttr[t] = new Dictionary<string, double>(Config.DefaultPrefAttr.GetValueOrDefault(t) ?? new Dictionary<string, double>());
            PrefAgent[t] = new Dictionary<string, double>(Config.DefaultPrefAgent.GetValueOrDefault(t) ?? new Dictionary<string, double>());
        }
        EnsurePrefTablesComplete();
        foreach (var k in AttrKeys)
        {
            InfluenceRangeAttr[k] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAttr[k][t] = Reach;
        }
        foreach (var s in TypeLabels)
        {
            InfluenceRangeAgent[s] = new Dictionary<string, int>();
            foreach (var t in TypeLabels)
                InfluenceRangeAgent[s][t] = Reach;
        }
        foreach (var t in TypeLabels)
            InfluenceRangeAttr["T"][t] = 20;
        EnsureInfluenceTablesComplete();
        DistAttr.Clear();
        foreach (var k in AttrKeys)
            DistAttr[k] = new int[W, H];
        ApplyLayout(CurrentLayout);
    }

    public bool IsAttr(int x, int y) => AttrGrid[x, y] != null;

    public void UpdateGrid()
    {
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Grid[x, y] = null;
        foreach (var a in Agents)
            Grid[a.X, a.Y] = a;
    }

    /// <summary>与建筑格网功能对齐：酒店→居、餐→咖、诊→公；四基类不变。不含绿地 (P) 在代理层。</summary>
    public static string MapStandardTypeToBuildingFour(string t) => t switch
    {
        "Resi" => "Resi",
        "Firm" => "Firm",
        "Shop" => "Shop",
        "Cafe" => "Cafe",
        "Hotel" => "Resi",
        "Restaurant" => "Cafe",
        "Clinic" => "Firm",
        _ => "Resi"
    };

    /// <summary>与建筑场「黑白/配色」及主类型条一致，用于与 BuildingModel 对照显示。</summary>
    public static string MapAgentTypeToBuildingFunctionKey(string t, bool useOriginalCitylab) =>
        useOriginalCitylab
            ? t switch
            {
                "residential" => "Resi",
                "office" => "Firm",
                "shop" => "Shop",
                "cafe" => "Cafe",
                _ => "Resi"
            }
            : MapStandardTypeToBuildingFour(t);

    public void RemapAgentsToBuildingFunctionTypes()
    {
        foreach (var a in Agents)
        {
            if (UseOriginalCitylab)
            {
                a.Type = a.Type switch
                {
                    "residential" => "residential",
                    "office" => "office",
                    "shop" => "shop",
                    "cafe" => "cafe",
                    _ => "residential"
                };
            }
            else
            {
                a.Type = MapStandardTypeToBuildingFour(a.Type);
            }
        }
        UpdateGrid();
        CalcTotalUtility();
    }

    /// <summary>切比雪夫邻域半径，用于 <see cref="ComputeCommercialDensityAt"/>。</summary>
    public int CommercialDensityHoodRadius { get; set; } = 4;

    /// <summary>
    /// 商业密度 ∈ [0,1]：邻域内 Shop 代理占比（半权）与邻域内各代理 3×3 图案黑格数/9 的均值（半权）混合；后者将「小网格黑格」并入。
    /// </summary>
    public double ComputeCommercialDensityAt(int cx, int cy, Func<Agent, int> blackCellsInAgentPattern)
    {
        var hood = 0;
        var shop = 0;
        var blackSum = 0;
        var r = CommercialDensityHoodRadius;
        foreach (var o in Agents)
        {
            if (Math.Max(Math.Abs(o.X - cx), Math.Abs(o.Y - cy)) > r) continue;
            hood++;
            if (MapAgentTypeToBuildingFunctionKey(o.Type, UseOriginalCitylab) == "Shop")
                shop++;
            blackSum += blackCellsInAgentPattern(o);
        }
        if (hood == 0) return 0.0;
        var shopPart = shop / (double)hood;
        var blackPart = (blackSum / 9.0) / hood;
        return 0.5 * shopPart + 0.5 * blackPart;
    }

    private string RandomAgentType()
    {
        if (BuildingFourAgentMode && !UseOriginalCitylab)
        {
            return BuildingModel.BuildingFunctionTypes[_rng.Next(BuildingModel.BuildingFunctionTypes.Length)];
        }
        var r = _rng.NextDouble();
        if (UseOriginalCitylab)
        {
            if (r < 0.55) return "residential";
            if (r < 0.75) return "office";
            if (r < 0.90) return "shop";
            return "cafe";
        }
        if (r < 0.55) return "Resi";
        if (r < 0.70) return "Firm";
        if (r < 0.82) return "Shop";
        if (r < 0.90) return "Cafe";
        if (r < 0.95) return "Hotel";
        if (r < 0.985) return "Restaurant";
        return "Clinic";
    }

    private HashSet<(int x, int y)> GetAttrCells()
    {
        var cells = new HashSet<(int, int)>();
        foreach (var lst in Attractors.Values)
        {
            foreach (var (ax, ay) in lst)
            {
                if (ax >= 0 && ax < W && ay >= 0 && ay < H)
                    cells.Add((ax, ay));
            }
        }
        return cells;
    }

    private void ResetAgentsOnce()
    {
        var attrCells = GetAttrCells();
        Agents.Clear();
        var slots = new List<(int x, int y)>();
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
            {
                if (!attrCells.Contains((x, y)))
                    slots.Add((x, y));
            }
        for (var i = slots.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }
        foreach (var (x, y) in slots)
        {
            if (attrCells.Contains((x, y)))
                continue;
            var ag = new Agent(RandomAgentType(), x, y);
            // 同类型也可因格点不同而初槽位不同
            var s = x * 11 + y * 17 + _rng.Next(0, 1_000_000);
            ag.BuildingPatternSlot = (s % BuildingShapeCatalog.PatternsPerMainType + BuildingShapeCatalog.PatternsPerMainType) % BuildingShapeCatalog.PatternsPerMainType;
            Agents.Add(ag);
        }
        Stats.Steps = 0;
        Stats.Accepted = 0;
        Stats.Rejected = 0;
        Stats.Sacrificed = 0;
        UpdateGrid();
        CalcTotalUtility();
    }

    public void LockTotalUtilityInt(int targetInt)
    {
        var raw = Stats.RawUtility;
        var bias = (double)targetInt - (int)raw;
        for (var i = 0; i < 6; i++)
        {
            var cur = (int)(raw + bias);
            if (cur == targetInt) break;
            bias += cur < targetInt ? 1.0 : -1.0;
        }
        UtilityBias = bias;
        Stats.TotalUtility = raw + UtilityBias;
    }

    public void Reset(int? targetInt = null)
    {
        ResetAgentsOnce();
        if (targetInt.HasValue)
            LockTotalUtilityInt(targetInt.Value);
    }

    public void ApplyLayout(string typ)
    {
        var layout = StandardAttrKeys.ToDictionary(k => k, _ => new List<(int, int)>());
        var cx = W / 2;
        var cy = H / 2;

        if (typ == "Grid")
        {
            for (var i = 0; i < W; i++)
                for (var j = 0; j < H; j++)
                    if (i % 8 == 0 || j % 8 == 0)
                        layout["R"]!.Add((i, j));
            var t1 = (Math.Max(1, Math.Min(W - 2, cx - 6)), Math.Max(1, Math.Min(H - 2, cy - 6)));
            var t2 = (Math.Max(1, Math.Min(W - 2, cx + 6)), Math.Max(1, Math.Min(H - 2, cy + 6)));
            var t3 = (Math.Max(1, Math.Min(W - 2, cx - 6)), Math.Max(1, Math.Min(H - 2, cy + 6)));
            var t4 = (Math.Max(1, Math.Min(W - 2, cx + 6)), Math.Max(1, Math.Min(H - 2, cy - 6)));
            layout["T"] = [t1, t2];
            var px1 = Math.Max(1, Math.Min(W - 2, cx - 6));
            var py1 = Math.Max(1, Math.Min(H - 2, cy - 6));
            var px2 = Math.Max(1, Math.Min(W - 2, cx + 6));
            var py2 = Math.Max(1, Math.Min(H - 2, cy + 6));
            layout["P"] = [(px1, py1), (px2, py2), t3];
            layout["S"] = [(Math.Max(0, cx - 8), cy), (Math.Min(W - 1, cx + 8), cy), t4];
            layout["H"] = [(cx, Math.Max(0, cy - 8)), (cx, Math.Min(H - 1, cy + 8))];
            for (var i = 0; i < W; i++)
                layout["W"]!.Add((i, H - 1));
            layout["G"] = [(cx, cy), (cx, Math.Max(0, cy - 10))];
        }
        else if (typ == "Radial")
        {
            var scale = Math.Min(W, H) / 40.0;
            var r1 = 8 * scale;
            var r2 = 10 * scale;
            var r3 = 14 * scale;
            var r4 = 16 * scale;
            var r5 = 18 * scale;
            var tol = Math.Max(0.5, 0.6 * scale);
            for (var i = 0; i < W; i++)
                for (var j = 0; j < H; j++)
                {
                    var d = Math.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                    if (Math.Abs(d - r1) < tol || Math.Abs(d - r4) < tol || Math.Abs(i - cx) < 0.6 || Math.Abs(j - cy) < 0.6)
                        layout["R"]!.Add((i, j));
                    if (d < Math.Max(2, 3 * scale))
                        layout["P"]!.Add((i, j));
                    if (Math.Abs(d - r2) < tol)
                        layout["S"]!.Add((i, j));
                    if (Math.Abs(d - r3) < tol)
                        layout["H"]!.Add((i, j));
                    if (Math.Abs(d - r5) < tol)
                        layout["W"]!.Add((i, j));
                }
            layout["T"] = [(cx, cy), (0, 0)];
            layout["G"] = [
                (cx, Math.Max(0, cy - 1)), (cx, Math.Min(H - 1, cy + 1)),
                (W - 1, H - 1), (0, H - 1)
            ];
            layout["S"]!.Add((W - 1, 0));
        }
        else if (typ == "Organic")
        {
            for (var t = 0; t < 10; t++)
            {
                var x = _rng.Next(W);
                var y = _rng.Next(H);
                for (var s = 0; s < 18; s++)
                {
                    layout["R"]!.Add((x, y));
                    x = Math.Max(0, Math.Min(W - 1, x + _rng.Next(3) - 1));
                    y = Math.Max(0, Math.Min(H - 1, y + _rng.Next(3) - 1));
                }
                layout["P"]!.Add((x, y));
            }
            for (var t = 0; t < 6; t++)
                layout["S"]!.Add((_rng.Next(W), _rng.Next(H)));
            for (var t = 0; t < 4; t++)
                layout["H"]!.Add((_rng.Next(W), _rng.Next(H)));
            for (var t = 0; t < 2; t++)
                layout["T"]!.Add((_rng.Next(W), _rng.Next(H)));
            for (var t = 0; t < 3; t++)
                layout["G"]!.Add((_rng.Next(W), _rng.Next(H)));
            var xw = _rng.Next(W);
            var yw = H - 2;
            for (var t = 0; t < W * 2; t++)
            {
                layout["W"]!.Add((xw, yw));
                xw = Math.Max(0, Math.Min(W - 1, xw + _rng.Next(3) - 1));
                yw = Math.Max(H / 2, Math.Min(H - 1, yw + _rng.Next(3) - 1));
            }
        }
        else if (typ == "Linear")
        {
            for (var i = 0; i < W; i++)
            {
                foreach (var yy in new[] { cy - 1, cy + 1 })
                    if (yy >= 0 && yy < H)
                        layout["R"]!.Add((i, yy));
                foreach (var yy in new[] { cy - 5, cy + 5 })
                    if (yy >= 0 && yy < H)
                        layout["P"]!.Add((i, yy));
                var sy = cy - 8 >= 0 && cy - 8 < H ? cy - 8 : Math.Max(0, cy - 2);
                var hy = cy + 8 >= 0 && cy + 8 < H ? cy + 8 : Math.Min(H - 1, cy + 2);
                if (i % 12 == 0)
                    layout["S"]!.Add((i, sy));
                if (i % 12 == 6)
                    layout["H"]!.Add((i, hy));
            }
            layout["T"] = [(0, cy), (W - 1, cy)];
            for (var i = 0; i < W; i++)
                layout["W"]!.Add((i, 0));
            layout["G"] = [(W / 4, cy), (W / 2, cy), (3 * W / 4, cy)];
        }
        else if (typ == "Polycentric")
        {
            (int hx, int hy)[] baseHubs = [(10, 10), (30, 10), (10, 30), (30, 30), (20, 20)];
            var hubs = baseHubs
                .Select(p => (Math.Max(1, Math.Min(W - 2, (int)(p.hx * W / 40.0))), Math.Max(1, Math.Min(H - 2, (int)(p.hy * H / 40.0)))))
                .ToArray();
            layout["T"] = [(0, 0), (W - 1, H - 1)];
            layout["G"] = [(0, cy), (W - 1, cy)];
            for (var idx = 0; idx < hubs.Length; idx++)
            {
                var (hx, hy) = hubs[idx];
                layout["G"]!.Add((hx, hy));
                for (var dx = -3; dx <= 3; dx++)
                {
                    foreach (var (xx, yy) in new[] { (hx + dx, hy - 3), (hx + dx, hy + 3), (hx - 3, hy + dx), (hx + 3, hy + dx) })
                    {
                        if (xx >= 0 && xx < W && yy >= 0 && yy < H)
                            layout["R"]!.Add((xx, yy));
                    }
                }
                if (hy + 1 < H) layout["P"]!.Add((hx, hy + 1));
                if (hy - 1 >= 0) layout["P"]!.Add((hx, hy - 1));
                if (idx % 2 == 0)
                    layout["S"]!.Add((hx, hy));
                else
                    layout["H"]!.Add((hx, hy));
            }
            for (var i = 0; i < W; i++)
                layout["W"]!.Add((i, H - 1));
        }
        else if (typ == "Superblock")
        {
            for (var i = 0; i < W; i++)
                for (var j = 0; j < H; j++)
                {
                    if (i % 14 == 0 || j % 14 == 0)
                        layout["R"]!.Add((i, j));
                    if ((i + 7) % 14 == 0 && (j + 7) % 14 == 0)
                    {
                        for (var di = -1; di <= 1; di++)
                            for (var dj = -1; dj <= 1; dj++)
                            {
                                var xx = i + di;
                                var yy = j + dj;
                                if (xx >= 0 && xx < W && yy >= 0 && yy < H)
                                    layout["P"]!.Add((xx, yy));
                            }
                    }
                }
            for (var i = 7; i < W; i += 14)
                for (var j = 7; j < H; j += 14)
                    layout["S"]!.Add((i, j));
            var sx1 = Math.Max(1, Math.Min(W - 2, cx - 7));
            var sy1 = Math.Max(1, Math.Min(H - 2, cy - 7));
            layout["S"]!.Add((sx1, cy));
            layout["S"]!.Add((cx, sy1));
            for (var i = 7; i < W; i += 28)
                for (var j = 7; j < H; j += 28)
                    layout["H"]!.Add((i + 3 < W ? i + 3 : i, j));
            var blockCenters = new List<(int, int)>();
            for (var i = 7; i < W; i += 14)
                for (var j = 7; j < H; j += 14)
                {
                    if (i < W && j < H)
                        blockCenters.Add((i, j));
                }
            layout["T"] = blockCenters.Count >= 2
                ? [blockCenters[0], blockCenters[1]]
                : [..blockCenters, (cx, cy)];
            for (var j = 0; j < H; j++)
                layout["W"]!.Add((W - 1, j));
            layout["G"] = [(cx, cy), (cx + 1 < W ? cx + 1 : cx, cy)];
            if (blockCenters.Count > 2) layout["G"]!.Add(blockCenters[2]);
            if (blockCenters.Count > 3) layout["G"]!.Add(blockCenters[3]);
            for (var bi = 4; bi < blockCenters.Count; bi++)
                layout["H"]!.Add(blockCenters[bi]);
        }
        else if (typ == "Hybrid")
        {
            for (var i = 0; i < W; i++)
                for (var j = 0; j < H; j++)
                {
                    if (i < cx)
                    {
                        if (_rng.NextDouble() < 0.05)
                            layout["R"]!.Add((i, j));
                    }
                    else
                    {
                        if (i % 6 == 0 || j % 6 == 0)
                            layout["R"]!.Add((i, j));
                    }
                }
            layout["T"] = [(cx, cy), (W - 1, cy)];
            for (var i = 0; i < Math.Min(W, H); i += 2)
                layout["P"]!.Add((i, i));
            layout["S"] = [
                (Math.Max(0, cx - 10), Math.Max(0, cy - 6)),
                (Math.Min(W - 1, cx + 6), Math.Min(H - 1, cy + 10))
            ];
            layout["H"] = [
                (Math.Max(0, cx - 6), Math.Min(H - 1, cy + 10)),
                (Math.Min(W - 1, cx + 10), Math.Max(0, cy - 6))
            ];
            for (var j = 0; j < H; j++)
                layout["W"]!.Add((0, j));
            layout["G"] = [(cx, cy), (Math.Min(W - 1, cx + 12), cy)];
        }

        CurrentLayout = typ;

        if (UseOriginalCitylab)
        {
            AttrKeys = Config.OriginalCitylabAttrDefs.Select(t => t.Key).ToList();
            Attractors = new Dictionary<string, List<(int, int)>>
            {
                ["transport"] = [..layout.GetValueOrDefault("T") ?? []],
                ["public"] = [..(layout.GetValueOrDefault("S") ?? []), ..(layout.GetValueOrDefault("H") ?? []), ..(layout.GetValueOrDefault("G") ?? [])],
                ["road"] = [..layout.GetValueOrDefault("R") ?? []],
                ["waterfront"] = [..layout.GetValueOrDefault("W") ?? []],
                ["landscape"] = [..layout.GetValueOrDefault("P") ?? []],
            };
        }
        else
        {
            AttrKeys = Config.AttrDefs.Select(t => t.Key).ToList();
            Attractors = new Dictionary<string, List<(int, int)>>();
            foreach (var k in AttrKeys)
                Attractors[k] = [..layout.GetValueOrDefault(k) ?? []];
        }

        EnsureAllAttractorsPresent();
        RebuildAttrDistanceFields();
        Reset();
    }

    public void ApplyRoadTopology(string typ)
    {
        if (Array.IndexOf(Config.RoadTopologyNames, typ) < 0)
            return;
        RoadTopology = typ;
        if (typ == "From Layout")
        {
            ApplyLayout(CurrentLayout);
            return;
        }
        var roadKey = UseOriginalCitylab ? "road" : "R";
        var cx = W / 2;
        var cy = H / 2;
        Attractors[roadKey] = [];

        if (typ == "Linear")
        {
            for (var i = 0; i < W; i++)
                Attractors[roadKey].Add((i, cy));
        }
        else if (typ == "Parallel")
        {
            for (var i = 0; i < W; i++)
            {
                foreach (var yy in new[] { cy - H / 6, cy, cy + H / 6 })
                {
                    if (yy >= 0 && yy < H)
                        Attractors[roadKey].Add((i, yy));
                }
            }
        }
        else if (typ == "Cross")
        {
            for (var i = 0; i < W; i++)
                Attractors[roadKey].Add((i, cy));
            for (var j = 0; j < H; j++)
                Attractors[roadKey].Add((cx, j));
        }
        else if (typ == "T-Junction")
        {
            for (var i = 0; i < W; i++)
                Attractors[roadKey].Add((i, cy));
            for (var j = cy; j < H; j++)
                Attractors[roadKey].Add((cx, j));
        }
        else if (typ == "Loop")
        {
            var margin = Math.Max(2, Math.Min(W, H) / 6);
            var x1 = Math.Max(0, cx - margin);
            var x2 = Math.Min(W - 1, cx + margin);
            var y1 = Math.Max(0, cy - margin);
            var y2 = Math.Min(H - 1, cy + margin);
            for (var i = x1; i <= x2; i++)
            {
                Attractors[roadKey].Add((i, y1));
                Attractors[roadKey].Add((i, y2));
            }
            for (var j = y1 + 1; j < y2; j++)
            {
                Attractors[roadKey].Add((x1, j));
                Attractors[roadKey].Add((x2, j));
            }
        }

        RebuildAttrDistanceFields();
    }

    private void EnsureAllAttractorsPresent()
    {
        var cx = W / 2;
        var cy = H / 2;
        Dictionary<string, (int, int)> fallbacks;
        if (UseOriginalCitylab)
        {
            fallbacks = new Dictionary<string, (int, int)>
            {
                ["transport"] = (cx, cy),
                ["public"] = (cx, Math.Max(0, cy - 1)),
                ["road"] = (0, cy),
                ["waterfront"] = (0, H - 1),
                ["landscape"] = (Math.Max(0, cx - 1), cy),
            };
        }
        else
        {
            fallbacks = new Dictionary<string, (int, int)>
            {
                ["T"] = (cx, cy),
                ["P"] = (Math.Max(0, cx - 1), cy),
                ["R"] = (0, cy),
                ["W"] = (0, H - 1),
                ["S"] = (cx, Math.Max(0, cy - 1)),
                ["H"] = (Math.Min(W - 1, cx + 1), cy),
                ["G"] = (cx, Math.Min(H - 1, cy + 1)),
            };
        }
        foreach (var k in AttrKeys)
        {
            var lst = (Attractors.GetValueOrDefault(k) ?? []).Where(p => p.x >= 0 && p.x < W && p.y >= 0 && p.y < H).ToList();
            if (lst.Count > 0)
            {
                Attractors[k] = lst;
            }
            else if (fallbacks.TryGetValue(k, out var fp))
            {
                var fx = Math.Max(0, Math.Min(W - 1, fp.Item1));
                var fy = Math.Max(0, Math.Min(H - 1, fp.Item2));
                Attractors[k] = [(fx, fy)];
            }
            else
            {
                Attractors[k] = [(cx, cy)];
            }
        }
    }

    private IEnumerable<Agent> AgentsInRange(int x, int y, int maxDist)
    {
        for (var dx = -maxDist; dx <= maxDist; dx++)
        {
            var rem = maxDist - Math.Abs(dx);
            for (var dy = -rem; dy <= rem; dy++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                var a = Grid[nx, ny];
                if (a != null)
                    yield return a;
            }
        }
    }

    public double GetUtility(Agent agent, int x, int y)
    {
        var pa = PrefAttr[agent.Type];
        var pg = PrefAgent[agent.Type];
        double u = 0;
        var myType = agent.Type;

        foreach (var k in AttrKeys)
        {
            var md = DistAttr[k][x, y];
            var rAttr = InfluenceRangeAttr.GetValueOrDefault(k)?.GetValueOrDefault(myType, Reach) ?? Reach;
            if (md <= rAttr)
                u += pa[k] / Math.Max(md, 1);
        }

        var maxR = TypeLabels.Max(t => InfluenceRangeAgent.GetValueOrDefault(t)?.GetValueOrDefault(myType, Reach) ?? Reach);
        foreach (var nb in AgentsInRange(x, y, maxR))
        {
            if (nb.X == x && nb.Y == y) continue;
            var dist = Math.Abs(nb.X - x) + Math.Abs(nb.Y - y);
            var rAgent = InfluenceRangeAgent.GetValueOrDefault(nb.Type)?.GetValueOrDefault(myType, Reach) ?? Reach;
            if (dist <= rAgent)
                u += pg[nb.Type] / Math.Max(dist, 1);
        }
        return u;
    }

    public StepResult Step()
    {
        Stats.Steps++;
        var n = Agents.Count;
        if (n < 2)
            return new StepResult(false, null, null, null, null, Array.Empty<Agent>());

        var i1 = _rng.Next(n);
        var i2 = _rng.Next(n - 1);
        if (i2 >= i1) i2++;
        var a1 = Agents[i1];
        var a2 = Agents[i2];
        if (IsAttr(a1.X, a1.Y))
        {
            Agents.Remove(a1);
            UpdateGrid();
            return new StepResult(false, null, null, null, null, [a1]);
        }
        if (IsAttr(a2.X, a2.Y))
        {
            Agents.Remove(a2);
            UpdateGrid();
            return new StepResult(false, null, null, null, null, [a2]);
        }

        var u1Old = GetUtility(a1, a1.X, a1.Y);
        var u2Old = GetUtility(a2, a2.X, a2.Y);
        var u1New = GetUtility(a1, a2.X, a2.Y);
        var u2New = GetUtility(a2, a1.X, a1.Y);

        var mode = SwapMode;
        bool accept;
        if (mode == "pareto")
            accept = (u1New >= u1Old && u2New >= u2Old) && (u1New > u1Old || u2New > u2Old);
        else if (mode == "greedy_total")
            accept = (u1New + u2New) > (u1Old + u2Old);
        else if (mode == "greedy_both")
            accept = u1New > u1Old && u2New > u2Old;
        else if (mode == "greedy_1")
            accept = u1New > u1Old;
        else
            accept = (u1New >= u1Old && u2New >= u2Old) && (u1New > u1Old || u2New > u2Old);

        if (accept)
        {
            if (u1New < u1Old || u2New < u2Old)
                Stats.Sacrificed++;
            var x1 = a1.X;
            var y1 = a1.Y;
            var x2 = a2.X;
            var y2 = a2.Y;
            a1.X = x2;
            a1.Y = y2;
            a2.X = x1;
            a2.Y = y1;
            Grid[x1, y1] = a2;
            Grid[x2, y2] = a1;
            Stats.Accepted++;
            return new StepResult(true, a1, a2, (x1, y1), (x2, y2), Array.Empty<Agent>());
        }
        Stats.Rejected++;
        return new StepResult(false, null, null, null, null, Array.Empty<Agent>());
    }

    public void CalcTotalUtility()
    {
        var raw = Agents.Sum(a => GetUtility(a, a.X, a.Y));
        Stats.RawUtility = raw;
        Stats.TotalUtility = raw + UtilityBias;
    }
}
