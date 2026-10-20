using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System.Reflection;

namespace Lefty.Xml;

/// <summary />
[Command( "lxml", Description = "XML tool" )]
[Subcommand( typeof( FormatCommand ) )]
[Subcommand( typeof( InfoCommand ) )]
[Subcommand( typeof( ValidateCommand ) )]
[VersionOptionFromMember( MemberName = nameof( GetVersion ) )]
public class Program
{
    /// <summary />
    public static int Main( string[] args )
    {
        /*
         * 
         */
        var svc = new ServiceCollection();
        svc.AddSingleton<IAnsiConsole>( AnsiConsole.Console );

        var sp = svc.BuildServiceProvider();


        /*
         * 
         */
        var app = new CommandLineApplication<Program>();

        try
        {
            app.Conventions
                .UseDefaultConventions()
                .UseConstructorInjection( sp );
        }
        catch ( Exception ex )
        {
            AnsiConsole.MarkupLineInterpolated( $"[red]ftl[/]: unhandled exception during setup" );
            Console.WriteLine( ex.ToString() );

            return 2;
        }


        /*
         * 
         */
        try
        {
            return app.Execute( args );
        }
        catch ( CommandParsingException ex )
        {
            AnsiConsole.MarkupLineInterpolated( $"[red]err[/]: {ex.Message}" );

            return 2;
        }
        catch ( Exception ex )
        {
            AnsiConsole.MarkupLineInterpolated( $"[red]ftl[/]: unhandled exception during execution" );
            Console.WriteLine( ex.ToString() );

            return 2;
        }
    }


    /// <summary />
    private static string GetVersion()
    {
        return typeof( Program )
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;
    }


    /// <summary />
    public Program()
    {
    }


    /// <summary />
    public async Task<int> OnExecuteAsync( CommandLineApplication app )
    {
        await Task.Yield();

        app.ShowHelp();
        return 1;
    }
}