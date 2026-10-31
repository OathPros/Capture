using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Capture.Core;
namespace Capture.App;

internal static class ScreenshotSmoke
{
    internal static bool Enabled => Array.Exists(Environment.GetCommandLineArgs(),argument=>argument=="--smoke-test");
    internal static readonly string Root=Path.Combine(Path.GetTempPath(),"Capture-Smoke-"+Guid.NewGuid());
    internal static async Task<Guide> CreateAsync(ProjectStore store)
    {
        var guide=new Guide { Title="Synthetic native screenshot test",Steps=[new(){Title="Preview this screenshot",Instruction="Verify the native bridge and PNG decoder.",AssetId="smoke",Annotations=[new("marker","highlight",.25,.25,.25,.25)]}] };
        var dir=Path.Combine(store.DirectoryFor(guide.Id),"assets");Directory.CreateDirectory(dir);
        using var bitmap=new Bitmap(640,360);using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.White);g.FillRectangle(Brushes.SteelBlue,120,90,250,110);}
        var path=Path.Combine(dir,"smoke.png");bitmap.Save(path,ImageFormat.Png);var bytes=await File.ReadAllBytesAsync(path);
        guide.Assets.Add(new("smoke","assets/smoke.png","image/png",640,360,Convert.ToHexString(SHA256.HashData(bytes))));await store.SaveAsync(guide);return guide;
    }
    internal static void Report(string message)
    {
        var path=Environment.GetEnvironmentVariable("CAPTURE_SMOKE_RESULT");
        if(!string.IsNullOrWhiteSpace(path))File.WriteAllText(path,message);
    }
}
