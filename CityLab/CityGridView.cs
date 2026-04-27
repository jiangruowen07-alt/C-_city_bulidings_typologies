// Canvas grid control — visual layer order vs. model

using System.Drawing;
using System.Windows.Forms;

namespace CityLab;

public sealed class CityGridView : Control
{
    public CityAppForm? App { get; set; }

    public CityGridView()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        DoubleBuffered = true;
        BackColor = Color.Black;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (App != null) App.PaintGrid(e.Graphics, ClientSize);
    }
}
