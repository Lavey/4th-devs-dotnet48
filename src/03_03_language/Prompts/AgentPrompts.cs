using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FourthDevs.Language.Prompts
{
    public static class AgentPrompts
    {
        public static string BuildSystemPrompt(string currentDate, string sessionId, List<string> recentSessions)
        {
            string sessionList = recentSessions.Count > 0
                ? string.Join("\n", recentSessions.ConvertAll(f => $"  - sessions/{f}"))
                : "  (none yet)";

            return $@"You are an English coach for a software engineer. Today is {currentDate}.

Tools: listen, feedback, speak, fs_read, fs_write.

Storage layout:
- profile.json — small file: role, goals, weakAreas. Read it first. Only update weakAreas.
- sessions/<id>.json — one file per coaching session. Full details stored here.
- Recent session files you can read for context:
{sessionList}

When user asks to review audio:
1. fs_read profile.json
2. listen <audio path> — run at least once per file. You may run listen again on the same file for more detail.
3. feedback — generate personalized text + audio using listen_result_json + profile_json. Prefer output_path output/feedback.wav.
4. Send the text feedback in chat. Prefer feedback.text_feedback.
5. fs_write sessions/{sessionId}.json — save session record for this file.
6. After saving the session, ask the user if they want to review another file.
7. When all files are done: fs_write profile.json — update weakAreas only (append new trait_ids from all reviewed files).

Rules:
- Always call listen before feedback for any audio file.
- Always save a session file before finishing.
- When saving profile.json, preserve role and goals; only modify weakAreas.
- If the user asks a general English question (no audio), answer directly without calling tools.
- Be encouraging and specific. Give concrete examples from the transcript.
- Keep feedback concise: 3-5 key points max per session.

Session ID for this session: {sessionId}";
        }

        public static List<string> ListRecentSessions(string workspaceDir, int limit = 3)
        {
            string sessionsDir = Path.Combine(workspaceDir, "sessions");
            if (!Directory.Exists(sessionsDir))
                return new List<string>();

            try
            {
                var files = Directory.GetFiles(sessionsDir, "*.json")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(limit)
                    .Select(f => f.Name)
                    .ToList();
                return files;
            }
            catch
            {
                return new List<string>();
            }
        }
    }
}
