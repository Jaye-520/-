# 工程手册

> **文档定位**：当前代码导航、可执行流程与防错规则 ｜ **对应版本**：08.10 ｜ **最后更新**：2026-10-05

现状见[项目记忆](../PROJECT_MEMORY.md)，方向见[长期计划](LONG_TERM_PLAN.md)，性能与接入见[性能文档](PERFORMANCE.md)。本手册保留有效技术约束，删除旧版过程、旧统计与已撤销实现。

## 目录

- [代码导航](#代码导航)
- [构建与验证](#构建与验证)
- [官方接口与原生包装](#官方接口与原生包装)
- [页面资源与输入](#页面资源与输入)
- [写入与保存](#写入与保存)
- [道具与气运](#道具与气运)
- [功法](#功法)
- [境界](#境界)
- [关系与立绘](#关系与立绘)
- [文档与Git](#文档与git)

## 代码导航

源码根ModProject/ModCode/ModMain/。调用关系ModMain→WorkshopController→WorkshopWindow→BookView；读取/草稿/写入职责分离，不提供反射任意字段写入。

| 文件 | 职责 |
| --- | --- |
| ModMain.cs | 初始化、帧计时器、重复释放和异常熔断 |
| ModVersion.cs | 版本号唯一来源；界面页脚与全部日志共用，升级只改此处 |
| Core/WorkshopController.cs | 大地图条件、F8、输入编辑保护、窗口刷新 |
| UI/WorkshopEntrances.cs | 原生双入口挂载、单值坐标转换、出厂位置常量、点击核对、独立于内容熔断的生命周期 |
| UI/WorkshopWindow.cs | 原生UITextInfoLong承载、共享窗口所有权、开关与释放 |
| UI/BookView.Pages.cs | 六页预建注册、活动/休眠/失效生命周期、配置缓存与气运行复用 |
| Services/WorkshopPerformance.cs | 有界CPU计时样本、分位数及自建控件计数 |
| UI/BookView.cs | 七项导航、四页共享人物与对象栏、名单/搜索/立绘及属性布局 |
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
| Services/CharacterActions.cs | 六资源恢复、满修为、当前人物现有叠加物品补量、NPC传送及双向好感度 |
| Services/CharacterRelations.cs | 原生身份/双向好感读取、已故记录、纯托管人物/连线/确认快照 |
| Services/RelationOperations.cs | 两人身份和数据容器复核、好感setter、原生建立/解除动作及Pending |
| Services/RelationLayout.cs | 分类整数槽布局、循环/重复人物合并后的不重叠位置 |
| UI/BookView.Relations.cs | 关系图、筛选、拖动缩放、节点菜单、人物选择与确认弹窗 |
| UI/BookView.RelationPictures.cs | 可见人物逐个生成静态小立绘、128张跨休眠LRU纹理缓存及释放 |
| Services/ItemInventory.cs | 道具配置、背包快照、原生发放/逐件删除、境界药效补给与人物上下文预检 |
| UI/BookView.Items.cs | 道具三栏、物品图鉴、背包网格与原生内容悬浮、绑定人物的添加/清空弹窗 |
| UI/BookView.ItemMenu.cs | 背包右键菜单、按唯一ID修改/增加数量及移除单项、菜单释放 |
| UI/BookView.Martials.cs | 背包秘籍/已学阶段切换、五类秘籍弹窗、多选添加和有来源的搭配获取 |
| Services/CharacterMartials.cs | 基础/传承功法配置与拥有记录、奖励工厂/传承身份生成及背包预检/回读 |
| Services/CharacterMartials.Operations.cs | 学习/修炼原生生命周期、动态词条容量/完整数组、候选草稿与保存回读 |
| UI/BookView.MartialEditing.cs | 学习确认、直接装备/心法槽位下拉、详情内词条草稿与保存，目标绑定 |
| Services/MartialBuilds.cs | 已核对攻略的搭配清单、出处/依赖说明、全套境界检查 |
| Services/CharacterRealmChange.cs | 境界配置成长差额、品质/经验/瓶颈及降阶截断记录 |
| UI/BookView.Editing.cs | 原位输入、独立选择按钮、下拉浮层及保存校验反馈 |
| Services/CharacterPortrait.cs | 原生身份解析与ConfDramaNpc.CreateTexture分流，普通修士保持原路径 |
| Example/Example.cs | 参与编译的官方示例；不是产品功能清单 |
| Properties/AssemblyInfo.cs | 程序集元数据；产品版本取`ModVersion.Current`，AssemblyVersion/FileVersion保持纯数字 |

## 构建与验证

工程身份MOD_YtxSQ1、目标net472，主类ModMain提供Init/Destroy。新增游戏引用Private=False，源码/嵌入资源登记csproj；新文件在导航表和受影响harness编译清单登记。partial按职责拆分，不机械按行数切方法；保持原分支和报错顺序。

```powershell
./build.ps1                         # 编译、旧DLL备份、同步游戏加载目录、哈希校验
./build.ps1 -NoDeploy               # 编译而不部署
./docs/verification/run-all.ps1      # 脚本卫生、工程一致性及全部托管检查
./docs/verification/run-all.ps1 -Build
```

使用项目脚本，不用系统MSBuild。Release只留本模组DLL；debug/Mod_YtxSQ1/ModCode/dll是官方游戏加载目录，不代表缩水版本。build读取AssemblyInformationalVersion与ModVersion.Current交叉校验，编译失败不覆盖成功产物，部署前备份至.tools/backups/before-<版本>，同名备份不覆盖。只部署DLL。磁盘同步不证明游戏已热重载，核对日志/页脚，不擅自终止游戏。

产品交付内容变化须升ModVersion.Current，界面/日志/产物引用唯一来源。WindowInputScope诊断中的版本编号是引入编号，无须随版本改写。

.ps1保持UTF-8 BOM及CRLF以兼容Windows PowerShell5.1，.cs/.md按.gitattributes保持LF。原生字节编码不能为改行尾做有损文本往返；5.1的.NET文件API相对路径须解析绝对路径。run-all以每个子进程退出码判定，任何失败阻止交付，不累加旧统计。

代码结构/行为变化运行全门禁；仅文档检查事实、引用、导航表和行尾，不需重新操作游戏。布局预览是GDI替身，不是游戏截图。输入改动另核对IME/粘贴/焦点，写入改动另用获授权测试存档。run-mutations会临时修改源码，仅人工使用并保护脏工作区；不从干净Git假设恢复用户未提交修改。

## 官方接口与原生包装

本机资料：游戏根Mod/modFQA/代码编写教程（含模板与GGBH_API.chm）、配置修改教程、资源教程和MOD模板；docs/api-reference是CHM解包缓存。新增原生调用核对MelonLoader/Managed程序集及必要的GameAssembly实现，不能套网上新版签名。

官方ModProject.cache保存soleID等编辑器身份，ModData.cache保存配置/命名空间；ModAssets随导出，不放工程笔记。配置源文件表名一致，MID&用于xlsx的excelMID偏移；只读官方表不是运行时新增配置。Unity资源工程采用官方2020.3系列路线，当前窗口仍使用原生UITextInfoLong承载，独立预制体迁移未实现。

- IL2CPP包装相同原生对象可能有不同托管引用。Unity控件登记/识别/注销用Unity ==，人物/容器/字段绑定用当前Pointer，不能用ReferenceEquals或List.Contains替代原生身份。
- GetWorldCorners的Il2CppStructArray输出不是普通Vector3[]写回；读取真实包装索引或使用单值TransformPoint/InverseTransformPoint。
- 原生泛型TryGetValue的byref float包装可能错误构造Single(IntPtr)，使用核实过的ContainsKey+索引路径。DataStruct<string,int>.t2门禁码以实际字段元数据偏移读取，不用错误包装getter或固定偏移。
- PropsData与MartialData是读取视图关系，使用PropsData.To<MartialData>()；不得把TryCast为空当成普通物品，替身不得虚构继承。
- 包装IsVirtual不是原生虚派发的完整依据，核对il2cpp_object_get_virtual_method；不能由基类空Destroy推导子类无副作用。
- 原生getter也可能抛错，记录初始化/过滤/预读取放进单条保护。未知类型只读，不伪造类别；集合失效和native访问违规不由托管catch保证恢复。

## 页面资源与输入

页面生命周期/计量规范见性能文档。重开先清旧导航再复位索引、强制激活属性页，即使关闭前已是属性。保存不参与页面选中。关闭休眠一套静态控件，动态模型/相机/图标和输入补丁释放；所有权/世界/画布失效及销毁最终释放。原生窗口已被别处占用时拒绝挂接，只关闭自己持有的窗口。

创建对象先局部接管，配置/挂载成功才赋长期字段；失败释放局部并保留原异常。清理先拆引用，隐藏与Destroy独立尝试，一步异常不跳过其他资源；访问childCount前判断Unity对象存活。动态游戏引用不得跨休眠保存。

PaperSkin主线程懒持有四张共享纹理，BookView借用；ModMain.Destroy先释放窗口再ClearCache。字体/关闭按钮纹理由视图拥有，最终释放；游戏字体回退是正常路径，失败字体不反复创建。动画0.16秒未缩放时间Smoothstep，仅透明度，不改布局；期间阻挡背景并禁操作，关闭可中断。

输入从原生ModWorkshopUpload模板克隆，清旧EventTrigger/验证器/回调，清理仅属于克隆的Caret与mesh/cache。仅活动页输入登记，Attach/Detach归窗口所有；仅本输入正在编辑时拒绝inactive/不可交互焦点目标，正常对象/null放行，不逐帧抢焦点。日志“已阻止无效焦点转移”是保护命中，不是新异常。

Text字形首顶点与Caret原点的像素对齐不能混用；Text字形中心转换到Caret局部，保留X/宽度/闪烁，超文字内边距时缩高并夹紧。OnValueChanged先更新textComponent.text再测preferredWidth。IME组合不提交，正常失焦合法输入确认，Escape撤销当前输入或先关浮层。

入口位置为代码常量：主入口textSPValue右边中点+40，NPC归一化中心0.2542969/0.746458352；56参照单位×参照缩放。无F9调整或文件覆盖。半秒同步入口/关闭状态，F8与保存Poll逐帧，异常控制器停止后由明确入口/F8恢复，不每帧重建或重放修改。

## 写入与保存

上下文绑定玩家原生身份/ID与DataMgr。每项写入/回读/Undo以及可能触发回调的嵌套原生步骤前后重新解析目标，核对unit/data/unitData、相关父容器与字段Pointer、内容基准及保存状态。Pointer相同仍须内容冲突检查；对象替换或世界改变停止旧引用操作，不污染新Pending，不盲Undo。

全批先预检，最后一次回调后再次完整回读。可回退步骤逆序Undo并核验；未知原生部分执行保留Pending，报告可能生效，不宣称事务。数字与境界草稿不能混批。

DynInt.value只读，明确持久baseValue setter与派生效果；差额表达式用long完整计算、checked转换后核范围，资源上限优先。倍率以独立DynInt复制计算规则、精确求基础整数，不复制事件或试写人物；固定覆盖/锁定/非单调/无精确解拒绝，应用前重求、单次写入后回读。满状态保留加法效果，不用max值直接覆盖baseValue。

DataMgr.SaveData回调只发布托管结果，主线程Poll更新UI/Pending；generation去重，旧世界或重复回调不能清新状态。派发异常若已收到同步结果仍等待Poll，未收到结果才保留显式重试。成功回调才清Pending；永久不回调保持Busy，无安全取消契约，不超时重试。原生自动保存可落盘即时内存修改。

姓名保持原二段结构，年龄/寿命年转月保留余数。兴趣三槽合并写入并绑定运行集合/顺序/身份，逐项移除自己的旧记录，不Clear当前列表；按配置重建运行类，不置cacheHobbyItems为null。初始化/集合冲突后保留未知状态并隔离再次修改，不能靠退出UI清隔离。性格按真实type候选、写原字段并更新权重。

## 道具与气运

AddProps(id,count,"")的空字符串触发原生唯一ID生成，null不等价。原生工厂维护编号/配置索引、附加效果与可见列表，不手拼记录；返回索引/编号/数量及批次最终结果必须回读。新增格数按GetGridCurNum(true)/GetGridMaxNum预检，不用可见条目数代替原生容量；满格工厂正常拒绝不能误判成成功。

数量修改按唯一ID/原数量/soleIDHashData绑定同一记录，保留附加值和索引。isOverlay仅0/1；允许正整数至int.MaxValue，0不隐式删除，追加先判溢出。满材料9999是快捷目标，按配置ID用long合计批次，不降低更大记录。

Read合并原生可见项与allProps中的隐藏资源，按soleID去重，隐藏红格前置且可修改数量；删除/清空明确excludeHidden，排除装备并每步重核可见性。补给按实际原生药效/境界选两药各20，合计格数/同药40数量先预检。后续每步重核同一人物与背包容器。

背包显示指纹及格键含附加数据/Hidden/原生排序和容量；只复用可见格，尾部假空位禁用，缩短后立即夹紧滚动。原生图标创建失败清半成品，CloseIconInfo失败不阻断其他销毁。目录介绍按首次悬停缓存纯文本，来源失效重读。

技能书识别propsType并To<MartialData>()，GetDesc后用真实BattleSkillValueData和GetDescRichText格式化，不删除模板变量冒充修复。物品提示移出即关、不接射线；长文只截获所属ScrollRect滚轮，关闭恢复。图鉴提示挂自己的遮罩，不能被浮层遮住。原生售价<0显示不可出售。

气运读取bornLuck/addLuck，conceal隐藏效果不展示；type=3逆天，1/6/11先天，其余按来源。介绍用真实人物/LuckData的ConfRoleCreateFeatureEx.tips并隔离回退；不可把角色随机生成的同组/成就/全局名额条件套主动修改。重复、无运行类和壶妖差异继续保护。先天维护bornLuck及CreateLuck/NewCreateLuck，后天/逆天走原生动作；失败不恢复整个旧快照，不假定返回null表示无部分运行对象。

拥有/候选/图鉴复用自绘色底与品阶，特殊2804/2805/2806天级雕饰按池绑定启闭，不改配置level。气运提示允许0.12秒移入滚动，物品提示没有此宽限。

## 功法

目录使用本机基础/传承配置，普通className=1；传承用(baseID,suitID)选择键，奖励约束由实际配置链解析，缺依据拒绝生成。按捕获境界奖励工厂/NewMartial入包，传承走suitID setter维护附加数据，不改名称伪造；整批容量/身份/境界检查及最终回读，失败不重复全套。完整书名先直搜，未命中仅已知原生前缀反查普通基型，不猜流派/词条。

学习调用GetStudyTip保留原生门禁，读取真实t2元数据码；StudyActionMartialProp消耗原秘籍。原对象count由1变0后soleIDHashData变化，学习前克隆计算数量0的预期完整签名；后验同指针/完整签名、原包消耗和已学归属，刷新后再验，不能只看数量增加。

修炼按实际词条数GetItems，GetExp用功法境界grade而不是品质level；按原生差额AddMartialExp，已装备重建运行对象/UpdateConf。装备通过原生IsCreateAction/CreateAction，使用skillLeft/skillRight/step/ultimate/abilitys，不混用物品IsEquipProps。恢复原槽前确认仍为空，不覆盖回调新增装备。

容量/品质按GetGradeMin、GetPrefixCount、GetLevel动态计算，不固定11槽/600经验。最终词条集合按multi/require/前置/互斥校验，随机weight不限制手动候选；空短强度数组拒绝，原未改值保留。

GetValues含六项头、词条ID、-1、对应强度及尾数据；恰六项头或仅-1可合法零词条。SetPrefixsID不扩容；克隆追加重建完整数组，六项无分隔符追加+3，已有分隔符追加+2，保留头/旧强度/尾/经验/传承并SetValues失效缓存。保存前重核身份与全部内容，已装备先卸下、写回/UpdateConf、按原槽重装；重装失败说明已应用、保留Pending，不假回滚。

异常无soleID条目只读，UI选择另用非零原生指针，同ID/签名不能替代写入身份。轻量刷新探针不取代完整写入校验。官方1657条参考CSV位于[词条目录](reference/martial-prefix-catalog.csv)，不作为运行时来源。搭配仅固定四套，出处保留于MartialBuilds；不自动装备气运/套装/器灵。

## 境界

境界编辑是配置成长差额，不等于自然突破奖励。读取玩家/NPC品质历史与当前已知来源配置，按GetAddAttr累计成长、寿命growAddLife按月、道点abilityPoint；SetGradeID同步经验/事件，注销旧锁经验气运，但不消费材料、随机奖气运或回退历史任务。

同grade/phase/quality可能有多条成长配置，历史quality/luck不足以区分。记录已知来源到objData并验证来源，不能用首条配置替代已知历史或由当前属性猜自然成长。绑定所有相关数据容器/动态字段/历史字典、记录Pointer和内容戳，每步及Undo核对键集合、顺序、quality/luck，不Clear重建历史。

BHML.realm.debt保存ideal-after的非正截断差额，零删除、负值InvariantCulture写读；null/非数值/正数/溢出拒绝，不吞错补偿。成长来源、品质、债务同步回读；未知原生事件不宣称可完整恢复。

## 关系与立绘

遍历RelationData真实结构，不调用可能清理死亡记录的GetRelation(type,true)代替只读。死亡人物从unitDie解析；朋友/仇怨按本机配置阈值而非可自由编辑身份。好感指向玩家和其他人物分别读取，按SetIntim的0.##量化目标，最终双向相等；未改值不记Pending，仅Undo实际尝试侧。

结构操作捕获完整影响计划，绑定全部关联人物/容器/结构/原好感，全批原生门禁后每步前后及最终回读。建立血亲要求父母槽空、无祖先循环，明确姓氏/家谱副作用；不替换既有家谱或直接设置私生/同胞。解除夫妻/道侣/师徒/义亲及血亲按整体计划双向处理，不直接改列表。

UnitActionRelationSet(None,0)不会维护第三人同胞，因此父母—子女解除单独规划失去共同父母依据的既有同胞；保留另一父母和仍有依据同胞。重复/不对称/血亲多重冲突/连带其他身份/剧情保护/已故失效关联整批拒绝。共有父母同胞不得孤立解除，须从父母—子女入口；异常保留Pending，不补偿、不盲重试。原生研究缓存.tools/relief-0806-native.txt。

图按可达展开集合/ID去重，节点/连线按键复用，仅拓扑变化重排。线宽按2/relationZoom补偿短边，长边保留；静态小立绘160×154匹配80×77卡片。等待原生模型完成回调，下一次刷新预渲染、测完整Renderer边界拟合，单任务、10秒超时，取消后迟到回调只销毁旧模型。

原生ModelRenderTexture把模型设为相机子物体，取景前SetParent(null,true)保留世界姿态，随后拟合相机；模型/renderer分别finally释放。五朵金花按GetFiveFlowerDramaNpcID→ConfDramaNpc.CreateTexture和avatar/modelID分流，不强取普通拼装数据或新增第二渲染器。静态纹理缓存键/LRU/最终释放见性能文档。

## 文档与Git

入口仅记现在与边界，计划仅记方向，手册记有效规则，变更记录保留精简交付证据。过程流水、旧统计、撤销方案和重复报告移出日常文档；必要时按需查Git历史或.tools备份，不全文带入新上下文。删除重复文档前修正链接。

Git跟踪源代码、源资源、验证脚本与当前文档，忽略.tools/debug/bin/obj及游戏依赖；截图feedback为问题依据、layout为模拟。旧zip归档仅按需追溯。ModProject/ModProject.cache是编辑器活文件，不随无关提交夹带，不回滚、不忽略；有意义配置变化另行提交。

提交前检查status、暂存范围与diff --check，保留用户修改，不reset --hard或批量清目录。用户要求本地提交不意味着推送，无远程时不宣称push。全局AI记忆只在明确要求时写指定扩展note，不直接修改记忆索引。
