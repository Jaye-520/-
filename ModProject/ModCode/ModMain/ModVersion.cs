namespace MOD_YtxSQ1
{
    // 版本号唯一来源：界面页脚与全部日志共用，升级只改这里。
    // 此前该字符串散落在4个文件的5处，需手工同步；漏改会让页脚与日志版本不一致，
    // 而本项目正是靠版本号判断“游戏里加载的是哪一版”，因此收敛为单一常量。
    // 注意：WindowInputScope中形如07.10/07.31的编号标记该诊断引入时的版本，属于历史事实，不随升级变动，不属于本常量。
    internal static class ModVersion
    {
        internal const string Current = "07.48";
    }
}
