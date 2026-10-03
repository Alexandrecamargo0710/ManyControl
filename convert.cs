using System;
using System.Drawing;
using System.Drawing.Imaging;

public class Program {
    public static void Main() {
        string inPath = @"C:\Users\Piruzin\.gemini\antigravity\brain\d0da5a2d-fb0b-4b0f-bf34-d2f5f7b0b3fc\.user_uploaded\media_1791054834294.png";
        string outPath = @"E:\Projetos\ManyControl\ManyControl\Resources\AppIcon\appicon.png";
        
        using (Bitmap bmp = new Bitmap(inPath)) {
            using (Bitmap result = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb)) {
                for(int y=0; y<bmp.Height; y++) {
                    for(int x=0; x<bmp.Width; x++) {
                        Color c = bmp.GetPixel(x, y);
                        if (c.R < 45 && c.G < 45 && c.B < 45) {
                            result.SetPixel(x, y, Color.Transparent);
                        } else {
                            result.SetPixel(x, y, c);
                        }
                    }
                }
                result.Save(outPath, ImageFormat.Png);
            }
        }
    }
}
