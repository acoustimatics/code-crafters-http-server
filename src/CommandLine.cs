class Options
{
    public string? Directory { get; set; }

    private Options() { }

    public static Options Instance { get; } = new();
}

static class CommandLine
{
    public static void Parse(string[] args)
    {
        for (var i = 0; i < args.Length;)
        {
            var option = args[i++];
            if (option.StartsWith("--"))
            {
                switch (option.AsSpan().Slice(2))
                {
                    case "directory":
                        if (i < args.Length)
                        {
                            Options.Instance.Directory = args[i++];
                        }
                        break;
                }
            }
        }
    }
}