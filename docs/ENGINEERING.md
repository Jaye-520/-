# 工程手册

更新：2026-10-02。仅维护实际结构、可执行流程和关键经验；当前状态见[项目记忆](../PROJECT_MEMORY.md)，历史证据见[变更记录](CHANGELOG.md)。

## 代码导航

源码根：`ModProject/ModCode/ModMain/`。

| 文件 | 职责 |
| --- | --- |
| ModMain.cs | 初始化、帧计时器、重复释放和异常熔断 |
| Core/WorkshopController.cs | 大地图条件、F8、输入编辑保护、窗口刷新 |
| UI/WorkshopWindow.cs | 原生UITextInfoLong承载、共享窗口所有权、开关与释放 |
| UI/BookView.cs | 七项导航布局、普通搜索、名单、角色属性、立绘和固定署名 |
| UI/WindowInputScope.cs | 本搜索框无效焦点目标保护、光标整体平移与补丁释放 |
| UI/PaperSkin.cs | 宣纸、墨刷和山水纹理加载与释放 |
| Services/CharacterRoster.cs | 名单查询、存活有效过滤、ID去重 |
| Services/CharacterReader.cs | 对象解析、存档上下文、只读快照及数值格式 |
| Services/CharacterFates.cs | 拥有气运只读快照、逆天分类及原生介绍格式化/回退 |
| Services/CharacterEditSession.cs | 纯托管字段键、草稿、输入校验及原值冲突预检；不写游戏 |
| Services/CharacterEditData.cs | 数字/槽位真实字段绑定、配置候选及本地化；不写游戏 |
| Services/CharacterFateCatalog.cs | 全量可见配置、类型前缀及候选介绍 |
| Services/CharacterFateOperations.cs | 气运拥有记录与原生运行效果增删 |
| Services/CharacterSave.cs | 全批写入预检、可回退字段适配、原生保存和主线程回调收取 |
| UI/BookView.FateLibrary.cs | 全库检索、图鉴多选及可见按钮池 |
| UI/BookView.Editing.cs | 原位输入、独立选择按钮、下拉浮层及保存校验反馈 |
| Services/CharacterPortrait.cs | 原生身份解析与ConfDramaNpc.CreateTexture分流，普通修士保持原路径 |
| Example/Example.cs | 参与编译的官方示例；不是产品功能清单 |
| Properties/AssemblyInfo.cs | 程序集元数据；与界面版本分别核对 |

调用关系：ModMain → WorkshopController → WorkshopWindow → BookView。视图使用读取/草稿/写入服务与PaperSkin；窗口负责输入补丁生命周期。当前没有独立Modules目录，写入通过明确字段适配和原生动作接入，不提供反射任意写入。

07.28用partial分开编辑与布局，07.29再拆图鉴；新增文件须登记csproj。共享窗口打开期间保留草稿；07.30每次打开清空未应用草稿，世界失效清理。候选读取本机具体_allConfList（IL2CPP IReadOnlyList包装无直接Count），性格按当前槽真实type匹配。输入复制搜索控件须清空旧回调，及时Register/Unregister到WindowInputScope；Attach/Detach归窗口所有；Attach清空旧登记后，从当前承载重新登记BHML_前缀的本视图输入，避免Build期间登记的气运搜索丢失保护。07.29的CharacterSave负责全批预检、原生字段写入/缓存维护、保存回调及主线程Poll；CharacterFateOperations负责即时气运生命周期，不能和只读/草稿服务混淆。替身不证明IL2CPP、Windows IME或保存重载结果。

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

2026-10-02按用户指令在工程根建立本地Git，主分支main，无远程。初始提交包含当时07.15源码、工程会话入口、配置、源资源、检查脚本及文档；最新验收以项目记忆为准。未配置原用户提交身份，因此仅仓库内使用Codex <codex@localhost>作为自动化身份，不修改全局设置。

Git跟踪ModMain源码工程、Art、Unity Assets（含.meta）/Packages/ProjectSettings、ModAssets/ModExcel源内容、实测截图及小型历史文档归档。ModProject.cache/ModData.cache保存官方身份，必须跟踪。忽略.tools、debug、bin/obj、游戏引用dll、Unity临时目录/生成工程、日志、用户设置、可重建模拟截图及凭据。换行约定见.gitattributes；当前功能源码未因初始化而改写。

变更前看git status/diff，完成独立小目标后核对暂存差异并提交相关文件，不混入他人未完成改动。检查用git diff --check；历史用git log --oneline。需要取回旧文件时先保护当前改动，避免无依据reset --hard或清空工作区。源码可回退不表示实际存档可回退；旧DLL等产物仍按需要放.tools/backups并记录版本/哈希。

首次导入的官方Unity序列化资源和部分旧源码带历史尾空白，已检查并保留原文，不为建立Git批量格式化源码；之后检查本轮新增差异。所有源码与资源在Git索引中按.gitattributes规范化换行，工作区未批量重写。

## 官方资料

游戏根在工程上两级；具体环境见项目记忆。

- `Mod/modFQA/代码编写教程/代码编写教程.docx`、`Mod/modFQA/资源修改教程/资源修改教程.docx`及工程Example.cs。
- `docs/api-reference/`是官方CHM解包缓存；调用签名须核对本机`MelonLoader/Managed/`程序集，不能直接套网上新版接口。
- 正式自定义UI路线涉及Unity2020.3.9、UIBase、预制体与组件注册，当前迁移未完成。原始教程研究细节保存在历史归档。

## 必须保留的实现与失败经验

| 主题 | 已确认事实与开发规则 |
| --- | --- |
| 搜索 | 普通InputField取自UIType.ModWorkshopUpload；清理旧EventTrigger、验证器和回调，文字变化即时筛选。禁止用弹窗或手工拼接字符串替代 |
| 失焦 | 实测焦点转移到TextInfoLong/Root/btnGroup/G:btnOK，active=False且不可交互。07.10保护实际命中；原生发起者完整栈未取得，不能断言是导航Update |
| 焦点保护 | 仅本搜索框选中/编辑时拒绝inactive或不可交互目标；放行正常对象与null。关闭、失效、初始化失败必须卸载与恢复；不逐帧抢焦点或全局禁止失焦 |
| 光标 | GenerateCaret后仅平移本框四个顶点到字形中心；空文本用占位文字，缓存缺失用文字矩形。保留高度、宽度、X和闪烁；偏低不是变短 |
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

### 07.30反馈修复与快捷操作依据

- 数值输入在OnValueChanged回调中先设置textComponent.text，再读preferredWidth；Unity回调发生在UpdateLabel之前，不能用旧字符串的宽度。克隆InputField清理继承的caretRectTrans/m_CachedInputRenderer/m_InputTextCache，销毁属于克隆的Caret，规范文字/占位矩形并AssignPositioningIfNeeded/UpdateLabel；不销毁原搜索框Caret，保留已认可GenerateCaret平移补丁。真实光标仍需游戏核对。
- 气运底图Image.Type.Simple/preserveAspect=true。取消修改器自加的同组、lockLuck/globalMax限制，不改配置或成就全局状态；重复、隐藏、无运行类和壶妖类型仍保护。UnitActionLuckAdd.IsCreate原生检查并不使用成就/全局名额条件，不能把随机角色生成条件套入主动修改。
- CharacterActions独立处理AddExp、背包propsCount/UpdateAllShowProps、UnitActionSetPoint、双向SetIntim；传送仅读回位置且动作结果为0才成功，无移动拒绝不增加Pending。原位置按上下文/人物缓存，重复来不覆盖。原生RelationData.SetIntim保留游戏关系上限；读取原浮点值叠加，不能先GetIntim取整，也不能把双方合并一个字段。
- 物品isOverlay只有0/1，不是数量上限；本机PropsData.propsCount setter直接存整数，无各物品最大堆叠配置。用户明确选择9999，因此补满现有可叠加物品到9999，不增加独立物品记录，也不降低已有更大数量。不能称9999为游戏官方最大值。
- CharacterRealmChange读取玩家DataWorld.World.playerLog.upGrade或NPC的npcUpGrade品质历史，按阶段调用原GetAddAttr(conf)累加，目标减原成长差额写DynInt持久setter，寿命growAddLife按月，道点abilityPoint。SetGradeID同步经验基线与事件；不执行ActionUpGradeComplete剧情、材料、随机气运奖励及历史任务回滚。旧材料/丹药/装备额外部分保留，不声称还原所有历史成长率。
- RoleGrade.lockFeature配置120等对应RoleEffect1201=limitGradeExp_1_1；旧瓶颈不注销会继续限制新境界经验。直接改境界通过LuckDel注销这些实际拥有记录，回退通过原LuckData/CreateLuck/NewCreateLuck恢复。满修为只走PropertyData.AddExp，其原生路径处理锁定效果、瓶颈及经验事件，不直接赋exp，也不改境界。
- 降阶的体力/寿命/道点下限及截断差额存人物objData的BHML.realm.debt:*键，下一次升阶补偿截断，避免反复升降加值；失败回退恢复原字段/经验/品质/扩展记录。复杂原生回调仍可能产生不能简单撤销的事件，托管回退不等于游戏事务。数字草稿和境界不能混批保存。
- WorkshopWindow成功取得承载后、Build之前Clear未应用草稿；切页/切人物仍保留当次草稿。真正磁盘保存失败的已应用内存Pending继续保留，原生保存成功回调才清零。预检错误包含具体人物和字段，范围以原生DynInt为准，不为消除报错而盲目扩大寿命/资源范围。
## 按变更验证

### 07.29写入与配置依据（原生游戏结果待验证）

- 用户明确唯一存档是开发测试存档，授权本轮接入真实写入；不终止游戏。使用本机Managed程序集、官方CHM的WorldUnitBase.CreateAction/DataMgr.SaveData和官方RoleCreateFeature/RoleGrade表头及JSON核实接口。新增引用保持Private=False。
- DynInt.value只读。baseValue setter负责钳制及原生变化回调；加法效果保留，目标显示值换算基础差值，年份保存时保留月余数。倍率/固定效果、缺持久setter、范围异常目前拒绝，不通过试写猜系数。全批先准备，应用顺序资源上限优先；异常逆序回退可回退字段并读回原值。不声称游戏原生复杂动作可事务回退。
- 名字原GetName只拼二段数组，保持二段结构；兴趣三个ID合并一次写入，重复拒绝；Destroy旧allHobby、按配置item的竖线/下划线拆分调用WorldFactory.CreateUnitHobby/Init重建，GetHobbyItems比较ID更新缓存。不可直接把cacheHobbyItems设null（原生路径会访问它）。性格按真实配置type候选，写入三个原字段并GetCharacterWeight更新。
- UnitActionRoleUpGrade原生路径修改能力点、寿命、成长属性、恢复资源并记录突破品质/日志及相关逆天气运。SetGradeID只有境界/经验/事件，不能替代完整突破。当前仅下一阶段调用CreateAction；NPC原IsCreate跳过玩家材料要求，玩家材料选择尚未接入；跨境界/降阶/历史品质改写拒绝，境界不可和数字草稿混批。原生动作结果读回核对；不绕过IsCreate强行OnCreate。
- 气运：官方表头type=1先天、2后天、3逆天、4仙人恩赐、5战前（后天）、6壶妖先天、11扩展先天。全库来自roleCreateFeature._allConfList，conceal=1不作为可见配置。原UINPCInfoProperty对type=2且conceal=1过滤，与敖瑶日志8012523/231125吻合；不能转而读取其他玩法DLCUnitData填世界人物。
- 后天/逆天用UnitActionLuckAdd，删除用UnitActionLuckDel注销运行回调和addLuck；先天添加保留bornLuck记录并CreateLuck/NewCreateLuck，删除补齐原动作不负责的bornLuck。隐藏、同组、成就解锁、全局数量、壶妖修士差异检查避免盲加。气运动作即时影响内存，记录待保存；复杂原生调用可能部分执行，错误后强制重读，不谎称无变化。
- DataMgr.isSaveData为静态并发标志，调用g.data.SaveData(Il2CppSystem.Action<bool>)。保留托管/IL2CPP委托，回调只发布托管结果，WorkshopController.Update主线程Poll刷新；成功才清待保存，失败仍可保存内存而不重放草稿；上下文切换的旧回调不清新世界状态。不能承诺游戏不会自动保存。
- Native反汇编依据存于忽略的.tools/writes-disassembly.txt、apply-disassembly.txt、npcinfo-disassembly.txt、binding-disassembly.txt；自动符号共享地址有误标，结论须结合字段偏移/配置/实际调用。诊断文件是研究缓存，不进入ModAssets或发布包。

| 改动 | 相关检查 | 游戏重点 |
| --- | --- | --- |
| 仅文档 | 本地链接、事实、版本与路径 | 无需编译或重测游戏 |
| 布局/资源 | 编译、布局脚本适配时运行 | 当前分辨率、裁切、长文字、切模块；改适配时补1920×1080/1366×768 |
| 输入 | 编译、run-input-scope.ps1 | IME、粘贴/清空、正常失焦、F8、关闭卸载、其他输入框 |
| 读取/名单/气运 | 编译、run-character-reader.ps1、气运改动运行run-character-fates.ps1及run-book-layout.ps1 | 原UI同角色对照、隐藏/死亡、目标失效、切存档；气运长列表/说明及分类 |
| 窗口生命周期 | 编译、run-lifecycle.ps1 | 连续开关、离开地图、共享窗口、失败回退 |
| 新写入 | 字段依赖研究、run-character-save.ps1及相关UI检查 | 仅测试存档、原UI变化、保存重载、对象失效；临时补丁验证关闭及离场释放 |

脚本在`docs/verification/`，使用前核对当前源码适配性。托管替身不覆盖Unity事件顺序、IL2CPP及Windows IME；run-book-layout.ps1会写screenshots模拟预览，必须与实测截图区分。

已有验收摘要仅在项目记忆维护；完整41项数值、IME全流程、多分辨率、跨场景/存档和地图输入隔离尚未全量验收。编译、托管检查、磁盘同步、加载和游戏结果分别记录。

## 小步迭代与文档维护

1. 读项目记忆和长期计划，按任务查本手册及真实代码/官方接口。
2. 定一个可独立验收的小目标，保护基线，完成实现和相关检查。
3. 同步需要的产物，取得游戏证据；没有证据就写待验证。持久写入先分析依赖并用测试存档。
4. 更新项目记忆现状和下一步，在CHANGELOG追加简短记录；范围变化才改计划/调查，结构与流程变化才改本手册。

变更记录统一写：日期/版本、目标与文件、接口或用户证据、编译/同步/游戏结果、未覆盖项及下一步。仅重大问题需要独立专项文档，普通小迭代不新建文件。状态为研究中、实现中、待游戏验证、已验收或延期；用户成功反馈只覆盖当轮需求。

记忆只放当前事实、约束和下一步；代码映射不逐行复制到文档，历史不常驻会话入口。删除重复文件前保留必要证据并修正链接。归档只按需打开，不自动全文读取。
