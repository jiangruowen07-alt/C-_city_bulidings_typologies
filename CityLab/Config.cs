// CITY LAB v1.5 - Configuration & constants

using System.Collections.Generic;
using System.Drawing;

namespace CityLab;

internal static class Config
{
    public static readonly (string Key, string Name, string ColorHex)[] AttrDefs =
    [
        ("T", "Transport", "#ffffff"),
        ("P", "Park", "#4d8047"),
        ("R", "Road", "#333333"),
        ("W", "Waterfront", "#89adcd"),
        ("S", "School", "#7a507e"),
        ("H", "Healthcare", "#1a2747"),
        ("G", "Government", "#f97316"),
    ];

    public static readonly string[] TypeLabels =
    [
        "Resi", "Firm", "Shop", "Cafe", "Hotel", "Restaurant", "Clinic",
    ];

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> DefaultPrefAttr =
        new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["Resi"] = new Dictionary<string, double> { ["T"] = 3.5, ["P"] = 5.0, ["R"] = -2.0, ["W"] = 4.5, ["S"] = 4.0, ["H"] = 2.0, ["G"] = 2.5 },
            ["Firm"] = new Dictionary<string, double> { ["T"] = 5.0, ["P"] = 1.0, ["R"] = 2.0, ["W"] = 1.0, ["S"] = 0.5, ["H"] = 1.0, ["G"] = 3.0 },
            ["Shop"] = new Dictionary<string, double> { ["T"] = 5.0, ["P"] = 4.0, ["R"] = 2.0, ["W"] = 2.0, ["S"] = 1.0, ["H"] = 1.0, ["G"] = 1.5 },
            ["Cafe"] = new Dictionary<string, double> { ["T"] = 2.0, ["P"] = 5.0, ["R"] = -1.0, ["W"] = 4.0, ["S"] = 2.0, ["H"] = 1.0, ["G"] = 1.0 },
            ["Hotel"] = new Dictionary<string, double> { ["T"] = 4.0, ["P"] = 3.0, ["R"] = 1.0, ["W"] = 4.0, ["S"] = 1.0, ["H"] = 2.0, ["G"] = 1.5 },
            ["Restaurant"] = new Dictionary<string, double> { ["T"] = 3.0, ["P"] = 4.5, ["R"] = 0.5, ["W"] = 4.0, ["S"] = 1.0, ["H"] = 1.0, ["G"] = 1.0 },
            ["Clinic"] = new Dictionary<string, double> { ["T"] = 2.5, ["P"] = 2.0, ["R"] = -0.5, ["W"] = 1.0, ["S"] = 2.0, ["H"] = 5.0, ["G"] = 2.0 },
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> DefaultPrefAgent =
        new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["Resi"] = new Dictionary<string, double> { ["Resi"] = 2.0, ["Firm"] = -1.0, ["Shop"] = 3.0, ["Cafe"] = 4.0, ["Hotel"] = 1.5, ["Restaurant"] = 3.5, ["Clinic"] = 2.5 },
            ["Firm"] = new Dictionary<string, double> { ["Resi"] = 1.0, ["Firm"] = 3.0, ["Shop"] = 4.0, ["Cafe"] = 2.0, ["Hotel"] = 2.0, ["Restaurant"] = 2.5, ["Clinic"] = 1.5 },
            ["Shop"] = new Dictionary<string, double> { ["Resi"] = 2.0, ["Firm"] = 4.0, ["Shop"] = 1.0, ["Cafe"] = 3.0, ["Hotel"] = 2.5, ["Restaurant"] = 4.0, ["Clinic"] = 1.5 },
            ["Cafe"] = new Dictionary<string, double> { ["Resi"] = 4.0, ["Firm"] = 2.0, ["Shop"] = 3.0, ["Cafe"] = -1.0, ["Hotel"] = 2.0, ["Restaurant"] = 4.0, ["Clinic"] = 1.0 },
            ["Hotel"] = new Dictionary<string, double> { ["Resi"] = 1.0, ["Firm"] = 2.0, ["Shop"] = 3.0, ["Cafe"] = 2.0, ["Hotel"] = -1.0, ["Restaurant"] = 4.0, ["Clinic"] = 1.5 },
            ["Restaurant"] = new Dictionary<string, double> { ["Resi"] = 3.0, ["Firm"] = 2.0, ["Shop"] = 3.5, ["Cafe"] = 4.0, ["Hotel"] = 3.0, ["Restaurant"] = -1.0, ["Clinic"] = 1.0 },
            ["Clinic"] = new Dictionary<string, double> { ["Resi"] = 2.5, ["Firm"] = 1.0, ["Shop"] = 1.0, ["Cafe"] = 1.0, ["Hotel"] = 1.0, ["Restaurant"] = 0.5, ["Clinic"] = -1.0 },
        };

    public static readonly IReadOnlyDictionary<string, string> PpBinColors = new Dictionary<string, string>
    {
        ["white"] = "#ffffff",
        ["lgray"] = "#cccccc",
        ["dgray"] = "#444444",
        ["black"] = "#000000",
    };

    public static readonly IReadOnlyDictionary<string, string> PpAgentRank = new Dictionary<string, string>
    {
        ["Shop"] = "white",
        ["Cafe"] = "white",
        ["Restaurant"] = "white",
        ["Clinic"] = "lgray",
        ["Hotel"] = "lgray",
        ["Firm"] = "dgray",
        ["Resi"] = "black",
    };

    public static readonly IReadOnlyDictionary<string, string> PpAttrRank = new Dictionary<string, string>
    {
        ["P"] = "white",
        ["W"] = "white",
        ["R"] = "white",
        ["T"] = "lgray",
        ["S"] = "dgray",
        ["H"] = "dgray",
        ["G"] = "dgray",
    };

    public static readonly IReadOnlyDictionary<string, string> AgentColors = new Dictionary<string, string>
    {
        ["Resi"] = "#063f76",
        ["Firm"] = "#b26d5d",
        ["Shop"] = "#d3b09d",
        ["Cafe"] = "#9a8fa8",
        ["Hotel"] = "#698e6c",
        ["Restaurant"] = "#ebead8",
        ["Clinic"] = "#8ba3c7",
    };

    public static class UiColors
    {
        public static readonly string Bg = "#0f0f0f";
        public static readonly string PanelBg = "#0a0a0a";
        public static readonly string Grid = "#1c1c1c";
        public static readonly string BoxBg = "#050505";
        public static readonly string Border = "#333333";
    }

    public static readonly string[] LayoutNames = ["Grid", "Radial", "Organic", "Linear", "Polycentric", "Superblock", "Hybrid"];
    public static readonly string[] RoadTopologyNames = ["From Layout", "Linear", "Parallel", "Cross", "T-Junction", "Loop"];

    public static readonly (string Label, string Value)[] SwapRuleOptions =
    [
        ("PARETO (no one worse)", "pareto"),
        ("GREEDY TOTAL (u1+u2 up)", "greedy_total"),
        ("GREEDY BOTH (both up)", "greedy_both"),
        ("GREEDY 1-SIDED (a1 up)", "greedy_1"),
    ];

    public static readonly (string Label, string Key)[] Tools =
    [
        ("VIEW", "None"), ("ROAD", "R"), ("PARK", "P"), ("TRANS", "T"), ("WATER", "W"),
        ("SCHOOL", "S"), ("HEALTH", "H"), ("GOV", "G"),
    ];

    // Original City Lab
    public static readonly (string Key, string Name, string ColorHex)[] OriginalCitylabAttrDefs =
    [
        ("transport", "Transport", "#ffffff"),
        ("public", "Public", "#7a507e"),
        ("road", "Road", "#333333"),
        ("waterfront", "Waterfront", "#89adcd"),
        ("landscape", "Landscape", "#4d8047"),
    ];

    public static readonly string[] OriginalCitylabTypeLabels = ["residential", "office", "shop", "cafe"];

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> OriginalCitylabDefaultPrefAttr =
        new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["residential"] = new Dictionary<string, double> { ["transport"] = 3.5, ["public"] = 4.0, ["road"] = -2.0, ["waterfront"] = 4.5, ["landscape"] = 5.0 },
            ["office"] = new Dictionary<string, double> { ["transport"] = 5.0, ["public"] = 6.0, ["road"] = 2.0, ["waterfront"] = 1.0, ["landscape"] = 1.0 },
            ["shop"] = new Dictionary<string, double> { ["transport"] = 5.0, ["public"] = 1.0, ["road"] = 2.0, ["waterfront"] = 2.0, ["landscape"] = 4.0 },
            ["cafe"] = new Dictionary<string, double> { ["transport"] = 2.0, ["public"] = 2.0, ["road"] = -1.0, ["waterfront"] = 4.0, ["landscape"] = 5.0 },
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> OriginalCitylabDefaultPrefAgent =
        new Dictionary<string, IReadOnlyDictionary<string, double>>
        {
            ["residential"] = new Dictionary<string, double> { ["residential"] = 2.0, ["office"] = -1.0, ["shop"] = 3.0, ["cafe"] = 4.0 },
            ["office"] = new Dictionary<string, double> { ["residential"] = 1.0, ["office"] = 3.0, ["shop"] = 4.0, ["cafe"] = 2.0 },
            ["shop"] = new Dictionary<string, double> { ["residential"] = 2.0, ["office"] = 4.0, ["shop"] = 1.0, ["cafe"] = 3.0 },
            ["cafe"] = new Dictionary<string, double> { ["residential"] = 4.0, ["office"] = 2.0, ["shop"] = 3.0, ["cafe"] = -1.0 },
        };

    public static readonly IReadOnlyDictionary<string, string> OriginalCitylabAgentColors = new Dictionary<string, string>
    {
        ["residential"] = "#063f76",
        ["office"] = "#b26d5d",
        ["shop"] = "#d3b09d",
        ["cafe"] = "#9a8fa8",
    };

    public static readonly (string Label, string Key)[] OriginalCitylabTools =
    [
        ("VIEW", "None"),
        ("ROAD", "road"),
        ("LANDSCAPE", "landscape"),
        ("TRANS", "transport"),
        ("WATER", "waterfront"),
        ("PUBLIC", "public"),
    ];

    public static readonly IReadOnlyDictionary<string, string> OriginalCitylabPpAgentRank = new Dictionary<string, string>
    {
        ["shop"] = "white",
        ["cafe"] = "white",
        ["office"] = "dgray",
        ["residential"] = "black",
    };

    public static readonly IReadOnlyDictionary<string, string> OriginalCitylabPpAttrRank = new Dictionary<string, string>
    {
        ["landscape"] = "white",
        ["waterfront"] = "white",
        ["road"] = "white",
        ["transport"] = "lgray",
        ["public"] = "dgray",
    };

    public static Color HexColor(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return Color.White;
        return ColorTranslator.FromHtml(hex.StartsWith('#') ? hex : "#" + hex);
    }
}
