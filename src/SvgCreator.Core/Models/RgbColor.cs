using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// Represents an RGB color.
/// </summary>
[DebuggerDisplay("({R}, {G}, {B})")]
public readonly record struct RgbColor(byte R, byte G, byte B);
