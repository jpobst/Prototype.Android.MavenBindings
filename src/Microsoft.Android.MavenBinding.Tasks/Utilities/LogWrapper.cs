using System.Collections.Generic;
using Microsoft.Build.Utilities;

namespace Prototype.Android.MavenBinding.Tasks
{
	// Quick hack to support unit testing. Should probably be replaced with something like Moq.
	public class LogWrapper
	{
		public List<string> Errors { get; } = new List<string> ();
		public List<string> Messages { get; } = new List<string> ();

		public virtual void LogError (string message, params object [] args)
			=> Errors.Add (string.Format (message, args));

		public virtual void LogMessage (string message, params object [] args)
			=> Messages.Add (string.Format (message, args));

		public virtual bool HasLoggedErrors => Errors.Count > 0;
	}

	public class MSBuildLogWrapper : LogWrapper
	{
		private TaskLoggingHelper _log;

		public MSBuildLogWrapper (TaskLoggingHelper log)
		{
			_log = log;
		}

		public override void LogError (string message, params object [] args)
		{
			_log.LogError (message, args);
		}


		public override void LogMessage (string message, params object [] args)
		{
			_log.LogMessage (message, args);
		}

		public override bool HasLoggedErrors => _log.HasLoggedErrors;
	}
}
