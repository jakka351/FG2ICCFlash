using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace FG2ICCFlasher.UI
{
    /// <summary>
    /// Renders the Australian National Flag to a Bitmap entirely in GDI+ (no image assets):
    /// the Union Jack canton (upper hoist quarter), the 7-pointed Commonwealth Star in the lower
    /// hoist quarter, and the Southern Cross (four 7-pointed stars plus the smaller 5-pointed
    /// Epsilon Crucis) on the fly. Proportions follow the 1:2 flag ratio.
    /// </summary>
    public static class AustralianFlagRenderer
    {
        private static readonly Color Navy = Color.FromArgb(0x00, 0x24, 0x7D);
        private static readonly Color Red = Color.FromArgb(0xCF, 0x14, 0x2B);
        private static readonly Color White = Color.White;

        /// <summary>Render the flag at the given pixel height (width = 2 x height).</summary>
        public static Bitmap Render(int height)
        {
            if (height < 8) height = 8;
            int W = height * 2, H = height;
            // Supersample x4 for crisp small renders, then draw down into the target bitmap.
            int ss = 4;
            var big = new Bitmap(W * ss, H * ss);
            using (var g = Graphics.FromImage(big))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                Draw(g, W * ss, H * ss);
            }
            var outp = new Bitmap(W, H);
            using (var g = Graphics.FromImage(outp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(big, new Rectangle(0, 0, W, H));
            }
            big.Dispose();
            return outp;
        }

        private static void Draw(Graphics g, int W, int H)
        {
            using (var navy = new SolidBrush(Navy)) g.FillRectangle(navy, 0, 0, W, H);

            // Canton occupies the upper hoist quarter: width W/2, height H/2.
            float cw = W / 2f, chh = H / 2f;
            DrawUnionJack(g, cw, chh);

            // Commonwealth (Federation) Star: 7 points, centred in the lower hoist quarter.
            DrawStar(g, W * 0.25f, H * 0.75f, H * 0.18f, 7, -90f, White);

            // Southern Cross on the fly (recognisable kite + the smaller Epsilon).
            float big = H * 0.11f, small = H * 0.06f;
            DrawStar(g, W * 0.75f, H * 0.165f, big, 7, -90f, White); // Gamma (top)
            DrawStar(g, W * 0.57f, H * 0.50f, big, 7, -90f, White);  // Beta (left)
            DrawStar(g, W * 0.84f, H * 0.38f, big, 7, -90f, White);  // Delta (right)
            DrawStar(g, W * 0.75f, H * 0.835f, big, 7, -90f, White); // Alpha (bottom)
            DrawStar(g, W * 0.735f, H * 0.56f, small, 5, -90f, White); // Epsilon (small)
        }

        private static void DrawUnionJack(Graphics g, float w, float h)
        {
            var clip = g.Clip;
            g.SetClip(new RectangleF(0, 0, w, h));
            try
            {
                float diagW = h * 0.30f;    // white St Andrew / St Patrick field
                float diagR = h * 0.10f;    // red St Patrick arms
                // White diagonals (St Andrew's saltire).
                using (var pen = new Pen(White, diagW))
                {
                    g.DrawLine(pen, 0, 0, w, h);
                    g.DrawLine(pen, 0, h, w, 0);
                }
                // Red diagonals (St Patrick), drawn slightly offset to suggest the counterchange.
                using (var pen = new Pen(Red, diagR))
                {
                    pen.StartCap = LineCap.Flat; pen.EndCap = LineCap.Flat;
                    g.DrawLine(pen, 0, 0, w, h);
                    g.DrawLine(pen, 0, h, w, 0);
                }
                // White cross (St George) field.
                float crossW = h * 0.34f, crossR = h * 0.20f;
                using (var white = new SolidBrush(White))
                {
                    g.FillRectangle(white, w / 2 - crossW / 2, 0, crossW, h);
                    g.FillRectangle(white, 0, h / 2 - crossW / 2, w, crossW);
                }
                using (var red = new SolidBrush(Red))
                {
                    g.FillRectangle(red, w / 2 - crossR / 2, 0, crossR, h);
                    g.FillRectangle(red, 0, h / 2 - crossR / 2, w, crossR);
                }
            }
            finally { g.Clip = clip; }
        }

        private static void DrawStar(Graphics g, float cx, float cy, float outerR, int points, float rotationDeg, Color color)
        {
            float innerR = outerR * (points == 5 ? 0.40f : 0.46f);
            var path = new GraphicsPath();
            var pts = new PointF[points * 2];
            double start = rotationDeg * Math.PI / 180.0;
            double step = Math.PI / points;
            for (int i = 0; i < points * 2; i++)
            {
                double a = start + i * step;
                float r = (i % 2 == 0) ? outerR : innerR;
                pts[i] = new PointF(cx + (float)(r * Math.Cos(a)), cy + (float)(r * Math.Sin(a)));
            }
            path.AddPolygon(pts);
            using (var b = new SolidBrush(color)) g.FillPath(b, path);
            path.Dispose();
        }
    }
}
