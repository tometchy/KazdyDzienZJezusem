namespace KazdyDzienZJezusem.Services;

public sealed class TagEditingOptions
{
    // Empty by default: local readers and screenshot tests never write to a developer checkout.
    public string RepositoryPath { get; set; } = "";
    public string Repository { get; set; } = "tometchy/KazdyDzienZJezusem";
    public string Branch { get; set; } = "master";
    // Mounted at deployment time, never baked into the image or used for pushes.
    public string ReadTokenFile { get; set; } = "";
    public bool Enabled => !string.IsNullOrWhiteSpace(RepositoryPath);
}

public sealed class TagEditException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
