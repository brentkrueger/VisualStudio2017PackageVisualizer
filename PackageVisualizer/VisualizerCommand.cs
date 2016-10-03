using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PackageVisualizer
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class VisualizerCommand
    {
        const string MessageBoxTitle = "Nuget Package Visualizer";

        #region Command Plumbing

        /// <summary>
        /// Command ID.
        /// </summary>
        private const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        private static readonly Guid CommandSet = new Guid("cb4a6e94-bba7-4a6f-9a37-6fe4e4aa8434");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly Package _package;

        /// <summary>
        /// Initializes a new instance of the <see cref="VisualizerCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        private VisualizerCommand(Package package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            _package = package;

            OleMenuCommandService commandService = ServiceProvider.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                var menuCommandId = new CommandID(CommandSet, CommandId);
                var menuItem = new MenuCommand(CreatePackageDiagram, menuCommandId);
                commandService.AddCommand(menuItem);
            }
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private IServiceProvider ServiceProvider => _package;

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        public static void Initialize(Package package)
        {
            new VisualizerCommand(package);
        }

        #endregion

        private void CreatePackageDiagram(object sender, EventArgs e)
        {
            var vsEnvironment = Package.GetGlobalService(typeof(DTE)) as DTE2;
            var solutionFullName = vsEnvironment.Solution.FullName;
            if (!string.IsNullOrEmpty(solutionFullName))
            {
                if (SolutionIsLoaded(vsEnvironment.Solution))
                {
                    var visualizer = new NugetPackageVisualizer(vsEnvironment);
                    var dgmlFilePath = Path.GetDirectoryName(solutionFullName) + @"\NugetVisualizerOutput.dgml";
                    visualizer.GenerateDgmlFile(dgmlFilePath);
                    vsEnvironment.ItemOperations.OpenFile(dgmlFilePath);
                }
                else
                {
                    VsShellUtilities.ShowMessageBox(
                    this.ServiceProvider,
                    "Initialization Error: Please make sure solution and all projects are totally initialized before running this extension.",
                    MessageBoxTitle,
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
                }
            }
            else
            {
                VsShellUtilities.ShowMessageBox(
                    this.ServiceProvider,
                    string.Format(CultureInfo.CurrentCulture, "You must have an open solution to run this extension"),
                    MessageBoxTitle,
                    OLEMSGICON.OLEMSGICON_INFO,
                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
            }
        }
        
        private bool SolutionIsLoaded(Solution solution)
        {
            try
            {
                foreach (EnvDTE.Project project in solution.Projects)
                {
                    if (!string.IsNullOrEmpty(project.FullName))
                    {
                    }
                }
                return true;
            }
            catch (NotImplementedException)
            {
                return false;
            }
        }
    }
}
