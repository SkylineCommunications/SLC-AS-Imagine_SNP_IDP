namespace Core.Defaults
{
	public static class GlobalDefaults
	{
		private static string defaultBackupPresetFolderPath = @"\\10.110.29.20\c$\Skyline DataMiner\Documents\Imagine Selenio\Configurations";

		public static string DefaultBackupPresetFolderPath { get => defaultBackupPresetFolderPath; }
	}
}