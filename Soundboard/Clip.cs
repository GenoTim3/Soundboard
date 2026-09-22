using System.Text.RegularExpressions;

namespace Soundboard
{
    public class Clip
    {
        private static readonly Regex Tag = new(@"\s*\[([^\]]+)\]\s*$");

        public string Path { get; }
        public string Name { get; }
        public string? Hotkey { get; }

        public string Label => Hotkey is null ? Name : $"{Name}\n[{Hotkey}]";

        public Clip(string path)
        {
            Path = path;
            var raw = System.IO.Path.GetFileNameWithoutExtension(path);

            var m = Tag.Match(raw);
            if (m.Success)
            {
                Hotkey = m.Groups[1].Value;
                Name = raw[..m.Index];
            }
            else
            {
                Name = raw;
            }
        }
    }
}