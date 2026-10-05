using Godot;

// Autoload: runs before the main scene. Exported apps built without game files (the GitHub build)
// load the player's `wa2-data.pck` from Wa2Res. That pack is the full project export made locally
// with the game-derived UI images and fonts, so it replaces the app's own scenes and resources.
// Both carry the engine commit they were built from: `build_commit.txt` (written by the CI
// workflow) and `data_commit.txt` (written by tools/ios-data-pack.sh).
public partial class Wa2DataPack : Node
{
	public enum PackState { NotNeeded, Loaded, Missing, Failed }
	public static PackState State = PackState.NotNeeded;
	public static string AppCommit = "";
	public static string DataCommit = "";

	public static string PackPath => OS.GetUserDataDir().PathJoin("Wa2Res").PathJoin("wa2-data.pck");
	public static bool CommitMismatch => State == PackState.Loaded && AppCommit != "" && DataCommit != AppCommit;

	public Wa2DataPack()
	{
		if (!OS.HasFeature("template") || OS.GetName() != "iOS")
			return;
		AppCommit = ReadText("res://build_commit.txt");
		if (!FileAccess.FileExists(PackPath))
		{
			State = PackState.Missing;
			return;
		}
		State = ProjectSettings.LoadResourcePack(PackPath, true) ? PackState.Loaded : PackState.Failed;
		DataCommit = ReadText("res://data_commit.txt");
	}

	private static string ReadText(string path) =>
		FileAccess.FileExists(path) ? FileAccess.GetFileAsString(path).StripEdges() : "";
}
