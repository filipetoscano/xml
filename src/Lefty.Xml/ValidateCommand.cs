using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace Lefty.Xml;

/// <summary />
[Command( "validate", Description = "Validates XML file against an XSD schema" )]
public class ValidateCommand
{
    /// <summary />
    [Argument( 0, Description = "Input files" )]
    [Required]
    public string[] Patterns { get; set; } = default!;

    /// <summary />
    [Option( "-s|--schema", CommandOptionType.SingleValue, Description = "Schema" )]
    [FileExists]
    public string? Schema { get; set; }

    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        Console.WriteLine( this.Schema );

        return 0;
    }
}