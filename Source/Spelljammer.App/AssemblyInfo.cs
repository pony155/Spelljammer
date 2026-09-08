using System.Windows;

/// <summary>
/// Configures WPF theme-resource lookup for the Spelljammer application assembly.
/// </summary>
/// <remarks>
/// Code flow: WPF reads this assembly attribute during startup, skips theme-specific dictionaries, and falls back to resources compiled into the application assembly.
/// </remarks>
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
