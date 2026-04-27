// CITY LAB v1.5 - WinForms UI

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CityLab;

public sealed partial class CityAppForm : Form
{
    internal readonly CityModel M;
    private CityGridView _gridView;
    private Panel _gridContainer;
    private FlowLayoutPanel _rightFlow;
    private Button _runBtn;
    private Control _statusDot;
    private Label _batchStatus;
    private TextBox _runNbox;
    private Label _speedLbl;
    private TrackBar _speedBar;
    private FlowLayoutPanel _layoutBtnPanel = null!;
    private FlowLayoutPanel _roadBtnPanel = null!;
    private TableLayoutPanel _toolFrame;
    private Button _ppBtn, _contribBtn, _reachBtn, _origBtn;
    private Panel _legendFrame;
    private TableLayoutPanel _matrixAttr, _matrixAgent, _matrixInflAttr, _matrixInflAg;
    private Panel _chartPanel;
    private Label _iterL, _utilL, _accL, _rejL, _sacL;
    private string _currentTool = "None";
    private int _cell = 15;
    private bool _running;
    private readonly Dictionary<string, Color> _colors = new();
    private bool _showView;
    private string _viewMode = "pubpriv";
    private bool _viewAgentFirst = true;
    private readonly Dictionary<string, string> _ppAgentRank = new();
    private readonly Dictionary<string, string> _ppAttrRank = new();
    private double[,] _contribCache = new double[1, 1];
    private double _contribMin, _contribMax;
    private readonly List<double> _utilHistory = new();
    private int _hoverCx = -1, _hoverCy = -1;
    private bool _showReachOverlay = true;
    private int? _resetTarget;
    private bool _batchRunning;
    private bool _batchPrevRun;
    private int _batchRem;
    private readonly HashSet<(int x, int y)> _batchDirty = new();
    private const int BatchChunk = 1200, ChartMax = 240, ContribInt = 30, ChartPad = 10;
    private readonly System.Windows.Forms.Timer _loopT = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _batchT = new() { Interval = 1 };
    private readonly System.Windows.Forms.Timer _hoverT = new() { Enabled = false, Interval = 15 };
    private readonly ToolTip _tip = new();
    private bool _configuring;
    private readonly List<Button> _layoutBtnList = new();
    private readonly List<Button> _roadBtnList = new();
    private readonly Dictionary<string, Button> _toolBtns = new();
    private readonly Dictionary<string, TextBox> _matrixEnt = new();
    private readonly List<RadioButton> _swapRadios = new();
    private Panel _rightScroll;
    internal readonly BuildingModel B;
    private bool _systemModeCity = true;
    private Button _modeCityBtn = null!, _modeBuildingBtn = null!;
    private FlowLayoutPanel _buildingPanel = null!;
    private ComboBox _buildingSizeCombo = null!;
    private Button _buildingBwBtn = null!;
    /// <summary>建筑模式：true 时黑=非绿地代理，白=绿地（与功能色对照）。</summary>
    private bool _buildingBwMode;
    private readonly List<Control> _cityOnlyControls = new();
    private Font _fTitle = null!, _fSection = null!, _fCaption = null!, _fBody = null!, _fBtn = null!,
        _fRadio = null!, _fMxH = null!, _fMxC = null!, _fStV = null!, _fCh = null!, _fChD = null!, _fChDB = null!;

    public CityAppForm()
    {
        Text = "CITY LAB v1.5 - Public/Private (4 bins) + Cell Satisfaction";
        BackColor = Config.HexColor(Config.UiColors.PanelBg);
        MinimumSize = new Size(1000, 700);
        Size = new Size(1280, 800);
        M = new CityModel(40, 40, 6);
        B = new BuildingModel(8, 8);
        RefreshColorMap();
        RefreshPpRanks();
        _contribCache = new double[M.W, M.H];
        InitThemeFonts();
        _loopT.Tick += LoopTick;
        _batchT.Tick += BatchTick;
        _hoverT.Tick += HoverDebounce;
        BuildUi();
        Shown += (_, _) => { SyncRightPanelWidth(); Boot(); };
        _configuring = true;
        ClientSize = ClientSize;
        _configuring = false;
        Resize += (_, _) => { if (!_configuring) ResizeGrid(); };
        FormClosed += (_, _) => DisposeThemeFonts();
    }

    void InitThemeFonts()
    {
        _fTitle = UiFonts.CreateTitle();
        _fSection = UiFonts.CreateSection();
        _fCaption = UiFonts.CreateCaption();
        _fBody = UiFonts.CreateBody();
        _fBtn = UiFonts.CreateButton();
        _fRadio = UiFonts.CreateRadio();
        _fMxH = UiFonts.CreateMatrixHeader();
        _fMxC = UiFonts.CreateMatrixCell();
        _fStV = UiFonts.CreateStatValue();
        _fCh = UiFonts.CreateChart();
        _fChD = UiFonts.CreateChartData();
        _fChDB = new Font(_fChD, FontStyle.Bold);
    }

    void DisposeThemeFonts()
    {
        _fTitle.Dispose();
        _fSection.Dispose();
        _fCaption.Dispose();
        _fBody.Dispose();
        _fBtn.Dispose();
        _fRadio.Dispose();
        _fMxH.Dispose();
        _fMxC.Dispose();
        _fStV.Dispose();
        _fCh.Dispose();
        _fChD.Dispose();
        _fChDB.Dispose();
    }

    void SyncRightPanelWidth()
    {
        if (_rightScroll == null || _rightFlow == null) return;
        var w = Math.Max(300, _rightScroll.ClientSize.Width - 20);
        _rightFlow.Width = w;
        foreach (Control c in _rightFlow.Controls)
        {
            if (c is TableLayoutPanel { Name: "mxGrid" })
            {
                c.MaximumSize = new Size(2000, 0);
            }
            else
            {
                // Height=0 在嵌套 FlowLayout+TableLayout 下会让首选高度为 0，子控件被压成不可见
                c.MaximumSize = new Size(w, 20000);
                c.Width = w;
            }
        }
    }

    Label LblSec(string t, int topPad = UiFonts.PadSectionTop) => new()
    { Text = t, ForeColor = Color.FromArgb(0x8c, 0x8c, 0x8c), AutoSize = true, Font = _fSection, BackColor = Config.HexColor(Config.UiColors.PanelBg), Padding = new Padding(0, topPad, 0, 6) };

    Label LblSub(string t) => new()
    { Text = t, ForeColor = Color.FromArgb(0x60, 0x60, 0x60), AutoSize = true, Font = _fCaption, BackColor = Config.HexColor(Config.UiColors.PanelBg), Padding = new Padding(0, 0, 0, 6) };

    FlowLayoutPanel MakeColorLeg(Color fill, string text)
    {
        var line = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
        var ic = new Panel { Width = 18, Height = 18, BackColor = Config.HexColor(Config.UiColors.PanelBg) };
        ic.Paint += (_, e) =>
        {
            using var b = new SolidBrush(fill);
            e.Graphics.FillRectangle(b, 1, 1, 16, 16);
            e.Graphics.DrawRectangle(Pens.Gray, 1, 1, 16, 16);
        };
        line.Controls.Add(ic);
        line.Controls.Add(new Label
        { Text = text, ForeColor = Color.FromArgb(0xe0, 0xe0, 0xe0), AutoSize = true, Font = _fBody, BackColor = Config.HexColor(Config.UiColors.PanelBg) });
        return line;
    }

    private void Boot()
    {
        SetLayoutBtnHi("Grid");
        SetRoadBtnHi(Config.RoadTopologyNames[0]);
        SetTool("None");
        M.UtilityBias = 0.0;
        M.ApplyLayout("Grid");
        M.CalcTotalUtility();
        B.SwapMode = M.SwapMode;
        _resetTarget = (int)M.Stats.TotalUtility;
        _utilHistory.Clear();
        RebuildAll();
    }

    private void ResizeGrid()
    {
        if (_gridContainer == null || M == null) return;
        var cw = _gridContainer.ClientSize.Width;
        var ch = _gridContainer.ClientSize.Height;
        if (cw <= 1 || ch <= 1) return;
        var gw = _systemModeCity ? M.W : B.W;
        var gh = _systemModeCity ? M.H : B.H;
        var size = Math.Max(200, Math.Min(cw, ch) - 20);
        var newCell = Math.Max(4, size / Math.Max(gw, gh));
        var wpx = gw * newCell;
        var hpx = gh * newCell;
        if (newCell == _cell && _gridView != null && _gridView.Size == new Size(wpx, hpx)) return;
        _cell = newCell;
        if (_gridView != null) _gridView.Size = new Size(wpx, hpx);
        _hoverCx = -1; _hoverCy = -1;
    }

    internal void PaintGrid(Graphics g, Size size)
    {
        g.Clear(Color.Black);
        if (M == null) return;
        if (!_systemModeCity)
        {
            PaintBuildingLattice(g);
            return;
        }
        var s = _cell;
        int w = M.W, h = M.H;
        int wpx = w * s, hpx = h * s;
        if (_showView)
        {
            for (var x = 0; x < w; x++)
                for (var y = 0; y < h; y++)
                {
                    var fill = ViewFillAt(x, y);
                    using var br = new SolidBrush(fill);
                    g.FillRectangle(br, x * s, y * s, s, s);
                }
        }
        else
        {
            var order = M.GetAttrDrawOrder();
            foreach (var k in order)
            {
                using var br = new SolidBrush(ColorForKey(k));
                foreach (var (x, y) in M.Attractors.GetValueOrDefault(k) ?? new List<(int, int)>())
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    g.FillRectangle(br, x * s + 1, y * s + 1, s - 2, s - 2);
                }
            }
            foreach (var a in M.Agents)
            {
                var cx = a.X * s + s / 2f;
                var cy = a.Y * s + s / 2f;
                var r = s * 0.35f;
                using var br = new SolidBrush(ColorForAgent(a.Type));
                using var pen = new Pen(Color.White, 1f);
                g.FillEllipse(br, cx - r, cy - r, 2 * r, 2 * r);
                g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
            }
        }
        var gridCol = _colors["grid"];
        using (var p = new Pen(gridCol, 1f))
        {
            for (var i = 0; i <= w; i++)
            {
                var px = i * s;
                g.DrawLine(p, px, 0, px, hpx);
            }
            for (var j = 0; j <= h; j++)
            {
                var py = j * s;
                g.DrawLine(p, 0, py, wpx, py);
            }
        }
        if (_showReachOverlay && _hoverCx >= 0)
            DrawReachOverlayGdi(g, _hoverCx, _hoverCy, s, w, h);
    }

    void PaintBuildingLattice(Graphics g)
    {
        var s = _cell;
        int w = B.W, h = B.H;
        int wpx = w * s, hpx = h * s;
        if (_buildingBwMode)
        {
            using var bNonPark = new SolidBrush(Color.Black);
            using var bPark = new SolidBrush(Color.White);
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                {
                    var a = B.Grid[x, y];
                    var t = a?.Type ?? BuildingModel.Park;
                    var isPark = t == BuildingModel.Park;
                    g.FillRectangle(isPark ? bPark : bNonPark, x * s, y * s, s, s);
                }
            }
            using (var p = new Pen(Color.FromArgb(0x55, 0x55, 0x55), 1f))
            {
                for (var i = 0; i <= w; i++) g.DrawLine(p, i * s, 0, i * s, hpx);
                for (var j = 0; j <= h; j++) g.DrawLine(p, 0, j * s, wpx, j * s);
            }
            return;
        }
        using var bP = new SolidBrush(ColorBuildingType(BuildingModel.Park));
        using var bResi = new SolidBrush(ColorBuildingType("Resi"));
        using var bFirm = new SolidBrush(ColorBuildingType("Firm"));
        using var bCafe = new SolidBrush(ColorBuildingType("Cafe"));
        using var bShop = new SolidBrush(ColorBuildingType("Shop"));
        SolidBrush BrushFor(string? t) => t switch
        {
            BuildingModel.Park => bP,
            "Resi" => bResi,
            "Firm" => bFirm,
            "Cafe" => bCafe,
            "Shop" => bShop,
            _ => bP
        };
        for (var x = 0; x < w; x++)
        {
            for (var y = 0; y < h; y++)
            {
                var a = B.Grid[x, y];
                var t = a?.Type ?? BuildingModel.Park;
                g.FillRectangle(BrushFor(t), x * s, y * s, s, s);
            }
        }
        using (var p = new Pen(Color.FromArgb(0x2a, 0x2a, 0x2a), 1f))
        {
            for (var i = 0; i <= w; i++) g.DrawLine(p, i * s, 0, i * s, hpx);
            for (var j = 0; j <= h; j++) g.DrawLine(p, 0, j * s, wpx, j * s);
        }
    }

    private void DrawReachOverlayGdi(Graphics g, int cx, int cy, int s, int gw, int gh)
    {
        if (cx < 0 || cy < 0 || cx >= gw || cy >= gh) return;
        var a = M.Grid[cx, cy];
        var at = (cx < gw && cy < gh) ? M.AttrGrid[cx, cy] : null;
        if (a != null)
        {
            int ra = M.TypeLabels.Max(t => M.InfluenceRangeAgent.GetValueOrDefault(a.Type)?.GetValueOrDefault(t, M.Reach) ?? M.Reach);
            if (ra > 0)
            {
                var poly = DiamondPixels(cx, cy, ra, s);
                using var p = new Pen(Color.White, 3f) { LineJoin = LineJoin.Round };
                g.DrawLines(p, poly);
            }
            using var wpen = new Pen(Color.White, 2f);
            g.DrawRectangle(wpen, cx * s + 1, cy * s + 1, s - 2, s - 2);
        }
        else if (at != null)
        {
            int rr = M.TypeLabels.Max(t => M.InfluenceRangeAttr.GetValueOrDefault(at)?.GetValueOrDefault(t, M.Reach) ?? M.Reach);
            if (rr > 0)
            {
                var poly = DiamondPixels(cx, cy, rr, s);
                using var p = new Pen(Color.FromArgb(0xaa, 0xaa, 0xaa), 2f) { DashPattern = new[] { 4f, 3f } };
                g.DrawLines(p, poly);
            }
            using var wpen = new Pen(Color.FromArgb(0xaa, 0xaa, 0xaa), 2f);
            g.DrawRectangle(wpen, cx * s + 1, cy * s + 1, s - 2, s - 2);
        }
    }

    private static PointF[] DiamondPixels(int cx, int cy, int r, int s)
    {
        (int gx, int gy)[] pts = [(cx, cy - r), (cx + r, cy), (cx, cy + r), (cx - r, cy), (cx, cy - r)];
        return pts.Select(p => new PointF(p.gx * s + s / 2f, p.gy * s + s / 2f)).ToArray();
    }

    void BuildUi()
    {
        _configuring = true;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Config.HexColor(Config.UiColors.PanelBg), Padding = new Padding(16) };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 500f));
        _gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Padding = new Padding(0)
        };
        _gridView = new CityGridView { App = this, Location = new Point(0, 0) };
        _gridContainer.Resize += (_, _) => { if (!_configuring) ResizeGrid(); };
        _gridContainer.Controls.Add(_gridView);
        _gridView.MouseDown += OnGridMouse;
        _gridView.MouseMove += OnGridMove;
        _gridView.MouseLeave += (_, _) => OnGridLeave();
        _gridView.Size = new Size(M.W * _cell, M.H * _cell);
        root.Controls.Add(_gridContainer, 0, 0);
        _rightScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Config.HexColor(Config.UiColors.PanelBg) };
        _rightFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Top,
            Width = 480,
            Padding = new Padding(6, 6, 6, 8)
        };
        _rightScroll.Resize += (_, _) => { if (!_configuring) SyncRightPanelWidth(); };
        _rightScroll.Controls.Add(_rightFlow);
        root.Controls.Add(_rightScroll, 1, 0);
        var hdr = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 4) };
        hdr.Controls.Add(new Label { Text = "CITY LAB v1.5", ForeColor = Color.White, AutoSize = true, Font = _fTitle, BackColor = Config.HexColor(Config.UiColors.PanelBg) });
        _statusDot = new Panel { Width = 14, Height = 14, BackColor = Config.HexColor(Config.UiColors.PanelBg) };
        _statusDot.Paint += (_, e) =>
        {
            e.Graphics.Clear(Config.HexColor(Config.UiColors.PanelBg));
            using var b = new SolidBrush(_running ? Color.FromArgb(0x22, 0xc5, 0x5e) : Color.FromArgb(0xef, 0x44, 0x44));
            e.Graphics.FillEllipse(b, 2, 2, 10, 10);
        };
        hdr.Controls.Add(_statusDot);
        _rightFlow.Controls.Add(hdr);
        var modeRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 10) };
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        modeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _modeCityBtn = new Button
        {
            Text = "城市系统", BackColor = Color.White, ForeColor = Color.Black, Font = _fBtn, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Height = 34, Margin = new Padding(0, 0, 4, 0)
        };
        _modeCityBtn.Click += (_, _) => SetSystemMode(true);
        _modeBuildingBtn = new Button
        {
            Text = "建筑单元 (房间/空地)", BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, Font = _fBtn, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Height = 34, Margin = new Padding(4, 0, 0, 0)
        };
        _modeBuildingBtn.Click += (_, _) => SetSystemMode(false);
        modeRow.Controls.Add(_modeCityBtn, 0, 0);
        modeRow.Controls.Add(_modeBuildingBtn, 1, 0);
        _rightFlow.Controls.Add(modeRow);

        void AddCity(Control c)
        {
            _rightFlow.Controls.Add(c);
            _cityOnlyControls.Add(c);
        }

        AddCity(LblSec("城市预设布局"));
        _layoutBtnPanel = new FlowLayoutPanel
        {
            Name = "presetLayoutFlow",
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0),
            BackColor = Config.HexColor(Config.UiColors.PanelBg),
            Width = 468
        };
        foreach (var n in Config.LayoutNames)
        {
            var nameCopy = n;
            var b = new Button
            {
                Text = n.ToUpperInvariant(),
                BackColor = Config.HexColor(Config.UiColors.PanelBg),
                ForeColor = Color.White,
                Font = _fBtn,
                Tag = n,
                FlatStyle = FlatStyle.Flat,
                AutoSize = false,
                Size = new Size(148, 32),
                Margin = new Padding(3),
                UseCompatibleTextRendering = true
            };
            b.Click += (_, _) => SetLayout(nameCopy);
            _layoutBtnPanel.Controls.Add(b);
            _layoutBtnList.Add(b);
        }
        AddCity(_layoutBtnPanel);
        AddCity(LblSec("道路预设"));
        _roadBtnPanel = new FlowLayoutPanel
        {
            Name = "presetRoadFlow",
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(0),
            BackColor = Config.HexColor(Config.UiColors.PanelBg),
            Width = 468
        };
        foreach (var roadName in Config.RoadTopologyNames)
        {
            var nameCopy = roadName;
            var b = new Button
            {
                Text = roadName.ToUpperInvariant(),
                BackColor = Config.HexColor(Config.UiColors.PanelBg),
                ForeColor = Color.White,
                Font = _fBtn,
                Tag = roadName,
                FlatStyle = FlatStyle.Flat,
                AutoSize = false,
                Size = new Size(148, 32),
                Margin = new Padding(2),
                UseCompatibleTextRendering = true
            };
            b.Click += (_, _) => SetRoad(nameCopy);
            _roadBtnPanel.Controls.Add(b);
            _roadBtnList.Add(b);
        }
        AddCity(_roadBtnPanel);

        _runBtn = new Button
        {
            Text = "START SIMULATION", BackColor = Color.White, ForeColor = Color.Black, Font = _fBtn, FlatStyle = FlatStyle.Flat, Height = 40, AutoSize = false, Margin = new Padding(0, 0, 0, 8), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _runBtn.Click += (_, _) => ToggleRun();
        _rightFlow.Controls.Add(_runBtn);
        var row1 = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 3, Margin = new Padding(0, 0, 0, 8) };
        for (var c = 0; c < 3; c++) row1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
        void MkBtn(string t, int col, EventHandler a)
        {
            var b = new Button
            { Text = t, BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, Font = _fBtn, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Height = 32, Margin = new Padding(3) };
            b.Click += a;
            row1.Controls.Add(b, col, 0);
        }
        MkBtn("STEP", 0, (_, _) => StepOnce());
        MkBtn("RESET", 1, (_, _) => DoReset());
        MkBtn("CLEAR", 2, (_, _) => DoClear());
        _rightFlow.Controls.Add(row1);
        var exBtn = new Button
        { Text = "EXPORT PNG", Height = 32, BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, Font = _fBtn, FlatStyle = FlatStyle.Flat, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 8) };
        exBtn.Click += (_, _) => ExportPng();
        _rightFlow.Controls.Add(exBtn);
        var runNf = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 4, Margin = new Padding(0, 0, 0, 8) };
        runNf.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        runNf.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80f));
        runNf.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        runNf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        runNf.Controls.Add(new Label { Text = "Run N", ForeColor = Color.FromArgb(0x8a, 0x8a, 0x8a), AutoSize = false, Font = _fCaption, BackColor = Config.HexColor(Config.UiColors.PanelBg), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 4, 8, 0) }, 0, 0);
        _runNbox = new TextBox { Text = "2000", BackColor = Config.HexColor(Config.UiColors.BoxBg), ForeColor = Color.White, Font = _fMxC, TextAlign = HorizontalAlignment.Center, BorderStyle = BorderStyle.FixedSingle, Height = 26, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
        runNf.Controls.Add(_runNbox, 1, 0);
        var go = new Button { Text = "GO", BackColor = Color.White, ForeColor = Color.Black, Font = _fBtn, FlatStyle = FlatStyle.Flat, AutoSize = false, Height = 28, Width = 44, Dock = DockStyle.Left, Margin = new Padding(0, 0, 8, 0) };
        go.Click += (_, _) => RunNSteps();
        runNf.Controls.Add(go, 2, 0);
        _batchStatus = new Label { Text = "", ForeColor = Color.FromArgb(0x70, 0x70, 0x70), AutoSize = true, Font = _fCaption, BackColor = Config.HexColor(Config.UiColors.PanelBg), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        runNf.Controls.Add(_batchStatus, 3, 0);
        _rightFlow.Controls.Add(runNf);
        _rightFlow.Controls.Add(LblSec("Speed (steps / frame)"));
        var speedRow = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40f));
        _speedBar = new TrackBar { Minimum = 1, Maximum = 1000, Value = 150, Height = 32, TickStyle = TickStyle.None, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0) };
        _speedLbl = new Label { Text = "150", ForeColor = Color.White, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = _fMxC, BackColor = Config.HexColor(Config.UiColors.PanelBg) };
        _speedBar.ValueChanged += (_, _) => _speedLbl.Text = _speedBar.Value.ToString();
        speedRow.Controls.Add(_speedBar, 0, 0);
        speedRow.Controls.Add(_speedLbl, 1, 0);
        _rightFlow.Controls.Add(speedRow);
        _buildingPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false,
            Visible = false,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 8)
        };
        _buildingPanel.Controls.Add(LblSub("建筑场地 (每格 = 一个建筑单元)"));
        var bsz = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Top, ColumnCount = 2, Margin = new Padding(0, 0, 0, 4) };
        bsz.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bsz.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100f));
        bsz.Controls.Add(new Label
        {
            Text = "网格 N×N",
            ForeColor = Color.FromArgb(0x8a, 0x8a, 0x8a),
            AutoSize = false,
            Font = _fCaption,
            BackColor = Config.HexColor(Config.UiColors.PanelBg),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 4, 8, 0)
        }, 0, 0);
        _buildingSizeCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Config.HexColor(Config.UiColors.BoxBg),
            ForeColor = Color.White,
            Font = _fMxC,
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat
        };
        int[] bGridSizes = [3, 4, 5, 6, 7, 8, 9, 10, 12, 16];
        foreach (var n in bGridSizes) _buildingSizeCombo.Items.Add($"{n}×{n}");
        _buildingSizeCombo.SelectedIndex = System.Array.IndexOf(bGridSizes, 8);
        _buildingSizeCombo.SelectedIndexChanged += (_, _) => { if (!_configuring) OnBuildingSizeChanged(); };
        bsz.Controls.Add(_buildingSizeCombo, 1, 0);
        _buildingPanel.Controls.Add(bsz);
        var bLeg = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
        bLeg.Controls.Add(MakeColorLeg(ColorBuildingType("Resi"), "居民"));
        bLeg.Controls.Add(MakeColorLeg(ColorBuildingType("Firm"), "公司"));
        bLeg.Controls.Add(MakeColorLeg(ColorBuildingType("Cafe"), "咖啡"));
        bLeg.Controls.Add(MakeColorLeg(ColorBuildingType("Shop"), "商店"));
        bLeg.Controls.Add(MakeColorLeg(ColorBuildingType(BuildingModel.Park), "绿地 (P)"));
        _buildingPanel.Controls.Add(bLeg);
        _buildingBwBtn = new Button
        {
            Text = "黑白视图 (关)  开=黑非绿地 / 白绿地",
            Font = _fBtn,
            FlatStyle = FlatStyle.Flat,
            Height = 36,
            AutoSize = false,
            Dock = DockStyle.Top,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 4, 0, 0),
            BackColor = Config.HexColor(Config.UiColors.PanelBg),
            ForeColor = Color.White
        };
        _buildingBwBtn.Click += (_, _) => TogBuildingBwMode();
        _buildingPanel.Controls.Add(_buildingBwBtn);
        _rightFlow.Controls.Add(_buildingPanel);

        _rightFlow.Controls.Add(LblSec("Swap rule"));
        var rfp = new TableLayoutPanel { AutoSize = true, ColumnCount = 1, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
        foreach (var (lab, val) in Config.SwapRuleOptions)
        {
            int row = rfp.RowCount; rfp.RowCount++; rfp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var r = new RadioButton { Text = lab, ForeColor = Color.FromArgb(0xe4, 0xe4, 0xe4), BackColor = Config.HexColor(Config.UiColors.PanelBg), AutoSize = true, Font = _fRadio, Tag = val, Padding = new Padding(0, 0, 0, 4) };
            if (val == "pareto") r.Checked = true;
            r.CheckedChanged += (_, _) => { if (r.Checked) { M.SwapMode = val; B.SwapMode = val; } };
            rfp.Controls.Add(r, 0, row);
            _swapRadios.Add(r);
        }
        _rightFlow.Controls.Add(rfp);
        AddCity(LblSec("Tools"));
        _toolFrame = new TableLayoutPanel { AutoSize = false, Dock = DockStyle.Top, ColumnCount = 4, RowCount = 2, Margin = new Padding(0, 0, 0, 10), Height = 2 * 32 + 4 };
        for (var c = 0; c < 4; c++) _toolFrame.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        for (var r0 = 0; r0 < 2; r0++) _toolFrame.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
        AddCity(_toolFrame);
        _ppBtn = new Button
        { Text = "公共 / 私有 视图 (4 档)", FlatStyle = FlatStyle.Flat, Font = _fBtn, Height = 32, BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 6) };
        _ppBtn.Click += (_, _) => TogPub();
        AddCity(_ppBtn);
        _contribBtn = new Button
        { Text = "格点满意度", FlatStyle = FlatStyle.Flat, Font = _fBtn, Height = 32, BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 6) };
        _contribBtn.Click += (_, _) => TogContrib();
        AddCity(_contribBtn);
        _reachBtn = new Button
        { Text = "影响范围 (悬停)", BackColor = Color.White, ForeColor = Color.Black, FlatStyle = FlatStyle.Flat, Font = _fBtn, Height = 32, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 6) };
        _reachBtn.Click += (_, _) => TogReach();
        AddCity(_reachBtn);
        _origBtn = new Button
        { Text = "Original City Lab 模式", BackColor = Config.HexColor(Config.UiColors.PanelBg), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = _fBtn, Height = 32, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 6) };
        _origBtn.Click += (_, _) => TogOrig();
        AddCity(_origBtn);
        AddCity(LblSec("图例"));
        _legendFrame = new Panel { BackColor = Config.HexColor(Config.UiColors.BoxBg), BorderStyle = BorderStyle.FixedSingle, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8, 6, 8, 6), Margin = new Padding(0, 0, 0, 10) };
        AddCity(_legendFrame);
        AddCity(LblSec("偏好与影响范围矩阵", 4));
        AddCity(LblSub("A 代理 → 吸引子 (权重)"));
        _matrixAttr = new TableLayoutPanel { Name = "mxGrid", BackColor = Config.HexColor(Config.UiColors.BoxBg), CellBorderStyle = TableLayoutPanelCellBorderStyle.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        AddCity(_matrixAttr);
        AddCity(LblSub("B 代理 → 代理 (权重)"));
        _matrixAgent = new TableLayoutPanel { Name = "mxGrid", BackColor = Config.HexColor(Config.UiColors.BoxBg), CellBorderStyle = TableLayoutPanelCellBorderStyle.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        AddCity(_matrixAgent);
        AddCity(LblSub("C 吸引子影响半径 → 代理"));
        _matrixInflAttr = new TableLayoutPanel { Name = "mxGrid", BackColor = Config.HexColor(Config.UiColors.BoxBg), CellBorderStyle = TableLayoutPanelCellBorderStyle.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 8) };
        AddCity(_matrixInflAttr);
        AddCity(LblSub("D 代理影响半径 → 代理"));
        _matrixInflAg = new TableLayoutPanel { Name = "mxGrid", BackColor = Config.HexColor(Config.UiColors.BoxBg), CellBorderStyle = TableLayoutPanelCellBorderStyle.None, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Margin = new Padding(0, 0, 0, 10) };
        AddCity(_matrixInflAg);
        _rightFlow.Controls.Add(LblSec("总效用趋势"));
        _chartPanel = new Panel { Height = 152, BackColor = Config.HexColor(Config.UiColors.BoxBg), BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Top, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, 10) };
        _chartPanel.Resize += (_, _) => { _chartPanel.Invalidate(); };
        _chartPanel.Paint += ChartPaint;
        _rightFlow.Controls.Add(_chartPanel);
        var sf = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        sf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        sf.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _iterL = AddStatBox(sf, "迭代", 0, 0);
        _utilL = AddStatBox(sf, "总效用", 1, 0);
        _rightFlow.Controls.Add(sf);
        var sf2 = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        sf2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        sf2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        _accL = AddStatBox(sf2, "接受", 0, 0);
        _rejL = AddStatBox(sf2, "拒绝", 1, 0);
        _rightFlow.Controls.Add(sf2);
        var sf3 = new TableLayoutPanel { ColumnCount = 1, Dock = DockStyle.Top, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
        sf3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        _sacL = AddStatBox(sf3, "牺牲 (一方变差)", 0, 0);
        _rightFlow.Controls.Add(sf3);
        Controls.Add(root);
        RebuildToolButtons();
        BuildLegend();
        BuildMatrices();
        SyncRightPanelWidth();
        _configuring = false;
    }

    Label AddStatBox(TableLayoutPanel t, string title, int c, int r)
    {
        var box = new Panel { BackColor = Config.HexColor(Config.UiColors.PanelBg), BorderStyle = BorderStyle.FixedSingle, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3) };
        box.Controls.Add(new Label { Text = title, ForeColor = Color.FromArgb(0x8a, 0x8a, 0x8a), Dock = DockStyle.Top, Font = _fCaption, AutoSize = true, Padding = new Padding(8, 6, 8, 0) });
        var v = new Label { Text = "0", ForeColor = Color.White, Font = _fStV, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8, 2, 8, 8) };
        v.Name = "val";
        box.Controls.Add(v);
        t.Controls.Add(box, c, r);
        return v;
    }

    void RefreshColorMap()
    {
        _colors.Clear();
        _colors["grid"] = Config.HexColor(Config.UiColors.Grid);
        _colors["bg"] = Config.HexColor(Config.UiColors.Bg);
        if (M.UseOriginalCitylab)
        {
            foreach (var kv in Config.OriginalCitylabAgentColors) _colors[kv.Key] = Config.HexColor(kv.Value);
            foreach (var t in Config.OriginalCitylabAttrDefs) _colors[t.Key] = Config.HexColor(t.ColorHex);
        }
        else
        {
            foreach (var kv in Config.AgentColors) _colors[kv.Key] = Config.HexColor(kv.Value);
            foreach (var t in Config.AttrDefs) _colors[t.Key] = Config.HexColor(t.ColorHex);
        }
    }

    void RefreshPpRanks()
    {
        _ppAgentRank.Clear();
        _ppAttrRank.Clear();
        if (M.UseOriginalCitylab)
        {
            foreach (var kv in Config.OriginalCitylabPpAgentRank) _ppAgentRank[kv.Key] = kv.Value;
            foreach (var kv in Config.OriginalCitylabPpAttrRank) _ppAttrRank[kv.Key] = kv.Value;
        }
        else
        {
            foreach (var kv in Config.PpAgentRank) _ppAgentRank[kv.Key] = kv.Value;
            foreach (var kv in Config.PpAttrRank) _ppAttrRank[kv.Key] = kv.Value;
        }
    }

    internal Color ColorForKey(string k) => _colors.GetValueOrDefault(k, Color.White);
    internal Color ColorForAgent(string t) => _colors.GetValueOrDefault(t, Color.White);

    /// <summary>建筑层配色：四代理用 Config.AgentColors，绿地 P 用城市中公园 P 的色（Config.AttrDefs）。</summary>
    internal Color ColorBuildingType(string t)
    {
        if (t == BuildingModel.Park)
        {
            foreach (var a in Config.AttrDefs)
            {
                if (a.Key == "P") return Config.HexColor(a.ColorHex);
            }
            return Color.FromArgb(0x4d, 0x80, 0x47);
        }
        return Config.HexColor(Config.AgentColors.GetValueOrDefault(t, "#888888"));
    }
}
