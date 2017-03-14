# VisualStudio2017PackageVisualizer
Implementation of the Visual Studio 2013 package visualizer as a Visual Studio 2017 extension. (Currently no support for .NET core projects)

How to Use: After building the solution, there will be a PackageVisualizer.vsix file in the bin. Use that to install the extension for VS2017. Once it is complete, you'll have to restart visual studio. After that is done, the extension should show up directly under Tools as "Nuget Package Visualizer". Once you run the extension/command, you will get an output file with your results called NugetVisualizerOutput.dgml.
