// SPDX-License-Identifier: MPL-2.0
//
// GeometricGlyphs — the "Geometric" piece style: every piece type is an abstract shape, drawn
// procedurally (no artwork files). Pure C# (no UnityEngine) so it can be tested headless.
//
//   Pawn (P)        circle
//   Apprentice (A)  triangle
//   Scholar (S)     rounded square
//   Technician (T)  pentagon
//   Sage (W)        hexagon
//   Hand (H)        five-pointed star
//   Mind (B)        eight-pointed star
//
// The glyphs are drawn in the same two-tone way as the artwork: a bright fill (tinted red-ish for
// Orange and green-ish for Green, which is how the piece shader tells the teams apart) inside a near-
// black outline. The shader then swaps the fill for the team colour chosen in Settings.

using System;

public static class GeometricGlyphs
{
    public const int Size = 128;

    // RGBA bytes, row 0 = TOP of the image. letter: P A S T W H B.
    public static byte[] Render(char letter, bool orange, int size = Size)
    {
        var rgba = new byte[size * size * 4];
        const int ss = 4; // 4x4 supersampling
        // Fill tint: the shader only looks at "red above green" for Orange, "green above red" for Green.
        double fr = orange ? 1.0 : .80, fg = orange ? .80 : 1.0, fb = .82;
        const double outline = .115;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                double a = 0, r = 0, g = 0, b = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        double px = ((x + (sx + .5) / ss) / size) * 2 - 1;
                        double py = 1 - ((y + (sy + .5) / ss) / size) * 2; // y up
                        double d = Distance(letter, px, py);
                        if (d >= 0) continue;                       // outside the shape
                        a += 1;
                        if (d > -outline) { r += .015; g += .015; b += .015; } // dark outline band
                        else { r += fr; g += fg; b += fb; }                    // bright fill
                    }
                int i = (y * size + x) * 4;
                if (a > 0) { rgba[i] = ToByte(r / a); rgba[i + 1] = ToByte(g / a); rgba[i + 2] = ToByte(b / a); }
                rgba[i + 3] = ToByte(a / (ss * ss));
            }
        return rgba;
    }

    static byte ToByte(double v) { return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v * 255))); }

    // Signed distance to the shape (negative inside), unit space -1..1, y up.
    static double Distance(char letter, double x, double y)
    {
        switch (letter)
        {
            case 'P': return Math.Sqrt(x * x + y * y) - .60;
            case 'A': return Polygon(x, y + .10, .86, 3);
            case 'S': return RoundedBox(x, y, .56, .56, .14);
            case 'T': return Polygon(x, y - .02, .86, 5);
            case 'W': return Polygon(x, y, .86, 6);
            case 'H': return Star(x, y - .02, .92, 5, 3.0);
            case 'B': return Star(x, y, .92, 8, 3.4);
            default:  return Math.Sqrt(x * x + y * y) - .60;
        }
    }

    static double RoundedBox(double x, double y, double hx, double hy, double radius)
    {
        double qx = Math.Abs(x) - hx + radius, qy = Math.Abs(y) - hy + radius;
        double ox = Math.Max(qx, 0), oy = Math.Max(qy, 0);
        return Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0) - radius;
    }

    // Regular n-gon with circumradius r, one vertex pointing up (after Inigo Quilez).
    static double Polygon(double x, double y, double r, int n)
    {
        double an = Math.PI / n, acs = Math.Cos(an), asn = Math.Sin(an);
        double bn = Mod(Math.Atan2(x, y), 2 * an) - an;
        double len = Math.Sqrt(x * x + y * y);
        double px = len * Math.Cos(bn) - r * acs, py = len * Math.Abs(Math.Sin(bn)) - r * asn;
        py += Clamp(-py, 0, r * asn);
        return Math.Sqrt(px * px + py * py) * Math.Sign(px);
    }

    // n-pointed star with outer radius r; m in [2, n] sets the sharpness (after Inigo Quilez).
    static double Star(double x, double y, double r, int n, double m)
    {
        double an = Math.PI / n, en = Math.PI / m;
        double acsx = Math.Cos(an), acsy = Math.Sin(an), ecsx = Math.Cos(en), ecsy = Math.Sin(en);
        double bn = Mod(Math.Atan2(x, y), 2 * an) - an;
        double len = Math.Sqrt(x * x + y * y);
        double px = len * Math.Cos(bn) - r * acsx, py = len * Math.Abs(Math.Sin(bn)) - r * acsy;
        double t = Clamp(-(px * ecsx + py * ecsy), 0, r * acsy / ecsy);
        px += ecsx * t; py += ecsy * t;
        return Math.Sqrt(px * px + py * py) * Math.Sign(px);
    }

    static double Mod(double a, double b) { double m = a % b; return m < 0 ? m + b : m; }
    static double Clamp(double v, double lo, double hi) { return v < lo ? lo : v > hi ? hi : v; }
}
