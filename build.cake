#addin nuget:?package=Cake.FileHelpers&version=3.2.1
#addin nuget:?package=SharpZipLib&version=1.2.0
#addin nuget:?package=Cake.Compression&version=0.2.4
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

var target = Argument("target", "Build");
var isBleedingEdge = Argument("bleeding_edge", false);
var buildId = Argument("build_id", 0);
var lastBuildCommit = Argument("last_build_commit", "");

var buildVersion = "";
var currentCommit = RunGit("rev-parse HEAD");
var currentCommitShort = RunGit("log -n 1 --pretty=\"format:%h\"").Trim();
var currentBranch = RunGit("rev-parse --abbrev-ref HEAD");
var latestTag = RunGit("describe --tags --abbrev=0");

string RunGit(string command, string separator = "") 
{
    using(var process = StartAndReturnProcess("git", new ProcessSettings { Arguments = command, RedirectStandardOutput = true })) 
    {
        process.WaitForExit();
        return string.Join(separator, process.GetStandardOutput());
    }
}

Task("Cleanup")
    .Does(() =>
{
    Information("Removing old binaries");
    CreateDirectory("./bin");
    CleanDirectory("./bin");

    Information("Cleaning up old build objects");
    CleanDirectories(GetDirectories("./**/bin/"));
    CleanDirectories(GetDirectories("./**/obj/"));
});

Task("Build")
    .IsDependentOn("Cleanup")
    .Does(() =>
{
    var buildSettings = new MSBuildSettings {
        Configuration = "Release",
        Restore = true
    };
    buildSettings.Properties["TargetFrameworks"] = new []{ "net35" };
    MSBuild("./GraphicsSettings.sln", buildSettings);
});

Task("MakeDist")
    .IsDependentOn("Build")
    .Does(() =>
{
    var binDir = Directory("./bin/Release");
    var distDir = binDir + Directory("dist");
    var pluginDir = distDir + Directory("plugins");
    CreateDirectory(pluginDir);

    var changelog = TransformText("<%commit_count%> commits since <%last_tag%>\r\n\r\nChangelog (excluding merges):\r\n<%commit_log%>")
                        .WithToken("commit_count", RunGit($"rev-list --count {latestTag}..HEAD"))
                        .WithToken("last_tag", latestTag)
                        .WithToken("commit_log", RunGit($"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD", "\r\n"))
                        .ToString();

    FileWriteText(pluginDir + File("graphicssettings_changelog.txt"), changelog);
    CopyFileToDirectory(binDir + File("GraphicsSettings.dll"), pluginDir);
    var fileInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(binDir + File("GraphicsSettings.dll"));
    buildVersion = fileInfo.FileVersion;
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .Does(() =>
{
    var binDir = Directory("./bin/Release");
    var distDir = binDir + Directory("dist");
    var pluginDir = distDir + Directory("plugins");
    var commitPrefix = isBleedingEdge ? $"_{currentCommitShort}_" : "_";

    Information("Packing");
    ZipCompress(pluginDir, distDir + File($"GraphicsSettings{commitPrefix}{buildVersion}.zip"));
    

    if(isBleedingEdge) 
    {
        var changelog = "";

        if(!string.IsNullOrEmpty(lastBuildCommit)) {
            changelog = TransformText("<ul><%changelog%></ul>")
                        .WithToken("changelog", RunGit($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {lastBuildCommit}..HEAD"))
                        .ToString();
        }

        FileWriteText(distDir + File("info.json"), 
            SerializeJsonPretty(new Dictionary<string, object>{
                ["id"] = buildId.ToString(),
                ["date"] = DateTime.Now.ToString("o"),
                ["changelog"] = changelog,
                ["hash"] = currentCommit,
                ["artifacts"] = new Dictionary<string, object>[] {
                    new Dictionary<string, object> {
                        ["file"] = $"GraphicsSettings{commitPrefix}{buildVersion}.zip",
                        ["description"] = "GraphicsSettings for BepInEx"
                    }
                }
            }));
    }
});

RunTarget(target);