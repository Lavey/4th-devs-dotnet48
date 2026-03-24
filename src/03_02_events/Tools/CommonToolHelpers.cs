using System.IO;

namespace FourthDevs.Events.Tools
{
    public static class CommonToolHelpers
    {
        public static string WorkspaceDir
        {
            get { return Config.EnvConfig.ProjectPath; }
        }

        public static string WorkspaceRootDir
        {
            get { return Config.EnvConfig.WorkspacePath; }
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
