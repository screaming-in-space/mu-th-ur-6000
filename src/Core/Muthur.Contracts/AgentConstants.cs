namespace Muthur.Contracts;

public static class AgentConstants
{
    public const string TaskQueue = "mu-th-ur-agent";

    public const string RoleUser = "user";
    public const string RoleAssistant = "assistant";
    public const string RoleSystem = "system";
    public const string RoleTool = "tool";

    public const int MaxTurnsBeforeContinueAsNew = 50;

    // Tool names — single source of truth for ToolRegistry and AgentWorkflow.
    public const string ToolPdfExtractText = "pdf_extract_text";
    public const string ToolStoreDocument = "store_document";

    public static string WorkflowId(string agentId) => $"agent-{agentId}";
}
