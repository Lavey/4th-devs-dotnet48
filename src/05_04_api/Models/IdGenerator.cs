using System;

namespace FourthDevs.MultiAgentApi.Models
{
    /// <summary>
    /// Generates prefixed unique IDs for all domain entities.
    /// Format: prefix + UUID without hyphens (e.g., acc_a1b2c3d4...)
    /// </summary>
    internal static class IdGenerator
    {
        public static string NewAccountId()           { return "acc_" + NewUuid(); }
        public static string NewTenantId()             { return "ten_" + NewUuid(); }
        public static string NewMembershipId()         { return "mem_" + NewUuid(); }
        public static string NewAgentId()              { return "agt_" + NewUuid(); }
        public static string NewRevisionId()           { return "rev_" + NewUuid(); }
        public static string NewWorkspaceId()          { return "ws_"  + NewUuid(); }
        public static string NewSessionId()            { return "ses_" + NewUuid(); }
        public static string NewThreadId()             { return "thr_" + NewUuid(); }
        public static string NewMessageId()            { return "msg_" + NewUuid(); }
        public static string NewRunId()                { return "run_" + NewUuid(); }
        public static string NewJobId()                { return "job_" + NewUuid(); }
        public static string NewItemId()               { return "itm_" + NewUuid(); }
        public static string NewToolExecutionId()      { return "tex_" + NewUuid(); }
        public static string NewEventId()              { return "evt_" + NewUuid(); }
        public static string NewFileId()               { return "fil_" + NewUuid(); }
        public static string NewUploadId()             { return "upl_" + NewUuid(); }
        public static string NewApiKeyId()             { return "key_" + NewUuid(); }
        public static string NewWaitId()               { return "ask_" + NewUuid(); }
        public static string NewMemoryRecordId()       { return "mrec_" + NewUuid(); }
        public static string NewMemoryRecordSourceId() { return "msrc_" + NewUuid(); }
        public static string NewFileLinkId()           { return "flnk_" + NewUuid(); }
        public static string NewIdempotencyKeyId()     { return "idk_" + NewUuid(); }
        public static string NewMcpServerId()          { return "mcp_" + NewUuid(); }
        public static string NewPreferenceId()         { return "prf_" + NewUuid(); }

        private static string NewUuid()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
