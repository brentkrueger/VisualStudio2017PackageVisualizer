using Newtonsoft.Json;

namespace PackageVisualizer
{
    public static class Dumper
    {
        public static string Dump(this object value)
        {
            return JsonConvert.SerializeObject(value, Formatting.Indented);
        }
    }
}