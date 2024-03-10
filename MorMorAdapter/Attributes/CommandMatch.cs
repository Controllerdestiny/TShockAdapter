namespace MorMorAdapter.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class CommandMatch : Attribute
{
    public string Name { get; }

    public string Permission { get; }

    public CommandMatch(string name, string perm)
    {
        Name = name;
        Permission = perm;
    }
}
