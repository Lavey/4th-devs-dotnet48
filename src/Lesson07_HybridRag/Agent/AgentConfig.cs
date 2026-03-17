namespace FourthDevs.Lesson07_HybridRag.Agent
{
    /// <summary>
    /// Agent configuration for the Hybrid RAG agent.
    /// Mirrors 02_02_hybrid_rag/src/agent/config.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class AgentConfig
    {
        internal const string Model    = "gpt-4.1-mini";
        internal const int    MaxSteps = 30;

        internal const string Instructions =
            "You are a helpful assistant that answers questions by searching a knowledge base. " +
            "Use the 'search' tool to find relevant information before answering. " +
            "Always ground your answers in the retrieved content.\n\n" +
            "## SEARCH GUIDANCE\n\n" +
            "- Use BOTH 'keywords' (specific terms) AND 'semantic' (natural language question) for best results.\n" +
            "- Run multiple searches with different keywords if the first attempt returns little.\n" +
            "- If information is not found in the knowledge base, say so explicitly.\n\n" +
            "## RULES\n\n" +
            "- Cite which documents and sections you consulted.\n" +
            "- Synthesise information from multiple chunks when relevant.\n" +
            "- Do not fabricate information not present in the retrieved content.";
    }
}
