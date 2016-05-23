using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace LocalizationManager.ViewModels {
    public class MainViewModel : INotifyPropertyChanged {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;
        protected void RaisePropertyChanged([CallerMemberName]string propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion

        const string settingsFileName = ".settings";
        const string resxKeyValuePattern = @"<data name=""(?<key>.+?)"" xml:space=""preserve"">\r\n.+?<value>(?<value>.+?)</value>";

        private readonly Regex resxRegex = new Regex(resxKeyValuePattern, RegexOptions.Compiled);

        private string _selectedDictionary;
        private DirectoryInfo _projectDirectory;
        private DirectoryInfo _resourceDirectory;
        private Dictionary<string, string> _settingsCache = new Dictionary<string, string>();
        private ObservableCollection<string> _dictionaries = new ObservableCollection<string>();
        private List<LocalizedValue> _CachedLocalizations = new List<LocalizedValue>();


        public List<LocalizedValue> CachedLocalizations {
            get { return _CachedLocalizations; }
            set { _CachedLocalizations = value; RaisePropertyChanged(); }
        }

        public ObservableCollection<string> Dictionaries {
            get { return _dictionaries; }
            set { _dictionaries = value; RaisePropertyChanged(); }
        }

        public DirectoryInfo ProjectDirectory {
            get { return _projectDirectory; }
            set {
                _projectDirectory = value;
                RaisePropertyChanged();

                CacheDirectory();
                ScanForDictionaries();
            }
        }

        public string SelectedDictionary {
            get { return _selectedDictionary; }
            set {
                _selectedDictionary = value;
                RaisePropertyChanged();

                CacheDictionary();
                ScanForLocalizations();
            }
        }

        public MainViewModel() {
            FileInfo settingsFile = new FileInfo(settingsFileName);
            if (!settingsFile.Exists)
                return;

            using (StreamReader sr = settingsFile.OpenText()) {
                string[] kv = sr.ReadLine().Split('=');

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                switch (key) {
                    case "project":
                        ProjectDirectory = new DirectoryInfo(value);
                        break;
                    default:
                        break;
                }
            }
        }


        private void CacheDirectory() {
            if (_settingsCache.Keys.Contains("project")) {
                _settingsCache.Remove("project");
            }

            _settingsCache.Add("project", ProjectDirectory.FullName);
        }

        private void CacheDictionary() {
            if (_settingsCache.Keys.Contains("dictionary")) {
                _settingsCache.Remove("dictionary");
            }

            _settingsCache.Add("dictionary", SelectedDictionary);
        }

        internal void PersistCache() {
            FileInfo settingsFile = new FileInfo(settingsFileName);

            using (StreamWriter sw = settingsFile.CreateText()) {
                foreach (var setting in _settingsCache) {
                    sw.WriteLine(String.Format("{0} = {1}", setting.Key, setting.Value));
                }
            }
        }

        private void ScanForDictionaries() {
            Dictionaries.Clear();

            var culturInfoList = CultureInfo.GetCultures(CultureTypes.AllCultures).Where(c => c.Name.Length > 0);
            _resourceDirectory = ProjectDirectory.GetDirectories("Resources").Single();
            var resxFiles = _resourceDirectory.GetFiles("*.resx");

            foreach (FileInfo resxFile in resxFiles) {
                if (culturInfoList.Any(ci => resxFile.Name.EndsWith(ci.Name + ".resx")))
                    continue;

                Dictionaries.Add(resxFile.Name.Replace(".resx", String.Empty));
            }

            if (Dictionaries.Any()) {
                if (!_settingsCache.Keys.Contains("dictionary"))
                    SelectedDictionary = Dictionaries.First();
                else
                    SelectedDictionary = Dictionaries.First(d => d == _settingsCache["dictionary"]);
            }
        }

        private void ScanForLocalizations() {
            var defaultResourceFile = _resourceDirectory.GetFiles(SelectedDictionary + ".resx").First();
            ParseLocalizedValues(defaultResourceFile);

            var localizedResourceFiles = _resourceDirectory.GetFiles(SelectedDictionary + "*.resx")
                                                           .Where(f => f.Name != defaultResourceFile.Name)
                                                           .OrderBy(f => f.Name.Length)
                                                           .ToList();

            string[] parts = defaultResourceFile.Name.Split('.');
            foreach (var localizedResourceFile in localizedResourceFiles) {
                string cultureString = localizedResourceFile.Name.Replace(parts[0] + ".", String.Empty)
                                                                 .Replace("." + parts[1], String.Empty);

                ParseLocalizedValues(localizedResourceFile, new CultureInfo(cultureString));
            }
        }

        private void ParseLocalizedValues(FileInfo resxFile, CultureInfo ci = null) {
            using (StreamReader sr = resxFile.OpenText()) {
                MatchCollection matches = resxRegex.Matches(sr.ReadToEnd());
                foreach (Match match in matches) {
                    if (!match.Success)
                        continue;

                    LocalizedValue locValue;
                    string key = match.Groups["key"].Value;
                    string value = match.Groups["value"].Value;

                    if (CachedLocalizations.Any(lv => lv.Key == key))
                        locValue = CachedLocalizations.First(lv => lv.Key == key);
                    else {
                        locValue = new LocalizedValue() { Key = key };
                        CachedLocalizations.Add(locValue);
                    }


                    if (ci == null)
                        locValue.Default = value;
                    else {
                        switch (ci.Name) {
                            case "de-DE":
                                locValue.de_DE = value;
                                break;
                            default:
                                throw new NotImplementedException("Culture " + ci.Name + " is not yet implemented!");
                        }
                    }
                }
            }
        }
    }
}
