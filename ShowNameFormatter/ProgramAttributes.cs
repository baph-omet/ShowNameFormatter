[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
sealed class ProgramAttributes : Attribute {
    public string Author { get; }
    public string Company { get; }
    public string FriendlyName { get; }
    public string Description { get; }
    public string Version { get; }
    public string Site { get; }

    public ProgramAttributes(string friendlyName, string author, string company, string version, string description, string site = "") {
        FriendlyName = friendlyName.Trim();
        Description = description.Trim();
        Author = author.Trim();
        Company = company.Trim();
        Version = version.Trim();
        Site = $"https://github.com/baph-omet/{FriendlyName.Replace(" ", string.Empty)}";
        if (!string.IsNullOrWhiteSpace(site)) Site = site.Trim();
    }
}