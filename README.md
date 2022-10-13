We are prototyping a new feature for .NET for Android 8 that allows Java libraries to be bound directly from Maven repositories.  Additionally, it uses the `.pom` file provided to ensure that the dependencies needed by the Java library are met.

In order to get early testing and feedback, we are shipping a preview of this as a NuGet package that can be used from .NET 6/7.

> Note this is a proposed new .NET for Android 8 feature. There is no guarantee that it will ship in the final .NET 8.

This feature focuses on tackling two pain points of binding from Maven:
- Acquiring the `.jar`/`.aar` and the related `.pom` from Maven
- Using the `.pom` to verify that required Java dependencies are being fulfilled

## Getting Started

Create a new .NET Android Bindings library either through the VS "New Project" dialog or the command line:

```
dotnet new androidlib
```

Add the `XamPrototype.Android.MavenBinding.Tasks` NuGet package to the library through VS or the command line:

```
dotnet add package XamPrototype.Android.MavenBinding.Tasks
```

Let's take an example: Square's `okhttp3` version `4.9.3` available in [Maven](https://mvnrepository.com/artifact/com.squareup.okhttp3/okhttp/4.9.3).

> Editor's note: we have an [official binding](https://www.nuget.org/packages/Square.OkHttp3) for this library, but for the sake of an example.

Add a new `<AndroidMavenLibrary>` which specifies the Java artifact we want to bind:

```xml
<!-- Include format is {GroupId}:{ArtifactId} -->
<ItemGroup>
  <AndroidMavenLibrary Include="com.squareup.okhttp3:okhttp" Version="4.9.3" />
</ItemGroup>
```

> Note: By default, this pulls the library from Maven Central. There is also support for Google's Maven, custom Maven repositories, and local Java artifact files.  See [Advanced MavenDownloadTask Usage](https://github.com/jpobst/Prototype.Android.MavenBindings/wiki/MavenDownloadTask-Advanced-Scenarios) for more details.

If you compile the binding now, the library will be automatically downloaded from Maven as well as the associated `.pom` file.  The `.pom` file details the dependencies needed by this library, and the following build errors will be generated:

```
error XA0000: Maven dependency 'com.squareup.okio:okio' version '2.8.0' is not satisfied. Microsoft maintains the NuGet package 'Square.OkIO' that could fulfill this dependency.
error XA0000: Maven dependency 'org.jetbrains.kotlin:kotlin-stdlib' version '1.4.10' is not satisfied. Microsoft maintains the NuGet package 'Xamarin.Kotlin.StdLib' that could fulfill this dependency.
```

These are both libraries that Microsoft provides official NuGet bindings for, so we can add those NuGet packages:

```
dotnet add package Xamarin.Kotlin.StdLib
dotnet add package Square.OkIO
```

> Note: Not all dependencies will have official NuGet bindings. For other examples of ways to fulfill dependencies, see [Advanced MavenDependencyVerifierTask Scenarios](https://github.com/jpobst/Prototype.Android.MavenBindings/wiki/MavenDependencyVerifierTask-Advanced-Scenarios).

> Note: We still need to get the required metadata into all of our packaged bindings.  If you still get resolution errors then we haven't added it to these packages.  You will need to add the following attributes as described in see [Advanced MavenDependencyVerifierTask Scenarios](https://github.com/jpobst/Prototype.Android.MavenBindings/wiki/MavenDependencyVerifierTask-Advanced-Scenarios).  In the future it will work automagically.

```xml
<PackageReference Include="Square.OkIO" Version="2.10.0.5" JavaArtifact="com.squareup.okio:okio" JavaVersion="2.10.0" />
<PackageReference Include="Xamarin.Kotlin.StdLib" Version="1.7.10" JavaArtifact="org.jetbrains.kotlin:kotlin-stdlib" JavaVersion="1.7.10" />
```

Now if you try to compile the library the dependencies will be detected as fulfilled, and the build continues.  If you get C# compile errors (like with this package) you are now back to the normal binding process. (ie: fixing with Metadata).

## Known Limitations

- Several of Microsoft's published NuGet bindings are missing needed metadata for automatic dependency resolution. Will be audited and fixed. [(Workaround)](https://github.com/jpobst/Prototype.Android.MavenBindings/wiki/MavenDependencyVerifierTask-Advanced-Scenarios)

## Feedback

If you give this a try, we'd love to hear your feedback!  Did it work?  Are there any additional scenarios that we should explore covering?

Please use the `Discussions` tab on this GitHub repo for feedback, or the `Issues` tab for bugs.