using System;
using System.Collections.Generic;

namespace SnipStudio.Editor;

public static class AnnotationColors
{
    public const string Default = "#E52535";
    public const double DefaultWidth = 5;
    public static readonly string[] Palette = [Default, "#FFB800", "#00B879", "#1677FF", "#9B42F5", "#101318", "#FFFFFF"];
    private static readonly Dictionary<string, string> LegacyPalette = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#F16B55"] = Default, ["#F0BB43"] = "#FFB800", ["#147D70"] = "#00B879",
        ["#4285DD"] = "#1677FF", ["#9868CB"] = "#9B42F5", ["#202E32"] = "#101318"
    };
    public static string Upgrade(string? value) => value != null && LegacyPalette.TryGetValue(value, out var replacement) ? replacement : value ?? Default;
}
