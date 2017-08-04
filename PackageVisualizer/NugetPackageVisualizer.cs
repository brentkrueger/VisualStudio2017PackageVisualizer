using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using EnvDTE80;
using NuGet;

namespace PackageVisualizer
{
    /// <summary>
    /// Some credit can be attributed to Pascal Laurin from: http://pascallaurin42.blogspot.com/2014/06/visualizing-nuget-packages-dependencies.html. 
    /// I modified a lot of his work from his LinqPad query to suit the needs of this extension. He gave me a great starting point.
    /// </summary>
    public class NugetPackageVisualizer
    {
        private readonly string _solutionFolder;
        private readonly List<Project> _projectList = new List<Project>();
        private readonly List<NugetPackage> _packageList = new List<NugetPackage>();
        private readonly string[] _projectExtensionExclusions = { ".vdproj", ".ndproj", ".wdproj", ".shfbproj", ".modelproj" };
        private readonly XNamespace _dgmlns = "http://schemas.microsoft.com/vs/2009/dgml";
        private readonly string packageAttributeName = "Package";
        private readonly string packageDependencyAttributeName = "Package Dependency";
        private readonly string projectAttributeName = "Project";
        private readonly string categoryAttributeName = "Category";
        private readonly string idAttributeName = "Id";
        private readonly string blueColorName = "Blue";
        private readonly string yellowColorName = "Yellow";

        public NugetPackageVisualizer(DTE2 vsEnvironment)
        {
            _solutionFolder = Path.GetDirectoryName(vsEnvironment.Solution.FullName);
        }

        public void GenerateDgmlFile(string filename)
        {
            LoadProjects();
            LoadPackageConfigs();

            var graph = new XElement(
                _dgmlns + "DirectedGraph", new XAttribute("GraphDirection", "LeftToRight"),
                CreateNodes(),
                CreateLinks(),
                CreateCategories(),
                CreateStyles());

            var doc = new XDocument(graph);
            doc.Save(filename);
        }

        #region DGML Elements
        private XElement CreateStyles()
        {
            return new XElement(_dgmlns + "Styles",
                CreateStyle(projectAttributeName, blueColorName, "Node"),
                CreateStyle(packageDependencyAttributeName, yellowColorName, "Link"));
        }

        private XElement CreateCategories()
        {
            return new XElement(_dgmlns + "Categories",
                CreateCategory(projectAttributeName),
                CreateCategory(packageAttributeName));
        }

        private XElement CreateCategory(string id)
        {
            return new XElement(_dgmlns + categoryAttributeName, new XAttribute(idAttributeName, id));
        }

        private XElement CreateNodes()
        {
            return new XElement(_dgmlns + "Nodes", _projectList.Select(p => CreateNode(p.Name, projectAttributeName)),
                _packageList.Select(p => CreateNode(p.Name + " " + p.Version, packageAttributeName)));
        }

        private XElement CreateLinks()
        {
            var linkElements = new List<XElement>();
            var allPackages = _projectList.SelectMany(p => p.Packages.Select(pa => new ProjectNugetPackage { Project = p, Package = pa }));

            AddPackageDependencyLinks(allPackages, linkElements);

            var installedPackageCategory = AddInstalledPackageLinks(allPackages, linkElements);

            RemoveDirectPackageLinks(linkElements, installedPackageCategory, allPackages);

            return new XElement(_dgmlns + "Links", linkElements);
        }

        private void RemoveDirectPackageLinks(List<XElement> linkElements, string installedPackageCategory, IEnumerable<ProjectNugetPackage> allPackages)
        {
            /*now we need to iterate through all the installed package links, to remove links that are not directly referenced under the project
            example:

            ThisIsAnExample.Project.Name 
                has a package dependency on Microsoft.AspNet.Web.Optimization 1.1.3
                    which has a package dependency on WebGrease 1.6.0
                        which has a package dependency on Antlr 3.4.1.9004
                        
            So in this example, ThisIsAnExample.Project.Name only has a link directly to Microsoft.AspNet.Web.Optimization 1.1.3, 
            and not to WebGrease 1.6.0 or Antlr 3.4.1.9004, because those are part of Microsoft.AspNet.Web.Optimization's dependencies
            */

            var packageLinksToAdd =
                linkElements.Where(e => e.Attribute(categoryAttributeName).Value.Equals(installedPackageCategory)).ToList();

            var elementsToRemove = new List<XElement>();
            foreach (var link in packageLinksToAdd)
            {
                //remove any links that are not directly under the project (see comment above)
                if (!ProjectLinkIsDirectDependency(link, allPackages))
                {
                    elementsToRemove.Add(link);
                }
            }

            foreach (var elementToRemove in elementsToRemove)
            {
                linkElements.Remove(elementToRemove);
            }
        }

        private string AddInstalledPackageLinks(IEnumerable<ProjectNugetPackage> allPackages, List<XElement> linkElements)
        {
            /*for each nuget package installed under a project, create an installed package link for it
            example:
            <Link Source="ThisIsAnExample.Project.Name" Target="AutoFixture.AutoMoq 3.30.4" Category="Installed Package" />
            */
            const string installedPackageCategory = "Installed Package";
            foreach (var installedPackage in allPackages)
            {
                var packageId = installedPackage.Package.Name + " " + installedPackage.Package.Version;
                var link = CreateLink(installedPackage.Project.Name, packageId, installedPackageCategory);
                linkElements.Add(link);
            }

            return installedPackageCategory;
        }

        private void AddPackageDependencyLinks(IEnumerable<ProjectNugetPackage> allPackages, List<XElement> linkElements)
        {
            /*for each nuget package referenced under a project, get all of its dependencies and create a package dependency link for each
            example:
            <Link Source="Microsoft.AspNet.WebPages 3.2.3" Target="Microsoft.AspNet.Razor 3.2.3" Category="Package Dependency" />
            <Link Source="Microsoft.AspNet.WebPages 3.2.3" Target="Microsoft.Web.Infrastructure 1.0.0.0" Category="Package Dependency" />
            */
            foreach (var package in allPackages)
            {
                var packageDependencies = GetPackageDependencies(package.Package.Name, package.Package.Version, package.Project);
                foreach (var packageDependency in packageDependencies)
                {
                    linkElements.Add(CreateLink(
                        package.Package.Name + " " + package.Package.Version,
                        packageDependency.Name + " " + packageDependency.Version,
                        "Package Dependency"));
                }
            }
        }
        
        private bool ProjectLinkIsDirectDependency(XElement projectLink, IEnumerable<ProjectNugetPackage> packages)
        {
            /* Given a project link, iterate through all packages for that project, to determine if the link is direct, or is part of another dependency.
        example:

        ThisIsAnExample.Project.Name 
                has a package dependency on Microsoft.AspNet.Web.Optimization 1.1.3
                    which has a package dependency on WebGrease 1.6.0
                        which has a package dependency on Antlr 3.4.1.9004
                        
            So in this example, ThisIsAnExample.Project.Name only has a link directly to Microsoft.AspNet.Web.Optimization 1.1.3, 
            and not to WebGrease 1.6.0 or Antlr 3.4.1.9004, because those are part of Microsoft.AspNet.Web.Optimization's dependencies

        */

            var packageInfo = projectLink.GetTarget().Split(' ');
            var linkPackageName = packageInfo[0];
            var linkPackageVersion = packageInfo[1];

            foreach (var package in packages.Where(p => p.Project.Name.Equals(projectLink.GetSource(), StringComparison.InvariantCultureIgnoreCase)))
            {
                var dependencies = GetPackageDependencies(package.Package.Name, package.Package.Version, package.Project);
                if (dependencies.Any(d => d.Name.Equals(linkPackageName, StringComparison.InvariantCultureIgnoreCase)
                                          &&
                                          d.Version.Equals(linkPackageVersion,
                                              StringComparison.InvariantCultureIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }
        private XElement CreateNode(string name, string category)
        {
            var labelAtt = new XAttribute("Label", name);
            return new XElement(_dgmlns + "Node", new XAttribute(idAttributeName, name), labelAtt, new XAttribute(categoryAttributeName, category));
        }

        private XElement CreateLink(string source, string target, string category)
        {
            return new XElement(_dgmlns + "Link", new XAttribute("Source", source), new XAttribute("Target", target), new XAttribute(categoryAttributeName, category));
        }

        private XElement CreateStyle(string label, string color, string targetType)
        {
            return new XElement(_dgmlns + "Style", new XAttribute("TargetType", targetType), new XAttribute("GroupLabel", label), new XAttribute("ValueLabel", "True"),
                new XElement(_dgmlns + "Condition", new XAttribute("Expression", "HasCategory('" + label + "')")),
                new XElement(_dgmlns + "Setter", new XAttribute("Property", "Background"), new XAttribute("Value", color)));
        }

        #endregion

        #region Loading Configs

        private void LoadProjects()
        {
            var dteHelper = new SolutionHelper();
            foreach (EnvDTE.Project project in dteHelper.GetProjects(dteHelper.GetSolution()))
            {
                if (!string.IsNullOrEmpty(project.FullName)
                    &&
                    !_projectExtensionExclusions.Any(ex => project.FullName.EndsWith(ex)))
                {
                    _projectList.Add(new Project {Path = project.FullName, Name = project.Name});
                }
            }
        }

        private void LoadPackageConfigs()
        {
            foreach (var pk in Directory.GetFiles(_solutionFolder, "packages.config", SearchOption.AllDirectories)
                .Where(pc => !pc.Contains(".nuget")))
            {
                var project = _projectList.SingleOrDefault(p => Path.GetDirectoryName(p.Path).Equals(Path.GetDirectoryName(pk), StringComparison.InvariantCultureIgnoreCase));
                if (project == null)
                {
                    ("Project not found in same folder than package " + pk).Dump();
                }
                else
                {
                    foreach (var pr in XDocument.Load(pk).Descendants("package"))
                    {
                        var package = GetOrCreatePackage(pr.Attribute(idAttributeName.ToLower()).Value, pr.Attribute("version").Value, project);
                        if (!project.Packages.Any(p => p.Equals(package)))
                        {
                            project.Packages.Add(package);
                        }
                    }

                    //spin through packages again to set package dependencies
                    foreach (var projectPackage in project.Packages)
                    {
                        projectPackage.PackageDependencies =
                            GetPackageDependencies(projectPackage.Name, projectPackage.Version, project);
                    }
                }
            }
        }

        #endregion

        #region Domain Objects

        private NugetPackage GetOrCreatePackage(string name, string version, Project project)
        {
            var p = _packageList.SingleOrDefault(l => l.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase) && 
            l.Version.Equals(version, StringComparison.InvariantCultureIgnoreCase));
            if (p == null)
            {
                p = new NugetPackage {Name = name, Version = version};
                _packageList.Add(p);
            }
            return p;
        }

        private IEnumerable<NugetPackage> GetPackageDependencies(string name, string version, Project project)
        {
            const string keyDelimiter = "@@@";
            var mapping = _packageList.ToDictionary(c => c.Name + keyDelimiter + c.Version, StringComparer.InvariantCultureIgnoreCase);
            var dependencies = new List<NugetPackage>();
            var nugetPackageFile = _solutionFolder + $@"\packages\{name}.{version}\{name}.{version}.nupkg";

            if (File.Exists(nugetPackageFile))
            {
                var package = new ZipPackage(nugetPackageFile);

                foreach (var dependency in package.GetCompatiblePackageDependencies(null))
                {
                    var keys = mapping.Keys.Where(k => k.StartsWith(dependency.Id + keyDelimiter, StringComparison.InvariantCultureIgnoreCase));

                    string key=null;

                    if (keys.Count().Equals(1))
                    {
                        key = keys.First();
                    }
                    else if (keys.Count() > 1 && project != null) //if we have multiple packages with various versions, figure out which version is being used for this project
                    {
                        var projectPackage = project.Packages.FirstOrDefault(k => k.Name.StartsWith(dependency.Id, StringComparison.InvariantCultureIgnoreCase));
                        if (projectPackage != null)
                            key = keys.FirstOrDefault(k => k.Equals(dependency.Id + keyDelimiter + projectPackage.Version, StringComparison.InvariantCultureIgnoreCase));
                    }

                    if (key != null)
                    {
                        var dependentPackage = mapping[key];

                        if (
                            !dependencies.Any(
                                d => d.Name.Equals(dependentPackage.Name) && d.Version.Equals(dependentPackage.Version)))
                        {
                            dependencies.Add(new NugetPackage
                            {
                                Name = dependentPackage.Name,
                                Version = dependentPackage.Version
                            });
                        }
                    }
                }
            }

            return dependencies;
        }

        #endregion
    }
}
