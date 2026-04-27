// 3×3 建筑黑白形：全量枚举 + 按「主要功能类」各预存 10 种互异形（不运行建筑模拟即可用于城市替圆）。

using System;
using System.Collections.Generic;
using System.Numerics;

namespace CityLab;

public static class BuildingShapeCatalog
{
    public const int PatternsPerMainType = 10;

    /// <summary>不区分 k 的全部非全白形（511）。</summary>
    public static int TotalNonEmptyMasks { get; }

    /// <summary>四种功能类各 10 个 3×3 掩模（黑=非绿地），键为 Resi/Firm/Cafe/Shop。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<bool[,]>> PresetPatternsByMainType { get; }

    /// <summary>与 <see cref="PresetPatternsByMainType"/> 同序：每个槽位对应图案的黑格数 1..9。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> PresetBlackCountsByMainType { get; }

    static BuildingShapeCatalog()
    {
        var allFlat = new List<bool[,]>(511);
        for (var mask = 0; mask < 512; mask++)
        {
            var nBlack = (int)BitOperations.PopCount((uint)mask);
            if (nBlack == 0) continue;
            allFlat.Add(MaskToGrid(mask));
        }
        TotalNonEmptyMasks = allFlat.Count;
        PresetPatternsByMainType = BuildPresetsTenPerType(allFlat);
        PresetBlackCountsByMainType = BuildBlackCountLists();
    }

    static IReadOnlyDictionary<string, IReadOnlyList<bool[,]>> BuildPresetsTenPerType(List<bool[,]> pool)
    {
        var dict = new Dictionary<string, IReadOnlyList<bool[,]>>(StringComparer.Ordinal);
        var n = pool.Count;
        foreach (var key in BuildingModel.BuildingFunctionTypes)
        {
            var rng = new Random(unchecked((int)(0x243F6A88 ^ (key.GetHashCode(StringComparison.Ordinal) * 0x9E3779B9))));
            var order = new int[n];
            for (var i = 0; i < n; i++) order[i] = i;
            for (var i = n - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var ten = new List<bool[,]>(PatternsPerMainType);
            for (var t = 0; t < PatternsPerMainType; t++)
                ten.Add(Clone(pool[order[t]]));
            dict[key] = ten;
        }
        return dict;
    }

    static IReadOnlyDictionary<string, IReadOnlyList<int>> BuildBlackCountLists()
    {
        var d = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var kv in PresetPatternsByMainType)
        {
            var counts = new int[kv.Value.Count];
            for (var i = 0; i < kv.Value.Count; i++)
                counts[i] = CountBlackInMask(kv.Value[i]);
            d[kv.Key] = counts;
        }
        return d;
    }

    public static int CountBlackInMask(bool[,] g)
    {
        var n = 0;
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                if (g[x, y]) n++;
        return n;
    }

    public static int GetBlackCountInPreset(string buildingFunctionKey, int slot)
    {
        if (Array.IndexOf(BuildingModel.BuildingFunctionTypes, buildingFunctionKey) < 0)
            buildingFunctionKey = "Resi";
        var i = (slot % PatternsPerMainType + PatternsPerMainType) % PatternsPerMainType;
        return PresetBlackCountsByMainType[buildingFunctionKey][i];
    }

    public const int MinBlackCellsCommercialDisplay = 1;
    public const int MaxBlackCellsCommercialDisplay = 9;

    /// <summary>
    /// 在 10 个预形中选取黑格数与 <paramref name="targetBlackCount"/> 最接近的槽位；优先 [1,9]，否则 ≥1，再否则任意。
    /// 多槽等距时由 <paramref name="tieBreaker"/> 决定取哪一个，使同映射类、同目标黑格时在同城不同格上也可不同形（建议传格点混合哈希，如 <c>x*73856093^y*19349663</c>）。
    /// </summary>
    public static int PickSlotForTargetBlackCount(string buildingFunctionKey, int targetBlackCount, int tieBreaker = 0)
    {
        if (Array.IndexOf(BuildingModel.BuildingFunctionTypes, buildingFunctionKey) < 0)
            buildingFunctionKey = "Resi";
        targetBlackCount = Math.Clamp(targetBlackCount, MinBlackCellsCommercialDisplay, MaxBlackCellsCommercialDisplay);
        var counts = PresetBlackCountsByMainType[buildingFunctionKey];
        for (var pass = 0; pass < 3; pass++)
        {
            bool Allow(int c) => pass switch
            {
                0 => c >= MinBlackCellsCommercialDisplay && c <= MaxBlackCellsCommercialDisplay,
                1 => c >= MinBlackCellsCommercialDisplay,
                _ => true
            };
            var bestErr = int.MaxValue;
            var candidates = new List<int>(PatternsPerMainType);
            for (var s = 0; s < counts.Count; s++)
            {
                if (!Allow(counts[s])) continue;
                var err = Math.Abs(counts[s] - targetBlackCount);
                if (err < bestErr)
                {
                    bestErr = err;
                    candidates.Clear();
                    candidates.Add(s);
                }
                else if (err == bestErr)
                {
                    candidates.Add(s);
                }
            }
            if (candidates.Count > 0)
            {
                if (candidates.Count == 1) return candidates[0];
                var u = (uint)tieBreaker;
                return candidates[(int)(u % (uint)candidates.Count)];
            }
        }
        return 0;
    }

    /// <summary>该功能类 10 个预形中黑格数最多的槽位；并列时由 <paramref name="tieBreaker"/> 打散。</summary>
    public static int PickSlotWithMaxBlackCells(string buildingFunctionKey, int tieBreaker = 0)
    {
        if (Array.IndexOf(BuildingModel.BuildingFunctionTypes, buildingFunctionKey) < 0)
            buildingFunctionKey = "Resi";
        var counts = PresetBlackCountsByMainType[buildingFunctionKey];
        var maxC = 0;
        for (var s = 0; s < counts.Count; s++)
        {
            if (counts[s] > maxC) maxC = counts[s];
        }
        var candidates = new List<int>(PatternsPerMainType);
        for (var s = 0; s < counts.Count; s++)
        {
            if (counts[s] == maxC) candidates.Add(s);
        }
        if (candidates.Count == 0) return 0;
        if (candidates.Count == 1) return candidates[0];
        var u = (uint)tieBreaker;
        return candidates[(int)(u % (uint)candidates.Count)];
    }

    /// <summary>该功能类 10 个预形中黑格数最少的槽位；并列时由 <paramref name="tieBreaker"/> 打散。</summary>
    public static int PickSlotWithMinBlackCells(string buildingFunctionKey, int tieBreaker = 0)
    {
        if (Array.IndexOf(BuildingModel.BuildingFunctionTypes, buildingFunctionKey) < 0)
            buildingFunctionKey = "Resi";
        var counts = PresetBlackCountsByMainType[buildingFunctionKey];
        var minC = int.MaxValue;
        for (var s = 0; s < counts.Count; s++)
        {
            if (counts[s] < minC) minC = counts[s];
        }
        var candidates = new List<int>(PatternsPerMainType);
        for (var s = 0; s < counts.Count; s++)
        {
            if (counts[s] == minC) candidates.Add(s);
        }
        if (candidates.Count == 0) return 0;
        if (candidates.Count == 1) return candidates[0];
        var u = (uint)tieBreaker;
        return candidates[(int)(u % (uint)candidates.Count)];
    }

    /// <summary>按功能类取预形，<paramref name="slot"/> 使用 0..9（对 PatternsPerMainType 取模）。</summary>
    public static bool[,] GetPresetPattern(string buildingFunctionKey, int slot)
    {
        if (Array.IndexOf(BuildingModel.BuildingFunctionTypes, buildingFunctionKey) < 0)
            buildingFunctionKey = "Resi";
        var list = PresetPatternsByMainType[buildingFunctionKey];
        var i = (slot % PatternsPerMainType + PatternsPerMainType) % PatternsPerMainType;
        return Clone(list[i]);
    }

    /// <summary>位序 i = y*3 + x，与 B.Grid[x,y] 一致。</summary>
    static bool[,] MaskToGrid(int mask)
    {
        var g = new bool[3, 3];
        for (var i = 0; i < 9; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            var x = i % 3;
            var y = i / 3;
            g[x, y] = true;
        }
        return g;
    }

    static bool[,] Clone(bool[,] src)
    {
        var c = new bool[3, 3];
        for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
                c[x, y] = src[x, y];
        return c;
    }
}
