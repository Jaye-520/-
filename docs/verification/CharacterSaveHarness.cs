using System;
using System.Linq;
using MOD_YtxSQ1.Services;

internal static class CharacterSaveHarness
{
    private static int passed;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); passed++;Console.WriteLine("PASS " + name); }
    private static CharacterEditSession session;
    private static string message;
    private static void Reset()
    {
        g.world=new World();g.conf=new Config();g.data=new DataMgr();DataMgr.isSaveData=false;WorldFactory.HobbyFactory=null;
        CharacterSave.Sync(CharacterReader.ContextKey());session=new CharacterEditSession();session.Sync(CharacterReader.ContextKey());
        g.world.playerUnit.data.unitData.propertyData.Name="测试人物";
    }
    private static void Stage(int group,int index,string value,int slot=0)
    {
        var f=new CharacterField(group,index,slot);string error;
        if(!session.Stage("player",f,CharacterEditData.Read(g.world.playerUnit,f),value,out error))throw new Exception(error);
    }
    private static void Commit() { CharacterSave.Commit(session,(ok,m)=>message=m); }
    public static void Main()
    { try { Run(); } catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); Environment.ExitCode=1; } }
    private static void Run()
    {
        Reset();var duplicateHobby=new WorldUnitHobbyBase();g.world.playerUnit.allHobby.Add(duplicateHobby);g.world.playerUnit.allHobby.Add(duplicateHobby);
        Stage(0,10,"204",1);Commit();
        Check(g.data.Calls==0 && CharacterSave.Pending==0 && session.Count==1 && duplicateHobby.DestroyCalls==0,
            "hobby preflight failure before mutation preserves draft without quarantine or pending");
        g.world.playerUnit.allHobby.Remove(duplicateHobby);Commit();
        Check(g.data.Calls==1 && duplicateHobby.DestroyCalls==1 && g.world.playerUnit.data.unitData.propertyData.hobby[1]==204,
            "resolved preflight error can retry because no native mutation occurred");g.data.Complete(true);CharacterSave.Poll();
        foreach(var isNpc in new[]{false,true})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            g.conf.roleCreateHobby.GetItem(204).item="Probe_1";
            var instances=new System.Collections.Generic.List<WorldUnitHobbyBase>();
            WorldFactory.HobbyFactory=()=>{var h=new WorldUnitHobbyBase();instances.Add(h);h.AfterInit=()=>{throw new InvalidOperationException("partial hobby init");};return h;};
            var field=new CharacterField(0,10,1);string error;
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"204",out error);Commit();
            var partial=instances.Single(h=>h.InitCalls==1);var count=instances.Count;var dirty=CharacterSave.Pending;
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"202",out error);Commit();
            Check(g.data.Calls==0 && partial.DestroyCalls==0 && instances.Count==count && CharacterSave.Pending==dirty && session.Count==1,
                "manual retry cannot destroy partial hobby runtime or repeat factory/init: NPC="+isNpc);
            CharacterSave.Sync(null);CharacterSave.Sync(CharacterReader.ContextKey());
            session=new CharacterEditSession();session.Sync(CharacterReader.ContextKey());
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"202",out error);Commit();
            Check(partial.DestroyCalls==0 && instances.Count==count && session.Count==1,
                "UI sync suspension does not clear uncertain hobby state for same world: NPC="+isNpc);
            g.data=new DataMgr();CharacterSave.Sync(CharacterReader.ContextKey());session=new CharacterEditSession();session.Sync(CharacterReader.ContextKey());
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"202",out error);Commit();
            Check(g.data.Calls==0 && partial.DestroyCalls==0 && instances.Count==count && session.Count==1,
                "save-manager replacement is not proof of hobby runtime recovery: NPC="+isNpc);
            // 独立人物不继承原人物的兴趣运行标记；直接检查生产Write准备，不操作磁盘保存。
            var healthy=new WorldUnitBase();healthy.data.unitData.unitID="healthy";g.world.unit.Npcs.Add(healthy);
            var change=new CharacterEditSession.Change{UnitId="healthy",Field=field,Original=CharacterEditData.Read(healthy,field),Value="204"};
            var prepare=typeof(CharacterSave).GetMethod("PrepareForUnit",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Check(prepare.Invoke(null,new object[]{change,new System.Collections.Generic.List<CharacterEditSession.Change>{change},healthy})!=null,
                "uncertain hobby marker is scoped to original unit, not all NPCs: NPC="+isNpc);
            Reset();g.conf.roleCreateHobby.GetItem(204).item="Probe_1";Stage(0,10,"204",1);Commit();
            Check(g.data.Calls==1 && g.world.playerUnit.data.unitData.propertyData.hobby[1]==204,
                "new native player/world identity clears stale hobby quarantine: NPC="+isNpc);g.data.Complete(true);CharacterSave.Poll();
        }
        foreach(var isNpc in new[]{false,true})
        foreach(var mutation in new[]{"add","removePeer","replaceList","initAdd","initThrow"})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var foreign=new WorldUnitHobbyBase();var peer=new WorldUnitHobbyBase();var first=new WorldUnitHobbyBase();
            var owned=target.allHobby;owned.Add(peer);owned.Add(first);var original=target.data.unitData.propertyData;
            if(mutation=="add")first.AfterDestroy=()=>owned.Add(foreign);
            if(mutation=="removePeer")first.AfterDestroy=()=>owned.Remove(peer);
            if(mutation=="replaceList")first.AfterDestroy=()=>{target.allHobby=new NativeList<WorldUnitHobbyBase>();target.allHobby.Add(foreign);};
            var created=new System.Collections.Generic.List<WorldUnitHobbyBase>();
            if(mutation.StartsWith("init")){
                g.conf.roleCreateHobby.GetItem(204).item="Probe_1";
                WorldFactory.HobbyFactory=()=>{var instance=new WorldUnitHobbyBase();created.Add(instance);instance.AfterInit=()=>{
                    if(mutation=="initAdd")target.allHobby.Add(foreign);else throw new InvalidOperationException("partial hobby init");
                };return instance;};
            }
            var field=new CharacterField(0,10,1);string error;session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"204",out error);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && first.DestroyCalls==1 && foreign.DestroyCalls==0,
                "hobby callback collection conflict or partial init stops save and blind cleanup: "+mutation+" NPC="+isNpc);
            Check(mutation=="removePeer"?peer.DestroyCalls==0:mutation=="initThrow"?created.Any(h=>h.InitCalls==1 && h.DestroyCalls==0):
                target.allHobby.Contains(foreign),"hobby foreign/partial runtime remains available for verification: "+mutation+" NPC="+isNpc);
        }
        foreach(var isNpc in new[]{false,true})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var self=new WorldUnitHobbyBase();self.AfterDestroy=()=>target.allHobby.Remove(self);target.allHobby.Add(self);
            var hobbyField=new CharacterField(0,10,1);string error;
            session.Stage(target.data.unitData.unitID,hobbyField,CharacterEditData.Read(target,hobbyField),"204",out error);Commit();
            Check(g.data.Calls==1 && self.DestroyCalls==1 && target.allHobby.Count==0 && target.data.unitData.propertyData.hobby[1]==204,
                "hobby self-removal during destroy remains valid: NPC="+isNpc);g.data.Complete(true);CharacterSave.Poll();
            Reset();target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var foreign=new WorldUnitHobbyBase();var active=target;var instances=new System.Collections.Generic.List<WorldUnitHobbyBase>();
            g.conf.roleCreateHobby.GetItem(204).item="Probe_1";WorldFactory.HobbyFactory=()=>{var runtime=new WorldUnitHobbyBase();instances.Add(runtime);return runtime;};
            // 不假定同Order的批写排序；直接取得生产Write，检验成功Apply之后的Undo边界。
            var change=new CharacterEditSession.Change{UnitId=target.data.unitData.unitID,Field=hobbyField,Original=CharacterEditData.Read(target,hobbyField),Value="204"};
            var prepare=typeof(CharacterSave).GetMethod("PrepareForUnit",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var write=prepare.Invoke(null,new object[]{change,new System.Collections.Generic.List<CharacterEditSession.Change>{change},target});
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            ((Action)write.GetType().GetField("Apply",flags).GetValue(write))();
            active.allHobby.Add(foreign);bool stopped=false;
            try{((Action)write.GetType().GetField("Undo",flags).GetValue(write))();}catch(InvalidOperationException){stopped=true;}
            Check(stopped && g.data.Calls==0 && target.allHobby.Contains(foreign) && foreign.DestroyCalls==0 && instances.Any(h=>h.InitCalls==1 && h.DestroyCalls==0),
                "hobby undo refuses collection changed after successful apply: NPC="+isNpc);
        }
        foreach(var isNpc in new[]{false,true})
        foreach(var replacement in new[]{"data","info","property"})
        foreach(var category in new[]{"name","sex","hobby","trait"})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var owned=target.data;var info=owned.unitData;var prop=info.propertyData;
            prop.Name="原名";int destroyCalls=0;
            target.allHobby.Add(new WorldUnitHobbyBase{AfterDestroy=()=>destroyCalls++});
            var upper=new CharacterField(0,4,1);var field=category=="name"?new CharacterField(0,11):category=="sex"?new CharacterField(0,9):category=="hobby"?new CharacterField(0,10,1):new CharacterField(1,10);
            var value=category=="name"?"新名":category=="sex"?"1":category=="hobby"?"204":"4";string failure;
            session.Stage(target.data.unitData.unitID,upper,CharacterEditData.Read(target,upper),"600",out failure);
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),value,out failure);
            owned.dynUnitData.hpMax.AfterWrite=()=>{
                if(replacement=="data"){target.data=new PlayerData();target.data.unitData.propertyData.Owner=target;}
                if(replacement=="info"){owned.unitData=new UnitData();owned.unitData.propertyData.Owner=target;}
                if(replacement=="property")info.propertyData=new PropertyData{Owner=target};
            };
            Commit();var current=target.data.unitData.propertyData;
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && prop.GetName()=="原名" && prop.sex==UnitSexType.Woman && prop.hobby[1]==202 && prop.inTrait==1 && destroyCalls==0,
                "property container replacement stops old property writes and runtime rebuilding: "+replacement+" "+category+" NPC="+isNpc);
            Check(current.GetName()=="" && current.sex==UnitSexType.Woman && current.hobby[1]==202 && current.inTrait==1,
                "replacement properties retain their own values: "+replacement+" "+category+" NPC="+isNpc);
        }
        foreach(var isNpc in new[]{false,true})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var original=target.data.unitData.propertyData;var fresh=new PropertyData{Owner=target};var data=target.data;
            data.AfterWeight=()=>data.unitData.propertyData=fresh;
            var field=new CharacterField(1,10);string failure;
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"4",out failure);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && original.inTrait==4 && fresh.inTrait==1 && data.WeightCalls==1,
                "trait weight callback replacing properties stops rollback and duplicate weight update: NPC="+isNpc);
            Reset();target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            original=target.data.unitData.propertyData;fresh=new PropertyData{Owner=target};var active=target;
            int destroys=0;target.allHobby.Add(new WorldUnitHobbyBase{AfterDestroy=()=>{destroys++;active.data.unitData.propertyData=fresh;}});
            field=new CharacterField(0,10,1);session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"204",out failure);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && original.hobby[1]==202 && fresh.hobby[1]==202 && destroys==1 && target.allHobby.Count==1,
                "hobby destroy callback replacing properties stops slot rewrite and blind runtime rebuild: NPC="+isNpc);
        }
        foreach(var isNpc in new[]{false,true})
        foreach(var fieldName in new[]{"hpMax","hp","mp","sp","mpMax","spMax","gradeID"})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var dyn=target.data.dynUnitData;var oldHp=dyn.hp;var oldHpMax=dyn.hpMax;
            g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=10,hpUp=100};
            var replacement=new DynInt{Stored=777};int observedAttack=0,observedHpMax=0,observedHp=0;
            Action replace=()=>{
                observedAttack=dyn.attack.Stored;observedHpMax=oldHpMax.Stored;observedHp=oldHp.Stored;
                typeof(DynamicData).GetField(fieldName).SetValue(dyn,replacement);
            };
            if(fieldName=="hpMax")dyn.attack.AfterWrite=replace;else target.data.unitData.propertyData.AfterGrade=replace;
            var field=new CharacterField(1,11);string failure;
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"104",out failure);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && target.ConfUpdates==0 && dyn.attack.Stored==observedAttack && oldHpMax.Stored==observedHpMax && oldHp.Stored==observedHp,
                "realm individual field replacement stops later writes/rollback/save: "+fieldName+" NPC="+isNpc);
            Check(replacement.Stored==777,"realm replacement dynamic field remains untouched: "+fieldName+" NPC="+isNpc);
        }
        Reset();var numericDyn=g.world.playerUnit.data.dynUnitData;var originalAttack=numericDyn.attack;
        var freshAttack=new DynInt{Stored=777};originalAttack.AfterWrite=()=>numericDyn.attack=freshAttack;
        Stage(1,0,"90");Commit();
        Check(g.data.Calls==0 && CharacterSave.Pending>0 && originalAttack.Stored==90 && freshAttack.Stored==777,
            "numeric setter replacing its field never reports saved success or undoes detached original");
        Reset();numericDyn=g.world.playerUnit.data.dynUnitData;originalAttack=numericDyn.attack;freshAttack=new DynInt{Stored=777};
        numericDyn.hpMax.AfterWrite=()=>numericDyn.attack=freshAttack;
        Stage(0,4,"600",1);Stage(1,0,"90");Commit();
        Check(g.data.Calls==0 && CharacterSave.Pending>0 && originalAttack.Stored==77 && freshAttack.Stored==777,
            "earlier batch callback replacing peer field prevents writing or undoing detached peer");
        foreach(var isNpc in new[]{false,true})
        foreach(var lateChange in new[]{false,true})
        foreach(var mutation in new[]{"add","remove","replace","quality","luck"})
        {
            Reset();var target=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){target.data.unitData.unitID="npc";g.world.unit.Npcs.Add(target);}
            var history=isNpc?target.data.unitData.npcUpGrade:g.data.world.playerLog.upGrade;
            var prior=new DataWorld.World.PlayerLogData.GradeData{quality=1,luck=20};history[2]=prior;
            var dyn=target.data.dynUnitData;int observedAttack=0,observedHp=0,observedResource=0;
            g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=10,hpUp=100};
            DataWorld.World.PlayerLogData.GradeData foreign=null;
            Action change=()=>{
                observedAttack=dyn.attack.Stored;observedHp=dyn.hpMax.Stored;observedResource=dyn.hp.Stored;
                if(mutation=="add"){foreign=new DataWorld.World.PlayerLogData.GradeData{quality=3,luck=55};history[99]=foreign;}
                if(mutation=="remove")history.Remove(2);
                if(mutation=="replace"){foreign=new DataWorld.World.PlayerLogData.GradeData{quality=1,luck=20};history[2]=foreign;}
                if(mutation=="quality")prior.quality=4;
                if(mutation=="luck")prior.luck=999;
            };
            if(lateChange)target.data.unitData.propertyData.AfterGrade=change;else dyn.attack.AfterWrite=change;
            var field=new CharacterField(1,11);string failure;
            session.Stage(target.data.unitData.unitID,field,CharacterEditData.Read(target,field),"104",out failure);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && target.ConfUpdates==0 && dyn.attack.Stored==observedAttack && dyn.hpMax.Stored==observedHp && dyn.hp.Stored==observedResource,
                "history content change stops subsequent writes/rollback/save: "+mutation+" NPC="+isNpc+" late="+lateChange);
            Check(mutation=="add"?history.ContainsKey(99)&&ReferenceEquals(history[99],foreign):
                mutation=="remove"?!history.ContainsKey(2):mutation=="replace"?ReferenceEquals(history[2],foreign):
                mutation=="quality"?history[2].quality==4:history[2].luck==999,
                "callback history content is retained: "+mutation+" NPC="+isNpc+" late="+lateChange);
        }
        foreach(var replacement in new[]{"data","unitData","dynamic","property","metadata","history"})
        foreach(var lateReplacement in new[]{false,true})
        {
            Reset();var target=g.world.playerUnit;var owned=target.data;var originalInfo=owned.unitData;
            var originalDyn=owned.dynUnitData;var originalProp=originalInfo.propertyData;
            var originalMeta=originalInfo.objData;var originalHistory=g.data.world.playerLog.upGrade;
            g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=10,hpUp=100};
            int attackAtCallback=0,hpAtCallback=0,resourceAtCallback=0;
            Action replace=()=>{
                attackAtCallback=originalDyn.attack.Stored;hpAtCallback=originalDyn.hpMax.Stored;resourceAtCallback=originalDyn.hp.Stored;
                if(replacement=="data"){target.data=new PlayerData();target.data.unitData.propertyData.Owner=target;}
                if(replacement=="unitData"){owned.unitData=new UnitData();owned.unitData.propertyData.Owner=target;}
                if(replacement=="dynamic")owned.dynUnitData=new DynamicData();
                if(replacement=="property")originalInfo.propertyData=new PropertyData{Owner=target};
                if(replacement=="metadata")originalInfo.objData=new DataObjectData();
                if(replacement=="history")g.data.world.playerLog.upGrade=new NativeDictionary<int,DataWorld.World.PlayerLogData.GradeData>();
            };
            if(lateReplacement)originalProp.AfterGrade=replace;else originalDyn.attack.AfterWrite=replace;
            Stage(1,11,"104");Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && target.ConfUpdates==0 &&
                originalDyn.attack.Stored==attackAtCallback && originalDyn.hpMax.Stored==hpAtCallback && originalDyn.hp.Stored==resourceAtCallback,
                "realm container replacement stops later writes/rollback/save and preserves pending: "+replacement+" late="+lateReplacement);
            Check(!lateReplacement ? !originalMeta.ContainsKey("BHML.realm.branch:3") && originalHistory.Count==0 : originalMeta.GetString("BHML.realm.branch:3")=="104" && originalHistory.ContainsKey(3),
                "realm container replacement never rewrites already observed metadata/history: "+replacement+" late="+lateReplacement);
        }
        foreach(var lateReplacement in new[]{false,true})
        {
            Reset();var npcRealm=new WorldUnitBase();npcRealm.data.unitData.unitID="npc";g.world.unit.Npcs.Add(npcRealm);
            var dyn=npcRealm.data.dynUnitData;var prop=npcRealm.data.unitData.propertyData;var prior=npcRealm.data.unitData.npcUpGrade;
            g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=10,hpUp=100};
            int observedAttack=0,observedHp=0;
            Action replace=()=>{observedAttack=dyn.attack.Stored;observedHp=dyn.hpMax.Stored;npcRealm.data.unitData.npcUpGrade=new NativeDictionary<int,DataWorld.World.PlayerLogData.GradeData>();};
            if(lateReplacement)prop.AfterGrade=replace;else dyn.attack.AfterWrite=replace;
            var field=new CharacterField(1,11);string failure;
            session.Stage("npc",field,CharacterEditData.Read(npcRealm,field),"104",out failure);Commit();
            Check(g.data.Calls==0 && CharacterSave.Pending>0 && npcRealm.ConfUpdates==0 && dyn.attack.Stored==observedAttack && dyn.hpMax.Stored==observedHp,
                "NPC realm history replacement stops writes and rollback: late="+lateReplacement);
            Check(npcRealm.data.unitData.npcUpGrade.Count==0 && (lateReplacement?prior.ContainsKey(3):prior.Count==0),"NPC replacement history remains untouched: late="+lateReplacement);
        }
        // 官方只读表39/40同品质不同成长；替身查询保留首条，复现原生品质缓存。
        foreach (var isNpc in new[] { false, true })
        {
            Reset();
            var canonical=new GradeItem{id=39,grade=10,phase=1,quality=5,atkUp=1200,hpUp=35000};
            var variant=new GradeItem{id=40,grade=10,phase=1,quality=5,atkUp=671,hpUp=14903};
            var later=new GradeItem{id=43,grade=10,phase=2,quality=0,atkUp=447,hpUp=9935};
            g.conf.roleGrade.Overrides[39]=canonical;g.conf.roleGrade.Overrides[40]=variant;g.conf.roleGrade.Overrides[43]=later;
            g.conf.roleGrade._allConfList.AddRange(new[]{canonical,variant,later});
            var branchUnit=isNpc?new WorldUnitBase():g.world.playerUnit;
            if(isNpc){branchUnit.data.unitData.unitID="npc";g.world.unit.Npcs.Add(branchUnit);}
            var history=isNpc?branchUnit.data.unitData.npcUpGrade:g.data.world.playerLog.upGrade;
            history[10]=new DataWorld.World.PlayerLogData.GradeData{quality=5,luck=321};
            var bd=branchUnit.data.dynUnitData;var meta=branchUnit.data.unitData.objData;
            bd.gradeID.Stored=40;bd.attack.Stored=2000;bd.hpMax.Stored=40000;
            var first=CharacterRealmChange.Prepare(branchUnit,43);first.Apply();
            Check(bd.attack.Stored==2447 && bd.hpMax.Stored==49935 && meta.GetString("BHML.realm.branch:10")=="40" && history[10].luck==321,"alternate branch advance adds only next-stage growth and preserves source/luck: NPC="+isNpc);
            first.Undo();
            Check(bd.gradeID.value==40 && bd.attack.Stored==2000 && bd.hpMax.Stored==40000 && !meta.ContainsKey("BHML.realm.branch:10") && history[10].luck==321,"undo restores original branch metadata absence and history: NPC="+isNpc);
            first.Apply();
            CharacterRealmChange.Prepare(branchUnit,40).Apply();
            Check(bd.attack.Stored==2000 && bd.hpMax.Stored==40000,"return to exact branch reverses only next-stage growth: NPC="+isNpc);
            CharacterRealmChange.Prepare(branchUnit,39).Apply();
            Check(bd.attack.Stored==2529 && bd.hpMax.Stored==60097 && meta.GetString("BHML.realm.branch:10")=="39","explicit same-quality branch switch applies actual growth difference: NPC="+isNpc);
            CharacterRealmChange.Prepare(branchUnit,43).Apply();
            Check(bd.attack.Stored==2976 && bd.hpMax.Stored==70032,"canonical branch advance retains its actual prior growth: NPC="+isNpc);
            CharacterRealmChange.Prepare(branchUnit,40).Apply();
            CharacterRealmChange.Prepare(branchUnit,43).Apply();
            var down=CharacterRealmChange.Prepare(branchUnit,103);down.Apply();
            Check(!meta.ContainsKey("BHML.realm.branch:10"),"downgrade prunes higher branch hints: NPC="+isNpc);
            down.Undo();Check(meta.GetString("BHML.realm.branch:10")=="40" && bd.attack.Stored==2447,"downgrade undo restores prior known branch: NPC="+isNpc);
            foreach(var invalid in new[]{"bad",null,"43","999"})
            {
                meta.SetString("BHML.realm.branch:10",invalid);var rejected=false;
                try{CharacterRealmChange.Prepare(branchUnit,40);}catch(InvalidOperationException){rejected=true;}
                Check(rejected && bd.attack.Stored==2447 && bd.gradeID.value==43,"invalid branch hint rejected before writes: "+(invalid??"null")+" NPC="+isNpc);
            }
            meta.SetString("BHML.realm.branch:10","40");history[10].quality=4;var mismatch=false;
            try{CharacterRealmChange.Prepare(branchUnit,40);}catch(InvalidOperationException){mismatch=true;}
            Check(mismatch && bd.attack.Stored==2447,"stale quality branch rejected rather than guessing source: NPC="+isNpc);
        }
        Reset();Stage(0,4,"100");Commit();var duplicate=g.data.Callback;duplicate.Invoke(false);duplicate.Invoke(true);CharacterSave.Poll();
        Check(!CharacterSave.Busy && CharacterSave.Pending>0 && message.Contains("失败"),"first save callback wins; repeated native result cannot overwrite failure");
        Commit();var currentCallback=g.data.Callback;duplicate.Invoke(true);CharacterSave.Poll();
        Check(CharacterSave.Busy && CharacterSave.Pending>0 && g.data.Callback==currentCallback,"old duplicate callback cannot release newer save or consume its pending changes");
        g.data.Complete(true);CharacterSave.Poll();
        Reset();Stage(0,4,"100");g.data.ThrowSave=true;Commit();var late=g.data.Callback;
        Check(!CharacterSave.Busy && CharacterSave.Pending>0,"save dispatch exception preserves applied changes for explicit retry");
        g.data.ThrowSave=false;Commit();late.Invoke(true);CharacterSave.Poll();
        Check(CharacterSave.Busy && CharacterSave.Pending>0,"late callback from throwing dispatch cannot terminate replacement save");g.data.Complete(true);CharacterSave.Poll();
        Reset();Stage(0,4,"100");g.data.ThrowSave=true;g.data.ImmediateResult=true;Commit();
        Check(CharacterSave.Busy && CharacterSave.Pending>0,"synchronous callback followed by dispatch throw remains pending until poll");CharacterSave.Poll();
        Check(!CharacterSave.Busy && CharacterSave.Pending==0,"accepted synchronous result survives subsequent dispatch exception");
        Reset();var oldWorld=g.world;var oldData=g.data;Stage(0,4,"600",1);Stage(1,0,"90");
        oldWorld.playerUnit.data.dynUnitData.hpMax.AfterWrite=()=>{g.world=new World();g.data=new DataMgr();};Commit();
        Check(oldWorld.playerUnit.data.dynUnitData.attack.value==77 && oldData.Calls==0 && g.data.Calls==0 && CharacterSave.Pending==0 && session.Context==CharacterReader.ContextKey(),"world change inside first setter stops later old-reference writes, rollback and new-world save");
        Reset();var deadUnit=new WorldUnitBase();deadUnit.data.unitData.unitID="npc";g.world.unit.Npcs.Add(deadUnit);string stageError;
        var hpField=new CharacterField(0,4,1);var attackField=new CharacterField(1,0);
        session.Stage("npc",hpField,CharacterEditData.Read(deadUnit,hpField),"600",out stageError);session.Stage("npc",attackField,CharacterEditData.Read(deadUnit,attackField),"90",out stageError);
        deadUnit.data.dynUnitData.hpMax.AfterWrite=()=>deadUnit.isDie=true;Commit();
        Check(deadUnit.data.dynUnitData.attack.value==77 && g.data.Calls==0 && CharacterSave.Pending>0 && session.Count==0,"destroyed target inside setter stops following writes and avoids rollback on dead object");
        Reset();var replaced=new WorldUnitBase();replaced.data.unitData.unitID="npc";g.world.unit.Npcs.Add(replaced);
        session.Stage("npc",hpField,CharacterEditData.Read(replaced,hpField),"600",out stageError);session.Stage("npc",attackField,CharacterEditData.Read(replaced,attackField),"90",out stageError);
        replaced.data.dynUnitData.hpMax.AfterWrite=()=>{g.world.unit.Npcs.Remove(replaced);var replacement=new WorldUnitBase();replacement.data.unitData.unitID="npc";g.world.unit.Npcs.Add(replacement);};Commit();
        Check(replaced.data.dynUnitData.attack.value==77 && g.data.Calls==0 && CharacterSave.Pending>0,"replacement with same unit ID never receives old writes or disk save");
        Reset();Stage(0,4,"600",1);Stage(1,0,"90");g.world.playerUnit.data.dynUnitData.hpMax.AfterWrite=()=>DataMgr.isSaveData=true;Commit();
        Check(g.world.playerUnit.data.dynUnitData.attack.value==77 && g.data.Calls==0 && CharacterSave.Pending>0,"native autosave starting in setter stops batch and retains uncertain applied state");DataMgr.isSaveData=false;
        Reset();var replacedData=g.data;Stage(0,4,"600",1);Stage(1,0,"90");g.world.playerUnit.data.dynUnitData.hpMax.AfterWrite=()=>g.data=new DataMgr();Commit();
        Check(g.world.playerUnit.data.dynUnitData.attack.value==77 && replacedData.Calls==0 && g.data.Calls==0 && CharacterSave.Pending==0,"save-manager replacement with same player changes context and stops old batch");
        Reset();var missingData=g.data;g.data=null;Stage(0,4,"100");Commit();
        Check(CharacterReader.ContextKey()==null && g.world.playerUnit.data.dynUnitData.hp.value==123 && missingData.Calls==0,"missing save manager invalidates editing context without native writes");g.data=missingData;
        Reset();var realmUnit=g.world.playerUnit;g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,hpUp=100,atkUp=10};Stage(1,11,"104");
        realmUnit.data.unitData.propertyData.AfterGrade=()=>g.world=new World();Commit();
        Check(realmUnit.data.dynUnitData.hp.value==123 && g.data.Calls==0 && session.Context==CharacterReader.ContextKey(),"realm grade event changing world stops nested resource refill and old rollback");
        Reset();var hobbyUnit=g.world.playerUnit;hobbyUnit.allHobby.Add(new WorldUnitHobbyBase{AfterDestroy=()=>g.world=new World()});Stage(0,10,"204",1);Commit();
        Check(hobbyUnit.data.unitData.propertyData.hobby[1]==202 && g.data.Calls==0,"hobby destruction changing world stops nested owning-slot replacement");
        string quickMessage;
        Reset();var quickWorld=g.world;var quickStatus=quickWorld.playerUnit.data.dynUnitData;
        quickStatus.hp.AfterWrite=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterActions.Execute("player","满状态",null,out quickMessage) && quickStatus.mp.value==12 && CharacterSave.Pending==0,"full status world switch stops following writes and never dirties new world");
        Reset();var quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);
        quickNpc.data.dynUnitData.hp.AfterWrite=()=>quickNpc.isDie=true;
        Check(!CharacterActions.Execute("npc","满状态",null,out quickMessage) && quickNpc.data.dynUnitData.mp.value==12 && quickNpc.data.dynUnitData.hp.value==456 && CharacterSave.Pending>0,"full status target death stops writes and avoids undo on dead target while retaining partial state");
        Reset();quickStatus=g.world.playerUnit.data.dynUnitData;quickStatus.hp.AfterWrite=()=>DataMgr.isSaveData=true;
        Check(!CharacterActions.Execute("player","满状态",null,out quickMessage) && quickStatus.mp.value==12 && CharacterSave.Pending>0,"full status autosave event stops following writes and avoids conflicting rollback");DataMgr.isSaveData=false;
        Reset();var quickProperty=g.world.playerUnit.data.unitData.propertyData;
        quickProperty.AfterExp=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterActions.Execute("player","满修为",null,out quickMessage) && CharacterSave.Pending==0,"cultivation event switching world does not report success or dirty replacement context");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);
        quickNpc.data.unitData.propertyData.AfterExp=()=>quickNpc.isDie=true;
        Check(!CharacterActions.Execute("npc","满修为",null,out quickMessage) && CharacterSave.Pending>0,"cultivation invalidated target retains partial same-world state without success");
        Reset();var quickManager=g.data;g.data=null;
        Check(!CharacterActions.Execute("player","满状态",null,out quickMessage) && g.world.playerUnit.data.dynUnitData.hp.value==123,"quick action requires save manager before writing");g.data=quickManager;
        Reset();var quickBag=g.world.playerUnit.data.unitData.propData;
        quickBag.allProps.Add(new DataProps.PropsData{propsCount=3,propsItem=new ConfItemPropsItem{isOverlay=1}});
        quickBag.AfterRefresh=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterActions.Execute("player","满材料",null,out quickMessage) && quickBag.allProps[0].propsCount==9999 && CharacterSave.Pending==0,"materials refresh switching world avoids old rollback and new pending");
        Reset();var replacedBag=g.world.playerUnit.data.unitData.propData;
        replacedBag.allProps.Add(new DataProps.PropsData{propsID=101,propsCount=2,propsItem=new ConfItemPropsItem{isOverlay=1}});
        var freshBag=new DataProps();freshBag.allProps.Add(new DataProps.PropsData{propsID=101,propsCount=77,propsItem=new ConfItemPropsItem{isOverlay=1}});
        replacedBag.AfterRefresh=()=>g.world.playerUnit.data.unitData.propData=freshBag;
        Check(!CharacterActions.Execute("player","满材料",null,out quickMessage) && replacedBag.allProps[0].propsCount==9999 && freshBag.allProps[0].propsCount==77 && replacedBag.RefreshCalls==1 && CharacterSave.Pending>0,"materials bag replacement rejects success, avoids old-bag rollback and preserves partial pending");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);quickNpc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);
        quickNpc.AfterMove=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterActions.Execute("npc","来",null,out quickMessage) && CharacterSave.Pending==0,"move callback switching world never marks replacement save or reports success");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);quickNpc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);
        CharacterActions.Execute("npc","来",null,out quickMessage);g.world.unit.Npcs.Remove(quickNpc);
        var sameIdNpc=new WorldUnitBase();sameIdNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(sameIdNpc);sameIdNpc.data.unitData.Point=new UnityEngine.Vector2Int(7,8);
        Check(!CharacterActions.Execute("npc","返",null,out quickMessage) && sameIdNpc.data.unitData.Point==new UnityEngine.Vector2Int(7,8),"return destination belongs to original NPC pointer, never replacement with same ID");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);
        g.world.playerUnit.data.unitData.relationData.AfterSet=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterActions.Execute("npc","好感度","10",out quickMessage) && quickNpc.data.unitData.relationData.intimToPlayerUnit==0 && CharacterSave.Pending==0,"affection first setter world switch stops reciprocal old-object write and new pending");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);
        g.world.playerUnit.data.unitData.relationData.IgnoreSet=true;
        Check(!CharacterActions.Execute("npc","好感度","10",out quickMessage) && quickNpc.data.unitData.relationData.intimToPlayerUnit==0 && CharacterSave.Pending==0,"ignored affection setter cannot produce reciprocal write or false success");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);quickNpc.data.unitData.relationData.intimToPlayerUnit=float.NaN;
        Check(!CharacterActions.Execute("npc","好感度","10",out quickMessage) && !g.world.playerUnit.data.unitData.relationData.intimToUnit.ContainsKey("npc") && CharacterSave.Pending==0,"nonfinite affection rejects whole operation before changing either side");
        Reset();quickNpc=new WorldUnitBase();quickNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(quickNpc);quickNpc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);
        CharacterActions.Execute("npc","来",null,out quickMessage);g.world.unit.Npcs.Remove(quickNpc);
        sameIdNpc=new WorldUnitBase();sameIdNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(sameIdNpc);sameIdNpc.data.unitData.Point=new UnityEngine.Vector2Int(7,8);
        CharacterActions.Execute("npc","来",null,out quickMessage);
        Check(CharacterActions.Execute("npc","返",null,out quickMessage) && sameIdNpc.data.unitData.Point==new UnityEngine.Vector2Int(7,8),"replacement NPC come starts new return origin instead of inheriting stale one");
        Reset();var itemConf=new ConfItemPropsItem{id=10,isOverlay=1,name="probe"};g.conf.itemProps._allConfList.Add(itemConf);
        var itemContext=CharacterReader.ContextKey();var itemBag=g.world.playerUnit.data.unitData.propData;
        itemBag.AfterAdd=()=>{g.world=new World();g.data=new DataMgr();};
        bool itemRejected=false;try{ItemInventory.Give(itemContext,"player",10,2);}catch{itemRejected=true;}
        Check(itemRejected && itemBag.RefreshCalls==0 && CharacterSave.Pending==0,"item add world switch stops old bag refresh and never dirties new world");
        Reset();g.conf.itemProps._allConfList.Add(itemConf);itemBag=g.world.playerUnit.data.unitData.propData;itemContext=CharacterReader.ContextKey();
        itemBag.allProps.Add(new DataProps.PropsData{propsID=10,propsCount=2,propsItem=itemConf,soleID="probe"});
        var itemSnapshot=ItemInventory.Read("player")[0];itemBag.AfterDelete=()=>{g.world=new World();g.data=new DataMgr();};
        itemRejected=false;try{ItemInventory.Remove(itemContext,"player",itemSnapshot);}catch{itemRejected=true;}
        Check(itemRejected && itemBag.RefreshCalls==0 && CharacterSave.Pending==0,"item delete world switch stops old bag refresh and new pending");
        Reset();g.conf.itemProps._allConfList.Add(itemConf);itemBag=g.world.playerUnit.data.unitData.propData;itemContext=CharacterReader.ContextKey();
        itemBag.allProps.Add(new DataProps.PropsData{propsID=10,propsCount=2,propsItem=itemConf,soleID="probe"});
        itemBag.AfterDelete=()=>{g.world=new World();g.data=new DataMgr();};
        itemRejected=false;try{ItemInventory.Clear(itemContext,"player",ItemInventory.Fingerprint(ItemInventory.Read("player",false)));}catch{itemRejected=true;}
        Check(itemRejected && itemBag.RefreshCalls==0 && CharacterSave.Pending==0,"last clear deletion world switch still stops old refresh and new pending");
        Reset();g.conf.itemProps._allConfList.Add(itemConf);itemBag=g.world.playerUnit.data.unitData.propData;itemContext=CharacterReader.ContextKey();
        itemBag.allProps.Add(new DataProps.PropsData{propsID=10,propsCount=2,propsItem=itemConf,soleID="probe"});itemSnapshot=ItemInventory.Read("player")[0];
        itemBag.AfterRefresh=()=>{g.world=new World();g.data=new DataMgr();};
        itemRejected=false;try{ItemInventory.SetCount(itemContext,"player",itemSnapshot,7);}catch{itemRejected=true;}
        Check(itemRejected && CharacterSave.Pending==0,"count refresh world switch cannot report success or dirty replacement context");
        Reset();g.conf.itemProps._allConfList.Add(itemConf);itemBag=g.world.playerUnit.data.unitData.propData;itemContext=CharacterReader.ContextKey();
        itemBag.AfterAdd=()=>g.world.playerUnit.data.unitData.propData=new DataProps();
        itemRejected=false;try{ItemInventory.Give(itemContext,"player",10,2);}catch{itemRejected=true;}
        Check(itemRejected && itemBag.RefreshCalls==0 && CharacterSave.Pending>0,"bag replacement inside add invalidates captured container before refreshing old reference");
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};var fateUnit=g.world.playerUnit;
        fateUnit.AfterLuck=()=>{g.world=new World();g.data=new DataMgr();};string fateMessage;
        Check(!CharacterFateOperations.Add("player",1,out fateMessage) && fateUnit.allLuck[0].NewCalls==0 && CharacterSave.Pending==0,"born fate initialization world switch stops NewCreateLuck and clears old pending context");
        Reset();g.conf.roleCreateFeature.Items[2]=new FateItem{type=2};fateUnit=g.world.playerUnit;
        fateUnit.AfterAction=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterFateOperations.Add("player",2,out fateMessage) && CharacterSave.Pending==0,"acquired fate action world switch never reports success or keeps old pending in new context");
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};fateUnit=g.world.playerUnit;
        CharacterFateOperations.Add("player",1,out fateMessage);fateUnit.AfterAction=()=>{g.world=new World();g.data=new DataMgr();};
        Check(!CharacterFateOperations.Remove("player",1,out fateMessage) && fateUnit.data.unitData.propertyData.bornLuck.Length==1 && CharacterSave.Pending==0,"fate deletion world switch stops subsequent old born record write");
        // Null return is an interop/hook fault boundary, not the native normal success path.
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};fateUnit=g.world.playerUnit;fateUnit.SuppressLuck=true;
        fateUnit.AfterSuppressedLuck=()=>fateUnit.data.unitData.propertyData.bornLuck=new UnhollowerBaseLib.Il2CppReferenceArray<DataUnit.LuckData>(new[]{new DataUnit.LuckData{id=1},new DataUnit.LuckData{id=99}});
        Check(!CharacterFateOperations.Add("player",1,out fateMessage) && fateUnit.data.unitData.propertyData.bornLuck!=null && fateUnit.data.unitData.propertyData.bornLuck.Any(x=>x.id==99) && CharacterSave.Pending>0,"unconfirmed congenital runtime never restores old snapshot over callback-added born record");
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};fateUnit=g.world.playerUnit;fateUnit.SuppressLuck=true;
        fateUnit.AfterSuppressedLuck=()=>fateUnit.allLuck.Add(new WorldUnitLuckBase{luckData=fateUnit.data.unitData.propertyData.bornLuck[0]});
        Check(!CharacterFateOperations.Add("player",1,out fateMessage) && fateUnit.data.unitData.propertyData.bornLuck!=null && fateUnit.data.unitData.propertyData.bornLuck.Any(x=>x.id==1) && fateUnit.GetLuck(1)!=null && CharacterSave.Pending>0,"unconfirmed return with partial runtime retains owning record instead of orphaning effect");
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};fateUnit=g.world.playerUnit;
        fateUnit.AfterLuck=()=>{throw new InvalidOperationException("init callback failed");};
        Check(!CharacterFateOperations.Add("player",1,out fateMessage) && fateUnit.GetLuck(1)!=null && fateUnit.data.unitData.propertyData.bornLuck!=null && fateUnit.data.unitData.propertyData.bornLuck.Any(x=>x.id==1) && fateMessage.Contains("部分") && CharacterSave.Pending>0,"initialization throw retains uncertain state and explicitly reports partial effect");
        int partialRuntimeCount=fateUnit.allLuck.Count;
        Check(!CharacterFateOperations.Add("player",1,out fateMessage) && fateUnit.allLuck.Count==partialRuntimeCount,"retry after partial initialization rejects duplicate native runtime creation");
        Reset();g.conf.roleCreateFeature.Items[2]=new FateItem{type=2};fateUnit=g.world.playerUnit;fateUnit.SuppressLuck=true;
        Check(!CharacterFateOperations.Add("player",2,out fateMessage) && CharacterSave.Pending>0,"acquired fate owning record without runtime cannot report full success");
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1};fateUnit=g.world.playerUnit;
        CharacterFateOperations.Add("player",1,out fateMessage);
        fateUnit.AfterAction=()=>fateUnit.data.unitData.propertyData.bornLuck=new UnhollowerBaseLib.Il2CppReferenceArray<DataUnit.LuckData>(new[]{new DataUnit.LuckData{id=1},new DataUnit.LuckData{id=99}});
        Check(CharacterFateOperations.Remove("player",1,out fateMessage) && fateUnit.data.unitData.propertyData.bornLuck.Length==1 && fateUnit.data.unitData.propertyData.bornLuck[0].id==99,"fate removal preserves other born records created inside native deletion callback");
        Reset();var supplyNpc=new WorldUnitBase();supplyNpc.data.unitData.unitID="npc";g.world.unit.Npcs.Add(supplyNpc);itemContext=CharacterReader.ContextKey();
        var hpConf=new ConfItemPropsItem{id=20,isOverlay=1,name="hp"};var mpConf=new ConfItemPropsItem{id=21,isOverlay=1,name="mp"};g.conf.itemProps._allConfList.Add(hpConf);g.conf.itemProps._allConfList.Add(mpConf);
        var hpPill=new ConfItemPillItem{id=20};hpPill.Effects["hp"]=10;var mpPill=new ConfItemPillItem{id=21};mpPill.Effects["mp"]=10;g.conf.itemPill._allConfList.Add(hpPill);g.conf.itemPill._allConfList.Add(mpPill);
        var replacementNpc=new WorldUnitBase();replacementNpc.data.unitData.unitID="npc";itemBag=supplyNpc.data.unitData.propData;
        itemBag.AfterAdd=()=>{g.world.unit.Npcs.Remove(supplyNpc);g.world.unit.Npcs.Add(replacementNpc);};
        itemRejected=false;try{ItemInventory.Supply(itemContext,"npc");}catch{itemRejected=true;}
        Check(itemRejected && replacementNpc.data.unitData.propData.AddCalls==0 && itemBag.RefreshCalls==0 && CharacterSave.Pending>0,"supply NPC replacement cannot route second medicine into replacement bag");
        bool concurrentSafe=true;
        for(int cycle=0;cycle<100;cycle++)
        {
            Reset();Stage(0,4,"100");g.data.ThrowSave=true;Commit();var abandoned=g.data.Callback;
            g.data.ThrowSave=false;Commit();var active=g.data.Callback;
            var workerA=new System.Threading.Thread(()=>{for(int i=0;i<200;i++)abandoned.Invoke(true);});
            var workerB=new System.Threading.Thread(()=>{for(int i=0;i<200;i++)active.Invoke(true);});
            workerA.Start();workerB.Start();workerA.Join();workerB.Join();CharacterSave.Poll();
            if(CharacterSave.Busy || CharacterSave.Pending!=0 || message!="修改已保存")concurrentSafe=false;
        }
        Check(concurrentSafe,"100 save cycles with 40000 interleaved stale and active callbacks preserve final state");
        Reset();Stage(0,4,"100");Stage(0,4,"600",1);Commit();
        Check(g.world.playerUnit.data.dynUnitData.hp.value==100&&g.world.playerUnit.data.dynUnitData.hpMax.value==600&&g.data.Calls==1&&CharacterSave.Busy&&CharacterSave.Pending>0,"resource halves write independent values and native save remains pending");
        g.data.Complete(false);Check(CharacterSave.Busy,"callback does not touch UI before main-thread poll");CharacterSave.Poll();
        Check(!CharacterSave.Busy&&CharacterSave.Pending>0&&message.Contains("失败"),"failed disk save preserves dirty memory for retry");
        Commit();Check(g.data.Calls==2&&g.world.playerUnit.data.dynUnitData.hp.value==100,"retry saves current memory without replaying drafts");g.data.Complete(true);CharacterSave.Poll();
        Check(CharacterSave.Pending==0&&message=="修改已保存","only successful callback clears dirty marker");
        Reset();Stage(0,4,"100");Stage(1,0,"90");g.world.playerUnit.data.dynUnitData.attack.allMulCall.Add(new object());Commit();
        Check(g.data.Calls==0&&g.world.playerUnit.data.dynUnitData.hp.value==123&&session.Count==2,"unsupported modifier rejects entire batch during preflight");
        Reset();Stage(0,4,"100");g.world.playerUnit.data.dynUnitData.hp.Stored=122;Commit();
        Check(g.data.Calls==0&&g.world.playerUnit.data.dynUnitData.hp.value==122,"external value change cannot overwrite newer game data");
        Reset();Stage(0,4,"100");Stage(1,0,"90");g.world.playerUnit.data.dynUnitData.attack.RejectWrite=true;Commit();
        Check(g.data.Calls==0&&g.world.playerUnit.data.dynUnitData.hp.value==123&&g.world.playerUnit.data.dynUnitData.attack.value==77&&session.Count==2,"setter exception restores preceding and failing numeric steps and does not save");
        Reset();g.world.playerUnit.data.dynUnitData.attack.Bonus=20;Stage(1,0,"150");Stage(0,0,"20");Commit();
        Check(g.world.playerUnit.data.dynUnitData.attack.baseValue==130&&g.world.playerUnit.data.dynUnitData.attack.value==150&&g.world.playerUnit.data.dynUnitData.age.value==240,"additive bonuses and year-to-month conversion are preserved");g.data.Complete(true);CharacterSave.Poll();
        Reset();Stage(0,11,"敖瑶新名");Stage(0,10,"204",1);Stage(1,10,"4");Commit();
        Check(g.world.playerUnit.data.unitData.propertyData.GetName()=="敖瑶新名"&&g.world.playerUnit.data.unitData.propertyData.name.Length==2&&g.world.playerUnit.data.unitData.propertyData.hobby[0]==201&&g.world.playerUnit.data.unitData.propertyData.hobby[1]==204&&g.world.playerUnit.data.unitData.propertyData.inTrait==4,"name structure, independent hobby slot and trait config remain valid");g.data.Complete(true);CharacterSave.Poll();
        Reset();Stage(0,10,"201",1);Commit();
        Check(g.data.Calls==0&&g.world.playerUnit.data.unitData.propertyData.hobby[1]==202,"duplicate hobbies fail before runtime rebuilding");
        Reset();Stage(0,4,"100");Commit();g.world=new World();g.data.Complete(true);CharacterSave.Poll();
        Check(!CharacterSave.Busy&&message.Contains("已切换"),"late callback cannot claim success for a different world");
        Reset();g.conf.roleGrade.Overrides[103]=new GradeItem{id=103,grade=2,phase=1};g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=2,phase=2,hpUp=100,atkUp=10};Stage(1,11,"104");Commit();
        Check(g.world.playerUnit.data.dynUnitData.gradeID.value==104&&g.world.playerUnit.ActionCalls==0&&g.world.playerUnit.data.dynUnitData.hpMax.value==556&&g.world.playerUnit.data.dynUnitData.attack.value==87,"realm edit applies configured growth without material actions or story rewards");g.data.Complete(true);CharacterSave.Poll();
        Stage(1,11,"103");Commit();
        Check(g.world.playerUnit.data.dynUnitData.hpMax.value==456&&g.world.playerUnit.data.dynUnitData.attack.value==77,"realm downgrade reverses growth delta without accumulating gains");g.data.Complete(true);CharacterSave.Poll();
        Reset();g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,quality=5,qualityName="天道品质",growItemNum=3,atkUp=20};Stage(1,11,"104");Commit();
        Check(g.world.playerUnit.ActionCalls==0&&g.data.Calls==1&&g.world.playerUnit.data.dynUnitData.gradeID.value==104&&g.world.playerUnit.data.dynUnitData.attack.value==97&&g.data.world.playerLog.upGrade[3].quality==5,"free realm jump updates growth and quality history without consuming materials");g.data.Complete(true);CharacterSave.Poll();
        Check(CharacterEditData.Label(new CharacterField(1,11),"104").Contains("天道品质"),"realm quality is visible in independently identified config choice");
        Reset();g.conf.roleGrade.Overrides[103]=new GradeItem{id=103,grade=2,phase=3};g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=100};
        g.world.playerUnit.data.dynUnitData.attack.Stored=50;Stage(1,11,"104");Commit();g.data.Complete(true);CharacterSave.Poll();
        g.world.playerUnit.data.dynUnitData.attack.Stored=20; // 模拟其他修改器在高境界重设低基础值。
        Stage(1,11,"103");Commit();g.data.Complete(true);CharacterSave.Poll();
        Check(g.world.playerUnit.data.dynUnitData.attack.value==0&&g.world.playerUnit.data.unitData.objData.ContainsKey("BHML.realm.debt:attack/0"),"downgrade records truncated growth rather than writing negative stats");
        Stage(1,11,"104");Commit();g.data.Complete(true);CharacterSave.Poll();
        Check(g.world.playerUnit.data.dynUnitData.attack.value==20&&!g.world.playerUnit.data.unitData.objData.ContainsKey("BHML.realm.debt:attack/0"),"upgrade after a clamped downgrade restores exact prior base without growth duplication");
        Reset();g.conf.roleGrade.Overrides[103]=new GradeItem{id=103,grade=2,phase=3};g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=100};
        g.world.playerUnit.data.dynUnitData.attack.Stored=0;g.world.playerUnit.data.unitData.objData.SetString("BHML.realm.debt:attack/0","-80");Stage(1,11,"104");
        var originalCulture=System.Threading.Thread.CurrentThread.CurrentCulture;var alteredCulture=(System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();alteredCulture.NumberFormat.NegativeSign="~";
        try {System.Threading.Thread.CurrentThread.CurrentCulture=alteredCulture;Commit();}
        finally {System.Threading.Thread.CurrentThread.CurrentCulture=originalCulture;}
        Check(g.data.Calls==1 && g.world.playerUnit.data.dynUnitData.attack.value==20 && !g.world.playerUnit.data.unitData.objData.ContainsKey("BHML.realm.debt:attack/0"),"persisted invariant debt parses independently of current culture negative sign");g.data.Complete(true);CharacterSave.Poll();
        foreach(var invalidDebt in new string[]{"5","not-a-number",null})
        {
            Reset();g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};g.world.playerUnit.data.unitData.objData.SetString("BHML.realm.debt:attack/0",invalidDebt);Stage(1,11,"104");Commit();
            Check(g.data.Calls==0 && g.world.playerUnit.data.dynUnitData.gradeID.value==103 && g.world.playerUnit.data.dynUnitData.attack.value==77 && g.world.playerUnit.data.unitData.objData.GetString("BHML.realm.debt:attack/0")==invalidDebt,"invalid positive or malformed debt rejects realm before mutation: "+(invalidDebt??"<null>"));
        }
        Reset();g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};g.world.playerUnit.data.dynUnitData.attack.Stored=0;g.world.playerUnit.data.dynUnitData.attack.minValue=1;
        var minimumDebt=long.MinValue.ToString(System.Globalization.CultureInfo.InvariantCulture);g.world.playerUnit.data.unitData.objData.SetString("BHML.realm.debt:attack/0",minimumDebt);Stage(1,11,"104");Commit();
        Check(g.data.Calls==0 && g.world.playerUnit.data.dynUnitData.attack.Stored==0 && g.world.playerUnit.data.unitData.objData.GetString("BHML.realm.debt:attack/0")==minimumDebt,"debt subtraction overflow is rejected before a wrapped positive debt can be persisted");
        Reset();g.conf.roleGrade.Overrides[103]=new GradeItem{id=103,grade=2,phase=3};g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,atkUp=10};Stage(1,11,"104");g.world.playerUnit.data.dynUnitData.attack.RejectWrite=true;Commit();
        Check(g.data.Calls==0&&g.world.playerUnit.data.dynUnitData.gradeID.value==103&&g.world.playerUnit.data.dynUnitData.attack.value==77,"realm setter failure restores growth, identity and prevents disk save");
        Reset();g.conf.roleGrade._allConfList.Add(new GradeItem{lockFeature=120});g.conf.roleCreateFeature.Items[120]=new FateItem{type=2};
        string lockResult;CharacterFateOperations.Add("player",120,out lockResult);
        g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};Stage(1,11,"104");Commit();
        Check(g.world.playerUnit.GetLuck(120)==null&&!g.world.playerUnit.data.unitData.propertyData.addLuck.Any(l=>l.id==120),"direct realm change unregisters old experience bottleneck effects and records");g.data.Complete(true);CharacterSave.Poll();
        Reset();g.conf.roleCreateFeature.Items[1]=new FateItem{type=1,name="born"};g.conf.roleCreateFeature.Items[2]=new FateItem{type=2,name="after"};g.conf.roleCreateFeature.Items[3]=new FateItem{type=3,name="fate"};g.conf.roleCreateFeature.Items[4]=new FateItem{type=2,name="hidden",conceal=1};
        string result;
        Check(CharacterFateCatalog.Read().Count==3&&CharacterFateCatalog.Prefix(0)=="[先] "&&CharacterFateCatalog.Prefix(1)=="[后] "&&CharacterFateCatalog.Prefix(2)=="[逆] ","catalog excludes concealed runtime effects and labels all three visible categories");
        Check(CharacterFateOperations.Add("player",1,out result)&&g.world.playerUnit.data.unitData.propertyData.bornLuck.Length==1&&g.world.playerUnit.data.unitData.propertyData.addLuck.Count==0,"congenital add updates born record and native runtime without moving it to acquired");
        Check(!CharacterFateOperations.Add("player",1,out result)&&g.world.playerUnit.data.unitData.propertyData.bornLuck.Length==1,"duplicate congenital add is rejected");
        Check(CharacterFateOperations.Add("player",2,out result)&&CharacterFateOperations.Add("player",3,out result)&&g.world.playerUnit.ActionCalls==2,"acquired and destiny additions use native action entry points");
        Check(!CharacterFateOperations.Add("player",4,out result)&&g.world.playerUnit.ActionCalls==2,"concealed effect cannot be manually added");
        Check(CharacterFateOperations.Remove("player",1,out result)&&g.world.playerUnit.data.unitData.propertyData.bornLuck.Length==0&&g.world.playerUnit.GetLuck(1)==null,"congenital delete clears both owning record and runtime");
        Check(CharacterFateOperations.Remove("player",3,out result)&&!g.world.playerUnit.data.unitData.propertyData.addLuck.Any(l=>l.id==3),"destiny delete delegates lifecycle cleanup to game action");
        g.conf.roleCreateFeature.Items[2].group=10;g.conf.roleCreateFeature.Items[5]=new FateItem{type=2,group=10};
        Check(CharacterFateOperations.Add("player",5,out result)&&g.world.playerUnit.GetLuck(2)!=null,"modifier allows deliberate same-group addition");
        g.conf.roleCreateFeature.Items[6]=new FateItem{type=1,lockLuck=8,globalMax=1};
        Check(CharacterFateOperations.Add("player",6,out result),"achievement and global generation gates do not block modifier addition");
        g.conf.roleCreateFeature.Items[2].lockLuck=1;
        Check(CharacterFateOperations.Remove("player",2,out result)&&g.world.playerUnit.GetLuck(2)==null,"creation achievement gate does not prevent native removal of an already owned effect");
        Reset();g.world.playerUnit.data.unitData.propertyData.exp=20;
        Check(CharacterActions.Execute("player","满修为",null,out result)&&g.world.playerUnit.data.unitData.propertyData.exp==100&&g.world.playerUnit.data.dynUnitData.gradeID.value==103,"full cultivation calls experience entry and never upgrades realm");
        var bag=g.world.playerUnit.data.unitData.propData;
        bag.allProps.Add(new DataProps.PropsData{propsCount=3,propsItem=new ConfItemPropsItem{isOverlay=1}});
        bag.allProps.Add(new DataProps.PropsData{propsCount=1,propsItem=new ConfItemPropsItem{isOverlay=0}});
        bag.allProps.Add(new DataProps.PropsData{propsCount=20000,propsItem=new ConfItemPropsItem{isOverlay=1}});
        Check(CharacterActions.Execute("player","满材料",null,out result)&&bag.allProps[0].propsCount==9999&&bag.allProps[1].propsCount==1&&bag.allProps[2].propsCount==20000,"full bag tops up stackable counts, preserving unique and larger existing stacks");
        var materialSaved=bag.allProps.ToArray();bag.allProps.Clear();
        bag.allProps.Add(new DataProps.PropsData{propsID=11,propsCount=int.MaxValue-1,propsItem=new ConfItemPropsItem{isOverlay=1}});
        bag.allProps.Add(new DataProps.PropsData{propsID=11,propsCount=1,propsItem=new ConfItemPropsItem{isOverlay=1}});
        bag.allProps.Add(new DataProps.PropsData{propsID=12,propsCount=2,propsItem=new ConfItemPropsItem{isOverlay=1}});
        var materialPending=CharacterSave.Pending;
        Check(!CharacterActions.Execute("player","满材料",null,out result) && bag.allProps[0].propsCount==int.MaxValue-1 && bag.allProps[1].propsCount==1 && bag.allProps[2].propsCount==2 && CharacterSave.Pending==materialPending,"full materials rejects aggregate overflow before changing any stack or dirty marker");
        bag.allProps[0].propsCount=int.MaxValue-9999;
        Check(CharacterActions.Execute("player","满材料",null,out result) && bag.allProps[0].propsCount==int.MaxValue-9999 && bag.allProps[1].propsCount==9999 && bag.allProps[2].propsCount==9999,"full materials accepts exact aggregate integer boundary and preserves larger quantity");
        bag.allProps.Clear();bag.allProps.AddRange(materialSaved);
        var npc=new WorldUnitBase();npc.data.unitData.unitID="npc";npc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);g.world.unit.Npcs.Add(npc);
        SceneType.map=new SceneMap();
        g.world.playerUnit.data.unitData.Point=new UnityEngine.Vector2Int(1,2);
        Check(CharacterActions.Execute("npc","来",null,out result)&&npc.data.unitData.Point==new UnityEngine.Vector2Int(1,2),"come moves selected NPC via native position action");
        CharacterActions.Execute("npc","来",null,out result);
        Check(CharacterActions.Execute("npc","返",null,out result)&&npc.data.unitData.Point==new UnityEngine.Vector2Int(20,30),"repeated come preserves original return destination");
        Check(CharacterActions.Execute("npc","去",null,out result)&&g.world.playerUnit.data.unitData.Point==npc.data.unitData.Point,"go moves player to selected NPC");
        Check(SceneType.map.world.Refreshes==4 && SceneType.map.world.Commands==4 && g.ui.Map.uiGridUnit.Refreshes==4,"each successful teleport refreshes native map grids and selected tile NPC count");
        g.world.playerUnit.data.unitData.relationData.SetIntim("npc",4.5f);npc.data.unitData.relationData.intimToPlayerUnit=7.5f;
        Check(CharacterActions.Execute("npc","好感度","10",out result)&&g.world.playerUnit.data.unitData.relationData.intimToUnit["npc"]==14.5f&&npc.data.unitData.relationData.intimToPlayerUnit==17.5f,"bidirectional affection adds separately and preserves fractional originals");
        Check(!CharacterActions.Execute("player","来",null,out result)&&!CharacterActions.Execute("npc","好感度","-2",out result),"self teleport and invalid affection increments reject before mutation");
        var beforeDirty=CharacterSave.Pending; npc.RejectAction=true;
        Check(!CharacterActions.Execute("npc","来",null,out result)&&CharacterSave.Pending==beforeDirty,"rejected movement does not create a false pending save count");
        npc.RejectAction=false;g.world.playerUnit.data.unitData.Point=new UnityEngine.Vector2Int(1,2);npc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);npc.ThrowMoveAfterWrite=true;
        beforeDirty=CharacterSave.Pending;
        Check(!CharacterActions.Execute("npc","来",null,out result) && npc.data.unitData.Point==new UnityEngine.Vector2Int(1,2) && CharacterSave.Pending>beforeDirty,"move callback failure preserves actual movement and pending");npc.ThrowMoveAfterWrite=false;
        Check(CharacterActions.Execute("npc","返",null,out result) && npc.data.unitData.Point==new UnityEngine.Vector2Int(20,30),"return origin survives partial come movement failure");
        foreach(bool callbackThrows in new[]{false,true})
        {
            Reset();var savingNpc=new WorldUnitBase();savingNpc.data.unitData.unitID="npc";savingNpc.data.unitData.Point=new UnityEngine.Vector2Int(20,30);g.world.unit.Npcs.Add(savingNpc);
            g.world.playerUnit.data.unitData.Point=new UnityEngine.Vector2Int(1,2);savingNpc.ThrowMoveAfterWrite=callbackThrows;
            savingNpc.AfterMove=()=>DataMgr.isSaveData=true;
            Check(!CharacterActions.Execute("npc","来",null,out result) && savingNpc.data.unitData.Point==new UnityEngine.Vector2Int(1,2) && CharacterSave.Pending>0,"autosave after come stops success and retains partial pending; callback throws="+callbackThrows);
            DataMgr.isSaveData=false;savingNpc.AfterMove=null;savingNpc.ThrowMoveAfterWrite=false;
            Check(CharacterActions.Execute("npc","返",null,out result) && savingNpc.data.unitData.Point==new UnityEngine.Vector2Int(20,30),"autosave after moved come preserves original return point; callback throws="+callbackThrows);
        }
        Reset();var status=g.world.playerUnit.data.dynUnitData;
        status.hp.Bonus=10;status.mp.Bonus=3;status.moodMax.Stored=100;status.healthMax.Stored=100;status.energyMax.Stored=100;
        Check(CharacterActions.Execute("player","满状态",null,out result) && status.hp.value==status.hpMax.value && status.mp.value==status.mpMax.value && status.sp.value==status.spMax.value && status.mood.value==100 && status.health.value==100 && status.energy.value==100,"restore fills all six resources while preserving additive effects");
        beforeDirty=CharacterSave.Pending;
        Check(CharacterActions.Execute("player","满状态",null,out result) && CharacterSave.Pending==beforeDirty,"repeating full status never accumulates values or false pending changes");
        Reset();status=g.world.playerUnit.data.dynUnitData;status.energy.lockValue=true;
        Check(!CharacterActions.Execute("player","满状态",null,out result) && status.hp.value==123 && CharacterSave.Pending==0,"locked resource fails whole status preflight before any mutation");
        Reset();status=g.world.playerUnit.data.dynUnitData;status.mood.RejectWrite=true;
        Check(!CharacterActions.Execute("player","满状态",null,out result) && status.hp.value==123 && status.mp.value==12 && status.mood.value==0 && CharacterSave.Pending==0,"status setter failure restores preceding and failing resources");
        Reset();status=g.world.playerUnit.data.dynUnitData;status.hp.Bonus=10;g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};Stage(1,11,"104");Commit();
        Check(status.hp.value==status.hpMax.value,"realm resource refill preserves additive bonus rather than assigning computed maximum as base");g.data.Complete(true);CharacterSave.Poll();
        Reset();status=g.world.playerUnit.data.dynUnitData;status.hp.Stored=int.MaxValue-10;status.hp.Bonus=10;status.hpMax.Stored=int.MaxValue;
        Check(CharacterActions.Execute("player","满状态",null,out result) && status.hp.Stored==int.MaxValue-10 && status.hp.value==int.MaxValue,"full status accepts representable result despite intermediate integer sum overflow");
        Reset();status=g.world.playerUnit.data.dynUnitData;status.hp.Stored=100;status.hp.Bonus=-10;status.hpMax.Stored=int.MaxValue;
        Check(!CharacterActions.Execute("player","满状态",null,out result) && status.hp.Stored==100 && status.mp.Stored==12 && CharacterSave.Pending==0,"full status rejects genuinely unrepresentable compensated base before writes");
        Reset();status=g.world.playerUnit.data.dynUnitData;status.hp.Stored=int.MaxValue-10;status.hp.Bonus=10;status.hpMax.Stored=int.MaxValue;
        g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};Stage(1,11,"104");Commit();
        Check(g.data.Calls==1 && status.gradeID.value==104 && status.hp.Stored==int.MaxValue-10 && status.hp.value==int.MaxValue,"realm refill accepts representable compensated resource without intermediate overflow");g.data.Complete(true);CharacterSave.Poll();
        Reset();status=g.world.playerUnit.data.dynUnitData;status.hp.Stored=100;status.hp.Bonus=-10;status.hpMax.Stored=int.MaxValue;
        g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1};Stage(1,11,"104");Commit();
        Check(g.data.Calls==0 && status.gradeID.value==103 && status.hp.Stored==100,"realm genuinely unrepresentable refill restores identity and resource and prevents disk save");
        Reset();Stage(0,4,"100");CharacterSave.Commit(session,(ok,m)=>{throw new Exception("closed UI feedback");});
        Check(CharacterSave.Busy && g.data.Calls==1 && CharacterSave.Pending>0,"throwing start feedback cannot reset in-flight save state");
        g.data.Complete(true);CharacterSave.Poll();
        Check(!CharacterSave.Busy && CharacterSave.Pending==0,"throwing completion feedback cannot discard successful save result");
        Reset();Stage(0,4,"100");CharacterSave.Commit(session,null);g.data.Complete(false);CharacterSave.Poll();
        Check(!CharacterSave.Busy && CharacterSave.Pending>0,"missing feedback recipient still preserves failure for retry");
        Reset();Stage(0,4,"100");DataMgr.isSaveData=true;Commit();
        Check(g.data.Calls==0 && g.world.playerUnit.data.dynUnitData.hp.value==123,"native save in progress blocks writes before applying drafts");DataMgr.isSaveData=false;
        Console.WriteLine(passed+" save/catalog/native-adapter checks passed; substitutes are not in-game persistence proof");
    }
}
