using System;
using FourthDevs.Common;

namespace FourthDevs.Events.Core
{
    /// <summary>
    /// Cached wrapper around the Responses API client from Common.
    /// </summary>
    internal static class OpenAIClient
    {
        private static readonly Lazy<ResponsesApiClient> _instance =
            new Lazy<ResponsesApiClient>(() => new ResponsesApiClient());

        public static ResponsesApiClient Instance
        {
            get { return _instance.Value; }
        }
    }
}
