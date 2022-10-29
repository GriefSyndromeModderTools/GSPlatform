using AMLCore.Misc;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GSPlatformClient
{
    internal static class RoomImage
    {
        public static readonly string BitmapData = GetBitmapData();

        private static string GetBitmapData()
        {
            if (File.Exists(PathHelper.GetPath("aml/user/Room.png")))
            {
                try
                {
                    using (var bitmap = new Bitmap(PathHelper.GetPath("aml/user/Room.png")))
                    {
                        using (var output = new MemoryStream())
                        {
                            bitmap.Save(output, ImageFormat.Png);
                            return Convert.ToBase64String(output.ToArray());
                        }
                    }
                }
                catch
                {
                }
            }
            int rand = new Random().Next(5);
            try
            {
                using (var input = typeof(RoomImage).Assembly.GetManifestResourceStream($"GSPlatformClient.room{rand}.png"))
                {
                    using (var bitmap = new Bitmap(input))
                    {
                        using (var output = new MemoryStream())
                        {
                            bitmap.Save(output, ImageFormat.Png);
                            return Convert.ToBase64String(output.ToArray());
                        }
                    }
                }
            }
            catch
            {
            }
            return string.Empty;
        }
    }
}
