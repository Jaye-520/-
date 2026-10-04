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
        public static int Live, Created, FailConfigAt;public static string FailConfig;public static bool FailApply;private int number;
        public bool Dead;private TextureWrapMode wrap;private FilterMode filter;private HideFlags hide;
        private void Configure(string field){if(number==FailConfigAt && field==FailConfig)throw new Exception(field+" failure");}
        public TextureWrapMode wrapMode {get{return wrap;}set{Configure("wrap");wrap=value;}}
        public FilterMode filterMode {get{return filter;}set{Configure("filter");filter=value;}}
        public HideFlags hideFlags {get{return hide;}set{Configure("hide");hide=value;}}
        public Texture2D(int width,int height,TextureFormat format,bool mip) { Live++;number=++Created; }
        public void SetPixels(Color[] pixels) { if(pixels.Length==0)throw new Exception("empty pixels");foreach(var c in pixels) if(float.IsNaN(c.a)||c.a<0||c.a>1)throw new Exception("invalid pixel"); }
        public void Apply(bool mip,bool discard) { if(FailApply)throw new Exception("apply failure"); }
    }
    public static class ImageConversion { public static bool FailDecode;public static bool LoadImage(Texture2D texture,byte[] bytes,bool discard) { return !FailDecode && bytes.Length>0; } }
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
        foreach(var field in new[]{"wrap","filter","hide"})
            for(int texture=1;texture<=4;texture++)
            {
                UnityEngine.Texture2D.FailConfig=field;UnityEngine.Texture2D.FailConfigAt=UnityEngine.Texture2D.Created+texture;
                failed=false;PaperSkin current=null;
                try {current=PaperSkin.Acquire();}catch(Exception ex){failed=ex.Message==field+" failure";}
                finally {UnityEngine.Texture2D.FailConfig=null;UnityEngine.Texture2D.FailConfigAt=0;}
                bool fallback=texture<3?failed && current==null:!failed && current!=null && (texture==3?current.Landscape==null:current.TitleBrush==null);
                PaperSkin.ClearCache();
                Check(fallback && UnityEngine.Texture2D.Live==0,field+" failure on texture "+texture+" preserves failure or optional fallback and releases resources");
                PaperSkin.Acquire();Check(UnityEngine.Texture2D.Live==4,field+" texture "+texture+" retry rebuilds four textures");PaperSkin.ClearCache();
            }
        UnityEngine.ImageConversion.FailDecode=true;var fallbackSkin=PaperSkin.Acquire();
        Check(fallbackSkin.Landscape==null && fallbackSkin.TitleBrush==null && UnityEngine.Texture2D.Live==2,"embedded decode failure releases both rejected textures and retains paper fallback");
        UnityEngine.ImageConversion.FailDecode=false;PaperSkin.ClearCache();PaperSkin.Acquire();
        Check(UnityEngine.Texture2D.Live==4,"decode failure retry restores full art cache");PaperSkin.ClearCache();
        Console.WriteLine("TOTAL: "+passed);
    }
}
