using System.Text.RegularExpressions;
using LitJson;
using Nitrox.Model.Helper;
using NitroxClient.Communication.MultiplayerSession;

namespace Nitrox.Test.Patcher.Patches.Persistent;

[TestClass]
public class Language_LoadLanguageFile_PatchTest
{
    [TestMethod]
    public void DefaultLanguageSanity()
    {
        string languageFolder = Path.Combine(NitroxUser.AssetsPath, "LanguageFiles");
        Assert.IsTrue(Directory.Exists(languageFolder), $"The language files folder does not exist at {languageFolder}.");

        string defaultLanguageFilePath = Path.Combine(languageFolder, "en.json");
        Assert.IsTrue(File.Exists(defaultLanguageFilePath), $"The english language file does not exist at {defaultLanguageFilePath}.");

        using StreamReader streamReader = new(defaultLanguageFilePath);
        try
        {
            JsonData defaultLanguage = JsonMapper.ToObject(streamReader);
            Assert.IsTrue(defaultLanguage.Keys.Count > 0, "The english language file has no entries.");
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unable to map default json file : {ex.Message}");
        }
    }

    [TestMethod]
    public void EveryJoinRefusalAndNewMenuTextHasAnEnglishText()
    {
        Dictionary<string, string> english = ReadLanguage("en");
        IEnumerable<string> keys = ReservationRefusalText.Keys.Concat(["Nitrox_Refused_Unknown", "Nitrox_Settings_Keybind_Ping", "Nitrox_Yes", "Nitrox_No", "Nitrox_OK", "Nitrox_Confirm", "Nitrox_Cancel", "Nitrox_LanServer", "Nitrox_ChatServerName", "Nitrox_JoinTimedOut", "Nitrox_JoinUnexpectedError", "Nitrox_LostConnection", "Nitrox_ServerStopped"]);

        keys.Where(key => !english.ContainsKey(key)).Should().BeEmpty();
    }

    [TestMethod]
    public void TranslationsUseOnlyPlaceholdersTheEnglishTextHas()
    {
        // A renamed placeholder is never filled in: the player would read "{JUGADOR}" instead of a name
        Dictionary<string, string> english = ReadLanguage("en");
        Regex placeholder = new(@"\{[A-Za-z_]+\}");
        List<string> wrong = [];
        foreach (string file in Directory.EnumerateFiles(Path.Combine(NitroxUser.AssetsPath, "LanguageFiles"), "*.json"))
        {
            string language = Path.GetFileNameWithoutExtension(file);
            foreach ((string key, string text) in ReadLanguage(language))
            {
                if (!english.TryGetValue(key, out string englishText))
                {
                    continue;
                }
                HashSet<string> allowed = placeholder.Matches(englishText).Cast<Match>().Select(m => m.Value).ToHashSet();
                wrong.AddRange(placeholder.Matches(text).Cast<Match>().Select(m => m.Value).Where(token => !allowed.Contains(token)).Select(token => $"{language} {key} {token}"));
            }
        }

        wrong.Should().BeEmpty();
    }

    private static Dictionary<string, string> ReadLanguage(string language)
    {
        using StreamReader streamReader = new(Path.Combine(NitroxUser.AssetsPath, "LanguageFiles", $"{language}.json"));
        JsonData json = JsonMapper.ToObject(streamReader);
        Dictionary<string, string> texts = [];
        foreach (string key in json.Keys)
        {
            if (json[key].IsString)
            {
                texts[key] = (string)json[key];
            }
        }
        return texts;
    }
}
