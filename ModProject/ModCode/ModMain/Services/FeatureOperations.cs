using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MOD_YtxSQ1.Services
{
    internal sealed class FeatureRequest
    {
        internal readonly string Context, Id, TargetId, PlayerId, Payload, SchoolId, LeftId, RightId, OriginalSchoolId, OriginalTitle;
        internal readonly IntPtr PlayerPointer, TargetPointer, PlayerData, TargetData, PlayerInfo, TargetInfo, TargetBag, LeftPointer, RightPointer, CapacityPointer, PlayerBag, LeftData, LeftInfo, RightData, RightInfo, SchoolPointer, TitlePointer, PlayerRelation, TargetRelation, MarriageConfig;
        internal readonly Vector2Int Origin;
        internal readonly int BeforeCapacity, BeforeCapacityBase, BeforeCapacityBonus;
        internal readonly IntPtr CapacityStore;
        internal readonly string CapacityRecord;
        internal readonly SchoolPostType Post;
        internal readonly SchoolDepartmentType Department;
        internal readonly string ItemSignature;
        internal readonly int ItemCount;
        internal FeatureRequest(string id, string targetId, string payload, string schoolId, SchoolPostType post,
            SchoolDepartmentType department, string leftId, string rightId)
        {
            Context = CharacterReader.ContextKey(); Id = id; TargetId = targetId; Payload = payload;
            SchoolId = schoolId; Post = post; Department = department; LeftId = leftId; RightId = rightId;
            var player = CharacterReader.Resolve(null); var target = CharacterReader.Resolve(targetId);
            if (player == null || target == null) throw new InvalidOperationException("人物或世界尚未就绪");
            PlayerId = player.data.unitData.unitID; TargetId = target.data.unitData.unitID;
            PlayerPointer = player.Pointer; TargetPointer = target.Pointer;
            PlayerData = player.data.Pointer; TargetData = target.data.Pointer;
            PlayerInfo = player.data.unitData.Pointer; TargetInfo = target.data.unitData.Pointer;
            PlayerBag = player.data.unitData.propData.Pointer; TargetBag = target.data.unitData.propData.Pointer; OriginalSchoolId = target.data.unitData.schoolID;
            LeftPointer = string.IsNullOrEmpty(leftId) ? IntPtr.Zero : CharacterReader.Resolve(leftId)?.Pointer ?? IntPtr.Zero;
            RightPointer = string.IsNullOrEmpty(rightId) ? IntPtr.Zero : CharacterReader.Resolve(rightId)?.Pointer ?? IntPtr.Zero;
            var left = string.IsNullOrEmpty(leftId) ? null : CharacterReader.Resolve(leftId);
            var right = string.IsNullOrEmpty(rightId) ? null : CharacterReader.Resolve(rightId);
            LeftData = left?.data.Pointer ?? IntPtr.Zero; LeftInfo = left?.data.unitData.Pointer ?? IntPtr.Zero;
            RightData = right?.data.Pointer ?? IntPtr.Zero; RightInfo = right?.data.unitData.Pointer ?? IntPtr.Zero;
            SchoolPointer = id == "join" ? g.world.build.GetBuild<MapBuildSchool>(schoolId)?.Pointer ?? IntPtr.Zero : target.data.school?.Pointer ?? IntPtr.Zero;
            if (id == "title") { TitlePointer = player.data.unitData.objData.Pointer; OriginalTitle = FeatureOperations.PlayerTitle(); }
            if (id == "marry")
            {
                PlayerRelation = player.data.unitData.relationData.Pointer; TargetRelation = target.data.unitData.relationData.Pointer;
                MarriageConfig = FeatureOperations.RequireMarriageConfig(player, target).Pointer;
            }
            Origin = target.data.unitData.GetPoint();
            BeforeCapacity = id == "capacity" ? target.data.unitData.propData.GetGridMaxNum() : 0;
            if (id == "capacity") { CapacityPointer = target.data.dynUnitData.propGridNum.Pointer; BeforeCapacityBase = target.data.dynUnitData.propGridNum.baseValue; CapacityStore = target.data.unitData.objData.Pointer; CapacityRecord = CapacityRuntime.Record(target.data.unitData.objData); BeforeCapacityBonus = CapacityRuntime.Bonus(target.data.unitData.objData); }
            if (id == "give" || id == "ask")
            {
                var source = id == "give" ? player : target;
                var record = string.IsNullOrEmpty(payload) ? null : source.data.unitData.propData.GetProps(payload);
                if (record == null) throw new InvalidOperationException("没有可用物品，请先添加物品或选择其他人物");
                ItemSignature = record.soleIDHashData; ItemCount = record.propsCount;
            }
        }
    }
    internal sealed class FeatureChoice
    {
        internal readonly string Id, Name;
        internal FeatureChoice(string id, string name) { Id = id; Name = name; }
    }
    internal static class FeatureOperations
    {
        private static string activeContext;
        private static long generation, endedGeneration;
        private static bool running;
        private static string blockedReason;
        internal const string TitleKey = "BHML.title.text";
        internal static string PlayerTitle() { var unit = CharacterReader.Resolve(null); var data = unit?.data.unitData.objData; return data != null && data.ContainsKey(TitleKey) ? data.GetString(TitleKey) ?? "" : ""; }
        internal static bool Busy { get { Sync(); return running; } }
        private static void Sync()
        {
            var current = CharacterReader.ContextKey();
            if (current == activeContext) return;
            activeContext = current; running = false; blockedReason = null; generation++;
        }
        internal static string Availability(string id, string targetId)
        {
            try
            {
                var definition = WorkshopFeatures.Find(id);
                if (definition != null && definition.Unavailable != null) return definition.Unavailable;
                if (id == "title" && CharacterReader.Resolve(targetId)?.Pointer != CharacterReader.Resolve(null)?.Pointer) return "自定义道号仅支持玩家显示名，原生道号效果保持原样";
                if (CharacterReader.ContextKey() == null) return "世界或玩家尚未就绪";
                // Runtime switches and the read-only list remain usable even when a write is quarantined.
                if (definition != null && definition.Toggle) return FeatureRuntime.Error;
                if (id == "dead") return null;
                if (Busy) return blockedReason ?? "原生交互正在进行，请等待游戏流程完成";
                if (CharacterSave.Busy || DataMgr.isSaveData) return "保存正在进行，请稍候";
                var target = CharacterReader.Resolve(targetId); var player = CharacterReader.Resolve(null);
                if (target == null) return "操作对象已失效，请重新选择";
                if (WorkshopFeatures.IsRole(id) && target.Pointer == player.Pointer) return "不能对自己使用";
                if (id == "marry") RequireMarriageConfig(player, target);
                if (id == "capacity")
                {
                    CapacityRuntime.EnsureBound(target);
                    var f = target.data.dynUnitData.propGridNum;
                    if (f == null || f.lockValue || f.onBaseValueSetCall == null || f.allMulCall.Count > 0 || f.allMulAddCall.Count > 0 || f.allEqualCall.Count > 0)
                        return "容量被锁定、倍率或固定覆盖影响，不能安全计算＋999";
                    WorkshopFeatures.CapacityTarget(f.value); WorkshopFeatures.CapacityTarget(CapacityRuntime.Bonus(target.data.unitData.objData));
                    if (!f.isOverflow && WorkshopFeatures.CapacityTarget(f.value) > f.maxValue) return "容量超过原生计算上限，未修改";
                }
                if (id == "revive" && !ConfRoleCreateFeatureEx.IsGhostLuck(target)) return "仅支持仍有原生人物实例的鬼魂；彻底死亡记录只读";
                if (id == "outer" || id == "post" || id == "exit" || id == "return")
                    if (target.data.school == null) return "当前对象没有有效宗门";
                    else if (target.data.school.buildData?.postData == null) return "当前宗门成员与职位数据不可读取，已停止提交";
                if (id == "join" && target.data.school != null) return "当前对象已有宗门，需先按原生流程退宗";
                return null;
            }
            catch (Exception ex) { return "暂不可用：" + ex.Message; }
        }
        internal static FeatureRequest Prepare(string id, string targetId, string payload = null, string schoolId = null,
            SchoolPostType post = SchoolPostType.Out, SchoolDepartmentType department = SchoolDepartmentType.None,
            string leftId = null, string rightId = null)
        {
            var reason = Availability(id, targetId); if (reason != null) throw new InvalidOperationException(reason);
            var request = new FeatureRequest(id, targetId, payload, schoolId, post, department, leftId, rightId);
            if (id == "command" && !ValidCommand(payload)) throw new InvalidOperationException("请输入一条原生命令，长度1–512，不含换行、分号或竖线");
            if (id == "drama")
            {
                int drama; if (!int.TryParse(payload, NumberStyles.None, CultureInfo.InvariantCulture, out drama) || drama <= 0 || g.conf.dramaDialogue.GetItem(drama) == null)
                    throw new InvalidOperationException("剧情ID必须对应当前配置中的有效正整数");
                RequireRole(leftId); RequireRole(rightId);
            }
            if (id == "join" && (string.IsNullOrEmpty(schoolId) || g.world.build.GetBuild<MapBuildSchool>(schoolId) == null))
                throw new InvalidOperationException("请先选择有效宗门");
            if (id == "title" && (payload == null || payload.Trim().Length > 24 || payload.IndexOfAny(new[] { '<', '>', '\r', '\n', '\0' }) >= 0)) throw new InvalidOperationException("道号最多24字，不含换行或富文本标记；留空恢复原生显示");
            Ensure(request); return request;
        }
        // Mirrors the read-only gates in UINPCSetRelation.UpdateUI. Native CreateAction still checks action conditions.
        internal static ConfRoleRelationItem RequireMarriageConfig(WorldUnitBase player, WorldUnitBase target)
        {
            var from = player?.data; var to = target?.data;
            var a = from?.unitData.relationData; var b = to?.unitData.relationData;
            if (a == null || b == null || from.dynUnitData?.sex == null || to.dynUnitData?.sex == null
                || from.dynUnitData.age == null || to.dynUnitData.age == null || g.conf?.roleRelation == null)
                throw new InvalidOperationException("求婚所需的人物关系、年龄、性别或配置数据不可读取");
            var item = g.conf.roleRelation.GetRelationItem(player, target, UnitRelationType.Married);
            if (item == null || string.IsNullOrEmpty(item.askText))
                throw new InvalidOperationException("原生求婚配置不完整，已停止提交");
            if (!string.IsNullOrEmpty(a.married) || !string.IsNullOrEmpty(b.married))
                throw new InvalidOperationException("玩家或对象已有配偶，不能发起新的求婚");
            int actorSex = from.dynUnitData.sex.value, targetSex = to.dynUnitData.sex.value;
            if (item.gender == 1 && targetSex != 1 || item.gender == 2 && targetSex != 2 || item.gender == 3 && targetSex == actorSex)
                throw new InvalidOperationException("不满足原生求婚性别条件");
            long difference = (long)to.dynUnitData.age.value - from.dynUnitData.age.value, threshold = (long)item.age * 12;
            if (item.age > 0 && difference < threshold || item.age < 0 && difference > threshold)
                throw new InvalidOperationException("不满足原生求婚年龄条件");
            if (item.relationRequire != 0 && !a.IsRelation(target, (UnitRelationType)item.relationRequire))
                throw new InvalidOperationException("不满足原生求婚前置关系条件");
            return item;
        }
        internal static bool ValidCommand(string value)
        { return !string.IsNullOrWhiteSpace(value) && value.Length <= 512 && value.IndexOfAny(new[] { '\r', '\n', '\0', '|', ';' }) < 0; }
        private static WorldUnitBase RequireRole(string id)
        { var unit = CharacterReader.Resolve(id); if (string.IsNullOrEmpty(id) || unit == null) throw new InvalidOperationException("左右角色必须明确选择存活人物"); return unit; }
        internal static void Ensure(FeatureRequest request, bool originalState = true)
        {
            var player = CharacterSave.RequireCurrentTarget(request.Context, request.PlayerId, request.PlayerPointer);
            var target = CharacterSave.RequireCurrentTarget(request.Context, request.TargetId, request.TargetPointer);
            if (player.data.Pointer != request.PlayerData || player.data.unitData.Pointer != request.PlayerInfo
                || player.data.unitData.propData.Pointer != request.PlayerBag
                || target.data.Pointer != request.TargetData || target.data.unitData.Pointer != request.TargetInfo
                || target.data.unitData.propData.Pointer != request.TargetBag)
                throw new InvalidOperationException("人物数据容器已被替换，请重新操作");
            if (originalState && target.data.unitData.schoolID != request.OriginalSchoolId) throw new InvalidOperationException("人物宗门身份已变化，请重新操作");
            if (!string.IsNullOrEmpty(request.LeftId) && CharacterReader.Resolve(request.LeftId)?.Pointer != request.LeftPointer) throw new InvalidOperationException("剧情左角色已被替换");
            if (!string.IsNullOrEmpty(request.RightId) && CharacterReader.Resolve(request.RightId)?.Pointer != request.RightPointer) throw new InvalidOperationException("剧情右角色已被替换");
            if (!string.IsNullOrEmpty(request.LeftId)) { var left = RequireRole(request.LeftId); if (left.data.Pointer != request.LeftData || left.data.unitData.Pointer != request.LeftInfo) throw new InvalidOperationException("剧情左角色数据已被替换"); }
            if (!string.IsNullOrEmpty(request.RightId)) { var right = RequireRole(request.RightId); if (right.data.Pointer != request.RightData || right.data.unitData.Pointer != request.RightInfo) throw new InvalidOperationException("剧情右角色数据已被替换"); }
            if (originalState && (request.Id == "join" || request.Id == "outer" || request.Id == "post" || request.Id == "exit" || request.Id == "return"))
            { var school = request.Id == "join" ? g.world.build.GetBuild<MapBuildSchool>(request.SchoolId) : target.data.school; if (school == null || school.Pointer != request.SchoolPointer) throw new InvalidOperationException("宗门实例已被替换"); }
            if (request.Id == "marry" && (player.data.unitData.relationData == null || target.data.unitData.relationData == null
                || player.data.unitData.relationData.Pointer != request.PlayerRelation || target.data.unitData.relationData.Pointer != request.TargetRelation))
                throw new InvalidOperationException("求婚关系容器已被替换，请重新确认");
            if (originalState && request.Id == "marry" && RequireMarriageConfig(player, target).Pointer != request.MarriageConfig)
                throw new InvalidOperationException("原生求婚配置已被替换，请重新确认");
            if (request.Id == "title" && (player.data.unitData.objData.Pointer != request.TitlePointer || originalState && PlayerTitle() != request.OriginalTitle)) throw new InvalidOperationException("玩家道号记录已变化，请重新确认");
            if (Busy) throw new InvalidOperationException(blockedReason ?? "上一次原生交互尚未结束");
        }
        private static UnitActionBase SimpleAction(string id, WorldUnitBase target, FeatureRequest request)
        {
            switch (id)
            {
                case "battle": return new UnitActionRoleBattle(target, "qiecuo");
                case "drill": return new UnitActionRoleDrill(target);
                case "attack": return new UnitActionRoleAttack(target);
                case "invite": return new UnitActionRoleInvite(target);
                case "trains": return new UnitActionRoleTrains(target);
                case "recruit": return new UnitActionRoleRoleSchoolRecruit(target);
                case "kill": return new UnitActionRoleKill(target, false, true, false, false);
                case "heal": return new UnitActionRoleSave();
                case "exit": return new UnitActionSchoolExit();
                case "outer": return new UnitActionSchoolSetPostType(target.data.unitData.schoolID, SchoolPostType.Out, SchoolDepartmentType.None);
                case "post": return request == null ? null : new UnitActionSchoolSetPostType(target.data.unitData.schoolID, request.Post, request.Department);
                case "join": return request == null ? null : new UnitActionSchoolJoin(request.SchoolId, SchoolPostType.Out, SchoolDepartmentType.None);
            }
            return null;
        }
        internal static string Execute(FeatureRequest request)
        {
            Ensure(request);
            var reason = Availability(request.Id, request.TargetId); if (reason != null) throw new InvalidOperationException(reason);
            var target = CharacterReader.Resolve(request.TargetId); var player = CharacterReader.Resolve(request.PlayerId);
            if (request.Id == "title")
            {
                FeatureRuntime.BindTitle(); Ensure(request);
                var data = player.data.unitData.objData; var pointer = data.Pointer;
                string desired = request.Payload.Trim(); CharacterSave.MarkDirty(request.Context);
                try { data.SetString(TitleKey, desired); Ensure(request, false); if (CharacterReader.Resolve(request.PlayerId).data.unitData.objData.Pointer != pointer || PlayerTitle() != desired) throw new InvalidOperationException("道号显示名回读不一致"); }
                catch { Quarantine(request); throw; }
                return desired.Length == 0 ? "已恢复原生道号显示；请保存" : "玩家显示道号已设为「" + desired + "」；原生道号效果不变，请保存";
            }
            if (request.Id == "capacity") return Capacity(request, target);
            if (request.Id == "command" || request.Id == "drama" || request.Id == "revive") return Tool(request, target);
            var actor = WorkshopFeatures.IsRole(request.Id) ? player : target;
            var action = SimpleAction(request.Id, target, request);
            if (request.Id == "marry")
            {
                // RoleMarry is a wedding outcome/log action, not a proposal. Its point must be a wedding venue.
                // Use the same request action as UINPCSetRelation; the game asks the NPC, selects a venue and schedules the wedding.
                var item = RequireMarriageConfig(player, target);
                action = new UnitActionRoleRelation(target, UnitRelationType.Married, item) { isRequestMarried = true };
            }
            if (request.Id == "give" || request.Id == "ask")
            {
                var source = request.Id == "give" ? player : target;
                var record = source.data.unitData.propData.GetProps(request.Payload);
                if (record == null || record.propsCount != request.ItemCount || record.soleIDHashData != request.ItemSignature
                    || record.propsItem == null || record.propsItem.hide == 1 || source.data.unitData.IsEquipProps(record.soleID))
                    throw new InvalidOperationException("所选物品已变化、隐藏或装备中，请重新选择");
                if (request.Id == "ask") action = new UnitActionRoleAskfor(target, record.soleID, 1);
                else
                {
                    if (!UnitActionRoleGive.PropsItemCanGive(player, target, record)) throw new InvalidOperationException("原生规则不允许赠送此物品");
                    var copy = record.Clone(); copy.propsCount = 1;
                    var list = new Il2CppSystem.Collections.Generic.List<DataProps.PropsData>(); list.Add(copy);
                    action = new UnitActionRoleGive(target, list);
                }
            }
            if (request.Id == "teach")
            {
                var learned = player.data.unitData.GetActionMartial(request.Payload);
                if (learned == null || !UnitActionRoleTeachSkill.SkillCanTeach(target, learned)) throw new InvalidOperationException("原生规则不允许传授此功法");
                action = new UnitActionRoleTeachSkill(target, learned, UnitActionRoleTeachSkill.TeachType.Teach);
            }
            if (request.Id == "return")
            {
                var school = target.data.school; if (school == null || school.points == null || school.points.Length == 0) throw new InvalidOperationException("宗门地点不可读取");
                action = new UnitActionSetPoint(school.points[0]);
            }
            if (request.Id == "move")
            {
                var values = (request.Payload ?? "").Split(','); int x, y;
                if (values.Length != 2 || !int.TryParse(values[0], out x) || !int.TryParse(values[1], out y)) throw new InvalidOperationException("地图坐标无效");
                var point = new Vector2Int(x, y);
                if (g.data.grid.GetGridData(point) == null) throw new InvalidOperationException("目标坐标不存在");
                actor = player; action = new UnitActionSetPoint(point);
            }
            if (action == null) throw new InvalidOperationException("此操作尚无已核实的执行入口");
            var postAction = action.TryCast<UnitActionSchoolSetPostType>();
            if (postAction != null && (postAction.school == null || postAction.school.Pointer != request.SchoolPointer
                || postAction.postItem == null || postAction.school.buildData?.postData == null))
                throw new InvalidOperationException("当前宗门缺少该职位配置或成员数据，已停止提交");
            if (postAction != null)
            {
                var school = target.data.school;
                if (school.buildData.GetPostType(request.TargetId) == request.Post
                    && (request.Department == SchoolDepartmentType.None || school.buildData.postData.GetUnitDepartmentType(request.TargetId) == request.Department))
                    return "人物已是所选职位及部门，无需重复变更";
                // Same option as DramaFunction.SetSchoolPost: administrative post assignment,
                // with native membership, occupied-seat and player post-cap checks still intact.
                postAction.isTest = true;
            }
            Ensure(request); // CreateAction invokes Init/IsCreate once; these native checks can open drama.
            if (request.Id == "return" || request.Id == "move")
                if (target.data.unitData.GetPoint() != request.Origin) throw new InvalidOperationException("人物位置已变化，请重新操作");
            var role = action.TryCast<UnitActionRoleBase>();
            long operation = ++generation;
            action.Init(actor); // Init installs native end handlers before our callback, exactly once.
            Ensure(request);
            if (role != null)
            {
                role.AddEndCall((Il2CppSystem.Action)(new Action(() =>
                {
                    if (CharacterReader.ContextKey() != request.Context || operation != generation || endedGeneration == operation) return;
                    endedGeneration = operation; running = false; CharacterSave.MarkDirty(request.Context);
                    try { if (request.Id == "kill") CharacterSave.RequireCurrentTarget(request.Context, request.PlayerId, request.PlayerPointer); else Ensure(request, false); }
                    catch (Exception ex) { running = true; blockedReason = "交互结束后身份回读失败，已保留待保存状态；请退出存档后核对：" + ex.Message; }
                })));
            }
            bool started = false;
            try
            {
                started = true; int result = actor.CreateAction(action, false);
                if (CharacterReader.ContextKey() != request.Context) throw new InvalidOperationException("执行期间世界已切换，原世界可能部分执行");
                if (result != 0)
                {
                    // CreateAction returns a failed IsCreate result BEFORE invoking Create.
                    // Role checks can themselves open drama, so retain Pending for those checks.
                    started = false;
                    if (role != null) CharacterSave.MarkDirty(request.Context);
                    throw new InvalidOperationException("原生条件拒绝本次操作（代码 " + result + "）" + (postAction != null ? "；请检查职位名额、剧情人物限制或玩家职位上限" : "；请按游戏提示核对条件") + "；未锁定后续功能");
                }
                if (role != null && endedGeneration != operation) { activeContext = request.Context; running = true; }
                else if (request.Id != "kill") Ensure(request, false);
                CharacterSave.MarkDirty(request.Context);
                if (request.Id == "join" && CharacterReader.Resolve(request.TargetId).data.unitData.schoolID != request.SchoolId) throw new InvalidOperationException("入宗身份回读不一致");
                if (request.Id == "exit" && !string.IsNullOrEmpty(CharacterReader.Resolve(request.TargetId).data.unitData.schoolID)) throw new InvalidOperationException("退宗身份回读不一致");
                if (postAction != null)
                {
                    var school = CharacterReader.Resolve(request.TargetId).data.school;
                    if (school == null || school.Pointer != request.SchoolPointer || school.buildData.GetPostType(request.TargetId) != postAction.postType
                        || (request.Department != SchoolDepartmentType.None && school.buildData.postData.GetUnitDepartmentType(request.TargetId) != request.Department))
                        throw new InvalidOperationException("宗门职位或部门回读不一致，可能部分执行，请核对成员记录");
                }
                if (request.Id == "return" || request.Id == "move")
                {
                    var moved = CharacterReader.Resolve(actor.data.unitData.unitID); var destination = action.Cast<UnitActionSetPoint>().point;
                    if (moved == null || moved.Pointer != actor.Pointer || moved.data.unitData.GetPoint() != destination)
                        throw new InvalidOperationException("移动结果回读不一致");
                }
                return role == null ? "原生操作已完成；请保存并核对游戏状态" : "原生交互已启动；结果以游戏流程为准，待完成后保存";
            }
            catch
            { if (started) { CharacterSave.MarkDirty(request.Context); if (CharacterReader.ContextKey() == request.Context) { activeContext = request.Context; running = true; blockedReason = "原生动作可能部分执行，已阻止重复提交；请退出存档后核对"; } } throw; }
        }
        private static string Capacity(FeatureRequest request, WorldUnitBase target)
        {
            var field = target.data.dynUnitData.propGridNum; var pointer = field.Pointer;
            int desired = WorkshopFeatures.CapacityTarget(request.BeforeCapacity);
            var store = target.data.unitData.objData;
            if (field.Pointer != request.CapacityPointer || field.baseValue != request.BeforeCapacityBase || field.value != request.BeforeCapacity || target.data.unitData.propData.GetGridMaxNum() != request.BeforeCapacity
                || store.Pointer != request.CapacityStore || CapacityRuntime.Record(store) != request.CapacityRecord)
                throw new InvalidOperationException("容量已变化，请重新确认");
            int next = WorkshopFeatures.CapacityTarget(request.BeforeCapacityBonus); Ensure(request);
            CharacterSave.MarkDirty(request.Context);
            try
            {
            store.SetString(CapacityRuntime.Key, next.ToString(CultureInfo.InvariantCulture)); Ensure(request, false);
            CapacityRuntime.EnsureBound(target); Ensure(request, false);
            var current = CharacterReader.Resolve(request.TargetId).data.dynUnitData.propGridNum;
            if (current.Pointer != pointer || current.baseValue != request.BeforeCapacityBase || current.value != desired
                || CharacterReader.Resolve(request.TargetId).data.unitData.objData.Pointer != request.CapacityStore || CapacityRuntime.Bonus(store) != next
                || target.data.unitData.propData.GetGridMaxNum() != desired)
                throw new InvalidOperationException("容量写入可能部分生效，回读不一致；已保留待保存状态");
            return "背包容量 " + request.BeforeCapacity + " → " + desired + "；请点击保存";
            } catch { Quarantine(request); throw; }
        }
        private static void Quarantine(FeatureRequest request)
        { if (CharacterReader.ContextKey() == request.Context) { activeContext = request.Context; running = true; blockedReason = "原生操作可能部分执行，已保留待保存状态并阻止再次操作；请退出存档后核对"; } }
        private static string Tool(FeatureRequest request, WorldUnitBase target)
        {
            Ensure(request); var data = new DramaFunctionData { unitLeft = CharacterReader.Resolve(request.PlayerId), unitRight = target };
            CharacterSave.MarkDirty(request.Context);
            try
            {
            if (request.Id == "command") DramaFunctionTool.OptionsFunction(request.Payload.Trim(), data);
            else if (request.Id == "revive")
            {
                if (!ConfRoleCreateFeatureEx.IsGhostLuck(target)) throw new InvalidOperationException("鬼魂状态已变化");
                DramaFunctionTool.OptionsFunction("npcReviveNoDrama_2", data);
            }
            else
            {
                var left = RequireRole(request.LeftId); var right = RequireRole(request.RightId);
                if (!DramaTool.OpenDrama(int.Parse(request.Payload, CultureInfo.InvariantCulture), new DramaData { unitLeft = left, unitRight = right }))
                    throw new InvalidOperationException("原生剧情未打开；请核对场景和剧情条件");
            }
            if (CharacterReader.ContextKey() != request.Context) throw new InvalidOperationException("原生指令执行期间世界已切换，原世界可能发生变化");
            if (request.Id == "revive")
            {
                Ensure(request); if (ConfRoleCreateFeatureEx.IsGhostLuck(CharacterReader.Resolve(request.TargetId)))
                    throw new InvalidOperationException("原生复活未移除鬼魂状态；已保留待保存状态，不自动重试");
            }
            return "原生流程已派发；请核对游戏结果，可能已改变内存并触发自动保存";
            } catch { Quarantine(request); throw; }
        }
        internal static List<FeatureChoice> SchoolChoices()
        {
            var result = new List<FeatureChoice>();
            foreach (var school in g.world.build.GetBuilds<MapBuildSchool>())
                if (school != null) result.Add(new FeatureChoice(school.buildData.id, school.name));
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name)); return result;
        }
        internal static string SchoolSummary(string id)
        {
            try { var unit = CharacterReader.Resolve(id); return unit == null ? "对象已失效" : unit.data.school == null ? "无宗门" : unit.data.school.name; }
            catch { return "宗门暂不可读取"; }
        }
        internal static List<FeatureChoice> DeathChoices()
        {
            var result = new List<FeatureChoice>();
            foreach (var pair in g.data.unitDie.allUnitDie)
            { var record = pair.Value; if (record == null) continue; string name = ""; foreach (var part in record.name) name += part; result.Add(new FeatureChoice(record.unitID, name + " · 已故")); }
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name)); return result;
        }
    }
}
