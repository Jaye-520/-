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
        Reset();g.conf.roleGrade.Overrides[103]=new GradeItem{id=103,grade=2,phase=1};g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=2,phase=2};Stage(1,11,"104");Commit();
        Check(g.world.playerUnit.data.dynUnitData.gradeID.value==104&&g.world.playerUnit.ActionCalls==1&&g.world.playerUnit.data.dynUnitData.hpMax.value==556&&g.world.playerUnit.data.dynUnitData.attack.value==87,"supported adjacent realm delegates both identity and growth to native upgrade action");g.data.Complete(true);CharacterSave.Poll();
        Reset();g.conf.roleGrade.Overrides[104]=new GradeItem{id=104,grade=3,phase=1,quality=5,qualityName="天道品质",growItemNum=3};Stage(1,11,"104");Commit();
        Check(g.world.playerUnit.ActionCalls==0&&g.data.Calls==0&&g.world.playerUnit.data.dynUnitData.gradeID.value==103&&message.Contains("材料"),"player major breakthrough requiring materials is rejected without raw grade overwrite");
        Check(CharacterEditData.Label(new CharacterField(1,11),"104").Contains("天道品质"),"realm quality is visible in independently identified config choice");
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
        Check(!CharacterFateOperations.Add("player",5,out result)&&g.world.playerUnit.GetLuck(2)!=null,"same-group add does not silently replace an existing effect");
        g.conf.roleCreateFeature.Items[2].lockLuck=1;
        Check(CharacterFateOperations.Remove("player",2,out result)&&g.world.playerUnit.GetLuck(2)==null,"creation achievement gate does not prevent native removal of an already owned effect");
        Console.WriteLine(passed+" save/catalog/native-adapter checks passed; substitutes are not in-game persistence proof");
    }
}
