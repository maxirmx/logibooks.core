using ClosedXML.Excel;
using System.Drawing;

namespace Logibooks.Core.Services;

internal static class ExcelColorParser
{
    internal static (bool hasColor, XLColor? color) GetRowColor(IXLWorksheet worksheet, int rowNumber, int columnCount)
    {
        try
        {
            var bg = worksheet.Cell(rowNumber, 1).Style.Fill.BackgroundColor;
            if (bg.ColorType == XLColorType.Theme || bg.ColorType == XLColorType.Indexed || bg.ColorType == XLColorType.Color)
            {
                XLColor resolvedColor = ConvertToRgbColor(bg);
                if (IsSignificantColor(resolvedColor))
                {
                    return (true, resolvedColor);
                }
            }
        }
        catch
        {
            return (true, null);
        }

        return (false, null);
    }

    internal static XLColor ConvertToRgbColor(XLColor color)
    {
        XLColor xLColor = color;
        if (color.ColorType == XLColorType.Theme)
        {
            var rgbColor = color.ThemeColor switch
            {
                XLThemeColor.Accent1 => XLColor.FromArgb(79, 129, 189),
                XLThemeColor.Accent2 => XLColor.FromArgb(192, 80, 77),
                XLThemeColor.Accent3 => XLColor.FromArgb(155, 187, 89),
                XLThemeColor.Accent4 => XLColor.FromArgb(128, 100, 162),
                XLThemeColor.Accent5 => XLColor.FromArgb(75, 172, 198),
                XLThemeColor.Accent6 => XLColor.FromArgb(247, 150, 70),
                XLThemeColor.Background1 => XLColor.White,
                XLThemeColor.Background2 => XLColor.FromArgb(242, 242, 242),
                XLThemeColor.Text1 => XLColor.Black,
                XLThemeColor.Text2 => XLColor.FromArgb(68, 68, 68),
                _ => XLColor.FromArgb(200, 200, 200)
            };

            if (color.ThemeTint != 0)
            {
                var baseColor = rgbColor.Color;
                var tint = color.ThemeTint;

                if (tint > 0)
                {
                    var factor = 1.0 + (tint * 0.5);
                    var r = Math.Min(255, (int)(baseColor.R * factor));
                    var g = Math.Min(255, (int)(baseColor.G * factor));
                    var b = Math.Min(255, (int)(baseColor.B * factor));
                    return XLColor.FromArgb(r, g, b);
                }
                else
                {
                    var factor = Math.Max(0.1, 1.0 + tint);
                    var r = Math.Max(0, (int)(baseColor.R * factor));
                    var g = Math.Max(0, (int)(baseColor.G * factor));
                    var b = Math.Max(0, (int)(baseColor.B * factor));
                    return XLColor.FromArgb(r, g, b);
                }
            }

            xLColor = rgbColor;
        }
        else if (color.ColorType == XLColorType.Indexed)
        {
            try
            {
                var rgbFromIndexed = XLColor.FromColor(color.Color);
                xLColor = rgbFromIndexed;
            }
            catch
            {
            }
        }

        return xLColor;
    }

    internal static bool IsSignificantColor(XLColor color)
    {
        int argb = color.Color.ToArgb();
        return argb != Color.White.ToArgb() &&
               argb != Color.Empty.ToArgb() &&
               argb != Color.Transparent.ToArgb();
    }
}

