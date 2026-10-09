# 功能页实现与核查记录

> **对应版本**：08.17 ｜ **最后更新**：2026-10-09 ｜ **证据边界**：用户08.16加载/创建故障日志、原生静态核查、08.17源码与托管替身；08.17游戏恢复未验收

## 本轮反馈修正（08.17，游戏恢复待验证）

- 日志确认08.16加载。前一次读档在BattleSkillValueData构造空引用，随后IntoWorld二次失败；最新新建角色在UICreatePlayerProperty.Init空引用。MultiThreadTask threadCount=16只是原生初始化日志。没有崩溃转储或无补丁对照，不据此宣布唯一根因或存档损坏。
- 静态调用UICreatePlayer.InitData→WorldUnitData.Init→WorldUnitDynData构造，创建界面随即读取动态字段；全局构造补丁是首要嫌疑。彻底移除CapacityRuntime的Harmony/ConstructorInfo挂载，Initialize仅设托管标志；创建/载入及玩家数据不全时不枚举、不加运行效果，原生构造保持原路径。
- 完整世界进入后主线程一次恢复现存单位；后续每秒仅核对已扩容人物字段是否重建，按当前有效对象恢复，不在构造/加载工作线程访问游戏对象。绑定表仍只释放自己的加法效果，持久扩容记录和08.15地图/关系修正保留。
- 只读证据.tools/fix0817-user-player.log（SHA256 `2BBF93180C35CC02880700620AC9DC19E8A964192E0DC93B2381EE496F7248B2`）、fix0817-user-player-prev.log（`FDEB64168E4A3FCC4D189D17A3FB5CA52B8ACAA769F3E01FE56C2E12D91C33E4`）；静态依据fix0817-create-player.txt、fix0817-create-data.txt、fix0817-unit-init.txt。未启动或终止游戏、未写存档。

## 历史反馈修正（08.16，已由08.17撤除构造补丁）

- 背包容量无效的直接原因：WorldUnitDynData构造中propGridNum使用读取配置的b__139 getter和空b__108_140 setter；非空onBaseValueSetCall不代表可持久写入。旧代码写baseValue后实际值不变，用户日志容量回读失败；旧托管替身错误模拟为普通字段。
- 独立累计扩容写入人物objData的BHML.capacity.bonus，经DynInt.AddValue加入原生容量计算。保留原配置基础值和其他加成，重复扩容只替换自己的一个效果；世界首次就绪恢复已有单位、WorldUnitDynData构造补丁恢复运行数据重建；卸载仅移除自有效果，扩容记录随原生保存保留，需加载本MOD恢复。
- 确认绑定世界/人物/背包/容量字段及扩容objData指针、记录原内容；原生基础值保持不变，完整回读value/GetGridMaxNum及累计记录。坏记录、溢出、锁定、倍率/固定覆盖和原生上限在持久写前拒绝；挂载未知状态不盲重试，部分失败保留Pending并隔离。单个人物恢复失败不阻断其他人物恢复；退档/换世界丢弃旧引用，不调用已失效对象。
- 原生证据：.tools/fix0816-native.txt、fix0816-dyn-init.txt、fix0816-capacity-delegates.txt、fix0816-addvalue.txt、fix0816-api.txt与官方CHM DynInt AddValue/DelValue文档。托管扩容字段改为模拟空setter，复测旧实现会回读失败；新版验证及DLL见下文。

## 历史反馈修正（08.15，游戏回归待验证）

- 全图视野漏接实际大地图场景。补上GridBaseUI.goMask/goMaskView；开关开启时只接管MapWorldMgr.showGrids已有格子，不整图刷新。UpdateUI前恢复旧对象、之后接管新对象；HideGrid/Destroy/InitData释放；ShowGrid补充刷新；关闭、退档、故障及卸载恢复原activeSelf。已探索/可见格不调用创建型迷雾getter；原探索/可交互条件保留。
- 剧情包角色的DramaPackageFeedback在104评分之前返回1/2并安装专用日志、jumpDrama、successDramaId/failDramaId；旧评分补丁到不了此分支。玩家104关系请求范围内在入口跳过该专用答复、返回0，继续SpecialFeedback其他检查、已有关系冲突、评分与原生关系维护；NPC互相请求、宗门行政及邀约/索取不改变。适用于走此剧情包路径的五朵金花等人物，不伪造夫妻/道侣记录；UI年龄、性别和前置关系仍由原生检查。
- 原生依据：.tools/fix0815-native.txt、fix0815-dlc.txt、fix0815-grid-ui.txt、fix0815-masks.txt、fix0815-grid-lifecycle.txt、fix0815-getui.txt及fix0815-api.txt；核对本机官方CHM接口及Managed包装。当前只读Player.log确认08.14，未操作实际存档或游戏进程。

## 历史反馈修正（08.14，已被08.15补充）

- 偷窃截图显示得手成功、掩盖失败。玩家PlayerToNPCAction前同时设isStealComplete=true和isStealDiscover=false；NPC不变，成本、物品条件、原生转移仍执行。
- 全图视野原先对IsOpenGrid每个像素跨层检查，UIMinMap.UpdateUI原生像素循环放大成本；已移除该补丁和UpdateAllUI，改为临时隐藏UIMinMap.imgMask并保存/恢复原先enabled状态。显示地图地形，不把未探索事件变成可交互，也不改探索记录；未测新版真实开图耗时。
- 地图移动为持续开关。地图原生OnUpdate中响应左键点选，核查按下/抬起的命中与地图实例、过滤拖动及遮挡；按原生坐标换算取格，停止旧路线，UnitActionSetPoint维护人物/探索并回读，MapWorldMgr.SetPlayerPosi同步模型、镜头、局部迷雾与选择。不存在的格、保存/交互中不提交；不再提供坐标输入框。
- 关系必定接入UnitActionFeedback104的接受分数结果：原生OnCreate先写state，再读取feedbackLog生成结果并通知关系动作；只在此读取前将玩家请求的评分拒绝state=2调整为1；使用SpecialFeedback返回0以及首次GetIntim评分读取标记，拒绝特殊剧情和已有关系冲突导致的提前拒绝；OnCreate finalizer恢复嵌套范围。不是随机概率getter；不修改NPC间关系、宗门行政反馈、未初始化状态或整个IsCreate条件。年龄、性别、已有关系、资源和剧情条件仍由原生流程检查。
- 职位选择保留职位与长老部门两级列表；SchoolSetPostType使用DramaFunction.SetSchoolPost同样的isTest=true管理选项，跳过个人晋升资历检查，成员维护、职位占用、玩家职位上限和剧情人物限制保持。不会清退已有职位人员；同职位/部门直接报告无需变更。
- 日志5803行明确outer条件返回7，旧catch却设置全局部分执行锁。非零CreateAction返回表示IsCreate拒绝、未调用Create，现不进入隔离；角色检查可能开启剧情，仍保留Pending。已经执行后的异常/回读冲突继续隔离写入，持续开关和只读死亡名单保持可用，锁定资源可以关闭。

## 历史反馈与修正依据（08.12–08.13）

| 反馈 | 查明原因与处理版本 | 验证边界 |
| --- | --- | --- |
| 对象栏应在左侧 | 所有人物页共用左侧对象栏；内容整体避让，保持纸墨和窗口复用。 | 四画布布局/命中检查与GDI预览通过，非游戏截图。 |
| 偷窃必成无效 | CompleteRate之后原生还会修改概率并抽取结果，旧概率getter不是最终结果。改在玩家PlayerToNPCAction前设置isStealComplete，保留发现标记、前置条件和原生物品处理。 | 托管检查最终结果及玩家范围；游戏派发和转移副作用待回归。 |
| 无冷却只有图标变化 | SkillAttack/StepBase.IsCreate直接读取运行data.cd.value，绕过cdTime。真实释放检查期间临时AddEqual(0)，finalizer无论正常/异常均DelEqual自己的覆盖；cdTime仍供图标。 | 替身原释放检查由拒绝变可释放，原CD基础值未改；真实IL2CPP派发待测。 |
| 体力修改后不能改回 | 删除周期补满，不在开启时修改资源；玩家原生负数消耗/伤害及世界hp/mp/sp/energy基础值减少时锁定。关闭立即停止拦截，资源上限不改。 | 当前值未补满、关闭后允许减少已检；08.11已经发生的补满没有可靠历史值可自动恢复。 |
| 小菱形无描述 | 原帮助浮层只允许属性页，功能页被早退。放开功能页；保留原标记大小以免窄栏遮挡文字，整个按钮含灰色项悬停显示条件与原因。 | 使用真实BookView/UIEventListener验证帮助浮层内容。 |
| 玩家道号不可输入 | 原生道号维护配置ID和效果，无自由文本接口。玩家显示道号改用原生DataObjectData独立扩展键BHML.title.text，并在原生UITaoistNameItem显示；留空恢复原显示。 | 显示别名、原效果保留；不是创建新的原生道号配置。游戏显示/保存重载待测。 |
| 宗门无法修改 | 移除可用性阶段构造/检查原生动作，增加实际宗门职位配置、成员数据校验及执行后职位/部门回读。原生权限条件仍保留，不强制成功。 | 未能仅凭截图确定该次原生拒绝原因；新增日志可定位，实际宗门变更待回归。 |
| 教功后反复对话 | Player.log调用链确认刷新Availability("kill")→IsCreateAction→UnitActionRoleKill.IsCreate→OnParentBless，反复加入6469102剧情，与教功本身无关。可用性改纯读取；原生Init一次，CreateAction检查一次；教功选中回调单次消费。 | 50轮全入口可用性检查零原生检查；异步及重复提交门禁通过。已排入08.11游戏的剧情不会由新磁盘DLL追溯清理。 |
| 结婚出现求情/NRE | 求情是08.12已修的刷新副作用。08.13继续查明另一真实缺陷：误将婚礼结果UnitActionRoleMarry(false, 玩家位置)当求婚，false反悔分支和OnLogMain要求地点建筑，野外可空引用；改走UnitActionRoleRelation(Married, 原生配置)，isRequestMarried=true，游戏自己选择婚礼地点/安排婚期。 | 旧截图无堆栈，不能排除其他异常；已移除查明的错误入口，求婚/婚礼实际全程待游戏回归。 |
| 审查全部功能 | 下表覆盖全部入口，区分接入路径、禁用和语义限制。旧08.11门禁不再被当成真实功能可用证明。 | 尚未另行授权并备份测试存档，本轮不做原生写入验收。 |

## 全入口核查

以下“接入”仅表示当前代码路径与本机静态依据，均未完成08.14游戏效果验收。界面不直接写字段；按钮只根据可安全读取的状态禁用，复杂原生条件在确认执行时检查一次，不能为追求灰色状态而先启动游戏流程。

| 入口 | 当前路径及限制 |
| --- | --- |
| 全图视野 | 控制UIMinMap地图窗口与GridBaseUI大地图场景两层迷雾；不开全图刷新、不钩逐像素探索查询、不改探索记录，关闭/释放恢复原状态。 |
| 地图任意移动 | 开关开启后地图点选；过滤拖动/遮挡，按原生坐标换算，UnitActionSetPoint并回读，SetPlayerPosi同步模型/镜头；不提供坐标输入。 |
| 关系必定 | 玩家104范围内跳过剧情包专用答复，再将评分拒绝state=2→1，在反馈展示及父动作回调前生效；保留完整IsCreate和关系结构门禁，不影响普通邀约/索取。 |
| 逃跑必成 | 玩家Escape.Init期间使用isSureComplete原生结果选择选项，随后恢复；保留IsCreate。 |
| 偷窃必成 | 玩家StealItem.PlayerToNPCAction前同时覆盖得手和掩盖结果；前置条件、成本及物品转移原生执行。 |
| 立即学习 | 玩家PropMartialStudy.OnCreate临时isOneStudy，保留消耗、完成事件及技能生成，随后恢复选项。 |
| 无限灵力／念力／体力 | 锁定减少，取消自动补满；战斗AddMP/AddSP/AddHP与世界玩家DynInt资源setter拦截。体力包含地图精力减少；不锁住正常恢复，不改最大值，关闭后恢复正常。 |
| 无敌金身 | 玩家战斗AddHP负数伤害不执行；直接死亡、剧情死亡未屏蔽。 |
| 技能无冷却 | SkillAttack/StepBase.IsCreate期间运行cd临时动态覆盖，finalizer清理；覆盖武技/灵技、绝技、身法、神通，施法、资源/技能条件保持。不扩到法宝、丹药和召唤物；运行cd锁定或已有固定覆盖时关闭辅助并说明。 |
| 解除年龄限制 | 禁用：官方语义是关系“年龄条件”，未核实独立拦截；不改出生年月、寿命或共享配置。 |
| 壶妖持久 | 禁用：官方语义是“壶妖在场持续时间”，回收含延迟回调，未核实安全撤销路径；保留原生死亡、重伤与回收。 |
| 背包容量＋999 | 人物objData独立累计＋999，经原生DynInt.AddValue效果接入；保存记录、加载本MOD恢复、卸载回原生容量。保留配置基础值和其他加成，绑定字段/基础值/计算值/背包及扩容记录指针/内容，回读value/GetGridMaxNum/记录；锁定、倍率、固定覆盖及原生上限拒绝。 |
| 切磋／论道／袭击／邀约／双修／招纳 | 对应原生UnitActionRole*，玩家为发起方、左侧NPC为对象；原生门禁/消耗/关系与事件处理保持。异步等待AddEndCall，不重复提交。 |
| 赠送／索取 | 玩家/对象可见背包一件物品，绑定唯一ID、完整签名与数量；赠送原生Clone+PropsItemCanGive，索取原生Askfor；隐藏/装备项拒绝。 |
| 教功 | UITeachSkill用玩家作为功法来源，单次选择回调；SkillCanTeach(target, learned)+TeachSkill原生动作。原生窗口参数是技能来源，不能误传目标NPC。 |
| 结婚 | UnitActionRoleRelation(Married, 原生配置)，isRequestMarried=true：原生求婚、答复、选址和婚期流程；不直接发起婚礼结果动作。沿用UINPCSetRelation纯读取门禁：配置、性别、以月计的年龄差、前置关系、双方无配偶；确认绑定双方关系容器/配置并重读。不伪造夫妻记录；关系必定开启时只调整原生接受判定。 |
| 击杀 | UnitActionRoleKill，不启用forceDie；确认后原生检查可触发长辈求情，这是原生保护流程。刷新不再触发该检查。 |
| 救治 | 选择对象为actor，原生UnitActionRoleSave；保持原生治疗成本与条件。 |
| 复活 | DramaFunctionTool的npcReviveNoDrama_2，2明确绑定右角色；仅仍有原生实例的鬼魂，回读鬼魂标记；不拼造已移除人物。 |
| 长辈庇护／复活壶妖 | 禁用：独立授予/恢复及家谱/健康/契约维护未核实，不拼改记录。 |
| 死亡名单 | 独立只读选择状态，不改变其他页面已故人物保护。 |
| 宗门显示／外门／入宗／设置职位／退宗／回宗 | 原生SchoolJoin/SchoolSetPostType/SchoolExit，校验宗门实例、职位配置、成员数据；身份及职位/指定部门执行后回读。职位采用剧情入口管理模式，保留占用/玩家职位上限；同职位不提交，成员、事件和关系维护原生执行。回宗只移动到当前宗门地点，不恢复已退身份。 |
| 玩家道号显示名 | 仅玩家，不依赖左侧NPC；最多24字，拒绝富文本/换行，独立扩展键保存，原生UI显示；留空恢复原生显示，原生称号ID和效果保持。 |
| 指令／剧情 | DramaFunctionTool单条1–512字符命令，不含换行/分号/竖线，未知命令不重试。DramaTool.OpenDrama验证配置ID与明确左右角色，绑定人物和容器后单次执行。 |

## 生命周期与数据保护

页面保留七项导航、六页静态预建与休眠复用，三组独立滚动栏和最左侧共享对象栏。隐藏页零控件刷新，切人/切页/关闭清理旧确认框；失效对象禁止提交。辅助绑定玩家，与页面独立，关窗保持，退档/换世界/玩家或存档管理器替换/卸载清空，不保存开关偏好；玩家道号扩展文本属于一次性待保存数据。

可用性不构造原生动作、不调用IsCreateAction。请求绑定当前世界、玩家、对象、数据/人物/背包容器、宗门及剧情角色；执行前重新解析。角色Init先安装原生结束处理，再加本模组结束回调，CreateAction只检查一次原生条件。原生CreateAction非零返回属于条件拒绝，不设部分执行隔离；角色条件检查可能打开剧情，仍保留Pending且不自动重试。已开始Create后的异常及回读冲突继续保留Pending并隔离写入；开关和只读死亡名单不被写入隔离锁住。异步等待原生结束，回读身份；容量/移动/宗门/复活/道号读回结果。

异常可能部分执行时保留Pending并停止同世界后续提交，不宣称事务回滚。原生动作/指令可能自动保存。补丁独立Harmony ID、仅卸自己补丁，安装部分失败全部关闭，错误同世界不自动重装；释放检查临时修正不改秘籍、配置、CD基础值或永久属性。资源关闭不需要回写，也不自动追溯08.11已发生的资源变化。

## 本机原生依据

来源为本机官方CHM/docs/api-reference、MelonLoader包装、Cpp2IL元数据和GameAssembly静态调用。旧研究缓存.tools/features-native-*.txt；本次.tools/fix0812-native-{core,check,steal,title,teach,school,post-marry,cooldown-overrides}.txt。SkillAttack/StepBase.IsCreate读取运行DynInt.cd；SkillSpecial及有独立IsCreate的身法子类均调用该基类释放检查；偷窃概率后另有roleEffect修改与抽取；SchoolSetPostType构造生成原生职位参数，OnCreate维护成员/PostData；原生UITeachSkill.UpdateUI读取传入unit的GetActionMartial。不能把包装方法存在当成派发及真实效果已验证。

08.11故障日志快照.tools/fix0812-user-player.log，SHA256 `35C767FDAA1A0806DD6E20F4849260F8EC6755546B46487A1617D62B1A317FE2`；已确认08.11加载和反复OnParentBless调用。用户NRE只有提示截图，旧catch不记堆栈。08.13静态研究.tools/fix0813-native-{marry,role,proposal,marriage-entry,wedding-ui}.txt补充可独立确认的入口错误：RoleMarry构造false设置state=2，OnEnd减少亲密、发反悔信件；OnLogMain对GetBuild(point)结果取建筑名，空地点会进入抛错路径。UINPCSetRelation点击回调则创建RoleRelation并设置isRequestMarried=true，对方同意后进入UINPCSelectMarriedPoint。精确截图异常没有堆栈，真实回归仍须验证。

## 验证与产物

08.14最终docs/verification/run-all.ps1 -Build通过，证据.tools/fix0814-final-all.txt：12套2440项、失败/非零退出/脚本卫生/一致性均0，编译零警告零错误，功能专项118项。Release与debug/Mod_YtxSQ1/ModCode/dll/MOD_YtxSQ1.dll均08.14且SHA256一致：`DD43BFB878F6D0D5CC7012FB2D35BB861E2174974D2C9C6CF33ACBA9485593FB`。旧08.13已备份.tools/backups/before-08.14/MOD_YtxSQ1.dll，SHA256 `EF284A1E978DC07B3F200FAE999D156AC01F6E25D873A80A157F129DBC3226E3`。新增得手/掩盖、显示层恢复、不改探索、地图点选/拖动/遮挡/迷雾命中与模型同步、关系评分/剧情保护/冲突保护、职位管理/无操作/拒绝继续提交、真实失败仍隔离且开关可关闭等验证。本轮只读日志仍为08.12，未加载或写入08.14游戏。

08.13历史执行docs/verification/run-all.ps1 -Build通过，证据.tools/fix0813-final-all.txt：12套2413项、失败/非零退出/脚本卫生/一致性均0，编译零警告零错误，功能专项91项。Release与debug/Mod_YtxSQ1/ModCode/dll/MOD_YtxSQ1.dll均08.13，SHA256 `EF284A1E978DC07B3F200FAE999D156AC01F6E25D873A80A157F129DBC3226E3`；旧08.12备份.tools/backups/before-08.13/MOD_YtxSQ1.dll，SHA256 `011F14A5D522A92E5AD7827FF7C038E895512BED25CD9906AB816C8AF829AD00`。新增求婚正确动作/标记、缺配置/对话、双方已有配偶、原生性别/年龄/前置关系、正负年龄差边界、确认后关系容器/配置/婚姻变化以及异步重复提交检查。

08.12历史执行docs/verification/run-all.ps1 -Build通过，证据.tools/fix0812-final-all.txt：12套2397项、失败/非零退出/脚本卫生/一致性均0，构建零警告零错误。其中功能专项75项、布局1463项、生命周期29项、关系228项。该版当时的Release与调试DLL为08.12（现已由08.14替代），SHA256 `011F14A5D522A92E5AD7827FF7C038E895512BED25CD9906AB816C8AF829AD00`；旧08.11备份.tools/backups/before-08.12/MOD_YtxSQ1.dll，SHA256 `6110104CC6DC94731E674AAD606385441DF1077FA8782CACB2822BDB57614E23`。docs/screenshots/features-layout-{960,1366,1600,1920}.png为托管GDI布局预览，不是游戏截图。

08.12–08.13新增真实释放条件临时覆盖/异常清理、资源不补满/关闭恢复、最终偷窃结果范围、页面只读50轮全入口、玩家道号与空值恢复、宗门职位/部门回读和缺配置/未落地隔离。托管替身不能模拟IL2CPP内联、真实剧情、成员副作用和保存重载。

**未验收**：08.14游戏/编辑器加载、Harmony实际派发、所有技能类别/更换/战斗进出、资源与无敌实际效果、偷窃转移/发现/关系、学习成本与事件、教功/结婚/宗门完整流程、鬼魂复活、命令/剧情、道号显示/保存重载、换档、第三方共存及长期游玩。游戏写入验证只使用另行授权并备份的测试存档；本轮未操作实际存档、重启或终止游戏。编辑器缓存与预览改动保留，未提交Git或写全局记忆。

08.14静态依据：.tools/fix0814-native-{check,map,map-click,entries,move,position}.txt；08.12游戏只读日志快照.tools/fix0814-user-player.log，SHA256 `164E4166A11561A655F0BEB1CA7627FB535680A0C10A348EB358B5B24FE2A79A`。原生104接受判定及地图像素循环已核查，但不代表IL2CPP补丁派发和真实游戏效果已验收。

## 08.15验证与交付

完整门禁12套2454项通过，失败/非零退出/脚本卫生/工程一致性均0；功能专项132项。证据.tools/fix0815-final-all.txt为本轮工具输出摘要。编译零警告零错误，Release与debug/Mod_YtxSQ1/ModCode/dll同步08.15，DLL SHA256 `C3EB0B84B967EC84B8D4115B87E02221DB223B592A982102C98C336960736586`。原游戏加载DLL备份.tools/backups/before-08.15/MOD_YtxSQ1.dll，SHA256 `DD43BFB878F6D0D5CC7012FB2D35BB861E2174974D2C9C6CF33ACBA9485593FB`。专项覆盖场景已显示/新增/刷新/隐藏/释放、原activeSelf恢复、已探索格不创建迷雾，以及剧情包关系请求范围、NPC/宗门/其他反馈隔离。未启动、终止游戏或写入实际存档；原生IL2CPP补丁派发、五朵金花实际关系/剧情联动及地图场景表现待用户加载08.15复测。

## 08.16验证与交付

完整门禁12套2467项通过，失败/非零退出/脚本卫生/工程一致性均0；功能专项145项。证据.tools/fix0816-final-all.txt、fix0816-features.txt；最终释放保护收尾后build.ps1重新编译并同步，证据fix0816-final-build.txt，零警告零错误。Release与debug/Mod_YtxSQ1/ModCode/dll同步08.16，SHA256 `80050D952996A7BC71928A7E6BF16E6AC2683AB40BB2E0A49D2A3C1E75DD3E04`；08.15备份.tools/backups/before-08.16/MOD_YtxSQ1.dll，SHA256 `C3EB0B84B967EC84B8D4115B87E02221DB223B592A982102C98C336960736586`。未写实际存档或操作游戏进程；游戏真实格数/物品发放、构造补丁派发、保存重载及其他MOD共存未验收。08.15地图/DLC关系修正保留，实际表现同样待用户复测。

## 08.17验证与交付

完整门禁12套2471项通过，失败/非零退出/脚本卫生/工程一致性均0；功能专项149项。证据.tools/fix0817-final-all.txt、fix0817-features.txt；新增主菜单/创建/加载无容量补丁、玩家动态数据不全不接管、世界完整后恢复及稳态不重扫名单检查。编译零警告零错误，Release与debug/Mod_YtxSQ1/ModCode/dll同步08.17，SHA256 `1F4AC274287358CEC500EFECBEB495B5DF4098718D3F1630E8130BCB524905D3`；08.16备份.tools/backups/before-08.17/MOD_YtxSQ1.dll，SHA256 `80050D952996A7BC71928A7E6BF16E6AC2683AB40BB2E0A49D2A3C1E75DD3E04`。游戏启动/新建/读档恢复、扩容保存重载及地图/DLC关系表现待用户加载08.17复测，托管门禁不替代游戏验收。
