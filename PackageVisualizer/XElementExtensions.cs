using System.Xml.Linq;

namespace PackageVisualizer
{
    public static class XElementExtensions
    {
        public static string GetTarget(this XElement element)
        {
            return element.Attribute("Target").Value;
        }

        public static string GetSource(this XElement element)
        {
            return element.Attribute("Source").Value;
        }
    }
}