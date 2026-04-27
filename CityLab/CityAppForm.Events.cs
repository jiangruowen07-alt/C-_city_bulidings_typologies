// Input, simulation loop, export

using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace CityLab;

public sealed partial class CityAppForm
{
    Point _pendingMouseClient;

    void ToggleRun()
    {
        if (_batchRunning) return;
        _running = !_running;
        if (_running)
        {
            _runBtn.Text = "PAUSE SIMULATION";
            _runBtn.BackColor = Color.FromArgb(0xef, 0x44, 0x44);
            _runBtn.ForeColor = Color.White;
        }
        else
        {
            _runBtn.Text = "START SIMULATION";
            _runBtn.BackColor = Color.White;
            _runBtn.ForeColor = Color.Black;
        }
        _statusDot.Invalidate();
        _loopT.Enabled = _running;
    }

    void LoopTick(object? s, EventArgs e)
    {
        if (!_running || _batchRunning) return;
        var speed = _speedBar.Value;
        for (var i = 0; i < speed; i++)
            M.Step();
        if (M.Stats.Steps % 20 == 0) { M.CalcTotalUtility(); SampleChart(); }
        if (_showView && _viewMode == "contrib" && (M.Stats.Steps % ContribInt == 0))
            RebuildContribCache();
        UpdateStats();
        _gridView.Invalidate();
    }

    void StepOnce()
    {
        if (_batchRunning) return;
        M.Step();
        if (M.Stats.Steps % 20 == 0) { M.CalcTotalUtility(); SampleChart(); }
        if (_showView && _viewMode == "contrib" && (M.Stats.Steps % ContribInt == 0)) { RebuildContribCache(); }
        UpdateStats();
        _gridView.Invalidate();
    }

    void DoReset()
    {
        if (_batchRunning) return;
        if (_resetTarget == null)
        {
            M.UtilityBias = 0.0; M.Reset(); M.CalcTotalUtility();
            _resetTarget = (int)M.Stats.TotalUtility;
        }
        else M.Reset(_resetTarget);
        _utilHistory.Clear();
        RebuildAll();
    }

    void DoClear()
    {
        if (_batchRunning) return;
        M.UtilityBias = 0.0; M.Clear(); M.CalcTotalUtility();
        _resetTarget = (int)M.Stats.TotalUtility;
        _utilHistory.Clear();
        RebuildAll();
    }

    void SetLayout(string name)
    {
        if (_batchRunning) return;
        SetLayoutBtnHi(name);
        M.UtilityBias = 0.0; M.ApplyLayout(name); M.CalcTotalUtility();
        _resetTarget = (int)M.Stats.TotalUtility; _utilHistory.Clear();
        _contribCache = new double[M.W, M.H];
        RebuildAll();
    }

    void SetRoad(string name)
    {
        if (_batchRunning) return;
        SetRoadBtnHi(name);
        M.UtilityBias = 0.0; M.ApplyRoadTopology(name);
        M.RebuildAttrDistanceFields();
        M.Reset(_resetTarget);
        M.CalcTotalUtility();
        _resetTarget = (int)M.Stats.TotalUtility; _utilHistory.Clear();
        _contribCache = new double[M.W, M.H];
        RebuildAll();
    }

    void TogPub()
    {
        if (_showView && _viewMode == "pubpriv") { _showView = false; _ppBtn.BackColor = Config.HexColor(Config.UiColors.PanelBg); _ppBtn.ForeColor = Color.White; _gridView.Invalidate(); return; }
        _viewMode = "pubpriv"; _showView = true; _ppBtn.BackColor = Color.White; _ppBtn.ForeColor = Color.Black;
        _contribBtn.BackColor = Config.HexColor(Config.UiColors.PanelBg); _contribBtn.ForeColor = Color.White;
        _gridView.Invalidate();
    }

    void TogContrib()
    {
        if (_showView && _viewMode == "contrib") { _showView = false; _contribBtn.BackColor = Config.HexColor(Config.UiColors.PanelBg); _contribBtn.ForeColor = Color.White; _gridView.Invalidate(); return; }
        _viewMode = "contrib"; _showView = true; _contribBtn.BackColor = Color.White; _contribBtn.ForeColor = Color.Black;
        _ppBtn.BackColor = Config.HexColor(Config.UiColors.PanelBg); _ppBtn.ForeColor = Color.White;
        RebuildContribCache(); _gridView.Invalidate();
    }

    void TogReach()
    {
        _showReachOverlay = !_showReachOverlay;
        _reachBtn.BackColor = _showReachOverlay ? Color.White : Config.HexColor(Config.UiColors.PanelBg);
        _reachBtn.ForeColor = _showReachOverlay ? Color.Black : Color.White;
        if (!_showReachOverlay) { _hoverCx = -1; }
        _gridView.Invalidate();
    }

    void TogOrig()
    {
        if (_batchRunning) return;
        if (M.UseOriginalCitylab) M.SwitchToStandard(); else M.SwitchToOriginalCitylab();
        RefreshColorMap(); RefreshPpRanks();
        RebuildToolButtons(); BuildLegend(); BuildMatrices();
        _origBtn.BackColor = M.UseOriginalCitylab ? Color.White : Config.HexColor(Config.UiColors.PanelBg);
        _origBtn.ForeColor = M.UseOriginalCitylab ? Color.Black : Color.White;
        SetTool("None");
        _utilHistory.Clear();
        _contribCache = new double[M.W, M.H];
        RebuildAll();
    }

    void RunNSteps()
    {
        if (_batchRunning) return;
        if (!int.TryParse(_runNbox.Text, out var n)) n = 0;
        n = Math.Max(0, n);
        if (n <= 0) return;
        _batchPrevRun = _running;
        if (_running) { _running = false; _loopT.Enabled = false; _runBtn.Text = "START SIMULATION"; _runBtn.BackColor = Color.White; _runBtn.ForeColor = Color.Black; _statusDot.Invalidate(); }
        _batchRunning = true; _batchRem = n; _batchDirty.Clear();
        _batchStatus.Text = $"{n} left";
        _batchT.Enabled = true;
    }

    void BatchTick(object? s, EventArgs e)
    {
        if (!_batchRunning) return;
        var chunk = Math.Min(BatchChunk, _batchRem);
        for (var i = 0; i < chunk; i++)
        {
            var res = M.Step();
            if (res.Swapped && _showView && _viewMode == "pubpriv")
            {
                if (res.Old1 != null) _batchDirty.Add(res.Old1.Value);
                if (res.Old2 != null) _batchDirty.Add(res.Old2.Value);
            }
        }
        _batchRem -= chunk;
        if (M.Stats.Steps % 20 == 0) M.CalcTotalUtility();
        if (_showView && _viewMode == "contrib")
            RebuildContribCache();
        UpdateStats(); SampleChart();
        _gridView.Invalidate();
        if (_batchRem > 0) _batchStatus.Text = $"{_batchRem} left";
        else
        {
            _batchRunning = false; _batchT.Enabled = false; _batchStatus.Text = "done";
            if (_batchPrevRun) { _running = true; _loopT.Enabled = true; _runBtn.Text = "PAUSE SIMULATION"; _runBtn.BackColor = Color.FromArgb(0xef, 0x44, 0x44); _runBtn.ForeColor = Color.White; _statusDot.Invalidate(); }
        }
    }

    void OnGridMouse(object? s, MouseEventArgs e)
    {
        if (_batchRunning || e.Button != MouseButtons.Left) return;
        if (_currentTool == "None") return;
        int x = (int)(e.X / _cell), y = (int)(e.Y / _cell);
        if (x < 0 || y < 0 || x >= M.W || y >= M.H) return;
        if (!M.Attractors.ContainsKey(_currentTool)) return;
        var pos = (x, y);
        if (M.Attractors[_currentTool].Any(p => p == pos)) M.Attractors[_currentTool].RemoveAll(p => p == pos);
        else
        {
            foreach (var k in M.Attractors.Keys.ToList())
                M.Attractors[k].RemoveAll(p => p == pos);
            M.Attractors[_currentTool].Add(pos);
            var a = M.Grid[x, y];
            if (a != null) M.Agents.Remove(a);
        }
        M.RebuildAttrDistanceFields();
        M.UpdateGrid();
        M.CalcTotalUtility();
        _resetTarget = (int)M.Stats.TotalUtility;
        UpdateStats(); SampleChart(true);
        if (_showView)
        {
            if (_viewMode == "pubpriv") { }
            else RebuildContribCache();
        }
        _gridView.Invalidate();
    }

    void OnGridMove(object? s, MouseEventArgs e)
    {
        if (!_showReachOverlay && !(_showView && _viewMode == "contrib")) return;
        _pendingMouseClient = e.Location;
        if (_hoverT.Enabled) return;
        _hoverT.Enabled = true;
    }

    void OnGridLeave()
    {
        _hoverT.Enabled = false; _hoverT.Stop();
        _hoverCx = -1; _hoverCy = -1; _tip.Hide(_gridView);
        _gridView.Invalidate();
    }

    void HoverDebounce(object? s, EventArgs e)
    {
        _hoverT.Enabled = false;
        var e2 = new MouseEventArgs(MouseButtons.None, 0, _pendingMouseClient.X, _pendingMouseClient.Y, 0);
        int x = (int)(_pendingMouseClient.X / _cell), y = (int)(_pendingMouseClient.Y / _cell);
        if (x < 0 || y < 0 || x >= M.W || y >= M.H) { OnGridLeave(); return; }
        if (x == _hoverCx && y == _hoverCy) return;
        _hoverCx = x; _hoverCy = y;
        if (_showView && _viewMode == "contrib")
        {
            var a = M.Grid[x, y];
            if (a != null)
            {
                var u = M.GetUtility(a, x, y);
                _tip.SetToolTip(_gridView, $"Utility: {u:F2}");
            }
            else _tip.Hide(_gridView);
        }
        else _tip.Hide(_gridView);
        _gridView.Invalidate();
    }

    void ExportPng()
    {
        if (_batchRunning) return;
        if (_showView && _viewMode == "contrib") RebuildContribCache();
        using var dlg = new SaveFileDialog { DefaultExt = "png", Filter = "PNG Image|*.png|All|*.*", FileName = "city_export.png" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var cell = Math.Max(8, _cell);
        var wpx = M.W * cell; var hpx = M.H * cell;
        using var bmp = new Bitmap(wpx, hpx, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            if (_showView)
            {
                for (var x = 0; x < M.W; x++)
                    for (var y = 0; y < M.H; y++)
                    {
                        using var br = new SolidBrush(ViewFillAt(x, y));
                        g.FillRectangle(br, x * cell, y * cell, cell, cell);
                    }
            }
            else
            {
                var order = M.GetAttrDrawOrder();
                foreach (var k in order)
                {
                    using var br = new SolidBrush(ColorForKey(k));
                    foreach (var (ax, ay) in M.Attractors.GetValueOrDefault(k) ?? new List<(int, int)>())
                    {
                        g.FillRectangle(br, ax * cell + 1, ay * cell + 1, cell - 2, cell - 2);
                    }
                }
                foreach (var a in M.Agents)
                {
                    var cx = a.X * cell + cell / 2f; var cy = a.Y * cell + cell / 2f; var r = cell * 0.35f;
                    using var br = new SolidBrush(ColorForAgent(a.Type));
                    g.FillEllipse(br, cx - r, cy - r, 2 * r, 2 * r);
                    g.DrawEllipse(Pens.White, cx - r, cy - r, 2 * r, 2 * r);
                }
            }
            var gc = _colors["grid"];
            using (var p = new Pen(gc, 1f))
            {
                for (var i = 0; i <= M.W; i++) g.DrawLine(p, i * cell, 0, i * cell, hpx);
                for (var j = 0; j <= M.H; j++) g.DrawLine(p, 0, j * cell, wpx, j * cell);
            }
        }
        try { bmp.Save(dlg.FileName, ImageFormat.Png); } catch (Exception ex) { MessageBox.Show(ex.Message, "Export Error"); }
    }
}
