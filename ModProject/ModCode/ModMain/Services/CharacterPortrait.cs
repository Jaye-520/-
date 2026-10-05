using System;

namespace MOD_YtxSQ1.Services
{
    internal static class CharacterPortrait
    {
        internal static string AppearanceKey(PortraitModelData model)
        {
            if (model == null) return "none";
            var key = new System.Text.StringBuilder();
            key.Append(model.sex).Append(':');
            key.Append(model.back).Append(':');
            key.Append(model.backBeauty).Append(':');
            key.Append(model.body).Append(':');
            key.Append(model.bodyBeauty).Append(':');
            key.Append(model.eyebrows).Append(':');
            key.Append(model.eyebrowsBeauty).Append(':');
            key.Append(model.eyebrowsOffsetY).Append(':');
            key.Append(model.eyes).Append(':');
            key.Append(model.eyesBeauty).Append(':');
            key.Append(model.eyesOffsetY).Append(':');
            key.Append(model.faceFull).Append(':');
            key.Append(model.faceFullBeauty).Append(':');
            key.Append(model.faceLeft).Append(':');
            key.Append(model.faceLeftBeauty).Append(':');
            key.Append(model.faceRight).Append(':');
            key.Append(model.faceRightBeauty).Append(':');
            key.Append(model.forehead).Append(':');
            key.Append(model.foreheadBeauty).Append(':');
            key.Append(model.hair).Append(':');
            key.Append(model.hairBeauty).Append(':');
            key.Append(model.hairFront).Append(':');
            key.Append(model.hairFrontBeauty).Append(':');
            key.Append(model.hat).Append(':');
            key.Append(model.hatBeauty).Append(':');
            key.Append(model.head).Append(':');
            key.Append(model.headBeauty).Append(':');
            key.Append(model.mouth).Append(':');
            key.Append(model.mouthBeauty).Append(':');
            key.Append(model.mouthOffsetY).Append(':');
            key.Append(model.nose).Append(':');
            key.Append(model.noseBeauty).Append(':');
            key.Append(model.noseOffsetY).Append(':');
            return key.ToString();
        }
        internal static string AppearanceKey(WorldUnitBase unit, int dramaId)
        {
            var key = AppearanceKey(unit.data.unitData.propertyData.modelData);
            if (dramaId <= 0 || g.conf == null || g.conf.dramaNpc == null) return key;
            var conf = g.conf.dramaNpc.GetItem(dramaId); if (conf == null) return key;
            key += ":" + conf.avatar + ":" + conf.modelID;
            if (conf.avatar == "0" && conf.modelID != 0) key += ":" + AppearanceKey(g.conf.dramaNpc.GetPortraitModelDataInModel(conf.modelID));
            return key;
        }

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
