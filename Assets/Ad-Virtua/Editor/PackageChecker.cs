using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using System.Linq;

public class IOSPackageCheckerBeforeBuild : IPreprocessBuildWithReport
{
    private const string RequiredPackageName = "com.unity.ads.ios-support";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        // Check during iOS build
        if (report.summary.platform == BuildTarget.iOS)
        {
            var request = Client.List(true);

            while (!request.IsCompleted)
            {
                System.Threading.Thread.Sleep(100);
            }

            if (request.Status == StatusCode.Success)
            {
                bool installed = request.Result.Any(p => p.name == RequiredPackageName);
                if (!installed)
                {
                    throw new BuildFailedException(
                        $"[Ad-Virtua] Missing required package: {RequiredPackageName}\n\n" +
                        "This package is required for IDFA retrieval on iOS.\n" +
                        "Solution:\n" +
                        "1. Open Window > Package Manager\n" +
                        "2. Select 'Unity Registry' from the dropdown at the top left\n" +
                        "3. Search for 'iOS 14 Advertising Support' and click Install\n" +
                        "4. Or, check the dependency settings in package.json");
                }
            }
            else
            {
                throw new BuildFailedException($"[Ad-Virtua] Package check failed: {request.Error?.message}");
            }
        }
    }
}
