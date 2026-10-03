# 工程手册

更新：2026-10-03（07.40入口校准；按主题重组，去掉逐版标题）。仅维护实际结构、可执行流程和关键经验；当前状态与未验收边界见[项目记忆](../PROJECT_MEMORY.md)，逐版证据见[变更记录](CHANGELOG.md)，方向与阶段见[长期计划](LONG_TERM_PLAN.md)。

结构：代码导航 → 构建和调试 → 官方资料 → 必须保留的实现与失败经验（写入与境界、入口坐标、生命周期与释放、打开过渡与固定纹理、视觉约定、原生调用防错等主题规则）→ 常驻与释放审查范围 → 按变更验证 → 小步迭代与文档维护。

## 代码导航

源码根：`ModProject/ModCode/ModMain/`。

| 文件 | 职责 |
| --- | --- |
| ModMain.cs | 初始化、帧计时器、重复释放和异常熔断 |
| Core/WorkshopController.cs | 大地图条件、F8、输入编辑保护、窗口刷新 |
| UI/WorkshopEntrances.cs | 原生双入口挂载、单值坐标转换、F9拖动/缩放、点击核对、独立于内容熔断的生命周期 |
| UI/EntranceLayout.cs | 独立map/npc归一化位置/尺寸、配置校验、文件读写及原子更新；不访问存档 |
| UI/WorkshopWindow.cs | 原生UITextInfoLong承载、共享窗口所有权、开关与释放 |
| UI/BookView.cs | 七项导航布局、普通搜索、名单、角色属性、立绘和固定署名 |
| UI/WindowInputScope.cs | 本视图登记输入的焦点保护、Text到Caret坐标转换、有限诊断与补丁释放 |
| UI/PaperSkin.cs | 宣纸、墨刷和山水纹理加载与释放 |
| Services/CharacterRoster.cs | 名单查询、存活有效过滤、ID去重 |
| Services/CharacterReader.cs | 对象解析、存档上下文、只读快照及数值格式 |
| Services/CharacterFates.cs | 拥有气运只读快照、逆天分类及原生介绍格式化/回退 |
| Services/CharacterEditSession.cs | 纯托管字段键、草稿、输入校验及原值冲突预检；不写游戏 |
| Services/CharacterEditData.cs | 数字/槽位真实字段绑定、配置候选及本地化；不写游戏 |
| Services/CharacterFateCatalog.cs | 全量可见配置、类型前缀及候选介绍 |
| Services/CharacterFateOperations.cs | 气运拥有记录与原生运行效果增删 |
| Services/CharacterSave.cs | 全批写入预检、可回退字段适配、原生保存和主线程回调收取 |
| UI/BookView.FateLibrary.cs | 全库检索、多选与可见按钮池；三处气运共用自绘品阶样式 |
| UI/BookView.Actions.cs | 七个快捷按钮、六个叠加帮助菱形和满状态金框 |
| Services/CharacterActions.cs | 六资源恢复、满修为、现有叠加物品补量、NPC传送及双向好感度 |
| Services/CharacterRealmChange.cs | 境界配置成长差额、品质/经验/瓶颈及降阶截断记录 |
| UI/BookView.Editing.cs | 原位输入、独立选择按钮、下拉浮层及保存校验反馈 |
| Services/CharacterPortrait.cs | 原生身份解析与ConfDramaNpc.CreateTexture分流，普通修士保持原路径 |
| Example/Example.cs | 参与编译的官方示例；不是产品功能清单 |
| Properties/AssemblyInfo.cs | 程序集元数据；与界面版本分别核对 |

调用关系：ModMain → WorkshopController → WorkshopWindow → BookView。视图使用读取/草稿/写入服务与PaperSkin；窗口负责输入补丁生命周期。当前没有独立Modules目录，写入通过明确字段适配和原生动作接入，不提供反射任意写入。

07.28用partial分开编辑与布局，07.29再拆图鉴；新增文件须登记csproj。同一次窗口打开期间切页/切人物保留草稿；每次重新打开清空未应用草稿，世界失效清理。候选读取本机具体_allConfList（IL2CPP IReadOnlyList包装无直接Count），性格按当前槽真实type匹配。输入复制搜索控件须清空旧回调，及时Register/Unregister到WindowInputScope；Attach/Detach归窗口所有；Attach清空旧登记后，从当前承载重新登记BHML_前缀的本视图输入，避免Build期间登记的气运搜索丢失保护。07.29的CharacterSave负责全批预检、原生字段写入/缓存维护、保存回调及主线程Poll；CharacterFateOperations负责即时气运生命周期，不能和只读/草稿服务混淆。替身不证明IL2CPP、Windows IME或保存重载结果。

## 构建和调试

工程身份为`MOD_YtxSQ1`，目标net472；新增游戏引用Private=False。build.ps1从csproj读取源码、引用和嵌入资源，新增文件必须登记；Art中的山水与墨刷嵌入DLL。工程笔记不放ModAssets。

在工程根目录运行：

```powershell
$ErrorActionPreference = 'Stop'
& ./build.ps1
Copy-Item -LiteralPath ./ModProject/ModCode/ModMain/bin/Release/MOD_YtxSQ1.dll -Destination ./debug/Mod_YtxSQ1/ModCode/dll/MOD_YtxSQ1.dll -Force
Get-FileHash -LiteralPath ./ModProject/ModCode/ModMain/bin/Release/MOD_YtxSQ1.dll,./debug/Mod_YtxSQ1/ModCode/dll/MOD_YtxSQ1.dll -Algorithm SHA256
```

build.ps1成功前不会覆盖上次Release产物；Release不混入额外DLL。PDB按实际同步情况单独记录。官方编辑器“进入游戏调试”复制Release DLL并加载；手动复制与哈希只能证明磁盘一致，不证明运行游戏重载。运行版本以日志、界面和实测反馈核对，不擅自终止游戏。

**脚本编码约定（2026-10-03实测并修复）**：`build.ps1`与`docs/verification/run-*.ps1`原先是无BOM的UTF-8且含中文字面量。PowerShell 7按UTF-8读取正常；**Windows PowerShell 5.1按系统ANSI读取，中文与其后的引号会被一起吞掉，脚本直接语法错误、完全无法运行**（本机仅有5.1：`$PSVersionTable.PSVersion`为5.1、`[Text.Encoding]::Latin1`不存在；修复前实测11个脚本全部解析失败，`build.ps1`含5处错误）。已给全部11个受跟踪`.ps1`加UTF-8 BOM（字节级前置，不改内容与换行，diff每文件仅首行），修复后11个全部解析通过，`./build.ps1`与`run-*.ps1`可直接在5.1下执行。**今后新增或改写`.ps1`必须保留BOM**（或写成纯ASCII），否则本机下次又会整体失效；`.cs`不受影响——Roslyn优先按UTF-8读源码，所以游戏日志中文正常。另注意5.1下`[IO.File]`等.NET API用进程工作目录而非`Set-Location`的当前位置，相对路径须先`Resolve-Path`成绝对路径。

2026-10-02按用户指令在工程根建立本地Git，主分支main，无远程。初始提交包含当时07.15源码、工程会话入口、配置、源资源、检查脚本及文档；最新验收以项目记忆为准。未配置原用户提交身份，因此仅仓库内使用Codex <codex@localhost>作为自动化身份，不修改全局设置。

Git跟踪ModMain源码工程、Art、Unity Assets（含.meta）/Packages/ProjectSettings、ModAssets/ModExcel源内容、实测截图及小型历史文档归档。ModProject.cache/ModData.cache保存官方身份，必须跟踪。忽略.tools、debug、bin/obj、游戏引用dll、Unity临时目录/生成工程、日志、用户设置、可重建模拟截图及凭据。换行约定见.gitattributes；当前功能源码未因初始化而改写。

变更前看git status/diff，完成独立小目标后核对暂存差异并提交相关文件，不混入他人未完成改动。检查用git diff --check；历史用git log --oneline。需要取回旧文件时先保护当前改动，避免无依据reset --hard或清空工作区。源码可回退不表示实际存档可回退；旧DLL等产物仍按需要放.tools/backups并记录版本/哈希。

首次导入的官方Unity序列化资源和部分旧源码带历史尾空白，已检查并保留原文，不为建立Git批量格式化源码；之后检查本轮新增差异。所有源码与资源在Git索引中按.gitattributes规范化换行，工作区未批量重写。

## 官方资料

游戏根在工程上两级；具体环境见项目记忆。

- `Mod/modFQA/`：`代码编写教程/`（含官方`ModMain`模板工程与`GGBH_API.chm`）、`资源修改教程/`、`配置修改教程/`、`鬼谷八荒MOD使用指南基础版 1.0/`、`MOD模板例子/`（DLC配置、静/动态立绘、技能配置、替换UI四个示例工程），以及工程`Example/Example.cs`。
- `docs/api-reference/`是官方CHM解包缓存（约110034个htm，未纳入Git）；调用签名须核对本机`MelonLoader/Managed/`程序集，不能直接套网上新版接口。
- 正式自定义UI路线涉及Unity2020.3.9、UIBase、预制体与组件注册，当前迁移未完成。原始教程研究细节保存在历史归档。

### 官方MOD工程结构与新建流程（2026-10-03只读核实）

教程正文很薄（代码教程53段、资源教程约60段、配置教程约50段），操作细节主要靠截图，本机未逐张核对截图。下列结构以官方模板工程、本机程序集和`Player.log`实测为准，不以教程叙述代替。

**新建流程（编辑器口径）**：游戏内`MOD`页面→`模组开发`→`更改目录`指定自建MOD文件夹→`新建项目`→模组编辑页（创建气运/NPC/宗门/道具/奇遇/副本等）→`保存项目`→右下角`进入游戏调试`。首次点击`编写代码`会生成VS工程与主类；主类名必须为`ModMain`，命名空间默认`MOD_<模组ID>`，改命名空间须同步程序集名与编辑器内设置。教程硬性要求：VS2022、net472、新增引用一律`Private=False`（否则Release会多出DLL导致MOD失效）、`bin/Release`只留本模组DLL、Harmony补丁在`Init`中手动`PatchAll`。

| 目录/文件 | 职责（模板工程与本机实测） |
| --- | --- |
| ModProject.cache | 模组身份JSON：soleID、name/author/desc/ver、exportVer、tags、excelEncrypt等；官方soleID为YtxSQ1 |
| ModData.cache | 各配置表的模组自有行(groupName/items)、excelMID及modNamespace；实测`modNamespace = "MOD_" + soleID`，excelMID是`MID&`的偏移基准 |
| ModCode/ModMain | C#工程；AssemblyName=MOD_YtxSQ1、net472；产物`bin/Release/MOD_YtxSQ1.dll` |
| ModAssets | 随MOD一起导出的资产；`g.mod.GetModPathRoot("<soleID>")`取模组根目录 |
| ModExcel | 配置表xlsx/json；文件名须与表名一致，json与xlsx不能同名 |
| ModRes/ResBuildABProject | Unity 2020.3.9f1c1工程，菜单`游戏工具/更新/AB`输出`Assets/AssetBundle`；`Assets/Resources`下资源全部打AB |
| debug/Mod_<soleID> | 编辑器调试导出实例：ModAssets、ModCode/dll/*.dll、ModRes/AssetBundle、ModExportData.cache |

**入口契约（官方模板`代码编写教程/ModMain/ModMain.cs`）**：`public class ModMain`提供`public void Init()`与`public void Destroy()`，用`g.timer.Frame(new Action(OnUpdate), 1, true)`做逐帧；Harmony须在`Init`中`new HarmonyLib.Harmony("<命名空间>")`后`PatchAll(Assembly.GetExecutingAssembly())`。教程只强制类名、未给签名，签名以模板工程为准。

**加载证据**：`Player.log`记录`加载模组：<游戏根>\MODS_INIT\ModProject_<soleID>/debug/Mod_<soleID>/ModExportData.cache`，调用栈为`DLCConfMgr:Init`←`ConfMgr:Init`←`ModMgr:Init`内的`ModMgr:LoadAllMod`/`LoadMod`。模组工程根目录即`<游戏根>\MODS_INIT\`；磁盘DLL与debug目录同步只证明文件一致，不证明运行中的游戏已重载该版本——以日志和界面核对。

**配置表头来源**：本机在`Mod/modFQA/配置修改教程/配置表头`（761个xlsx）；`配置（只读）`128个中文名xlsx与`配置（只读）Json格式`761个json仅供查表，不放入模组；`配置对照/配置对照表.xlsx`与`效果配置对照表.xlsx`是条件/命令/效果对照。教程所称`StreamingAssets\modFQA\...`在本机不存在（该目录仅有`assets`），以本机实际路径为准。新增配置ID在xlsx里写`MID&XXXX`（json不支持），实际ID=excelMID+该值。

**教程未覆盖、须靠模板与本机核实**：各目录职责与两个cache字段、`Init`/`Destroy`签名、DLL在MOD内的确切落位与加载时机、日志位置、soleID概念、导出与创意工坊上传流程、`g.*`接口清单（见`Example/Example.cs`）。编辑器UI类型可在本机`MelonLoader/Managed/Assembly-CSharp.dll`核对，如`UIModMain`/`UIModMainProject`/`UIModCode`/`UIModExcel`/`UIModRes`/`UIModDress`/`UIModAllTable`与`ModMgr`/`ModExcelTool`/`ModExportTool`/`ModData*`。

## 必须保留的实现与失败经验

| 主题 | 已确认事实与开发规则 |
| --- | --- |
| 搜索 | 普通InputField取自UIType.ModWorkshopUpload；清理旧EventTrigger、验证器和回调，文字变化即时筛选。禁止用弹窗或手工拼接字符串替代 |
| 失焦 | 实测焦点转移到TextInfoLong/Root/btnGroup/G:btnOK，active=False且不可交互。07.10保护实际命中；原生发起者完整栈未取得，不能断言是导航Update |
| 焦点保护 | 仅本视图登记的输入框选中/编辑时拒绝inactive或不可交互目标；放行正常对象与null。关闭、失效、初始化失败必须卸载与恢复；不逐帧抢焦点或全局禁止失焦 |
| 光标 | GenerateCaret后将Text字形中心转换到Caret局部坐标，再平移本框四个顶点；空文本用占位文字，缓存缺失用文字矩形。保留高度、宽度、X和闪烁；偏低不是变短 |
| 属性 | 用真实getter与同人物原UI对照，不用猜测数据自证单位。年龄/寿命按月除12；暴击整数200直接显示200%，旧valueFloat×100及千分比猜测已被推翻 |
| 字段 | gradeID是配置行ID，应查境界/阶段；水灵根字段basisFroze、功法抗性phycicalFree按本机实际拼写；映射以CharacterReader源码为准 |
| 名单 | GetUnits(true)/GetUnit(id,true)包含隐藏单位，过滤死亡/无效、ID去重、自己置顶；按打开/刷新缓存，不逐帧扫描。203/204只是当时存档结果 |
| 立绘 | 五朵金花用GetFiveFlowerDramaNpcID→ConfDramaNpc.CreateTexture，由原生按avatar/modelID分流；07.14强取拼装数据实测失败，不再使用。专属RawImage销毁触发原生autoDestroy检查、RenderTexture释放及计时器停止；不额外创建第二个渲染器，不强制启用原生已关闭的RawImage。普通修士保持原路径/取景；无纹理时隐藏RawImage，异常日志标注调用阶段 |
| 页脚 | 全窗口居中固定“Jaye@2026-爱你一生一世”，动态反馈放相关正文区域，不再占用署名 |
| 身份字段 | 性别按UnitSexType；兴趣用WorldUnitData.GetHobbyItems；性格按inTrait/outTrait1/outTrait2查roleCreateCharacter，sc5asd_sd34为名称，用静态ConfLocalText.GetText本地化。可选字段失败不清空独立数值 |
| 气运 | bornLuck/addLuck拥有记录，type=3逆天改命，其他按来源归类；介绍用ConfRoleCreateFeatureEx.tips(item,选中unit,真实LuckData,2)，失败回退。07.27行使用UIEventListener的UnityEvent悬停回调，本视图面板控制尺寸/位置，不使用UISkyTipEffect默认窗。FateRowBounds累加本视图全左上锚点/左上pivot且单位localScale祖先的实际anchoredPosition，包含ScrollRect长页偏移；IL2CPP的parent显式GetComponent<RectTransform>，不以as强转包装；不再使用CalculateRelativeRectTransformBounds定位，FateTipBounds处理换侧/边界；长文ScrollRect，0.12秒退出过渡允许移入提示滚动。原列表滚动/重建/切换/失效/关闭清理。提示行无Button，不写游戏对象；托管替身不证明原生缩放/指针事件顺序 |
| 界面资源 | 圆形×的64×64自绘纹理归BookView所有，随视图释放；不得借用未知Sprite或漏掉失败清理。正文统一BodyTop，左右共底边须随适配改动检查 |
| 重开读取 | WorkshopWindow每次打开新建BookView，Build→Select(0)重读名单并强制读取当前人物；打开期间属性每秒更新，名单不逐帧扫描，无独立刷新按钮 |

同一症状连续两次修复失败，先补真实事件证据；区分安装、分支命中和行为改变。不要把禁用控件状态、托管测试或模拟预览当成原生游戏验证。

### 写入、动作与境界依据

- 数值输入在OnValueChanged回调中先设置textComponent.text，再读preferredWidth；Unity回调发生在UpdateLabel之前，不能用旧字符串的宽度。克隆InputField清理继承的caretRectTrans/m_CachedInputRenderer/m_InputTextCache，销毁属于克隆的Caret，规范文字/占位矩形并AssignPositioningIfNeeded/UpdateLabel；不销毁原搜索框Caret，保留已认可GenerateCaret平移补丁。真实光标仍需游戏核对。
- 气运07.32以统一自绘纯色底、细线和品阶文字绘制，sprite=null，不再使用原生气运贴图。取消修改器自加的同组、lockLuck/globalMax限制，不改配置或成就全局状态；重复、隐藏、无运行类和壶妖类型仍保护。UnitActionLuckAdd.IsCreate原生检查并不使用成就/全局名额条件，不能把随机角色生成条件套入主动修改。
- CharacterActions独立处理AddExp、背包propsCount/UpdateAllShowProps、UnitActionSetPoint、双向SetIntim；传送仅读回位置且动作结果为0才成功，无移动拒绝不增加Pending。原位置按上下文/人物缓存，重复来不覆盖。原生RelationData.SetIntim保留游戏关系上限；读取原浮点值叠加，不能先GetIntim取整，也不能把双方合并一个字段。
- 物品isOverlay只有0/1，不是数量上限；本机PropsData.propsCount setter直接存整数，无各物品最大堆叠配置。用户明确选择9999，因此补满现有可叠加物品到9999，不增加独立物品记录，也不降低已有更大数量。不能称9999为游戏官方最大值。
- CharacterRealmChange读取玩家DataWorld.World.playerLog.upGrade或NPC的npcUpGrade品质历史，按阶段调用原GetAddAttr(conf)累加，目标减原成长差额写DynInt持久setter，寿命growAddLife按月，道点abilityPoint。SetGradeID同步经验基线与事件；不执行ActionUpGradeComplete剧情、材料、随机气运奖励及历史任务回滚。旧材料/丹药/装备额外部分保留，不声称还原所有历史成长率。
- RoleGrade.lockFeature配置120等对应RoleEffect1201=limitGradeExp_1_1；旧瓶颈不注销会继续限制新境界经验。直接改境界通过LuckDel注销这些实际拥有记录，回退通过原LuckData/CreateLuck/NewCreateLuck恢复。满修为只走PropertyData.AddExp，其原生路径处理锁定效果、瓶颈及经验事件，不直接赋exp，也不改境界。
- 降阶的体力/寿命/道点下限及截断差额存人物objData的BHML.realm.debt:*键，下一次升阶补偿截断，避免反复升降加值；失败回退恢复原字段/经验/品质/扩展记录。复杂原生回调仍可能产生不能简单撤销的事件，托管回退不等于游戏事务。数字草稿和境界不能混批保存。
- WorkshopWindow成功取得承载后、Build之前Clear未应用草稿；切页/切人物仍保留当次草稿。真正磁盘保存失败的已应用内存Pending继续保留，原生保存成功回调才清零。预检错误包含具体人物和字段，范围以原生DynInt为准，不为消除报错而盲目扩大寿命/资源范围。
### 常驻与释放审查范围

| 范围 | 已核对的机制 | 仍需游戏证据 |
| --- | --- | --- |
| 长时间闲置 | 关闭时保留帧入口/F8/保存Poll，世界失效低频清会话；每次重开新建视图重读名单，存活目标重新解析；模拟4小时后打开通过 | 正常推进月份、战斗、菜单及闲置2–4小时后重开、读数与原UI一致 |
| 更新异常 | 释放失败控制器但保留F8人工恢复，不每帧重建，不重放人物修改；显式卸载停止计时器 | IL2CPP异常及换场景真实行为；严重原生崩溃不在托管保护范围 |
| 关闭释放 | 输入补丁UnpatchSelf，导航恢复；控件/动态字体/圆形×/立绘释放；静态四张美术纹理MOD持有，卸载清空 | Unity延迟Destroy、原生特殊立绘autoDestroy及RenderTexture实际释放、内存趋势 |
| 世界隔离 | 草稿托管值，世界键/人物ID，不持有原生目标；NPC返程字典仅ID/坐标，下一次传送核对世界键后清旧记录 | 快速换档、对象死亡/替换、旧记录不可跨世界使用 |
| 保存 | 全批预检/原值冲突/回读/回退、原生SaveData、成功回调才清Pending、失败重试不重放；反馈异常与状态隔离 | 测试存档实际保存重载、自动保存、磁盘失败。回调长期不返回会保留Busy，尚无安全取消契约；不能盲目超时解锁 |
| 兼容 | Harmony唯一ID，仅三处输入方法补丁，条件限制本输入，关闭UnpatchSelf；共享TextInfoLong已有窗口拒绝占用，关闭验证所有权 | 本次日志仅本模组加载，缺第三方MOD共存证据；F8同键及相同Harmony方法补丁仍可能冲突 |

资源关闭后允许有界常驻四张自身纹理、帧计时器及少量托管状态；不要求总内存回到同一值，不以一次进程内存/工作集判断泄漏。长期趋势必须考虑地图/战斗/游戏资产缓存。正常存档不代做写入试验；保持独立备份，保存与重载专项在测试存档进行。实测重点为2–4小时正常游玩不使用秘录后F8、名单刷新、搜索/切人物、关闭、日志；不需要为闲置测试改数值。

### 打开过渡

书页遮罩CanvasGroup只由BookView持有，构建后开始0.16秒未缩放时间Smoothstep透明度变化，无缩放/布局变化。过渡期间interactable=false、blocksRaycasts=true，完成才恢复操作；TickAppearance放在属性刷新节流前，每帧执行。关闭无需等淡入完成，Dispose清状态及引用。检查初态/中途/完成，GDI替身不渲染动画。同步打开日志不含过渡时间，不能将淡入称为消除构建卡帧。

### 固定美术纹理生命周期

PaperSkin.Acquire在主线程懒创建唯一四纹理缓存（纸、刷、山水、标题）；BookView只借用，Dispose不销毁共享纹理。ModMain.Destroy先释放控制器/窗口，再ClearCache；不放人物/世界对象、不开后台Unity线程。生成逐像素数学用System.Math，避免Mathf原生包装重复往返。`run-paper-skin.ps1`编译真实实现、嵌入实际PNG，验证固定数量、重复获取、释放/重建、部分失败及重试；图像解码是替身，不测GPU或Unity原生泄漏。窗口/人物重建规则保持，现有逐阶段日志用来验证冷/热打开收益。

### 入口定位与坐标

- 07.39错误：GetWorldCorners实际签名为Il2CppStructArray<Vector3>，普通Vector3[]隐式转换只复制输入到新数组；读取原数组仍是零。不能把本机原生包装API当普通Unity托管方法。处理输出数组须读原生包装索引或改用单值返回；当前使用reference.rect→TransformPoint→root.InverseTransformPoint。禁止恢复原代码，托管数组替身通过不能证明原生写回。
- 默认位置：主界面=textSPValue右边中点+40单位，按原生参照控件计算；NPC入口在无坐标文件时改用出厂归一化预设（见下条），参照控件`btnAppellationAttack`只用于缩放与就绪判断，不再决定默认位置。根矩形/缩放未就绪拒绝，半秒挂载核对，错误3秒重试。保持原UI层，不使用截图像素硬编码原生坐标。
- F9仅无文本编辑且有效入口在层顶时切换；调整期间入口点击不打开内容。鼠标按下命中图标后保留抓取偏移，ScreenPointToLocalPointInRectangle使用所属UI canvas.worldCamera；松手记录并短时抑制Click。滚轮仅悬停修改本入口size40–100。提示显示局部坐标，靠边自动向内/上下面板。
- EntranceLayout独立map/npc，Custom位置是相对各自UI根矩形的x/y归一化中心，尺寸是参照控件单位；保存后常规Sync使用Custom，不能把拖动结果每半秒覆写。存放Application.persistentDataPath/BHML/entrance-layout.ini，Player.log记录路径、两组值和画面尺寸。纯布局文件不关联人物/世界存档；换档保留布局，清理入口和调整状态。
- 出厂预设与用户调整必须分开：`Position.Preset`是随代码发布的归一化默认（当前只有NPC入口，取自用户2560x1600实测），仅在文件缺少该入口时生效；`Position.Custom`表示用户调整过、需要写回文件。`Sync`按`Custom || Preset`使用归一化中心，`Save`只写`Custom`条目，日志区分`default`/`preset`/实测值。不得把预设直接标成`Custom`，否则文件和日志会谎称用户调整过；用户重新拖动后`Custom`自然覆盖预设。主界面入口不设预设，仍按原生参照控件计算。
- 写入UTF8、InvariantCulture、同目录.tmp+File.Replace+bak；校验范围、NaN/Infinity及文件大小，非法条目忽略，读失败保留默认。写失败保留dirty并提示，可F9再次锁定重试。不得提前编造最终用户坐标；用户说调整好后读本机ini和日志再固化默认值。
- 入口由ModMain独立持有，内容熔断后点击重建控制器，不重放修改；关闭/离图/换世界/卸载清理回调及原生对象。正常模式只检测F9，不逐帧查原生世界/UI；调整模式才逐帧处理鼠标。
- 图标v2减少细碎纹饰；mipmap=true+Trilinear处理1254原图缩小采样。原图高清不能自证56单位下清晰；必须按真实游戏截图评估。只共用一张纹理，卸载释放。
- run-entrances.ps1编译真实入口及真实布局文件服务；26项涵盖独立拖动/保存/恢复/大小、松手抑制、非法值与原子写入，仍不模拟IL2CPP、CanvasScaler或真实指针时序。原生方法签名/隐式转换IL已用本机Cecil核对；同类byref/数组输出必须再查包装语义。

### 视图异常释放

原生窗口创建/初始化须与视图/补丁安装共同在局部异常边界内，恢复前确认共享UI所有权，回退失败只关闭自己窗口。清理先拆持有引用，各原生步骤单独捕获；一个输入/立绘失败不能跳过纹理及原控件恢复。输入endingEditor由finally复位。生命周期托管检查含30次重复开关与OpenUI/null/InitData/回退/Attach故障；布局检查对四画布真实BookView注入输入停用错误，观察注销、Destroy和后续资源释放。替身Destroy从父Children移除但不清parent，断言遵守替身模型，不以它证明Unity销毁时序或内存无泄漏。

### 常驻节流与打开计时

关闭时WorkshopController不逐帧查询地图UI，半秒复核世界失效及窗口所有权；保存回调Poll与F8逐帧，按键不受节流限制。`run-controller-idle.ps1`编译真实控制器核对节流、F8、保存结果收取、离图、退出世界及输入/顶层UI保护。不会以托管计数证明游戏CPU收益。

WorkshopWindow打开总计覆盖原生窗口、视图及输入补丁；BookView阶段日志拆字体/纸墨、骨架/搜索模板、名单/排序、属性控件、快照/编辑、立绘及气运。总计是同步调用耗时，不包含下一帧渲染/GPU呈现；首次与重复打开需分别记录。诊断只在打开构建期间，Dispose清除失败计时状态。名单局部getter复用不保留跨帧原生引用。性能优化须据真实日志，不做后台预读游戏对象或过期名单缓存。

### 视觉与布局约定

- 微小帮助标记叠加在按钮角上，不通过标题缩进或缩宽给它占位；满状态没有说明。命中区独立，保留父按钮中心线。布局增加正文高度时同步页脚边界，宽版共底、小画布滚动及Box越界检查不能失效。
- 三项天级天赋有明确配置编号2804/2805/2806，原level=6并不意味着所有6都为神；显示快照特判这三项，不改配置、不靠本地化名称识别。天级雕饰随行池绑定启闭，回用普通条目必须关掉，不能遗留金框。

### 帮助标记与气运绘制

- 除满状态无介绍外，按钮介绍只绑角落帮助标记，不绑父按钮；点有独立点击接收器，防止把看介绍变成执行操作。提示与对象栏/正文共同使用书页坐标，按按钮下方/上方定位，不能将左栏提示强行约束到正文区域。
- 气运贴图的等比与拉伸均已被用户否定；按当前需求统一自绘色底，不再恢复SpriteTool.GetLuckBG。level在读取快照时保存；滚动池只更新颜色和文字，不加载贴图或重建装饰。名称保留固定品阶栏、删除空间和单行BestFit，检查拥有/检索/图鉴三个入口。

### 原生调用与数据换算防错

- 本机Unhollower生成的`Dictionary<TKey,TValue>.TryGetValue`对byref值使用引用包装构造，`float`会报`Single..ctor(IntPtr)`。用Mono.Cecil核对Il2Cppmscorlib的IL后，采用ContainsKey+索引getter的PointerToValueGeneric解箱。新增涉及原生泛型byref基本类型时先核实生成IL，普通托管字典替身不会暴露此问题。
- UnitActionSetPoint成功和坐标回读不代表地图人数已经刷新。后续走MapWorldMgr.UpdateGridUI/UpdateGridUICmd及UIMapMain.uiGridUnit.UpdateUI(curSelectPoint)，不通过重建单位或直接改人数掩盖显示错误；游戏回归覆盖来、重复来、返和去。
- 07.30等比和07.31拉伸贴图均被后续用户反馈否定，不能继续当成现行方案。当前气运sprite=null，名称用固定左上矩形、品阶栏和删除预留，避免锚点/offset混用。性格描述字段为xdash_54sd；气运/性格保留既有提示路径，快捷说明使用共同书页坐标及下方/上方定位。
- 资源显示上限不是资源baseValue；满状态和境界后恢复使用`baseValue + max.value - value`保留加法效果，锁定/倍率/固定状态拒绝任意反解。六项先预检，失败恢复所有已尝试字段并核对，无法恢复时保留Pending，不能谎称无修改。
- 不把所有资质随境界提高当成既定游戏规则。RoleGrade和ItemRarityMaterials官方只读配置、UnitActionRoleUpGrade原生代码应共同核对；保留丹药/修炼和特殊气运效果，不制造无配置的固定资质奖励。
- 光标连续反馈失败后不能再用托管测试自证：07.31记录每输入框空/非空的真实字形范围、原光标范围、转换位移和两矩形位置。按Unity原生AssignPositioningIfNeeded同步矩形，并将Text字形坐标转换到Caret局部坐标；诊断命中及游戏画面对照仍待取得。参考[Unity InputField源码](https://github.com/Unity-Technologies/uGUI/blob/main/com.unity.ugui/Runtime/UGUI/UI/Core/InputField.cs)，具体接口以本机包装程序集为准。
- 性能结论必须测量：空气运检索不加载全配置，介绍按悬停查询，已失败字体创建不重复执行，去掉逐次悬停日志；界面构建Stopwatch日志供新版实测。保留每次打开重读名单，不用过期世界缓存换取打开速度。

### 持久写入与配置依据

- 用户明确唯一存档是开发测试存档，授权本轮接入真实写入；不终止游戏。使用本机Managed程序集、官方CHM的WorldUnitBase.CreateAction/DataMgr.SaveData和官方RoleCreateFeature/RoleGrade表头及JSON核实接口。新增引用保持Private=False。
- DynInt.value只读。baseValue setter负责钳制及原生变化回调；加法效果保留，目标显示值换算基础差值，年份保存时保留月余数。倍率/固定效果、缺持久setter、范围异常目前拒绝，不通过试写猜系数。全批先准备，应用顺序资源上限优先；异常逆序回退可回退字段并读回原值。不声称游戏原生复杂动作可事务回退。
- 名字原GetName只拼二段数组，保持二段结构；兴趣三个ID合并一次写入，重复拒绝；Destroy旧allHobby、按配置item的竖线/下划线拆分调用WorldFactory.CreateUnitHobby/Init重建，GetHobbyItems比较ID更新缓存。不可直接把cacheHobbyItems设null（原生路径会访问它）。性格按真实配置type候选，写入三个原字段并GetCharacterWeight更新。
- UnitActionRoleUpGrade原生路径修改能力点、寿命、成长属性、资源、品质/日志和相关逆天气运。SetGradeID不能替代完整突破。07.29仅下一阶段CreateAction的方案已被07.30的CharacterRealmChange成长差额编辑替换：允许升降配置行及品质，不执行材料/突破剧情/随机奖励；境界不可和数字草稿混批。此处保留原生研究依据，不作为旧实现指令。
- 气运：官方表头type=1先天、2后天、3逆天、4仙人恩赐、5战前（后天）、6壶妖先天、11扩展先天。全库来自roleCreateFeature._allConfList，conceal=1不作为可见配置。原UINPCInfoProperty对type=2且conceal=1过滤，与敖瑶日志8012523/231125吻合；不能转而读取其他玩法DLCUnitData填世界人物。
- 后天/逆天用UnitActionLuckAdd，删除用UnitActionLuckDel注销运行回调和addLuck；先天添加保留bornLuck记录并CreateLuck/NewCreateLuck，删除补齐原动作不负责的bornLuck。当前保护重复、隐藏/缺失配置、无运行类及壶妖修士差异；07.30已移除随机生成的同组、成就和全局名额限制，不恢复这些主动赋予门槛。气运动作即时影响内存，记录待保存；复杂原生调用可能部分执行，错误后强制重读，不谎称无变化。
- DataMgr.isSaveData为静态并发标志，调用g.data.SaveData(Il2CppSystem.Action<bool>)。保留托管/IL2CPP委托，回调只发布托管结果，WorkshopController.Update主线程Poll刷新；成功才清待保存，失败仍可保存内存而不重放草稿；上下文切换的旧回调不清新世界状态。不能承诺游戏不会自动保存。
- Native反汇编依据存于忽略的.tools（writes/apply/npcinfo/binding及fate-style-native等研究输出）；自动符号共享地址有误标，结论须结合字段偏移/配置/实际调用。诊断文件是研究缓存，不进入ModAssets或发布包。

## 按变更验证

| 改动 | 相关检查 | 游戏重点 |
| --- | --- | --- |
| 仅文档 | 本地链接、事实、版本与路径 | 无需编译或重测游戏 |
| 布局/资源 | 编译、布局脚本适配时运行 | 当前分辨率、裁切、长文字、切模块；改适配时补1920×1080/1366×768 |
| 输入 | 编译、run-input-scope.ps1 | IME、粘贴/清空、正常失焦、F8、关闭卸载、其他输入框 |
| 读取/名单/气运 | 编译、run-character-reader.ps1、气运改动运行run-character-fates.ps1及run-book-layout.ps1 | 原UI同角色对照、隐藏/死亡、目标失效、切存档；气运长列表/说明及分类 |
| 窗口生命周期 | 编译、run-lifecycle.ps1 | 连续开关、离开地图、共享窗口、失败回退 |
| 新写入 | 字段依赖研究、run-character-save.ps1及相关UI检查 | 仅测试存档、原UI变化、保存重载、对象失效；临时补丁验证关闭及离场释放 |

脚本在`docs/verification/`，使用前核对当前源码适配性。托管替身不覆盖Unity事件顺序、IL2CPP及Windows IME；run-book-layout.ps1会写screenshots模拟预览，必须与实测截图区分。

托管检查项按当轮计，报告时说明轮次与累计口径，不与历史项相加。验收结论与未验收边界只在[项目记忆](../PROJECT_MEMORY.md)维护，逐版证据只在[变更记录](CHANGELOG.md)；全部数字字段与单位、IME全流程、多分辨率、跨场景/存档和地图输入隔离尚未全量验收。编译、托管检查、磁盘同步、编辑器加载和游戏结果分别记录。

## 小步迭代与文档维护

1. 读项目记忆和长期计划，按任务查本手册及真实代码/官方接口。
2. 定一个可独立验收的小目标，保护基线，完成实现和相关检查。
3. 同步需要的产物，取得游戏证据；没有证据就写待验证。持久写入先分析依赖并用测试存档。
4. 更新项目记忆现状和下一步，在CHANGELOG追加简短记录；范围变化才改计划/调查，结构与流程变化才改本手册。

变更记录统一写：日期/版本、目标与文件、接口或用户证据、编译/同步/游戏结果、未覆盖项及下一步。仅重大问题需要独立专项文档，普通小迭代不新建文件。状态为研究中、实现中、待游戏验证、已验收或延期；用户成功反馈只覆盖当轮需求。

记忆只放当前事实、约束和下一步；代码映射不逐行复制到文档，历史不常驻会话入口。删除重复文件前保留必要证据并修正链接。归档只按需打开，不自动全文读取。
