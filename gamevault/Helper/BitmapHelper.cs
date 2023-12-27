﻿using SkiaSharp;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace gamevault.Helper
{
    internal class BitmapHelper
    {
        public static BitmapImage GetBitmapImage(string uri)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(uri);
            image.EndInit();
            return image;
        }
        public static async Task<BitmapImage> GetBitmapImageAsync(string uri)
        {
            BitmapImage bitmap = null;

            var httpclient = new HttpClient();

            using (var response = await httpclient.GetAsync(uri))
            {
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = new MemoryStream())
                    {
                        await response.Content.CopyToAsync(stream);
                        stream.Seek(0, SeekOrigin.Begin);

                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }
                }
            }

            return bitmap;
        }
        public static MemoryStream BitmapSourceToMemoryStream(BitmapSource src)
        {
            System.Drawing.Bitmap bitmap;
            MemoryStream pasteStream = new MemoryStream();
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(src));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
                bitmap.Save(pasteStream, ImageFormat.Png);
            }
            pasteStream.Seek(0, SeekOrigin.Begin);
            return pasteStream;
        }
    }
}
