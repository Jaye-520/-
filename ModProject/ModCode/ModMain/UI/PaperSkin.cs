using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MOD_YtxSQ1.UI
{
    // 代码生成的轻量 UI 材质：边界明确，不依赖原生图集裁切或新增 AB。
    // 每个窗口两张小纹理、山水底图与独立标题墨底；只读使用、随视图释放。
    internal sealed class PaperSkin : IDisposable
    {
        public Texture2D Paper { get; private set; }
        public Texture2D Brush { get; private set; }
        public Texture2D Landscape { get; private set; }
        public Texture2D TitleBrush { get; private set; }

        public PaperSkin()
        {
            try
            {
                Paper = NewTexture(192, 192, TextureWrapMode.Repeat);
                var pixels = new Color[192 * 192];
                var random = new System.Random(4127);
                for (int y = 0; y < 192; y++)
                    for (int x = 0; x < 192; x++)
                    {
                        var noise = (float)(random.NextDouble() - .5) * .025f;
                        var fibers = Mathf.Sin(x * (float)Math.PI / 3f) * .0035f + Mathf.Sin(y * (float)Math.PI / 4f) * .004f;
                        var shade = noise + fibers;
                        pixels[y * 192 + x] = new Color(.862f + shade, .855f + shade, .832f + shade, 1f);
                    }
                Paper.SetPixels(pixels);
                Paper.Apply(false, true);

                Brush = NewTexture(384, 64, TextureWrapMode.Clamp);
                pixels = new Color[384 * 64];
                for (int x = 0; x < 384; x++)
                {
                    var edge = Mathf.Min(1f, Mathf.Min(x / 24f, (383 - x) / 30f));
                    var half = 22f * Mathf.Sqrt(Mathf.Max(0, edge));
                    var center = 32f + Mathf.Sin(x * .035f) * 1.5f;
                    var fray = (float)random.NextDouble() * 3f;
                    for (int y = 0; y < 64; y++)
                    {
                        var alpha = Mathf.Clamp01((half + fray - Mathf.Abs(y - center)) / 2.5f) * .96f;
                        if ((y % 13 == 0 || y % 19 == 2) && random.NextDouble() < .15) alpha *= .55f;
                        pixels[y * 384 + x] = new Color(.12f, .13f, .12f, alpha);
                    }
                }
                Brush.SetPixels(pixels);
                Brush.Apply(false, true);
                try { Landscape = LoadEmbeddedTexture("MOD_YtxSQ1.Art.InkLandscape.png"); }
                catch (Exception ex) { Debug.LogWarning("[八荒秘录] 山水底图回退为纸纹：" + ex.Message); }
                try { TitleBrush = LoadEmbeddedTexture("MOD_YtxSQ1.Art.TitleBrush.png"); }
                catch (Exception ex) { Debug.LogWarning("[八荒秘录] 标题墨底回退为原墨迹：" + ex.Message); }
            }
            catch { Dispose(); throw; }
        }

        private Texture2D LoadEmbeddedTexture(string resourceName)
        {
            using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (input == null) throw new InvalidOperationException("缺少嵌入资源：" + resourceName);
                using (var buffer = new MemoryStream())
                {
                    input.CopyTo(buffer);
                    var texture = NewTexture(2, 2, TextureWrapMode.Clamp);
                    try
                    {
                        if (!ImageConversion.LoadImage(texture, buffer.ToArray(), true))
                            throw new InvalidOperationException("图片解码失败：" + resourceName);
                        return texture;
                    }
                    catch { UnityEngine.Object.Destroy(texture); throw; }
                }
            }
        }

        private static Texture2D NewTexture(int width, int height, TextureWrapMode wrap)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = wrap, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave
            };
        }

        public void Dispose()
        {
            var paper = Paper; Paper = null;
            var brush = Brush; Brush = null;
            var landscape = Landscape; Landscape = null;
            var titleBrush = TitleBrush; TitleBrush = null;
            Release(paper); Release(brush); Release(landscape); Release(titleBrush);
        }

        private static void Release(Texture2D texture)
        {
            if (texture == null) return;
            try { UnityEngine.Object.Destroy(texture); }
            catch (Exception ex) { Debug.LogWarning("[八荒秘录] 释放纹理失败：" + ex); }
        }
    }
}
