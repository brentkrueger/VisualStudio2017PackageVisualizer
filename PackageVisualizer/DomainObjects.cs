using System.Collections.Generic;

namespace PackageVisualizer
{
    public class Project
    {
        public Project()
        {
            Projects = new List<Project>();
            Packages = new List<NugetPackage>();
        }
        public string Path { get; set; }
        public string Name { get; set; }
        public List<Project> Projects { get; private set; }
        public List<NugetPackage> Packages { get; private set; }
    }

    public class NugetPackage
    {
        private bool Equals(NugetPackage other)
        {
            return string.Equals(Name, other.Name) && string.Equals(Version, other.Version);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NugetPackage) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0)*397) ^ (Version != null ? Version.GetHashCode() : 0);
            }
        }

        public string Name { get; set; }
        public string Version { get; set; }
        public IEnumerable<NugetPackage> PackageDependencies { get; set; }
    }

    public class ProjectNugetPackage
    {
        public NugetPackage Package { get; set; }
        public Project Project { get; set; }
    }
}
