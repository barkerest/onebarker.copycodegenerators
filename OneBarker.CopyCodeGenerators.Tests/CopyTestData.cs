using System.Collections.Generic;

namespace OneBarker.CopyCodeGenerators.Tests;

public class CopyTestData
{
    public string Name { get; init; } = "";

    public string Source { get; init; } = "";

    private readonly Dictionary<string, string> _copies    = new();
    private readonly Dictionary<string, string> _inits     = new();
    private readonly Dictionary<string, string> _updates   = new();
    private readonly Dictionary<string, string> _copyTos   = new();
    private readonly Dictionary<string, string> _updateTos = new();

    public void AddCopy(string name, string source)
    {
        _copies[name] = source;
    }

    public void AddInit(string name, string source)
    {
        _inits[name] = source;
    }

    public void AddUpdate(string name, string source)
    {
        _updates[name] = source;
    }

    public void AddCopyTo(string name, string source)
    {
        _copyTos[name] = source;
    }

    public void AddUpdateTo(string name, string source)
    {
        _updateTos[name] = source;
    }
    
    public IEnumerable<KeyValuePair<string, string>> CopyResults   => _copies;
    public IEnumerable<KeyValuePair<string, string>> InitResults   => _inits;
    public IEnumerable<KeyValuePair<string, string>> UpdateResults => _updates;

    public IEnumerable<KeyValuePair<string, string>> CopyToResults => _copyTos;

    public IEnumerable<KeyValuePair<string, string>> UpdateToResults => _updateTos;
    
    public override string ToString() => Name;
}
