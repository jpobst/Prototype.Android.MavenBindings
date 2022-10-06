using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MavenNet;
using MavenNet.Models;

namespace Prototype.Android.MavenBinding.Tasks
{
	static class MavenExtensions
	{
		public static Artifact? ParseArtifact (string id, string version, LogWrapper log)
		{
			var parts = id.Split (new [] { ':' }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length != 2 || parts.Any (p => string.IsNullOrWhiteSpace (p))) {
				log.LogError ("Artifact specification '{0}' is invalid.", id);
				return null;
			}

			var artifact = new Artifact (parts [1], parts [0], version);

			return artifact;
		}

		// TODO: Fix this in MavenNet
		public static void SetRepository (this Artifact artifact, MavenRepository repository)
		{
			var method = artifact.GetType ().GetProperty ("Repository");

			method.GetSetMethod (true).Invoke (artifact, new [] { repository });
		}

		// TODO: Fix this in MavenNet
		public static Project ParsePom (string pomFile)
		{
			var parser = typeof (Project).Assembly.GetType ("MavenNet.PomParser");
			var method = parser.GetMethod ("Parse");

			using var sr = File.OpenRead (pomFile);

			var pom = method.Invoke (null, new [] { sr }) as Project;

			return pom!;
		}

		// Returns artifact output path
		public static async Task<string?> DownloadPayload (Artifact artifact, string cacheDir, LogWrapper log)
		{
			var version = artifact.Versions.First ();

			var output_directory = Path.Combine (cacheDir, artifact.GetRepositoryCacheName (), artifact.GroupId, artifact.Id, version);

			Directory.CreateDirectory (output_directory);

			var filename = $"{artifact.GroupId}_{artifact.Id}";
			var jar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.jar"));
			var aar_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.aar"));

			// We don't need to redownload if we already have a cached copy
			if (File.Exists (jar_filename))
				return jar_filename;

			if (File.Exists (aar_filename))
				return aar_filename;

			if (!(await TryDownloadPayload (artifact, jar_filename) is string jar_error))
				return jar_filename;

			if (!(await TryDownloadPayload (artifact, aar_filename) is string aar_error))
				return aar_filename;

			log.LogError ("Cannot download artifact '{0}:{1}'.\n- {2}: {3}\n- {4}: {5}", artifact.GroupId, artifact.Id, Path.GetFileName (jar_filename), jar_error, Path.GetFileName (aar_filename), aar_error);

			return null;
		}

		// Returns artifact output path
		public static async Task<string?> DownloadPom (Artifact artifact, string cacheDir, LogWrapper log)
		{
			var version = artifact.Versions.First ();
			var output_directory = Path.Combine (cacheDir, artifact.GetRepositoryCacheName (), artifact.GroupId, artifact.Id, version);

			Directory.CreateDirectory (output_directory);

			var filename = $"{artifact.GroupId}_{artifact.Id}";
			var pom_filename = Path.Combine (output_directory, Path.Combine ($"{filename}.pom"));

			// We don't need to redownload if we already have a cached copy
			if (File.Exists (pom_filename))
				return pom_filename;

			if (!(await TryDownloadPayload (artifact, pom_filename) is string pom_error))
				return pom_filename;

			log.LogError ("Cannot download POM file for artifact '{0}:{1}'.\n- {2}: {3}", artifact.GroupId, artifact.Id, Path.GetFileName (pom_filename), pom_error);

			return null;
		}

		// Return value indicates download success
		static async Task<string?> TryDownloadPayload (Artifact artifact, string filename)
		{
			try {
				using var src = await artifact.OpenLibraryFile (artifact.Versions.First (), Path.GetExtension (filename));
				using var sw = File.Create (filename);

				await src.CopyToAsync (sw);

				return null;
			} catch (Exception ex) {
				return ex.Message;
			}
		}

		public static string GetRepositoryCacheName (this Artifact artifact)
		{
			var type = artifact.Repository;

			if (type is MavenCentralRepository)
				return "central";

			if (type is GoogleMavenRepository)
				return "google";

			if (type is UrlMavenRepository url) {
				using var hasher = SHA256.Create ();
				var hash = hasher.ComputeHash (Encoding.UTF8.GetBytes (url.BaseUri.ToString ()));
				return Convert.ToBase64String (hash);
			}

			// Should never be hit
			throw new ArgumentException ($"Unexpected repository type: {type.GetType ()}");
		}

		public static void FixDependency (Project project, Dependency dependency)
		{
			var version = dependency.Version;

			if (string.IsNullOrWhiteSpace (version))
				return;

			version = ReplaceVersionProperties (project, version);

			// VersionRange.Parse cannot handle single number versions that we sometimes see in Maven, like "1".
			// Fix them to be "1.0".
			// https://github.com/NuGet/Home/issues/10342
			if (!version.Contains ("."))
				version += ".0";

			dependency.Version = version;
		}

		static string ReplaceVersionProperties (Project project, string version)
		{
			// Handle versions with Properties, like:
			// <properties>
			//   <java.version>1.8</java.version>
			//   <gson.version>2.8.6</gson.version>
			// </properties>
			// <dependencies>
			//   <dependency>
			//     <groupId>com.google.code.gson</groupId>
			//     <artifactId>gson</artifactId>
			//     <version>${gson.version}</version>
			//   </dependency>
			// </dependencies>
			if (string.IsNullOrWhiteSpace (version) || project?.Properties == null)
				return version;

			foreach (var prop in project.Properties.Any)
				version = version.Replace ($"${{{prop.Name.LocalName}}}", prop.Value);

			return version;
		}

		public static bool IsCompileDependency (this Dependency dependency) => string.IsNullOrWhiteSpace (dependency.Scope) || dependency.Scope.ToLowerInvariant ().Equals ("compile");

		public static bool IsRuntimeDependency (this Dependency dependency) => dependency?.Scope != null && dependency.Scope.ToLowerInvariant ().Equals ("runtime");
	}
}
