// Layout-only substitutes. Native feature behavior is checked separately by FeatureHarness.
using System;
using System.Collections.Generic;
using MOD_YtxSQ1.Services;
public enum SchoolPostType { None,SchoolMain,BigElders,Elders,Inherit,In,Out }
public enum SchoolDepartmentType { None,Personnel,Security,Diplomacy,Constructor,Train }
public static class UITipItem { public static void AddTip(string value,float time) { } }
public sealed class UITeachSkill { public void InitData(WorldUnitBase unit,Il2CppSystem.Action<DataUnit.ActionMartialData> callback) { } }
namespace MOD_YtxSQ1.Services {
    internal sealed class FeatureChoice {
        internal readonly string Id,Name;
        internal FeatureChoice(string id,string name) { Id=id;Name=name; }
    }
    internal sealed class FeatureRequest {
        internal string Id,TargetId,PlayerId,Payload,LeftId,RightId;
        internal int BeforeCapacity;
        internal UnityEngine.Vector2Int Origin;
        internal FeatureRequest(string id,string targetId,string payload,string leftId,string rightId) {
            Id=id;TargetId=targetId;PlayerId="player";Payload=payload;LeftId=leftId;RightId=rightId;
            BeforeCapacity=100;Origin=new UnityEngine.Vector2Int(0,0);
        }
    }
    internal static class FeatureRuntime {
        private static readonly HashSet<string> toggles=new HashSet<string>();
        internal static string Error {get {return null;}}
        internal static bool Enabled(string id) {return toggles.Contains(id);}
        internal static void Toggle(string id) {if(!toggles.Remove(id))toggles.Add(id);}
    }
    internal static class FeatureOperations {
        internal static string Availability(string id,string target) {
            var definition=WorkshopFeatures.Find(id);
            if(definition!=null && definition.Unavailable!=null)return definition.Unavailable;
            if(id=="title" && target!="player")return "Player title only";
            if(WorkshopFeatures.IsRole(id) && target=="player")return "Cannot target yourself";
            if(CharacterReader.Resolve(target)==null)return "Target invalid";
            return null;
        }
        internal static FeatureRequest Prepare(string id,string target,string payload=null,string schoolId=null,
            SchoolPostType post=SchoolPostType.Out,SchoolDepartmentType department=SchoolDepartmentType.None,string leftId=null,string rightId=null) {
            return new FeatureRequest(id,target,payload,leftId,rightId);
        }
        internal static void Ensure(FeatureRequest request) { }
        internal static string Execute(FeatureRequest request) {return "layout substitute only";}
        internal static string SchoolSummary(string id) {return "无宗门";}
        internal static List<FeatureChoice> DeathChoices() {return new List<FeatureChoice>{new FeatureChoice("dead","已故测试人物")};}
        internal static List<FeatureChoice> SchoolChoices() {return new List<FeatureChoice>{new FeatureChoice("school","测试宗门")};}
    }
}
