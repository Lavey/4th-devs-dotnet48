namespace FourthDevs.Events.Helpers
{
    /// <summary>
    /// Workspace navigation instructions constant appended to agent system prompts.
    /// </summary>
    internal static class WorkspaceNav
    {
        public const string WORKSPACE_NAV_INSTRUCTIONS =
@"## Workspace Navigation

The workspace directory contains the following structure:
- `project/` – deliverables go here
- `tasks/` – task markdown files with YAML frontmatter
- `agents/` – agent template definitions
- `events/` – event logs
- `goal.md` – the goal contract

When reading or writing files, always use paths relative to the workspace root.
Prefer placing final deliverables in the `project/` subdirectory.";
    }
}
