using McMaster.Extensions.CommandLineUtils;

namespace Lefty.Xml;

/// <summary />
[Command( "format", Description = "Formats XML file based on editorconfig" )]
public class FormatCommand
{
    /// <summary />
    [Argument( 0, Description = "Input files" )]
    public string[] Patterns { get; set; } = default!;

    /// <summary />
    [Option( "-k|--check", CommandOptionType.NoValue, Description = "" )]
    public bool Check { get; set; }


    /// <summary />
    public async Task<int> OnExecuteAsync()
    {

        return 0;
    }
}