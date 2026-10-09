# 八荒秘录

鬼谷八荒综合修改器 MOD 源码，官方项目身份为 `MOD_YtxSQ1`，当前代码版本为 **08.17**。

包含属性、道具、功法、关系及功能页面，采用模块化方式逐步接入业务。功能现状与限制见 [PROJECT_MEMORY.md](PROJECT_MEMORY.md) 和 [docs/FEATURES.md](docs/FEATURES.md)，后续方向见 [docs/LONG_TERM_PLAN.md](docs/LONG_TERM_PLAN.md)。计划不代表功能已经实现。

## 源码结构

- `ModProject/ModCode/ModMain/`：C# 主工程、服务与界面代码。
- `ModProject/ModRes/`、`Art/`：Unity 资源工程与美术源文件。
- `ModProject/ModProject.cache`、`ModProject/ModData.cache`：官方模组工程配置。
- `docs/verification/`：托管验证程序与运行脚本。
- `build.ps1`：构建与调试部署脚本。

## 构建

使用 Windows PowerShell，准备匹配的游戏环境；本项目当前开发环境为游戏版本 `v1.2.113.259`。游戏程序集与本地工具依赖不随源码仓库分发。脚本会下载所需的 .NET Framework 4.7.2 参考程序集及编译器。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -GameRoot '你的游戏目录' -NoDeploy
```

省略 `-NoDeploy` 会同步到本地调试包。完整工程要求与引用约定见 [docs/ENGINEERING.md](docs/ENGINEERING.md)。

## 验证状态

08.17 已完成构建与 12 套托管验证，共 2471 项检查通过；编译零警告、零错误。针对创建角色/读档异常已撤除背包扩容的全局构造补丁，实际游戏恢复和新增功能表现仍待游戏内复测。编译与托管验证通过不等于游戏验收通过。

日志、本地工具、游戏程序集、构建产物及个人凭据由 `.gitignore` 排除。
