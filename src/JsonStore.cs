using System;
using System.IO;
using System.Web.Script.Serialization;

public static class JsonStore {
    public static bool Exists(string path) { return File.Exists(path) || File.Exists(path + ".bak"); }

    public static T Read<T>(string path, Func<T,bool> valid) {
        foreach (string candidate in new string[] { path, path + ".bak" }) {
            if (!File.Exists(candidate)) continue;
            try {
                T value = new JavaScriptSerializer().Deserialize<T>(File.ReadAllText(candidate));
                if (valid(value)) return value;
            } catch (ArgumentException) { }
              catch (InvalidOperationException) { }
              catch (FormatException) { }
              catch (IOException) { }
        }
        throw new IOException("Invalid saved data: " + Path.GetFileName(path));
    }

    public static void Write<T>(string path, T value) {
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(value));
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
        else File.Move(temporary, path);
    }
}
