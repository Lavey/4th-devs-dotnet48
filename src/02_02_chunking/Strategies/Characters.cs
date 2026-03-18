using System.Collections.Generic;

namespace FourthDevs.Lesson07_Chunking.Strategies
{
    /// <summary>
    /// Character-based chunking: splits text into fixed-size windows with overlap.
    ///
    /// Mirrors 02_02_chunking/src/strategies/characters.js (i-am-alice/4th-devs)
    /// </summary>
    internal static class Characters
    {
        private const int DefaultChunkSize    = 1000;
        private const int DefaultChunkOverlap = 200;

        /// <summary>
        /// Splits <paramref name="text"/> into fixed-size character windows.
        /// </summary>
        internal static List<Chunk> ChunkByCharacters(
            string text,
            int size    = DefaultChunkSize,
            int overlap = DefaultChunkOverlap)
        {
            var chunks = new List<Chunk>();
            int start  = 0;
            int idx    = 0;

            while (start < text.Length)
            {
                string content = text.Substring(start,
                    System.Math.Min(size, text.Length - start));

                chunks.Add(new Chunk
                {
                    Content  = content,
                    Metadata = new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["strategy"] = "characters",
                        ["index"]    = idx,
                        ["chars"]    = content.Length,
                        ["size"]     = size,
                        ["overlap"]  = overlap
                    }
                });

                start += size - overlap;
                idx++;
            }

            return chunks;
        }
    }
}
