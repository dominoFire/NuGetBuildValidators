using Microsoft.Extensions.CommandLineUtils;
using NuGetValidators.Artifact;
using System;

namespace NuGetValidator
{
    internal static class ArtifactValidatorCommand
    {
        private static readonly string Description = "Validate the localization.";
        private static readonly string HelpOption = "-h|--help";
        private static readonly string VsixSwitchDescription = "Switch to indicate that a vsix needs to be validated. " +
                                                               "If -x|--vsix switch is provided, then the tool validates the NuGet vsix. " +
                                                               "Else the tool validates an artifacts location for the NuGet code base.";
        private static readonly string VsixPathDescription = "Path to NuGet.Tools.Vsix containing all dlls to be verified.";
        private static readonly string VsixExtractPathDescription = "Path to extract NuGet.Tools.Vsix into. Folder need not be present, but Program should have write access to the location.";
        private static readonly string OutputPathDescription = "Path to the directory for writing errors. File need not be present, but Program should have write access to the location.";
        private static readonly string ArtifactsPathDescription = "Path to the local NuGet artifacts folder. This option is used to validate a locally built NuGet repository.";


        public static void Register(CommandLineApplication app)
        {
            app.Command("artifact", localizationValidator =>
            {
                localizationValidator.Description = Description;
                localizationValidator.HelpOption(HelpOption);

                var vsixSwitch = localizationValidator.Option(
                    "-x|--vsix",
                    VsixSwitchDescription,
                    CommandOptionType.NoValue);

                var vsixPath = localizationValidator.Option(
                    "-p|--vsix-path",
                    VsixPathDescription,
                    CommandOptionType.SingleValue);

                var vsixExtractPath = localizationValidator.Option(
                    "-e|--vsix-extract-path",
                    VsixExtractPathDescription,
                    CommandOptionType.SingleValue);

                var outputPath = localizationValidator.Option(
                    "-o|--output-path",
                    OutputPathDescription,
                    CommandOptionType.SingleValue);

                var artifactsPath = localizationValidator.Option(
                    "-a|--artifacts-path",
                    ArtifactsPathDescription,
                    CommandOptionType.SingleValue);

                localizationValidator.OnExecute(() =>
                {
                    var exitCode = 0;

                    if (vsixSwitch.HasValue())                   
                    {
                        if(!vsixPath.HasValue() || !vsixExtractPath.HasValue() || !outputPath.HasValue())
                        {
                            Console.WriteLine("Since -x|--vsix switch was passed, please enter the following arguments - ");
                            Console.WriteLine($"{vsixPath.ShortName}|{vsixPath.LongName}: {vsixPath.Description}");
                            Console.WriteLine($"{vsixExtractPath.ShortName}|{vsixExtractPath.LongName}: {vsixExtractPath.Description}");
                            Console.WriteLine($"{outputPath.ShortName}|{outputPath.LongName}: {outputPath.Description}");
                            exitCode = 1;
                        }
                        else
                        {
                            exitCode = ArtifactValidator.ExecuteForVsix(vsixPath.Value(), vsixExtractPath.Value(), outputPath.Value());
                        }
                    }
                    else
                    {
                        if (!artifactsPath.HasValue() || !outputPath.HasValue())
                        {
                            Console.WriteLine("Since -x|--vsix switch was not passed, please enter the following arguments - ");
                            Console.WriteLine($"{artifactsPath.ShortName}|{artifactsPath.LongName}: {artifactsPath.Description}");
                            Console.WriteLine($"{outputPath.ShortName}|{outputPath.LongName}: {outputPath.Description}");
                            exitCode = 1;
                        }
                        else
                        {
                            exitCode = ArtifactValidator.ExecuteForArtifacts(artifactsPath.Value(), outputPath.Value());
                        }
                    }

                    return exitCode;
                });
            });
        }
    }
}
