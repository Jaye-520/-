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
| Services/CharacterPortrait.cs | 原生身份解析与ConfDramaNpc.CreateTexture分流，普通修士保持原路径 |
| Example/Example.cs | 参与编译的官方示例；不是产品功能清单 |
| Properties/AssemblyInfo.cs | 程序集元数据；与界面版本分别核对 |

调用关系：ModMain → WorkshopController → WorkshopWindow → BookView。视图使用只读服务与PaperSkin；窗口负责输入补丁生命周期。当前没有独立Modules目录或持久写入服务；业务接入现有边界，按需要增加抽象，不为整理文档重构稳定代码。

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
| 气运 | propertyData.bornLuck/addLuck记录拥有条目；roleCreateFeature.GetItem及ConfLocalText.GetText读名称，官方表头type=3为逆天改命、type=5战前气运算后天，其余按来源分类。介绍用ConfRoleCreateFeatureEx.tips(item,选中unit,真实LuckData,2)，与原生UIAddLuckItem的bgType一致；失败回退基础介绍。行挂UISkyTipEffect.InitData(text,Vector3零偏移)，由原生UIEventListener处理进入/离开，行无Button；滚动/重建/切页/释放仅显式隐藏本视图仍处于悬停的提示。原生OnDisable触发PointerExit，OnDestroy解绑；托管替身不证明实际提示布局。未知ID保留，读失败不展示部分列表，不写游戏对象 |
| 界面资源 | 圆形×的64×64自绘纹理归BookView所有，随视图释放；不得借用未知Sprite或漏掉失败清理。正文统一BodyTop，左右共底边须随适配改动检查 |
| 重开读取 | WorkshopWindow每次打开新建BookView，Build→Select(0)重读名单并强制读取当前人物；打开期间属性每秒更新，名单不逐帧扫描，无独立刷新按钮 |

同一症状连续两次修复失败，先补真实事件证据；区分安装、分支命中和行为改变。不要把禁用控件状态、托管测试或模拟预览当成原生游戏验证。

## 按变更验证

| 改动 | 相关检查 | 游戏重点 |
| --- | --- | --- |
| 仅文档 | 本地链接、事实、版本与路径 | 无需编译或重测游戏 |
| 布局/资源 | 编译、布局脚本适配时运行 | 当前分辨率、裁切、长文字、切模块；改适配时补1920×1080/1366×768 |
| 输入 | 编译、run-input-scope.ps1 | IME、粘贴/清空、正常失焦、F8、关闭卸载、其他输入框 |
| 读取/名单/气运 | 编译、run-character-reader.ps1、气运改动运行run-character-fates.ps1及run-book-layout.ps1 | 原UI同角色对照、隐藏/死亡、目标失效、切存档；气运长列表/说明及分类 |
| 窗口生命周期 | 编译、run-lifecycle.ps1 | 连续开关、离开地图、共享窗口、失败回退 |
| 新写入 | 字段依赖研究及针对性检查 | 仅测试存档、原UI变化、保存重载、对象失效；临时补丁验证关闭及离场释放 |

脚本在`docs/verification/`，使用前核对当前源码适配性。托管替身不覆盖Unity事件顺序、IL2CPP及Windows IME；run-book-layout.ps1会写screenshots模拟预览，必须与实测截图区分。

已有验收摘要仅在项目记忆维护；完整41项数值、IME全流程、多分辨率、跨场景/存档和地图输入隔离尚未全量验收。编译、托管检查、磁盘同步、加载和游戏结果分别记录。

## 小步迭代与文档维护

1. 读项目记忆和长期计划，按任务查本手册及真实代码/官方接口。
2. 定一个可独立验收的小目标，保护基线，完成实现和相关检查。
3. 同步需要的产物，取得游戏证据；没有证据就写待验证。持久写入先分析依赖并用测试存档。
4. 更新项目记忆现状和下一步，在CHANGELOG追加简短记录；范围变化才改计划/调查，结构与流程变化才改本手册。

变更记录统一写：日期/版本、目标与文件、接口或用户证据、编译/同步/游戏结果、未覆盖项及下一步。仅重大问题需要独立专项文档，普通小迭代不新建文件。状态为研究中、实现中、待游戏验证、已验收或延期；用户成功反馈只覆盖当轮需求。

记忆只放当前事实、约束和下一步；代码映射不逐行复制到文档，历史不常驻会话入口。删除重复文件前保留必要证据并修正链接。归档只按需打开，不自动全文读取。
