xml
==========================================================================

[![CI](https://github.com/filipetoscano/xml/workflows/CI/badge.svg)](https://github.com/filipetoscano/xml/actions)
[![NuGet](https://img.shields.io/nuget/vpre/Lefty.Xml.svg?label=NuGet)](https://www.nuget.org/packages/Lefty.Xml/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

.NET tool to validate and format XML


Installing
--------------------------------------------------------------------------

To install the tool as a repository tool, run the following command in the
root of your repository:

```
> dotnet tool install --local Lefty.Xml --create-manifest-if-needed
```

If you'd rather have the tool installed globally in your machine, run the
following command:

```
> dotnet tool install --global Lefty.Xml
```


Usage
--------------------------------------------------------------------------

```
> lxml --help

Usage: lxml [command] [options]

Options:
  --version     Show version information.
  -?|-h|--help  Show help information.

Commands:
  format        Formats XML file based on editorconfig
  info          Shows encoding, declaration, namespaces, layout and other facts about XML files
  validate      Validates XML file against an XSD schema

Run 'lxml [command] -?|-h|--help' for more information about a command.
```


Continuous integration
--------------------------------------------------------------------------

`lxml format --check` fails when any file is not formatted. Add `--diff` to
see what formatting would change, as a unified diff which `git apply`
accepts:

```
> lxml format --check --diff .
```

On GitHub Actions, `--output github` keeps the usual output and also writes
each problem as an annotation, so it shows on the run summary and on the
lines of a pull request:

```yaml
- run: dotnet tool restore
- run: dotnet lxml format --check --diff --output github .
- run: dotnet lxml validate --output github .
```

For scripts, `validate` and `info` can write their results as JSON with
`--output json`. Standard output then holds only the JSON; patterns which
match no file are reported on standard error.