// 编译真实PaperSkin，检查固定缓存数量、卸载与部分创建失败；不测Unity解码/GPU性能。
using System;
using MOD_YtxSQ1.UI;
namespace UnityEngine {
    public enum TextureWrapMode { Repeat, Clamp }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum HideFlags { HideAndDontSave }
    public struct Color { public float r,g,b,a; public Color(float r,float g,float b,float a) { this.r=r;this.g=g;this.b=b;this.a=a; } }
    public class Object {
        public static void Destroy(Object obj) { var t=obj as Texture2D; if(t!=null&&!t.Dead) { t.Dead=true;Texture2D.Live--; } }
    }
    public class Texture2D : Object {
        public static int Live, Created; public static bool FailApply;
        public bool Dead; public TextureWrapMode wrapMode;public FilterMode filterMode;public HideFlags hideFlags;
        public Texture2D(int width,int height,TextureFormat format,bool mip) { Live++;Created++; }
        public void SetPixels(Color[] pixels) { if(pixels.Length==0)throw new Exception("empty pixels");foreach(var c in pixels) if(float.IsNaN(c.a)||c.a<0||c.a>1)throw new Exception("invalid pixel"); }
        public void Apply(bool mip,bool discard) { if(FailApply)throw new Exception("apply failure"); }
    }
    public static class ImageConversion { public static bool LoadImage(Texture2D texture,byte[] bytes,bool discard) { return bytes.Length>0; } }
    public static class Debug { public static void LogWarning(string message) { } }
}
internal static class PaperSkinHarness {
    private static int passed;
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message);passed++;Console.WriteLine("PASS: "+message); }
    public static void Main() {
        var first=PaperSkin.Acquire();
        Check(UnityEngine.Texture2D.Live==4 && first.Landscape!=null && first.TitleBrush!=null,"first acquisition builds four textures and embedded art");
        for(int i=0;i<30;i++) if(!ReferenceEquals(first,PaperSkin.Acquire()))throw new Exception("cache not reused");
        Check(UnityEngine.Texture2D.Created==4 && UnityEngine.Texture2D.Live==4,"30 repeated acquisitions create no extra textures");
        PaperSkin.ClearCache();PaperSkin.ClearCache();Check(UnityEngine.Texture2D.Live==0,"cache release destroys all textures and is idempotent");
        var second=PaperSkin.Acquire();Check(!ReferenceEquals(first,second)&&UnityEngine.Texture2D.Live==4,"new lifecycle creates fresh cache");PaperSkin.ClearCache();
        UnityEngine.Texture2D.FailApply=true;bool failed=false;try { PaperSkin.Acquire(); } catch { failed=true; }
        Check(failed&&UnityEngine.Texture2D.Live==0,"partial texture creation failure leaves no cached resources");
        UnityEngine.Texture2D.FailApply=false;PaperSkin.Acquire();Check(UnityEngine.Texture2D.Live==4,"retry after failure rebuilds full cache");PaperSkin.ClearCache();
        Console.WriteLine("TOTAL: "+passed);
    }
}
