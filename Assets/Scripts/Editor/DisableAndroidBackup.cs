#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;

// Android's auto backup would restore PlayerPrefs (accounts, guest name, progress) after the app
// is uninstalled and reinstalled. Turn it off so a reinstall really starts fresh.
public class DisableAndroidBackup : IPostGenerateGradleAndroidProject
{
    private const string AndroidNs = "http://schemas.android.com/apk/res/android";
    private const string ToolsNs = "http://schemas.android.com/tools";

    public int callbackOrder => 0;

    // `path` is the unityLibrary module; the app's own manifest is in the sibling launcher module.
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifest = Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifest)) return;

        var doc = new XmlDocument();
        doc.Load(manifest);
        var root = doc.DocumentElement;
        root.SetAttribute("xmlns:tools", ToolsNs);
        var app = (XmlElement)root.SelectSingleNode("application");
        if (app == null)
        {
            app = doc.CreateElement("application");
            root.AppendChild(app);
        }
        app.SetAttribute("allowBackup", AndroidNs, "false");
        app.SetAttribute("fullBackupContent", AndroidNs, "false");
        app.SetAttribute("replace", ToolsNs, "android:allowBackup,android:fullBackupContent");
        doc.Save(manifest);
    }
}
#endif
