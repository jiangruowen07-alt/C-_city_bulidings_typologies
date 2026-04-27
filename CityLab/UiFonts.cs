// Unified UI typography: spacing via Padding/Margins, readable sans for labels, monospaced for numbers.

using System.Drawing;

namespace CityLab;

/// <summary>Central font sizes (pt). Letter-spacing is not supported on WinForms controls; we use line height + margins instead.</summary>
internal static class UiFonts
{
    public const string Sans = "Microsoft YaHei UI";
    public const string Mono = "Consolas";

    public const int PadSectionTop = 10;
    public const int PadBlockBottom = 10;
    public const int PadRow = 4;
    public const int MatrixColWidth = 46;
    public const int MatrixRowHeader = 62;
    public const int MatrixRowHeight = 26;
    public const int MatrixHeaderHeight = 24;

    public static Font CreateTitle() => new(Sans, 11.25f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font CreateSection() => new(Sans, 9f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font CreateCaption() => new(Sans, 8.25f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateBody() => new(Sans, 9f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateButton() => new(Sans, 8.75f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateRadio() => new(Sans, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateMatrixHeader() => new(Sans, 7.75f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font CreateMatrixCell() => new(Mono, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateStatValue() => new(Mono, 15f, FontStyle.Bold, GraphicsUnit.Point);
    public static Font CreateChart() => new(Sans, 8f, FontStyle.Regular, GraphicsUnit.Point);
    public static Font CreateChartData() => new(Mono, 8f, FontStyle.Regular, GraphicsUnit.Point);
}
