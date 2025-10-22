using System.Diagnostics;

namespace SvgCreator.Core.Models;

/// <summary>
/// RGB カラーを表す値型です。
/// </summary>
[DebuggerDisplay("({R}, {G}, {B})")]
public readonly record struct RgbColor(byte R, byte G, byte B);
