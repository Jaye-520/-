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
        g.world=new World();g.conf=new Config();g.data=new DataMgr();DataMgr.isSaveData=false;
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
    {
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
