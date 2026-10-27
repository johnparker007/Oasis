# Oasis Player agent guidance

Read the repository-root `AGENTS.md` and `WindowsNetProjects/OasisEditor/00_CURRENT_PRIORITY.md`. For Fabric/Amber integration, read `WindowsNetProjects/OasisEditor/OasisEditor.Mcp/PLAYER_FABRIC_IMPACT.md` and use the Editor implementation as the reference. Read only architecture sections relevant to the task.

## Unity compatibility

Target the Unity project's supported default/recommended scripting environment:

- Use C# 9 syntax and block-scoped namespaces.
- Do not use file-scoped namespaces, global using directives, `required` members, or other C# 10+ features.
- Do not change scripting runtime, API compatibility level, or related .NET settings merely to accommodate newer generated syntax.
- Keep Unity-side contracts and DTOs valid C# 9 even when corresponding Editor code supports newer features.

## Runtime and verification

Preserve authored machine behavior and real Fabric/Amber execution paths. Verify current provider and visual-format limitations before designing around historical restrictions; see `Docs/AGENT_PROJECT_CONTEXT.md` at the repository root.

Use supported local Unity tools and focused automated checks. Do not launch visible Unity/Player sessions, capture the desktop, or perform manual input tests without the authorization required by the root guidance. Preserve user-owned sessions and content. Report runtime checks separately from visual, audio, and manual verification.
