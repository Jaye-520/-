using System;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterPortrait
    {
        public static int GetDramaId(WorldUnitBase unit)
        {
            if (unit == null) throw new ArgumentNullException("unit");
            var drama = g.conf == null ? null : g.conf.dramaNpc;
            // 游戏根据特殊身份解析五朵金花当前的剧情立绘配置，不按姓名/固定ID判断。
            return drama == null ? 0 : drama.GetFiveFlowerDramaNpcID(unit);
        }

        public static void CreateSpecialTexture(int dramaId, UnityEngine.UI.RawImage image, Action<UnityEngine.GameObject> onCreate)
        {
            var drama = g.conf == null ? null : g.conf.dramaNpc;
            if (drama == null) throw new InvalidOperationException("特殊人物立绘配置未就绪");
            // 原生入口按avatar/modelID分流；独立资源不能强取拼装数据，也不创建第二个渲染器。
            drama.CreateTexture(dramaId, image, new UnityEngine.Vector2(0, -8), 1f, null, onCreate);
        }
    }
}
