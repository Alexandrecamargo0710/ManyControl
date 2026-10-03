using System;
using System.Drawing;
using System.Drawing.Imaging;

public class Program {
    public static void Main() {
        string inPath = @"C:\Users\Piruzin\.gemini\antigravity\brain\d0da5a2d-fb0b-4b0f-bf34-d2f5f7b0b3fc\.user_uploaded\media_1791054878322.jpg";
        
        using (Bitmap bmp = new Bitmap(inPath)) {
            int cx = 512, cy = 280;
            
            // Find left
            int left = 100;
            while(left < cx && bmp.GetPixel(left, cy).R > 230) left++;
            
            // Find right
            int right = 900;
            while(right > cx && bmp.GetPixel(right, cy).R > 230) right--;
            
            // Find top
            int top = 50;
            while(top < cy && bmp.GetPixel(cx, top).R > 230) top++;
            
            // Find bottom
            int bottom = 500;
            while(bottom > cy && bmp.GetPixel(cx, bottom).R > 230) bottom--;
            
            Console.WriteLine(string.Format("Top: {0}, Bottom: {1}, Left: {2}, Right: {3}", top, bottom, left, right));
        }
    }
}
