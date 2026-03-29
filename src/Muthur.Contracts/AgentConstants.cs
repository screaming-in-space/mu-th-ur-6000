namespace MuThUr.Contracts;

public static class AgentConstants
{
    public const string TaskQueue = "mu-th-ur-agent";

    public const string RoleUser = "user";
    public const string RoleAssistant = "assistant";
    public const string RoleSystem = "system";
    public const string RoleTool = "tool";

    public const int MaxTurnsBeforeContinueAsNew = 50;

    public static string WorkflowId(string agentId) => $"agent-{agentId}";
}
