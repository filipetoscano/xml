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
    [Option( "-s|--schema", CommandOptionType.SingleValue, Description = "XSD schema" )]
    [FileExists]
    public string? Schema { get; set; }

    /// <summary />
    [Option( "-e|--errors", CommandOptionType.NoValue, Description = "" )]
    public bool EmitErrors { get; set; }

    /// <summary />
    public async Task<int> OnExecuteAsync()
    {
        // for each file as per patterns

        // load xml
        // if loading fails, error with "not well formed"

        // if Schema is provided, load that file (and all referenced xsd:include schemas into schema set)
        // otherwise, check @xsi:schemaLocation in root element and load that file (and all referenced xsd:include schemas into schema set)
        // otherwise, ok with "well formed"

        // if has schema set, run validation
        // if invalid, error with "invalid"
        // otherwise, ok with "valid"

        return 0;
    }
}