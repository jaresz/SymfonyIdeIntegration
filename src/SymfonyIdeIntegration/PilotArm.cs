using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SymfonyIdeIntegration;

// Both segments share one depth surface. The elbow projects toward the camera,
// so a bent arm can overlap itself without a sprite edge cutting through the joint.
internal sealed class PilotArm : IDisposable
{
    private const int Scale = 2;
    private readonly int[,] skin = new int[64, 64];
    private Bitmap? surface;
    private float[] depth = [];
    private int[] pixels = [];

    internal PilotArm(Bitmap forearm)
    {
        // Sample the opaque core of the original skin texture, from cool shadow to warm light.
        for (int y = 0; y < 64; y++)
        {
            int row = (int)((.18f + .62f * y / 63) * forearm.Height);
            int left = 0, right = forearm.Width - 1;
            while (forearm.GetPixel(left, row).A < 230) left++;
            while (forearm.GetPixel(right, row).A < 230) right--;
            for (int x = 0; x < 64; x++)
            {
                int column = left + (int)((right - left) * (.06f + .88f * x / 63));
                skin[y, x] = forearm.GetPixel(column, row).ToArgb() | unchecked((int)0xff000000);
            }
        }
    }

    internal void Draw(Graphics g, PointF sleeve, PointF elbow, PointF wrist)
    {
        float left = MathF.Floor(MathF.Min(sleeve.X, MathF.Min(elbow.X, wrist.X))) - 12;
        float top = MathF.Floor(MathF.Min(sleeve.Y, MathF.Min(elbow.Y, wrist.Y))) - 12;
        int w = (int)MathF.Ceiling(MathF.Max(sleeve.X, MathF.Max(elbow.X, wrist.X)) - left + 12) * Scale;
        int h = (int)MathF.Ceiling(MathF.Max(sleeve.Y, MathF.Max(elbow.Y, wrist.Y)) - top + 12) * Scale;
        if (surface == null || w > surface.Width || h > surface.Height)
        {
            int width = Math.Max(surface?.Width ?? 0, (w + 31) & ~31);
            int height = Math.Max(surface?.Height ?? 0, (h + 31) & ~31);
            surface?.Dispose();
            surface = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            depth = new float[width * height];
            pixels = new int[width * height];
        }
        Array.Fill(depth, float.NegativeInfinity, 0, w * h);
        Array.Clear(pixels, 0, w * h);
        Vector3 Local(PointF p, float z) => new(p.X - left, p.Y - top, z);
        var joint = Local(elbow, 12);
        DrawSegment(Local(sleeve, 2), joint, true, w);
        DrawSegment(joint, Local(wrist, -12), false, w);

        var bounds = new Rectangle(0, 0, w, h);
        var data = surface.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            for (int y = 0; y < h; y++)
                Marshal.Copy(pixels, y * w, data.Scan0 + y * data.Stride, w);
        }
        finally { surface.UnlockBits(data); }
        g.DrawImage(surface, new RectangleF(left, top, w / (float)Scale, h / (float)Scale), bounds, GraphicsUnit.Pixel);
    }

    private void DrawSegment(Vector3 start, Vector3 end, bool upper, int stride)
    {
        int steps = (int)MathF.Ceiling(Vector3.Distance(start, end) * 1.5f);
        for (int i = 0; i <= steps; i++)
        {
            float u = (float)i / steps;
            var center = Vector3.Lerp(start, end, u) * Scale;
            float bulge = MathF.Sin(u * MathF.PI);
            float radius = Scale * (upper ? 9.5f - 2.6f * u + .6f * bulge : 6.9f - 2.9f * u + 1.6f * bulge);
            int row = (int)(upper ? 16 + 32 * u : 48 - 45 * u);
            for (int y = (int)MathF.Ceiling(center.Y - radius); y <= center.Y + radius; y++)
            {
                float ny = (y - center.Y) / radius;
                float extent = MathF.Sqrt(MathF.Max(0, 1 - ny * ny));
                int endX = (int)(center.X + radius * extent);
                for (int x = (int)MathF.Ceiling(center.X - radius * extent); x <= endX; x++)
                {
                    float nx = (x - center.X) / radius;
                    float nz = MathF.Sqrt(MathF.Max(0, extent * extent - nx * nx));
                    float z = center.Z + radius * nz;
                    int index = y * stride + x;
                    if (z <= depth[index]) continue;
                    depth[index] = z;
                    float light = .45f + .43f * nx - .22f * ny + .13f * nz;
                    pixels[index] = skin[row, (int)(63 * Math.Clamp(light, 0, 1))];
                }
            }
        }
    }

    public void Dispose() => surface?.Dispose();
}
