# 变更记录

本文件保存影响当前开发的版本结论。当前状态以[项目记忆](../PROJECT_MEMORY.md)为准；历史失败不代表当前仍未修复。

## 2026-10-02：建立本地Git

按用户指令在ModProject_YtxSQ1建立main分支的本地仓库，无远程。首个提交保存07.15源码、构建入口、工程配置、Unity源资源与.meta、文档/实测截图/检查脚本；项目内AGENTS.md保留会话指令。扩展.gitignore排除游戏引用、生成产物、缓存/备份、日志及模拟图，保留官方ModProject.cache/ModData.cache。新增.gitattributes统一提交换行。

当前环境无已配置Git作者，本仓库设置Codex <codex@localhost>，未改全局身份。初始化不重新构建、不改DLL、存档或游戏进程；07.15的真实DLC显示仍待用户验收，Git基线不代表已验收。详细提交记录由git log查询，避免在记忆内重复维护提交哈希。

## 07.15：原生特殊立绘入口与白块清理（待游戏验证）

2026-10-02。[07.14用户失败截图](screenshots/five-flowers-07-14-failure.png)显示敖瑶“立绘暂不可用”及白色底块。当前Player.log第1381/1385/1389行记录NullReferenceException，但旧日志无调用阶段，不能认定具体抛错位置。

只读用游戏自带Cpp2IL解析本机GameAssembly/metadata，并通过Capstone查看相关原生方法：UINPCInfoUnitInfo.UpdateUI调用PortraitModel.CreateTexture(WorldUnitBase...)；其特殊分支调用ConfDramaNpc.CreateTexture。后者判断avatar/modelID，分别创建拼装模型、独立资源/预制体，并自行创建渲染器。GetPortraitModelData只走配置模型路径，不适用于所有独立资源。原生ModelRenderTexture帧回调在autoDestroyGameObject销毁后调用Destroy、释放RenderTexture并停止Timer。

CharacterPortrait改为调用ConfDramaNpc.CreateTexture，BookView提供每次可销毁的专属RawImage，不再额外构造特殊人物渲染器；保留原生禁用RawImage并显示子预制体的行为。普通修士路径与相机不变。无纹理/失败时隐藏图形，清理承载对象，异常日志增加阶段与剧情ID。

编译零警告零错误；布局替身验证五种身份、独立头像不提供拼装数据、预制体分支、缓存变化、失败白块清理、普通人物恢复与切页/关闭释放通过；角色读取9项通过。原生释放机制有静态调用依据，替身仅模拟行为，实际延迟释放、人物大小/动画和真实DLC资源仍待游戏确认。Release/debug同步，DLL SHA256：D77E84E066D90C204561D54CA4BB5E11346FC00036264A375B37D8523DC68336。

07.14源码/DLL/检查备份在.tools/backups/2026-10-02-07.14-portrait；本机方法地址、反汇编与失败日志摘录在.tools/portrait-native-metadata，分析脚本为.tools/inspect_portrait_native.py。未停止游戏或写存档；07.15尚无运行验收证据。

## 07.14：五朵金花特殊立绘解析（游戏实测失败，已由07.15替代）

2026-10-02。用户提供[原游戏敖瑶](screenshots/five-flowers-native-reference.png)与[修改器错误立绘](screenshots/five-flowers-trainer-before-fix.png)，要求修复整类人物。原代码仅从WorldUnitBase创建静态普通拼装模型，未读取五朵金花剧情配置。

本机Mono.Cecil元数据确认ConfMgr.dramaNpc、ConfDramaNpc.GetFiveFlowerDramaNpcID(WorldUnitBase)、GetPortraitModelData(int)和CreateModel(PortraitModelData,bool,bool,Action)签名；官方CHM确认剧情肖像入口及动态/静态模型接口。缓存文档的CreateTexture旧签名与本机不同，未直接套用。

新增Services/CharacterPortrait.cs并登记csproj，按原生身份取得当前剧情模型数据，创建独立同步模型、允许动态立绘；没有姓名/固定人物ID清单，也不改存档模型数据。BookView复用原渲染/取景与清理，缓存加入剧情ID以处理同人物身份变化。特殊配置缺失显示“立绘暂不可用”，不冒用普通形象；普通修士保持原静态路径。

build.ps1零警告零错误，Release/debug DLL同步，SHA256：0091B755D4F49D9B7FCD8CAFADC95FDB3E8D64DA6F7B7B8317444CDB040870A1。扩展现有布局替身检查：五种合成身份、同人物剧情ID变化、配置缺失、恢复普通模型、替换/关闭释放，以及四种画布几何、搜索和原取景均通过；角色读取9项通过。首次检查因新增5个合成人物后旧名单计数断言仍为302而失败，修正期望307后通过。合成配置不验证原生DLC映射、动态动画边界或真实资源贴图。

备份：.tools/backups/2026-10-02-07.13-portrait，含原源码/工程配置、DLL及受影响检查脚本。无存档写入、游戏终止。后续用户07.14截图确认加载但敖瑶立绘不可用、白块；日志为空引用。此前通过的合成配置检查没有覆盖独立资源路径，不能作为修复成功依据，详见07.15记录。

## 2026-10-02：文档精简

将34份Markdown精简为6份：项目记忆、文档入口、长期计划、工程手册、功能调查和本记录。合并工程导航、流程、验证与复盘，删除独立模板和逐版说明；原始34份文档完整保存到[历史归档](archive/documentation-before-consolidation-2026-10-02.zip)，归档前逐文件SHA256核对一致。

归档中的链接按原工程相对位置保留；需要完整历史时解压到独立目录后查阅，不覆盖当前工程。归档只用于追溯，里面的“当前/最新/待验证”是当时状态。未来不每次打包归档或新增版本文件，仅必要时归档大型历史。

本次只改文档，不改源码、DLL、存档或游戏进程。最初入口的重复状态和旧猜测不再进入日常阅读。

## 07.13：固定居中页脚（已验收）

BookView移除随页面与读取状态变化的status字段和写入，页脚两侧对称留白214单位、MiddleCenter对齐，固定显示“Jaye@2026-爱你一生一世”。右侧保留版本/F8提示；输入、立绘和读取实现不改。

编译零警告零错误；Release/debug DLL同步，SHA256为BA0D7B59FDF13F2E522FBE593B892A94D376CCD7501FDC1BA61AD3694E0B8700。用户交付后反馈“完美”，本轮需求关闭。无修改后截图，不扩大为逐页/多分辨率/失败路径全量实测。

## 07.12：光标整体平移（已验收）

WindowInputScope在GenerateCaret后整体平移四个顶点到文字中心，不缩短光标、不改变X/宽度/闪烁；保留07.10焦点保护。编译通过，17项托管焦点/几何检查通过，Release/debug同步。用户反馈“搞定了”，仅本轮输入与光标问题确认；没有成功截图。

## 07.10与此前输入试验

07.10观察到本搜索框被转移至停用的原生G:btnOK，增加仅本框编辑期间的无效目标保护；真实保护分支命中。用户[截图](screenshots/role-07-10-input-confirmed.png)确认输入“乐”得到4/203。

07.11文字配置调整未修复光标，见[失败截图](screenshots/role-07-11-caret-offset.png)，随后07.12解决。此前手工控件、TMP、弹窗输入、只防null、UpdateHandleInput隔离及停用导航等均未解决原问题或偏离交互目标；这些失败保留在归档，不作为现方案。完整原生发起栈未取得。

## 07.0–07.3：角色布局、名单与立绘

宽版属性靠下、窄版滚动；查询改为包含隐藏单位并过滤死亡/无效、去重。暴击缩放错误由真实getter与游戏200%对照纠正，改为整数加百分号。立绘后续取景与大小获认可。曾采用经验放大等中间方案，不应恢复；名单人数不是固定世界标准。

## 06.0–06.1：角色只读属性

06.0六项有静态游戏对照；06.1扩展为五组41项。当前映射见CharacterReader源码；最初百分比换算猜测已被后续实测推翻。编译和合成数据检查不能证明全部字段/单位正确，完整原UI对照仍待后续回归。没有存档写入。

## 01–05.3：环境与美术

建立net472/Roslyn构建、原生UITextInfoLong承载和F8入口；逐步形成山水宣纸、墨刷、字体和八页签，05.3美术获用户认可。早期基础开关有用户反馈，但不代表完整生命周期/多分辨率验收；Unity自定义UI迁移仍未完成。原教程研究、旧构建哈希、详细试验和当时备份路径按需查归档。
