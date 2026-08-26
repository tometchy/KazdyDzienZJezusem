namespace KazdyDzienZJezusem.Services;

public sealed class PhysicalBibleFileSystem : IBibleFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern) =>
        Directory.EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

    public bool FileExists(string path) => File.Exists(path);

    public IEnumerable<string> ReadLines(string path) => File.ReadLines(path);
}
