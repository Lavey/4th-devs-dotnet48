using System.IO;

namespace FourthDevs.Events.Tools
{
    public static class CommonToolHelpers
    {
        public static string WorkspaceDir
        {
            get { return Config.EnvConfig.Paths.WorkspaceDir; }
        }

        public static string ImageAssetsDir
        {
            get { return "assets"; }
        }

        public static string AsWorkspaceSafePath(object value)
        {
            return Helpers.PathHelper.AsRelativeSafePath(WorkspaceDir, value);
        }
    }
}
