# VisualStudio2017PackageVisualizer
Implementation of the Visual Studio 2013 package visualizer as a Visual Studio 2017 extension.

# Usage
After building the solution, there will be a PackageVisualizer.vsix file in the bin. Use that to install the extension for VS2017. Once it is complete, you'll have to restart visual studio. After that is done, the extension should show up in the Visual Studio Menu as "Nuget Package Visualizer". Once you run the extension/command, you will get an output file with your results called NugetVisualizerOutput.dgml.
You can also create a dgml file for specific packages by choosing the `Generate (with Filter)` option, this supports regex and will create a dgml file containing only the packages which match your filter.