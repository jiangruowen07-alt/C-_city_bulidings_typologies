// 建筑场地：每格一个代理。四种城市代理(居民/公司/咖啡/商店)+ 绿地 "P"（城市中为公园吸引子，此处为可交换的代理颜色与邻接规则来自 Config）。

using System;
using System.Collections.Generic;
using System.Linq;

namespace CityLab;

public sealed class BuildingModel
{
    /// <summary>建筑网格边长钳制：最小 2、最大 5（UI 仅提供 3–5）。</summary>
    public const int GridSizeMin = 2;
    public const int GridSizeMax = 5;

    /// <summary>绿地，与城市吸引子 P（Park）同一键，便于与配色表对齐。</summary>
    public const string Park = "P";
    public static readonly string[] TypeCycle = [Park, "Resi", "Firm", "Cafe", "Shop"];
    public static readonly string[] BuildingFunctionTypes = ["Resi", "Firm", "Cafe", "Shop"];

    private readonly Random _rng;
    public int W { get; private set; }
    public int H { get; private set; }
    public string SwapMode { get; set; } = "pareto";
    public SimulationStats Stats { get; } = new();
    public List<Agent> Agents { get; } = [];
    public Agent?[,] Grid { get; private set; } = null!;
    public double UtilityBias;
    public double NeighborWeight = 1.0;
    public double MutateStepChance = 0.22;

    /// <summary>当前网格中数量最多的非绿地代理类型；同数时由随机数决定。仅绿地时不为 null 的无意义值——为 null。</summary>
    public string? MainAgentType { get; private set; }

    public BuildingModel(int w, int h, Random? rng = null)
    {
        _rng = rng ?? new Random();
        SetSize(w, h);
    }

    public void SetSize(int w, int h)
    {
        w = Math.Clamp(w, GridSizeMin, GridSizeMax);
        h = Math.Clamp(h, GridSizeMin, GridSizeMax);
        W = w;
        H = h;
        Grid = new Agent[W, H];
        Agents.Clear();
        for (var x = 0; x < W; x++)
        {
            for (var y = 0; y < H; y++)
            {
                var t = RandomInitialType();
                var a = new Agent(t, x, y);
                Agents.Add(a);
                Grid[x, y] = a;
            }
        }
        Stats.Steps = 0;
        Stats.Accepted = 0;
        Stats.Rejected = 0;
        Stats.Sacrificed = 0;
        UtilityBias = 0;
        CalcTotalUtility();
        RefreshMainAgentType();
    }

    string RandomInitialType() => TypeCycle[_rng.Next(TypeCycle.Length)];

    /// <summary>统计各非绿地代理子数量，取最大；绿地不参与。平局时随机择一。</summary>
    public void RefreshMainAgentType()
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var t in BuildingFunctionTypes)
            counts[t] = 0;
        foreach (var a in Agents)
        {
            if (a.Type == Park) continue;
            if (counts.TryGetValue(a.Type, out var c))
                counts[a.Type] = c + 1;
        }
        var max = 0;
        foreach (var c in counts.Values)
            if (c > max) max = c;
        if (max == 0)
        {
            MainAgentType = null;
            return;
        }
        var top = new List<string>();
        foreach (var t in BuildingFunctionTypes)
        {
            if (counts[t] == max) top.Add(t);
        }
        MainAgentType = top.Count == 1 ? top[0] : top[_rng.Next(top.Count)];
    }

    /// <summary>与「黑白视图」开启时黑格数一致：非绿地（非 P）的格子数。</summary>
    public int CountBlackCellsBwView() => Agents.Count(a => a.Type != Park);

    public void UpdateGrid()
    {
        for (var x = 0; x < W; x++)
            for (var y = 0; y < H; y++)
                Grid[x, y] = null;
        foreach (var a in Agents)
            if (a.X >= 0 && a.X < W && a.Y >= 0 && a.Y < H)
                Grid[a.X, a.Y] = a;
    }

    static string OccupantTypeIfSwapped(
        int nx, int ny, Agent a1, int x1, int y1, Agent a2, int x2, int y2, Agent?[,] grid)
    {
        if (nx == x1 && ny == y1) return a2.Type;
        if (nx == x2 && ny == y2) return a1.Type;
        return grid[nx, ny]!.Type;
    }

    /// <summary>邻接偏好：与城市中代理–代理、代理对「公园P」的偏好同构；绿地作为邻居时来自 DefaultPrefAttr 的 P 项折算。</summary>
    public static double PrefNeighbor(string me, string neighbor)
    {
        if (me == Park)
        {
            return neighbor switch
            {
                "Resi" => 2.2,
                "Firm" => 0.35,
                "Cafe" => 1.8,
                "Shop" => 1.1,
                "P" => 1.8,
                _ => 0
            };
        }
        if (neighbor == Park)
        {
            if (Config.DefaultPrefAttr.TryGetValue(me, out var attrRow) && attrRow.TryGetValue("P", out var pv))
                return pv * 0.4;
            return 0;
        }
        if (Config.DefaultPrefAgent.TryGetValue(me, out var agentRow) && agentRow.TryGetValue(neighbor, out var w))
            return w;
        return 0;
    }

    public double NeighborPrefSum(
        int atX, int atY, string me, Agent? a1, int x1, int y1, Agent? a2, int x2, int y2, bool asSwapped)
    {
        double u = 0;
        (int dx, int dy)[] d = [(-1, 0), (1, 0), (0, -1), (0, 1)];
        foreach (var (dx, dy) in d)
        {
            var nx = atX + dx;
            var ny = atY + dy;
            if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
            var nType = (asSwapped && a1 != null && a2 != null)
                ? OccupantTypeIfSwapped(nx, ny, a1, x1, y1, a2, x2, y2, Grid)
                : Grid[nx, ny]!.Type;
            u += PrefNeighbor(me, nType);
        }
        return u;
    }

    public double GetUtility(Agent agent, int x, int y) =>
        NeighborWeight * NeighborPrefSum(x, y, agent.Type, null, 0, 0, null, 0, 0, false);

    public double ProposedUtility(Agent me, int atX, int atY, Agent a1, int x1, int y1, Agent a2, int x2, int y2) =>
        NeighborWeight * NeighborPrefSum(atX, atY, me.Type, a1, x1, y1, a2, x2, y2, true);

    public StepResult Step()
    {
        Stats.Steps++;
        var n = Agents.Count;
        if (n < 1)
            return new StepResult(false, null, null, null, null, Array.Empty<Agent>());

        if (_rng.NextDouble() < MutateStepChance)
            return StepMutate();

        if (n < 2)
            return new StepResult(false, null, null, null, null, Array.Empty<Agent>());

        return StepParetoSwap();
    }

    StepResult StepMutate()
    {
        var parkCells = new List<Agent>();
        var bldCells = new List<Agent>();
        foreach (var a in Agents)
        {
            if (a.Type == Park) parkCells.Add(a);
            else bldCells.Add(a);
        }
        if (parkCells.Count == 0 && bldCells.Count == 0)
            return new StepResult(false, null, null, null, null, Array.Empty<Agent>());

        if (parkCells.Count > 0 && (bldCells.Count == 0 || _rng.Next(2) == 0))
        {
            var a = parkCells[_rng.Next(parkCells.Count)];
            a.Type = BuildingFunctionTypes[_rng.Next(BuildingFunctionTypes.Length)];
            UpdateGrid();
            Stats.Accepted++;
            RefreshMainAgentType();
            return new StepResult(false, a, null, (a.X, a.Y), null, Array.Empty<Agent>());
        }
        if (bldCells.Count > 0)
        {
            var a = bldCells[_rng.Next(bldCells.Count)];
            a.Type = Park;
            UpdateGrid();
            Stats.Accepted++;
            RefreshMainAgentType();
            return new StepResult(false, a, null, (a.X, a.Y), null, Array.Empty<Agent>());
        }
        return new StepResult(false, null, null, null, null, Array.Empty<Agent>());
    }

    StepResult StepParetoSwap()
    {
        var n = Agents.Count;
        var i1 = _rng.Next(n);
        var i2 = _rng.Next(n - 1);
        if (i2 >= i1) i2++;
        var a1 = Agents[i1];
        var a2 = Agents[i2];
        if (a1 == a2)
        {
            Stats.Rejected++;
            return new StepResult(false, null, null, null, null, Array.Empty<Agent>());
        }

        var x1 = a1.X; var y1 = a1.Y;
        var x2 = a2.X; var y2 = a2.Y;

        var u1Old = GetUtility(a1, x1, y1);
        var u2Old = GetUtility(a2, x2, y2);
        var u1New = ProposedUtility(a1, x2, y2, a1, x1, y1, a2, x2, y2);
        var u2New = ProposedUtility(a2, x1, y1, a1, x1, y1, a2, x2, y2);

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
            a1.X = x2; a1.Y = y2;
            a2.X = x1; a2.Y = y1;
            Grid[x1, y1] = a2;
            Grid[x2, y2] = a1;
            Stats.Accepted++;
            RefreshMainAgentType();
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
        foreach (var a in Agents)
            a.Type = RandomInitialType();
        UpdateGrid();
        Stats.Steps = 0;
        Stats.Accepted = 0;
        Stats.Rejected = 0;
        Stats.Sacrificed = 0;
        CalcTotalUtility();
        if (targetInt.HasValue)
            LockTotalUtilityInt(targetInt.Value);
        RefreshMainAgentType();
    }

    public void Clear()
    {
        foreach (var a in Agents)
            a.Type = Park;
        UpdateGrid();
        Stats.Steps = 0;
        Stats.Accepted = 0;
        Stats.Rejected = 0;
        Stats.Sacrificed = 0;
        CalcTotalUtility();
        RefreshMainAgentType();
    }

    public static string NextTypeCyclic(string t)
    {
        var i = Array.IndexOf(TypeCycle, t);
        if (i < 0) return TypeCycle[0];
        return TypeCycle[(i + 1) % TypeCycle.Length];
    }

    /// <summary>与「黑白视图」一致：黑=非 P，白=绿地。仅 3×3 场用于与城市格内 3×3 同构。</summary>
    public bool[,]? TryGetBlackWhiteMask3x3()
    {
        if (W != 3 || H != 3) return null;
        var m = new bool[3, 3];
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            {
                var a = Grid[x, y];
                m[x, y] = a != null && a.Type != Park;
            }
        return m;
    }
}
