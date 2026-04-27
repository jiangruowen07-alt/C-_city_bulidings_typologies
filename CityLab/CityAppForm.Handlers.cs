// Event handlers & view logic

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CityLab;

public sealed partial class CityAppForm
{
    internal void SetLayoutBtnHi(string name)
    {
        foreach (var b in _layoutBtnList)
        {
            var isHi = (string?)b.Tag == name;
            b.BackColor = isHi ? Color.White : Config.HexColor(Config.UiColors.PanelBg);
            b.ForeColor = isHi ? Color.Black : Color.White;
        }
    }

    internal void SetRoadBtnHi(string name)
    {
        foreach (var b in _roadBtnList)
        {
            var isHi = (string?)b.Tag == name;
            b.BackColor = isHi ? Color.White : Config.HexColor(Config.UiColors.PanelBg);
            b.ForeColor = isHi ? Color.Black : Color.White;
        }
    }

    void SetTool(string key)
    {
        _currentTool = key;
        foreach (var kv in _toolBtns)
        {
            var isHi = kv.Key == key;
            kv.Value.BackColor = isHi ? Color.White : Config.HexColor(Config.UiColors.PanelBg);
            kv.Value.ForeColor = isHi ? Color.Black : Color.White;
        }
    }

    void RebuildToolButtons()
    {
        if (_toolFrame is not TableLayoutPanel tlp) return;
        tlp.Controls.Clear();
        _toolBtns.Clear();
        var tools = M.UseOriginalCitylab ? Config.OriginalCitylabTools : Config.Tools;
        var list = tools.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var (label, key) = list[i];
            int col = i % 4, row = i / 4;
            var b = new Button
            {
                Text = label,
                Dock = DockStyle.Fill,
                Margin = new Padding(3),
                FlatStyle = FlatStyle.Flat,
                Font = _fBtn,
                Tag = key,
                BackColor = key == "None" ? Color.White : Config.HexColor(Config.UiColors.PanelBg),
                ForeColor = key == "None" ? Color.Black : Color.White
            };
            var k = key;
            b.Click += (_, _) => SetTool(k);
            tlp.Controls.Add(b, col, row);
            _toolBtns[key] = b;
        }
    }

    void BuildLegend()
    {
        _legendFrame.Controls.Clear();
        var flow = new FlowLayoutPanel
        { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(4), BackColor = Config.HexColor(Config.UiColors.BoxBg) };
        _legendFrame.Controls.Add(flow);
        void AddLine(string text, Color? fill, string shape = "rect")
        {
            var line = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            var ic = new Panel { Width = 18, Height = 18, BackColor = Config.HexColor(Config.UiColors.BoxBg) };
            ic.Paint += (_, e) =>
            {
                if (!fill.HasValue) return;
                using var b = new SolidBrush(fill.Value);
                if (shape == "rect") e.Graphics.FillRectangle(b, 2, 2, 14, 14);
                else e.Graphics.FillEllipse(b, 2, 2, 14, 14);
            };
            line.Controls.Add(ic);
            line.Controls.Add(new Label { Text = text, ForeColor = Color.FromArgb(0xe0, 0xe0, 0xe0), AutoSize = true, Font = _fBody, BackColor = Config.HexColor(Config.UiColors.BoxBg) });
            flow.Controls.Add(line);
        }
        flow.Controls.Add(new Label { Text = "吸引子", ForeColor = Color.FromArgb(0xa0, 0xa0, 0xa0), AutoSize = true, Font = _fSection, BackColor = Config.HexColor(Config.UiColors.BoxBg), Padding = new Padding(0, 0, 0, 4) });
        var adefs = M.UseOriginalCitylab ? Config.OriginalCitylabAttrDefs : Config.AttrDefs;
        foreach (var t in adefs) AddLine(t.Name, ColorForKey(t.Key), "rect");
        flow.Controls.Add(new Label { Text = "代理", ForeColor = Color.FromArgb(0xa0, 0xa0, 0xa0), AutoSize = true, Font = _fSection, BackColor = Config.HexColor(Config.UiColors.BoxBg), Margin = new Padding(0, 10, 0, 4) });
        if (M.UseOriginalCitylab)
        {
            AddLine("Residential", ColorForAgent("residential"), "oval");
            AddLine("Office", ColorForAgent("office"), "oval");
            AddLine("Shop", ColorForAgent("shop"), "oval");
            AddLine("Cafe", ColorForAgent("cafe"), "oval");
        }
        else
        {
            AddLine("Residential", ColorForAgent("Resi"), "oval");
            AddLine("Company", ColorForAgent("Firm"), "oval");
            AddLine("Retail", ColorForAgent("Shop"), "oval");
            AddLine("Cafe", ColorForAgent("Cafe"), "oval");
            AddLine("Hotel", ColorForAgent("Hotel"), "oval");
            AddLine("Restaurant", ColorForAgent("Restaurant"), "oval");
            AddLine("Clinic", ColorForAgent("Clinic"), "oval");
        }
    }

    void BuildMatrices()
    {
        _matrixEnt.Clear();
        void Clear(Control c) { c.Controls.Clear(); }
        Clear(_matrixAttr); Clear(_matrixAgent); Clear(_matrixInflAttr); Clear(_matrixInflAg);

        void MatrixTable(TableLayoutPanel parent, string which, IReadOnlyList<string> colKeys)
        {
            parent.SuspendLayout();
            try
            {
                parent.Controls.Clear();
                var totalCols = colKeys.Count + 1;
                var totalRows = M.TypeLabels.Count + 1;
                parent.ColumnCount = totalCols;
                parent.RowCount = totalRows;
                parent.ColumnStyles.Clear();
                parent.RowStyles.Clear();
                parent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiFonts.MatrixRowHeader));
                for (var c = 1; c < totalCols; c++)
                    parent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiFonts.MatrixColWidth));
                parent.RowStyles.Add(new RowStyle(SizeType.Absolute, UiFonts.MatrixHeaderHeight));
                for (var r = 1; r < totalRows; r++)
                    parent.RowStyles.Add(new RowStyle(SizeType.Absolute, UiFonts.MatrixRowHeight));
                var cellPad = new Padding(1);
                parent.Controls.Add(new Label { Text = "", BackColor = parent.BackColor, Dock = DockStyle.Fill, Margin = cellPad }, 0, 0);
                for (var j = 0; j < colKeys.Count; j++)
                {
                    parent.Controls.Add(new Label
                    {
                        Text = colKeys[j],
                        ForeColor = Color.FromArgb(0xa8, 0xa8, 0xa8),
                        AutoSize = false,
                        Font = _fMxH,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Dock = DockStyle.Fill,
                        BackColor = parent.BackColor,
                        Margin = cellPad
                    }, j + 1, 0);
                }
                for (var i = 0; i < M.TypeLabels.Count; i++)
                {
                    var row = M.TypeLabels[i];
                    parent.Controls.Add(new Label
                    {
                        Text = row,
                        ForeColor = Color.FromArgb(0xc0, 0xc0, 0xc0),
                        AutoSize = false,
                        Font = _fMxH,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Dock = DockStyle.Fill,
                        BackColor = parent.BackColor,
                        Margin = cellPad
                    }, 0, i + 1);
                    for (var j = 0; j < colKeys.Count; j++)
                    {
                        var col = colKeys[j];
                        var tb = new TextBox
                        {
                            Text = (which == "attr" ? M.PrefAttr[row][col] : M.PrefAgent[row][col]).ToString(CultureInfo.InvariantCulture),
                            BackColor = Config.HexColor(Config.UiColors.BoxBg),
                            ForeColor = Color.White,
                            Font = _fMxC,
                            BorderStyle = BorderStyle.FixedSingle,
                            TextAlign = HorizontalAlignment.Center,
                            Dock = DockStyle.Fill,
                            Margin = cellPad
                        };
                        var wch = which; var r = row; var cj = col;
                        void Commit(object? s, EventArgs e)
                        {
                            if (double.TryParse(tb.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var v2))
                            {
                                if (wch == "attr") M.PrefAttr[r][cj] = v2; else M.PrefAgent[r][cj] = v2;
                            }
                            else
                            {
                                v2 = 0; tb.Text = "0";
                                if (wch == "attr") M.PrefAttr[r][cj] = 0; else M.PrefAgent[r][cj] = 0;
                            }
                            M.CalcTotalUtility();
                            _resetTarget = (int)M.Stats.TotalUtility;
                            UpdateStats();
                            SampleChart(true);
                            if (_showView && _viewMode == "contrib")
                            { RebuildContribCache(); _gridView?.Invalidate(); }
                        }
                        tb.Leave += Commit;
                        tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Commit(s, e); (s as Control)?.Parent?.Focus(); e.SuppressKeyPress = true; } };
                        _matrixEnt[mk(wch, row, col)] = tb;
                        parent.Controls.Add(tb, j + 1, i + 1);
                    }
                }
            }
            finally { parent.ResumeLayout(); }
        }

        static string mk(string a, string b, string c) => a + "\t" + b + "\t" + c;

        MatrixTable(_matrixAttr, "attr", M.AttrKeys);
        MatrixTable(_matrixAgent, "agent", M.TypeLabels);

        void InflTable(TableLayoutPanel parent, bool isAttr)
        {
            var cellPad = new Padding(1);
            parent.SuspendLayout();
            try
            {
                parent.Controls.Clear();
                var totalCols = M.TypeLabels.Count + 1;
                var totalRows = isAttr ? M.AttrKeys.Count + 1 : M.TypeLabels.Count + 1;
                parent.ColumnCount = totalCols;
                parent.RowCount = totalRows;
                parent.ColumnStyles.Clear();
                parent.RowStyles.Clear();
                parent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiFonts.MatrixRowHeader));
                for (var c = 1; c < totalCols; c++)
                    parent.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, UiFonts.MatrixColWidth));
                parent.RowStyles.Add(new RowStyle(SizeType.Absolute, UiFonts.MatrixHeaderHeight));
                for (var r = 1; r < totalRows; r++)
                    parent.RowStyles.Add(new RowStyle(SizeType.Absolute, UiFonts.MatrixRowHeight));
                parent.Controls.Add(new Label { Text = "", BackColor = parent.BackColor, Dock = DockStyle.Fill, Margin = cellPad }, 0, 0);
                for (var j = 0; j < M.TypeLabels.Count; j++)
                {
                    var shortT = M.TypeLabels[j].Length > 4 ? M.TypeLabels[j].AsSpan(0, 4).ToString() : M.TypeLabels[j];
                    parent.Controls.Add(new Label
                    {
                        Text = shortT,
                        ForeColor = Color.FromArgb(0xa8, 0xa8, 0xa8),
                        AutoSize = false,
                        Font = _fMxH,
                        Dock = DockStyle.Fill,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = parent.BackColor,
                        Margin = cellPad
                    }, j + 1, 0);
                }
                if (isAttr)
                {
                    for (var i = 0; i < M.AttrKeys.Count; i++)
                    {
                        var k = M.AttrKeys[i];
                        parent.Controls.Add(new Label
                        {
                            Text = k,
                            ForeColor = Color.FromArgb(0xc0, 0xc0, 0xc0),
                            AutoSize = false,
                            Font = _fMxH,
                            TextAlign = ContentAlignment.MiddleLeft,
                            Dock = DockStyle.Fill,
                            BackColor = parent.BackColor,
                            Margin = cellPad
                        }, 0, i + 1);
                        for (var j = 0; j < M.TypeLabels.Count; j++)
                        {
                            var t = M.TypeLabels[j];
                            var tb = new TextBox
                            {
                                Text = M.InfluenceRangeAttr[k][t].ToString(),
                                BackColor = Config.HexColor(Config.UiColors.BoxBg),
                                ForeColor = Color.White,
                                Font = _fMxC,
                                TextAlign = HorizontalAlignment.Center,
                                Dock = DockStyle.Fill,
                                Margin = cellPad
                            };
                            var kk = k; var tt = t;
                            void Cmt(object? s, EventArgs e)
                            {
                                if (!int.TryParse(tb.Text, out var v)) v = M.Reach; v = Math.Max(1, v);
                                tb.Text = v.ToString();
                                M.InfluenceRangeAttr[kk][tt] = v;
                                M.CalcTotalUtility();
                                _resetTarget = (int)M.Stats.TotalUtility;
                                UpdateStats();
                                SampleChart(true);
                                if (_showView && _viewMode == "contrib") { RebuildContribCache(); _gridView?.Invalidate(); }
                                if (_showReachOverlay && _hoverCx >= 0) _gridView?.Invalidate();
                            }
                            tb.Leave += Cmt; tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Cmt(s, e); (s as Control)?.Parent?.Focus(); e.SuppressKeyPress = true; } };
                            _matrixEnt[mk("influence_attr", k, t)] = tb;
                            parent.Controls.Add(tb, j + 1, i + 1);
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < M.TypeLabels.Count; i++)
                    {
                        var src = M.TypeLabels[i];
                        parent.Controls.Add(new Label
                        {
                            Text = src,
                            ForeColor = Color.FromArgb(0xc0, 0xc0, 0xc0),
                            AutoSize = false,
                            Font = _fMxH,
                            TextAlign = ContentAlignment.MiddleLeft,
                            Dock = DockStyle.Fill,
                            BackColor = parent.BackColor,
                            Margin = cellPad
                        }, 0, i + 1);
                        for (var j = 0; j < M.TypeLabels.Count; j++)
                        {
                            var tgt = M.TypeLabels[j];
                            var tb = new TextBox
                            {
                                Text = M.InfluenceRangeAgent[src][tgt].ToString(),
                                BackColor = Config.HexColor(Config.UiColors.BoxBg),
                                ForeColor = Color.White,
                                Font = _fMxC,
                                TextAlign = HorizontalAlignment.Center,
                                Dock = DockStyle.Fill,
                                Margin = cellPad
                            };
                            var ss = src; var tg = tgt;
                            void Cmt(object? s, EventArgs e)
                            {
                                if (!int.TryParse(tb.Text, out var v)) v = M.Reach; v = Math.Max(1, v);
                                tb.Text = v.ToString();
                                M.InfluenceRangeAgent[ss][tg] = v;
                                M.CalcTotalUtility();
                                _resetTarget = (int)M.Stats.TotalUtility;
                                UpdateStats();
                                SampleChart(true);
                                if (_showView && _viewMode == "contrib") { RebuildContribCache(); _gridView?.Invalidate(); }
                                if (_showReachOverlay && _hoverCx >= 0) _gridView?.Invalidate();
                            }
                            tb.Leave += Cmt;
                            tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Cmt(s, e); (s as Control)?.Parent?.Focus(); e.SuppressKeyPress = true; } };
                            _matrixEnt[mk("influence_agent", src, tgt)] = tb;
                            parent.Controls.Add(tb, j + 1, i + 1);
                        }
                    }
                }
            }
            finally { parent.ResumeLayout(); }
        }

        InflTable(_matrixInflAttr, true);
        InflTable(_matrixInflAg, false);
    }

    void RebuildContribCache()
    {
        if (_contribCache.GetLength(0) != M.W || _contribCache.GetLength(1) != M.H)
            _contribCache = new double[M.W, M.H];
        var vals = new List<double>();
        for (var x = 0; x < M.W; x++)
        {
            for (var y = 0; y < M.H; y++)
            {
                var a = M.Grid[x, y];
                var v = a != null ? M.GetUtility(a, x, y) : 0.0;
                _contribCache[x, y] = v;
                vals.Add(v);
            }
        }
        _contribMin = vals.Count > 0 ? vals.Min() : 0; _contribMax = vals.Count > 0 ? vals.Max() : 1;
    }

    string PubprivFillAt(int x, int y)
    {
        var a = M.Grid[x, y];
        var k = M.AttrGrid[x, y];
        if (_viewAgentFirst && a != null)
        {
            var rank = _ppAgentRank.GetValueOrDefault(a.Type, "black");
            return Config.PpBinColors.GetValueOrDefault(rank, "#000000");
        }
        if (k != null)
        {
            var rank2 = _ppAttrRank.GetValueOrDefault(k, "black");
            return Config.PpBinColors.GetValueOrDefault(rank2, "#000000");
        }
        if (!_viewAgentFirst && a != null)
        {
            var rank = _ppAgentRank.GetValueOrDefault(a.Type, "black");
            return Config.PpBinColors.GetValueOrDefault(rank, "#000000");
        }
        return Config.PpBinColors["black"];
    }

    string ContribFillAt(int x, int y)
    {
        var v = _contribCache[x, y];
        if (Math.Abs(_contribMax - _contribMin) < 1e-9) return "#808080";
        var t = (v - _contribMin) / (_contribMax - _contribMin);
        t = Math.Max(0, Math.Min(1, t));
        var g = (int)(255 * t);
        return $"#{g:X2}{g:X2}{g:X2}";
    }

    internal Color ViewFillAt(int x, int y)
    {
        if (_viewMode == "contrib")
        {
            var s = ContribFillAt(x, y);
            return Config.HexColor(s);
        }
        return Config.HexColor(PubprivFillAt(x, y));
    }

    void UpdateStats()
    {
        _iterL.Text = M.Stats.Steps.ToString("N0", CultureInfo.InvariantCulture);
        _utilL.Text = ((int)M.Stats.TotalUtility).ToString("N0", CultureInfo.InvariantCulture);
        _accL.Text = M.Stats.Accepted.ToString("N0", CultureInfo.InvariantCulture);
        _rejL.Text = M.Stats.Rejected.ToString("N0", CultureInfo.InvariantCulture);
        _sacL.Text = M.Stats.Sacrificed.ToString("N0", CultureInfo.InvariantCulture);
    }

    void SampleChart(bool force = false)
    {
        if (force || (M.Stats.Steps % 20 == 0))
        {
            var v = M.Stats.TotalUtility;
            if (force || _utilHistory.Count == 0 || Math.Abs(_utilHistory[^1] - v) > 1e-9) { _utilHistory.Add(v); if (_utilHistory.Count > ChartMax) _utilHistory.RemoveAt(0); }
            _chartPanel.Invalidate();
        }
    }

    void ChartPaint(object? s, PaintEventArgs e)
    {
        var cv = e.Graphics; var w = _chartPanel.ClientSize.Width; var h2 = _chartPanel.ClientSize.Height;
        cv.Clear(Config.HexColor(Config.UiColors.BoxBg));
        if (w < 40 || h2 < 40) return;
        var pad = ChartPad; var labelH = 14;
        var x0 = pad; var y0 = pad + labelH; var x1 = w - pad; var y1 = h2 - pad - labelH;
        if (x1 <= x0 + 10 || y1 <= y0 + 10) return;
        using (var p = new Pen(Color.FromArgb(0x33, 0x33, 0x33), 1f)) { cv.DrawRectangle(p, pad, pad, w - 2 * pad, h2 - 2 * pad); }
        var data = _utilHistory;
        if (data.Count < 2) { cv.DrawString("运行模拟以查看总效用趋势", _fCh, Brushes.Gray, w / 2f, h2 / 2f, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }); return; }
        var vmin = data.Min(); var vmax = data.Max();
        if (Math.Abs(vmax - vmin) < 1e-9) vmax = vmin + 1;
        for (var i = 1; i < 4; i++) { var yy = y0 + (y1 - y0) * i / 4f; using var pen = new Pen(Color.FromArgb(0x11, 0x11, 0x11), 1f); cv.DrawLine(pen, x0, yy, x1, yy); }
        cv.DrawString(((int)vmax).ToString("N0", CultureInfo.InvariantCulture), _fChD, Brushes.Gray, pad + 6, pad + 4);
        cv.DrawString(((int)vmin).ToString("N0", CultureInfo.InvariantCulture), _fChD, Brushes.Gray, pad + 6, h2 - pad - 4, new StringFormat { LineAlignment = StringAlignment.Far });
        cv.DrawString(((int)data[^1]).ToString("N0", CultureInfo.InvariantCulture), _fChDB, Brushes.White, w - pad - 6, pad + 4, new StringFormat { Alignment = StringAlignment.Far });
        var n = data.Count; var dx = (x1 - x0) / (n - 1);
        PointF? last = null;
        for (var i = 0; i < n; i++)
        {
            var t = (data[i] - vmin) / (vmax - vmin);
            var px = x0 + i * dx;
            var py = y1 - (float)(t * (y1 - y0));
            if (last != null) cv.DrawLine(new Pen(Color.White, 2f), last.Value, new PointF(px, py));
            last = new PointF(px, py);
        }
        if (last.HasValue) cv.FillEllipse(Brushes.White, last.Value.X - 3, last.Value.Y - 3, 6, 6);
    }

    public void RebuildAll()
    {
        M.RemoveAgentsOnAttractors();
        if (_showView && _viewMode == "contrib") RebuildContribCache();
        _gridView?.Invalidate();
        UpdateStats();
        SampleChart(true);
    }
}
