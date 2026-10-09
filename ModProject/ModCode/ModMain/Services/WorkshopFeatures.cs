using System;
using System.Collections.Generic;

namespace MOD_YtxSQ1.Services
{
    internal sealed class WorkshopFeature
    {
        internal readonly string Id, Name, Hint, Unavailable;
        internal readonly bool Toggle;
        internal WorkshopFeature(string id, string name, string hint, bool toggle = false, string unavailable = null)
        { Id = id; Name = name; Hint = hint; Toggle = toggle; Unavailable = unavailable; }
    }
    internal static class WorkshopFeatures
    {
        internal static readonly WorkshopFeature[] Assists = {
            new WorkshopFeature("vision", "全图视野", "临时显示地图窗口和大地图场景的迷雾区域；不写已探索格子。", true),
            new WorkshopFeature("move", "地图任意移动", "开启后打开地图，单击地图位置传送玩家；拖动、筛选按钮和其他弹窗不触发。关闭后恢复原生地图操作。", true),
            new WorkshopFeature("relation", "关系必定", "玩家提出结义、师徒、道侣或求婚时，原生关系接受判定必定同意；覆盖剧情包人物的专用关系答复；保留年龄、性别和已有关系检查，不影响普通邀约或索取。", true),
            new WorkshopFeature("escape", "逃跑必成", "玩家主动逃跑时调整成功率；不绕过逃跑前置条件。", true),
            new WorkshopFeature("steal", "偷窃必成", "玩家偷窃必定得手且掩盖成功；保留物品条件、消耗和原生物品转移流程。", true),
            new WorkshopFeature("study", "立即学习", "保留秘籍消耗、学习条件和原生完成事件。", true),
            new WorkshopFeature("mp", "无限灵力", "锁定玩家灵力消耗，不自动补满；关闭后可正常修改与消耗。", true),
            new WorkshopFeature("sp", "无限念力", "锁定玩家念力消耗，不自动补满；关闭后可正常修改与消耗。", true),
            new WorkshopFeature("hp", "无限体力", "锁定玩家体力和地图精力的减少，不自动补满；关闭后可正常修改与消耗。", true),
            new WorkshopFeature("invincible", "无敌金身", "阻止玩家战斗实例受到体力伤害；不屏蔽剧情死亡或直接死亡动作。", true),
            new WorkshopFeature("age", "解除年龄限制", "只解除玩家师徒、结义等关系的年龄门槛，不改寿命。", true, "年龄判断在原生关系配置选择内部，尚无可靠的单项拦截；不改共享配置或整体跳过条件"),
            new WorkshopFeature("pot", "壶妖持久", "只解除玩家召唤壶妖的在场时间限制，保留死亡与回收。", true, "在场回收包含延迟回调，尚未核实可撤销的独立计时入口；不修改壶妖配置"),
            new WorkshopFeature("capacity", "背包容量＋999", "增加当前对象背包容量999；独立扩容记录随保存保留，加载本MOD时恢复；保留装备和动态加成。"),
            new WorkshopFeature("cooldown", "技能无冷却", "玩家武技／灵技、绝技、身法、神通无冷却；保留施法和技能条件，不影响法宝或丹药。", true)
        };
        internal static readonly WorkshopFeature[] Interactions = {
            new WorkshopFeature("battle", "切磋", "玩家与当前NPC进行原生切磋。"),
            new WorkshopFeature("drill", "论道", "启动原生论道，保留消耗与结果。"),
            new WorkshopFeature("attack", "袭击", "启动原生袭击；可能进入战斗并产生仇恨。"),
            new WorkshopFeature("invite", "邀约", "启动原生邀约，保留地点与关系条件。"),
            new WorkshopFeature("trains", "双修", "启动原生双修，保留关系、资源和剧情条件。"),
            new WorkshopFeature("give", "赠送", "从玩家可见背包选择一件物品，走原生赠送动作。"),
            new WorkshopFeature("ask", "索取", "选择NPC可见背包的一件物品，走原生索取动作。"),
            new WorkshopFeature("teach", "教功", "使用原生功法选择窗口与教功动作。"),
            new WorkshopFeature("marry", "结婚", "启动原生求婚对话；对方同意后由游戏选择婚礼地点和安排婚期，保留婚姻冲突与剧情条件。"),
            new WorkshopFeature("recruit", "招纳", "启动原生宗门招纳，保留权限和宗门条件。"),
            new WorkshopFeature("kill", "击杀", "启动原生击杀；死亡、庇护、仇恨和掉落由游戏处理。"),
            new WorkshopFeature("heal", "救治", "由原生救治动作处理当前对象的濒死状态。"),
            new WorkshopFeature("revive", "复活", "仅对仍有原生人物实例的鬼魂使用原生复活；已彻底移除的人物只读。"),
            new WorkshopFeature("bless", "长辈庇护", "由原生长辈关系与庇护规则维护。", false, "尚未核实可独立授予／解除庇护且维护长辈关系的动作；不伪造家谱或庇护字段"),
            new WorkshopFeature("dead", "死亡名单", "只读查看死亡记录，与操作对象选择分离。"),
            new WorkshopFeature("potrevive", "复活壶妖", "恢复壶妖必须维护健康、重伤计时与契约身份。", false, "未核实独立原生恢复动作及健康计时副作用；不直接修改重伤字段")
        };
        internal static readonly WorkshopFeature[] Schools = {
            new WorkshopFeature("outer", "外门", "通过原生职位动作设为当前宗门外门弟子。"),
            new WorkshopFeature("join", "入宗", "选择宗门，以外门身份通过原生加入动作入宗。"),
            new WorkshopFeature("post", "设置职位", "选择当前宗门职位，保留原生权限和名额检查。"),
            new WorkshopFeature("exit", "退宗", "通过原生退出动作离开宗门；可能影响贡献、职位及关系。"),
            new WorkshopFeature("return", "回宗", "将当前人物通过原生移动送回其当前宗门；不恢复已退出的宗门身份。")
        };
        internal static WorkshopFeature Find(string id)
        {
            foreach (var group in new[] { Assists, Interactions, Schools })
                foreach (var feature in group) if (feature.Id == id) return feature;
            return null;
        }
        internal static bool IsRole(string id)
        { return id == "battle" || id == "drill" || id == "attack" || id == "invite" || id == "trains" || id == "give" || id == "ask" || id == "teach" || id == "marry" || id == "recruit" || id == "kill"; }
        internal static int CapacityTarget(int current) { return checked(current + 999); }
    }
}
