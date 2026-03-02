// CITY LAB - 以物换物 游戏逻辑 (单文件 C#)
// Grasshopper C# 组件 - GH_ScriptInstance 格式
// Inputs: W, H, Reset, Run, SwapCount, Layout, SwapMode, RoadTopology
// Outputs: A=Agent点位, B=Agent类型, C=总效用, D=统计信息, E=吸引子点位

#region Usings
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
#endregion

public class Script_Instance : GH_ScriptInstance
{
    // --- Persistent Simulation State ---
    private CityLabGH _model;
    private int _lastW, _lastH;
    private string _lastLayout, _lastRoadTopology;
    private Random _rng = new Random();

    // Timer to prevent UI locking
    private Stopwatch _watch = new Stopwatch();

    private void RunScript(
        object w,
        object h,
        bool reset,
        bool run,
        int swapCount,
        object layout,
        object swapMode,
        object roadTopology,
        ref object A,
        ref object B,
        ref object C,
        ref object D,
        ref object E)
    {
        int width = 40, height = 40;
        if (w != null) { double v; if (double.TryParse(Convert.ToString(w), out v)) width = Math.Max(4, (int)v); }
        if (h != null) { double v; if (double.TryParse(Convert.ToString(h), out v)) height = Math.Max(4, (int)v); }
        string layoutStr = (layout != null) ? layout.ToString() : "Grid";
        string swapModeStr = (swapMode != null) ? swapMode.ToString() : "pareto";
        string roadStr = (roadTopology != null) ? roadTopology.ToString() : "From Layout";

        swapCount = Math.Max(0, swapCount);

        // 1. Initialize or Reset Simulation
        bool needInit = reset || _model == null || _model.W != width || _model.H != height
            || _lastLayout != layoutStr || _lastRoadTopology != roadStr;
        if (needInit)
        {
            _model = new CityLabGH(width, height, 6, _rng);
            _model.SwapMode = swapModeStr;
            _model.RoadTopology = roadStr;
            _model.ApplyLayout(layoutStr);
            _lastW = width; _lastH = height;
            _lastLayout = layoutStr; _lastRoadTopology = roadStr;
            _watch.Restart();
        }
        else if (swapModeStr != _model.SwapMode)
        {
            _model.SwapMode = swapModeStr;
        }

        // 2. Simulation Loop with UI Breathing Room
        int successfulSwaps = 0;
        if (run)
        {
            successfulSwaps = _model.RunSteps(swapCount);

            if (_watch.ElapsedMilliseconds > 20)
            {
                _watch.Restart();
                Component.ExpireSolution(true);
            }
            else
            {
                System.Threading.Thread.Sleep(1);
                Component.ExpireSolution(true);
            }
        }

        // 3. Prepare Visual Outputs
        double scale = 1.0;
        A = _model.GetAgentPoints(scale);
        B = _model.GetAgentTypes();
        C = _model.TotalUtility;
        D = string.Format("SOCIAL VALUE: {0:F2}\nTRADES: {1}\nSTEPS: {2}\nACCEPTED: {3}\nREJECTED: {4}\nPARCELS: {5}\nSTATUS: {6}",
            _model.TotalUtility, successfulSwaps, _model.Steps, _model.Accepted, _model.Rejected,
            _model.Agents.Count, run ? "RUNNING (Safe)" : "STOPPED");
        E = _model.GetAttractorPoints(scale);
    }

    #region Agent
    public class Agent
    {
        public string Type;
        public int X, Y;
        public Agent(string type, int x, int y) { Type = type; X = x; Y = y; }
        public Point3d ToPoint(double scale) { return new Point3d(X * scale, Y * scale, 0); }
    }
    #endregion

    #region CityLabGH - Full Game Logic
    public class CityLabGH
    {
        public int W, H, Reach;
        public Dictionary<string, List<Tuple<int, int>>> Attractors;
        public Dictionary<string, Dictionary<string, double>> PrefAttr;
        public Dictionary<string, Dictionary<string, double>> PrefAgent;
        public Dictionary<string, Dictionary<string, int>> InfluenceRangeAttr, InfluenceRangeAgent;
        public List<string> AttrKeys, TypeLabels;
        public List<Agent> Agents;
        public Agent[,] Grid;
        public double[][][] DistAttr;
        public string[,] AttrGrid;
        public string SwapMode;
        public double TotalUtility;
        public int Steps, Accepted, Rejected;
        public string CurrentLayout, RoadTopology;
        private Random _rnd;

        public CityLabGH(int w, int h, int reach, Random rnd)
        {
            W = w; H = h; Reach = reach; _rnd = rnd ?? new Random();
            AttrKeys = new List<string> { "T", "P", "R", "W", "S", "H", "G" };
            TypeLabels = new List<string> { "Resi", "Firm", "Shop", "Cafe", "Hotel", "Restaurant", "Clinic" };
            PrefAttr = InitPrefAttr();
            PrefAgent = InitPrefAgent();
            InfluenceRangeAttr = InitInfluenceAttr();
            InfluenceRangeAgent = InitInfluenceAgent();
            Attractors = new Dictionary<string, List<Tuple<int, int>>>();
            foreach (var k in AttrKeys) Attractors[k] = new List<Tuple<int, int>>();
            Agents = new List<Agent>();
            Grid = new Agent[w, h];
            DistAttr = new double[AttrKeys.Count][][];
            AttrGrid = new string[w, h];
        }

        static Dictionary<string, Dictionary<string, double>> InitPrefAttr()
        {
            return new Dictionary<string, Dictionary<string, double>> {
                {"Resi", new Dictionary<string, double> {{"T", 3.5}, {"P", 5.0}, {"R", -2.0}, {"W", 4.5}, {"S", 4.0}, {"H", 2.0}, {"G", 2.5}}},
                {"Firm", new Dictionary<string, double> {{"T", 5.0}, {"P", 1.0}, {"R", 2.0}, {"W", 1.0}, {"S", 0.5}, {"H", 1.0}, {"G", 3.0}}},
                {"Shop", new Dictionary<string, double> {{"T", 5.0}, {"P", 4.0}, {"R", 2.0}, {"W", 2.0}, {"S", 1.0}, {"H", 1.0}, {"G", 1.5}}},
                {"Cafe", new Dictionary<string, double> {{"T", 2.0}, {"P", 5.0}, {"R", -1.0}, {"W", 4.0}, {"S", 2.0}, {"H", 1.0}, {"G", 1.0}}},
                {"Hotel", new Dictionary<string, double> {{"T", 4.0}, {"P", 3.0}, {"R", 1.0}, {"W", 4.0}, {"S", 1.0}, {"H", 2.0}, {"G", 1.5}}},
                {"Restaurant", new Dictionary<string, double> {{"T", 3.0}, {"P", 4.5}, {"R", 0.5}, {"W", 4.0}, {"S", 1.0}, {"H", 1.0}, {"G", 1.0}}},
                {"Clinic", new Dictionary<string, double> {{"T", 2.5}, {"P", 2.0}, {"R", -0.5}, {"W", 1.0}, {"S", 2.0}, {"H", 5.0}, {"G", 2.0}}}
            };
        }
        static Dictionary<string, Dictionary<string, double>> InitPrefAgent()
        {
            return new Dictionary<string, Dictionary<string, double>> {
                {"Resi", new Dictionary<string, double> {{"Resi", 2.0}, {"Firm", -1.0}, {"Shop", 3.0}, {"Cafe", 4.0}, {"Hotel", 1.5}, {"Restaurant", 3.5}, {"Clinic", 2.5}}},
                {"Firm", new Dictionary<string, double> {{"Resi", 1.0}, {"Firm", 3.0}, {"Shop", 4.0}, {"Cafe", 2.0}, {"Hotel", 2.0}, {"Restaurant", 2.5}, {"Clinic", 1.5}}},
                {"Shop", new Dictionary<string, double> {{"Resi", 2.0}, {"Firm", 4.0}, {"Shop", 1.0}, {"Cafe", 3.0}, {"Hotel", 2.5}, {"Restaurant", 4.0}, {"Clinic", 1.5}}},
                {"Cafe", new Dictionary<string, double> {{"Resi", 4.0}, {"Firm", 2.0}, {"Shop", 3.0}, {"Cafe", -1.0}, {"Hotel", 2.0}, {"Restaurant", 4.0}, {"Clinic", 1.0}}},
                {"Hotel", new Dictionary<string, double> {{"Resi", 1.0}, {"Firm", 2.0}, {"Shop", 3.0}, {"Cafe", 2.0}, {"Hotel", -1.0}, {"Restaurant", 4.0}, {"Clinic", 1.5}}},
                {"Restaurant", new Dictionary<string, double> {{"Resi", 3.0}, {"Firm", 2.0}, {"Shop", 3.5}, {"Cafe", 4.0}, {"Hotel", 3.0}, {"Restaurant", -1.0}, {"Clinic", 1.0}}},
                {"Clinic", new Dictionary<string, double> {{"Resi", 2.5}, {"Firm", 1.0}, {"Shop", 1.0}, {"Cafe", 1.0}, {"Hotel", 1.0}, {"Restaurant", 0.5}, {"Clinic", -1.0}}}
            };
        }
        static Dictionary<string, Dictionary<string, int>> InitInfluenceAttr()
        {
            var types = new[] { "Resi", "Firm", "Shop", "Cafe", "Hotel", "Restaurant", "Clinic" };
            var d = new Dictionary<string, Dictionary<string, int>>();
            foreach (var k in new[] { "T", "P", "R", "W", "S", "H", "G" })
            {
                int r = (k == "T") ? 20 : 6;
                d[k] = types.ToDictionary(t => t, t => r);
            }
            return d;
        }
        static Dictionary<string, Dictionary<string, int>> InitInfluenceAgent()
        {
            var types = new[] { "Resi", "Firm", "Shop", "Cafe", "Hotel", "Restaurant", "Clinic" };
            return types.ToDictionary(s => s, s => types.ToDictionary(t => t, t => 6));
        }

        string RandomAgentType()
        {
            double r = _rnd.NextDouble();
            if (r < 0.55) return "Resi";
            if (r < 0.70) return "Firm";
            if (r < 0.82) return "Shop";
            if (r < 0.90) return "Cafe";
            if (r < 0.95) return "Hotel";
            if (r < 0.985) return "Restaurant";
            return "Clinic";
        }

        public void ApplyLayout(string typ)
        {
            CurrentLayout = typ ?? "Grid";
            foreach (var k in AttrKeys) Attractors[k].Clear();
            int cx = W / 2, cy = H / 2;

            if (CurrentLayout == "Grid")
            {
                for (int i = 0; i < W; i++)
                    for (int j = 0; j < H; j++)
                        if (i % 8 == 0 || j % 8 == 0) Attractors["R"].Add(Tuple.Create(i, j));
                Attractors["T"] = new List<Tuple<int, int>> {
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx - 6)), Math.Max(1, Math.Min(H - 2, cy - 6))),
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx + 6)), Math.Max(1, Math.Min(H - 2, cy + 6)))
                };
                Attractors["P"] = new List<Tuple<int, int>> {
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx - 6)), Math.Max(1, Math.Min(H - 2, cy - 6))),
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx + 6)), Math.Max(1, Math.Min(H - 2, cy + 6))),
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx - 6)), Math.Max(1, Math.Min(H - 2, cy + 6)))
                };
                Attractors["S"] = new List<Tuple<int, int>> {
                    Tuple.Create(Math.Max(0, cx - 8), cy), Tuple.Create(Math.Min(W - 1, cx + 8), cy),
                    Tuple.Create(Math.Max(1, Math.Min(W - 2, cx + 6)), Math.Max(1, Math.Min(H - 2, cy + 6)))
                };
                Attractors["H"] = new List<Tuple<int, int>> {
                    Tuple.Create(cx, Math.Max(0, cy - 8)), Tuple.Create(cx, Math.Min(H - 1, cy + 8))
                };
                for (int i = 0; i < W; i++) Attractors["W"].Add(Tuple.Create(i, H - 1));
                Attractors["G"] = new List<Tuple<int, int>> {
                    Tuple.Create(cx, cy), Tuple.Create(cx, Math.Max(0, cy - 10))
                };
            }
            else if (CurrentLayout == "Radial")
            {
                double scale = Math.Min(W, H) / 40.0;
                double r1 = 8 * scale, r2 = 10 * scale, r3 = 14 * scale, r4 = 16 * scale, r5 = 18 * scale;
                double tol = Math.Max(0.5, 0.6 * scale);
                for (int i = 0; i < W; i++)
                    for (int j = 0; j < H; j++)
                    {
                        double d = Math.Sqrt((i - cx) * (i - cx) + (j - cy) * (j - cy));
                        if (Math.Abs(d - r1) < tol || Math.Abs(d - r4) < tol || Math.Abs(i - cx) < 0.6 || Math.Abs(j - cy) < 0.6)
                            Attractors["R"].Add(Tuple.Create(i, j));
                        if (d < Math.Max(2, 3 * scale)) Attractors["P"].Add(Tuple.Create(i, j));
                        if (Math.Abs(d - r2) < tol) Attractors["S"].Add(Tuple.Create(i, j));
                        if (Math.Abs(d - r3) < tol) Attractors["H"].Add(Tuple.Create(i, j));
                        if (Math.Abs(d - r5) < tol) Attractors["W"].Add(Tuple.Create(i, j));
                    }
                Attractors["T"] = new List<Tuple<int, int>> { Tuple.Create(cx, cy), Tuple.Create(0, 0) };
                Attractors["G"] = new List<Tuple<int, int>> {
                    Tuple.Create(cx, Math.Max(0, cy - 1)), Tuple.Create(cx, Math.Min(H - 1, cy + 1)),
                    Tuple.Create(W - 1, H - 1), Tuple.Create(0, H - 1)
                };
                Attractors["S"].Add(Tuple.Create(W - 1, 0));
            }
            else if (CurrentLayout == "Organic")
            {
                for (int _ = 0; _ < 10; _++)
                {
                    int x = _rnd.Next(W), y = _rnd.Next(H);
                    for (int __ = 0; __ < 18; __++)
                    {
                        Attractors["R"].Add(Tuple.Create(x, y));
                        x = Math.Max(0, Math.Min(W - 1, x + _rnd.Next(3) - 1));
                        y = Math.Max(0, Math.Min(H - 1, y + _rnd.Next(3) - 1));
                    }
                    Attractors["P"].Add(Tuple.Create(x, y));
                }
                for (int _ = 0; _ < 6; _++) Attractors["S"].Add(Tuple.Create(_rnd.Next(W), _rnd.Next(H)));
                for (int _ = 0; _ < 4; _++) Attractors["H"].Add(Tuple.Create(_rnd.Next(W), _rnd.Next(H)));
                for (int _ = 0; _ < 2; _++) Attractors["T"].Add(Tuple.Create(_rnd.Next(W), _rnd.Next(H)));
                for (int _ = 0; _ < 3; _++) Attractors["G"].Add(Tuple.Create(_rnd.Next(W), _rnd.Next(H)));
                int xw = _rnd.Next(W), yw = H - 2;
                for (int _ = 0; _ < W * 2; _++)
                {
                    Attractors["W"].Add(Tuple.Create(xw, yw));
                    xw = Math.Max(0, Math.Min(W - 1, xw + (_rnd.Next(3) - 1)));
                    yw = Math.Max(H / 2, Math.Min(H - 1, yw + (_rnd.Next(3) - 1)));
                }
            }
            else if (CurrentLayout == "Linear")
            {
                for (int i = 0; i < W; i++)
                {
                    foreach (int yy in new[] { cy - 1, cy + 1 }) if (yy >= 0 && yy < H) Attractors["R"].Add(Tuple.Create(i, yy));
                    foreach (int yy in new[] { cy - 5, cy + 5 }) if (yy >= 0 && yy < H) Attractors["P"].Add(Tuple.Create(i, yy));
                    int sy = (cy - 8 >= 0 && cy - 8 < H) ? cy - 8 : Math.Max(0, cy - 2);
                    int hy = (cy + 8 >= 0 && cy + 8 < H) ? cy + 8 : Math.Min(H - 1, cy + 2);
                    if (i % 12 == 0) Attractors["S"].Add(Tuple.Create(i, sy));
                    if (i % 12 == 6) Attractors["H"].Add(Tuple.Create(i, hy));
                }
                Attractors["T"] = new List<Tuple<int, int>> { Tuple.Create(0, cy), Tuple.Create(W - 1, cy) };
                for (int i = 0; i < W; i++) Attractors["W"].Add(Tuple.Create(i, 0));
                Attractors["G"] = new List<Tuple<int, int>> {
                    Tuple.Create(W / 4, cy), Tuple.Create(W / 2, cy), Tuple.Create(3 * W / 4, cy)
                };
            }
            else if (CurrentLayout == "Polycentric")
            {
                int[,] baseHubs = { { 10, 10 }, { 30, 10 }, { 10, 30 }, { 30, 30 }, { 20, 20 } };
                Attractors["T"] = new List<Tuple<int, int>> { Tuple.Create(0, 0), Tuple.Create(W - 1, H - 1) };
                Attractors["G"] = new List<Tuple<int, int>> { Tuple.Create(0, cy), Tuple.Create(W - 1, cy) };
                for (int idx = 0; idx < 5; idx++)
                {
                    int hxi = Math.Max(1, Math.Min(W - 2, baseHubs[idx, 0] * W / 40));
                    int hyi = Math.Max(1, Math.Min(H - 2, baseHubs[idx, 1] * H / 40));
                    Attractors["G"].Add(Tuple.Create(hxi, hyi));
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int[] xs = { hxi + dx, hxi + dx, hxi - 3, hxi + 3 }, ys = { hyi - 3, hyi + 3, hyi + dx, hyi + dx };
                        for (int i = 0; i < 4; i++)
                            if (xs[i] >= 0 && xs[i] < W && ys[i] >= 0 && ys[i] < H)
                                Attractors["R"].Add(Tuple.Create(xs[i], ys[i]));
                    }
                    if (hyi + 1 < H) Attractors["P"].Add(Tuple.Create(hxi, hyi + 1));
                    if (hyi - 1 >= 0) Attractors["P"].Add(Tuple.Create(hxi, hyi - 1));
                    if (idx % 2 == 0) Attractors["S"].Add(Tuple.Create(hxi, hyi));
                    else Attractors["H"].Add(Tuple.Create(hxi, hyi));
                }
                for (int i = 0; i < W; i++) Attractors["W"].Add(Tuple.Create(i, H - 1));
            }
            else if (CurrentLayout == "Superblock")
            {
                for (int i = 0; i < W; i++)
                    for (int j = 0; j < H; j++)
                    {
                        if (i % 14 == 0 || j % 14 == 0) Attractors["R"].Add(Tuple.Create(i, j));
                        if ((i + 7) % 14 == 0 && (j + 7) % 14 == 0)
                            for (int di = -1; di <= 1; di++)
                                for (int dj = -1; dj <= 1; dj++)
                                {
                                    int xx = i + di, yy = j + dj;
                                    if (xx >= 0 && xx < W && yy >= 0 && yy < H) Attractors["P"].Add(Tuple.Create(xx, yy));
                                }
                    }
                for (int i = 7; i < W; i += 14)
                    for (int j = 7; j < H; j += 14) Attractors["S"].Add(Tuple.Create(i, j));
                Attractors["S"].Add(Tuple.Create(Math.Max(1, Math.Min(W - 2, cx - 7)), cy));
                Attractors["S"].Add(Tuple.Create(cx, Math.Max(1, Math.Min(H - 2, cy - 7))));
                for (int i = 7; i < W; i += 28)
                    for (int j = 7; j < H; j += 28)
                        Attractors["H"].Add(Tuple.Create(i + 3 < W ? i + 3 : i, j));
                var blockCenters = new List<Tuple<int, int>>();
                for (int i = 7; i < W; i += 14)
                    for (int j = 7; j < H; j += 14)
                        if (i < W && j < H) blockCenters.Add(Tuple.Create(i, j));
                Attractors["T"] = blockCenters.Count >= 2 ? blockCenters.Take(2).ToList() : blockCenters.Concat(new[] { Tuple.Create(cx, cy) }).ToList();
                for (int j = 0; j < H; j++) Attractors["W"].Add(Tuple.Create(W - 1, j));
                Attractors["G"] = new List<Tuple<int, int>> { Tuple.Create(cx, cy), Tuple.Create(cx + 1 < W ? cx + 1 : cx, cy) };
                if (blockCenters.Count > 2) Attractors["G"].Add(blockCenters[2]);
                if (blockCenters.Count > 3) Attractors["G"].Add(blockCenters[3]);
                for (int idx = 4; idx < blockCenters.Count; idx++) Attractors["H"].Add(blockCenters[idx]);
            }
            else if (CurrentLayout == "Hybrid")
            {
                for (int i = 0; i < W; i++)
                    for (int j = 0; j < H; j++)
                    {
                        if (i < cx && _rnd.NextDouble() < 0.05) Attractors["R"].Add(Tuple.Create(i, j));
                        else if (i >= cx && (i % 6 == 0 || j % 6 == 0)) Attractors["R"].Add(Tuple.Create(i, j));
                    }
                Attractors["T"] = new List<Tuple<int, int>> { Tuple.Create(cx, cy), Tuple.Create(W - 1, cy) };
                for (int i = 0; i < Math.Min(W, H); i += 2) Attractors["P"].Add(Tuple.Create(i, i));
                Attractors["S"] = new List<Tuple<int, int>> {
                    Tuple.Create(Math.Max(0, cx - 10), Math.Max(0, cy - 6)),
                    Tuple.Create(Math.Min(W - 1, cx + 6), Math.Min(H - 1, cy + 10))
                };
                Attractors["H"] = new List<Tuple<int, int>> {
                    Tuple.Create(Math.Max(0, cx - 6), Math.Min(H - 1, cy + 10)),
                    Tuple.Create(Math.Min(W - 1, cx + 10), Math.Max(0, cy - 6))
                };
                for (int j = 0; j < H; j++) Attractors["W"].Add(Tuple.Create(0, j));
                Attractors["G"] = new List<Tuple<int, int>> { Tuple.Create(cx, cy), Tuple.Create(Math.Min(W - 1, cx + 12), cy) };
            }
            else { ApplyLayout("Grid"); return; }

            EnsureAllAttractorsPresent();
            ApplyRoadTopology(RoadTopology ?? "From Layout");
            if (string.IsNullOrEmpty(RoadTopology) || RoadTopology == "From Layout")
            {
                RebuildDistanceFields();
                Reset();
            }
        }

        void EnsureAllAttractorsPresent()
        {
            int cx = W / 2, cy = H / 2;
            var fallbacks = new Dictionary<string, Tuple<int, int>> {
                {"T", Tuple.Create(cx, cy)}, {"P", Tuple.Create(Math.Max(0, cx - 1), cy)},
                {"R", Tuple.Create(0, cy)}, {"W", Tuple.Create(0, H - 1)},
                {"S", Tuple.Create(cx, Math.Max(0, cy - 1))}, {"H", Tuple.Create(Math.Min(W - 1, cx + 1), cy)},
                {"G", Tuple.Create(cx, Math.Min(H - 1, cy + 1))}
            };
            foreach (var k in AttrKeys)
            {
                var lst = Attractors[k].Where(p => p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H).ToList();
                if (lst.Count > 0) Attractors[k] = lst;
                else if (fallbacks.ContainsKey(k)) Attractors[k] = new List<Tuple<int, int>> { fallbacks[k] };
                else Attractors[k] = new List<Tuple<int, int>> { Tuple.Create(cx, cy) };
            }
        }

        public void ApplyRoadTopology(string typ)
        {
            RoadTopology = typ ?? "From Layout";
            if (RoadTopology == "From Layout") return;
            int cx = W / 2, cy = H / 2;
            Attractors["R"].Clear();
            if (RoadTopology == "Linear")
            {
                for (int i = 0; i < W; i++) Attractors["R"].Add(Tuple.Create(i, cy));
            }
            else if (RoadTopology == "Parallel")
            {
                foreach (int yy in new[] { cy - H / 6, cy, cy + H / 6 })
                    if (yy >= 0 && yy < H)
                        for (int i = 0; i < W; i++) Attractors["R"].Add(Tuple.Create(i, yy));
            }
            else if (RoadTopology == "Cross")
            {
                for (int i = 0; i < W; i++) Attractors["R"].Add(Tuple.Create(i, cy));
                for (int j = 0; j < H; j++) Attractors["R"].Add(Tuple.Create(cx, j));
            }
            else if (RoadTopology == "T-Junction")
            {
                for (int i = 0; i < W; i++) Attractors["R"].Add(Tuple.Create(i, cy));
                for (int j = cy; j < H; j++) Attractors["R"].Add(Tuple.Create(cx, j));
            }
            else if (RoadTopology == "Loop")
            {
                int margin = Math.Max(2, Math.Min(W, H) / 6);
                int x1 = Math.Max(0, cx - margin), x2 = Math.Min(W - 1, cx + margin);
                int y1 = Math.Max(0, cy - margin), y2 = Math.Min(H - 1, cy + margin);
                for (int i = x1; i <= x2; i++)
                {
                    Attractors["R"].Add(Tuple.Create(i, y1));
                    Attractors["R"].Add(Tuple.Create(i, y2));
                }
                for (int j = y1 + 1; j < y2; j++)
                {
                    Attractors["R"].Add(Tuple.Create(x1, j));
                    Attractors["R"].Add(Tuple.Create(x2, j));
                }
            }
            RebuildDistanceFields();
            Reset();
        }

        bool IsAttr(int x, int y) { return x >= 0 && x < W && y >= 0 && y < H && AttrGrid[x, y] != null; }

        void UpdateGrid()
        {
            for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) Grid[i, j] = null;
            foreach (var a in Agents)
                if (a.X >= 0 && a.X < W && a.Y >= 0 && a.Y < H) Grid[a.X, a.Y] = a;
        }

        void RebuildDistanceFields()
        {
            for (int i = 0; i < W; i++) for (int j = 0; j < H; j++) AttrGrid[i, j] = null;
            foreach (var kv in Attractors)
                foreach (var p in kv.Value)
                    if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
                        AttrGrid[p.Item1, p.Item2] = kv.Key;

            for (int ki = 0; ki < AttrKeys.Count; ki++)
            {
                var k = AttrKeys[ki];
                var dist = new double[W][];
                for (int i = 0; i < W; i++) { dist[i] = new double[H]; for (int j = 0; j < H; j++) dist[i][j] = 1e9; }
                var q = new Queue<Tuple<int, int>>();
                foreach (var p in Attractors[k])
                {
                    if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
                    {
                        dist[p.Item1][p.Item2] = 0;
                        q.Enqueue(p);
                    }
                }
                while (q.Count > 0)
                {
                    var t = q.Dequeue();
                    int x = t.Item1, y = t.Item2;
                    double nd = dist[x][y] + 1;
                    if (x > 0 && nd < dist[x - 1][y]) { dist[x - 1][y] = nd; q.Enqueue(Tuple.Create(x - 1, y)); }
                    if (x < W - 1 && nd < dist[x + 1][y]) { dist[x + 1][y] = nd; q.Enqueue(Tuple.Create(x + 1, y)); }
                    if (y > 0 && nd < dist[x][y - 1]) { dist[x][y - 1] = nd; q.Enqueue(Tuple.Create(x, y - 1)); }
                    if (y < H - 1 && nd < dist[x][y + 1]) { dist[x][y + 1] = nd; q.Enqueue(Tuple.Create(x, y + 1)); }
                }
                DistAttr[ki] = dist;
            }
        }

        public void Reset()
        {
            var attrCells = new HashSet<Tuple<int, int>>();
            foreach (var lst in Attractors.Values)
                foreach (var p in lst)
                    if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
                        attrCells.Add(p);
            var slots = new List<Tuple<int, int>>();
            for (int i = 0; i < W; i++)
                for (int j = 0; j < H; j++)
                    if (!attrCells.Contains(Tuple.Create(i, j))) slots.Add(Tuple.Create(i, j));
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = _rnd.Next(i + 1);
                var tmp = slots[i]; slots[i] = slots[j]; slots[j] = tmp;
            }
            Agents.Clear();
            foreach (var p in slots) Agents.Add(new Agent(RandomAgentType(), p.Item1, p.Item2));
            Steps = 0; Accepted = 0; Rejected = 0;
            UpdateGrid();
            CalcTotalUtility();
        }

        double GetUtility(Agent agent, int x, int y)
        {
            var pa = PrefAttr.ContainsKey(agent.Type) ? PrefAttr[agent.Type] : new Dictionary<string, double>();
            var pg = PrefAgent.ContainsKey(agent.Type) ? PrefAgent[agent.Type] : new Dictionary<string, double>();
            string myType = agent.Type;
            double u = 0;
            for (int ki = 0; ki < AttrKeys.Count; ki++)
            {
                var k = AttrKeys[ki];
                double md = DistAttr[ki][x][y];
                int rAttr = (InfluenceRangeAttr.ContainsKey(k) && InfluenceRangeAttr[k].ContainsKey(myType)) ? InfluenceRangeAttr[k][myType] : Reach;
                if (md <= rAttr && pa.ContainsKey(k)) u += pa[k] / Math.Max(md, 1);
            }
            foreach (var nb in Agents)
            {
                if (nb.X == x && nb.Y == y) continue;
                int dist = Math.Abs(nb.X - x) + Math.Abs(nb.Y - y);
                int rAgent = (InfluenceRangeAgent.ContainsKey(nb.Type) && InfluenceRangeAgent[nb.Type].ContainsKey(myType)) ? InfluenceRangeAgent[nb.Type][myType] : Reach;
                if (dist <= rAgent && pg.ContainsKey(nb.Type)) u += pg[nb.Type] / Math.Max(dist, 1);
            }
            return u;
        }

        public bool Step()
        {
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
            if (accept)
            {
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

        public int RunSteps(int n)
        {
            int count = 0;
            for (int i = 0; i < n; i++) if (Step()) count++;
            return count;
        }

        void CalcTotalUtility()
        {
            TotalUtility = 0;
            foreach (var a in Agents) TotalUtility += GetUtility(a, a.X, a.Y);
        }

        public List<Point3d> GetAgentPoints(double scale)
        {
            var pts = new List<Point3d>();
            foreach (var a in Agents) pts.Add(a.ToPoint(scale));
            return pts;
        }
        public List<string> GetAgentTypes() { return Agents.Select(a => a.Type).ToList(); }
        public List<Point3d> GetAttractorPoints(double scale)
        {
            var pts = new List<Point3d>();
            foreach (var lst in Attractors.Values)
                foreach (var p in lst)
                    if (p.Item1 >= 0 && p.Item1 < W && p.Item2 >= 0 && p.Item2 < H)
                        pts.Add(new Point3d(p.Item1 * scale, p.Item2 * scale, 0));
            return pts;
        }
    }
    #endregion
}
