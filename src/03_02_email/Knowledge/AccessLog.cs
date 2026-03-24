using System.Collections.Generic;
using FourthDevs.Email.Models;

namespace FourthDevs.Email.Knowledge
{
    /// <summary>
    /// Static list of knowledge base access records for auditing.
    /// </summary>
    public static class AccessLog
    {
        public static readonly List<KnowledgeAccess> Log = new List<KnowledgeAccess>();
    }
}
