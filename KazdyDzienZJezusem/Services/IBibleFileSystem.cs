namespace KazdyDzienZJezusem.Services;

public interface IBibleFileSystem
{
    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateFiles(string path, string searchPattern);

    bool FileExists(string path);

    IEnumerable<string> ReadLines(string path);
}
