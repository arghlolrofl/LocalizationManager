using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Xml.Linq;

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

        private DataTable _locTable;
        public DataTable LocTable {
            get { return _locTable; }
            set { _locTable = value; RaisePropertyChanged(); }
        }

        private bool _hasChanges;

        public bool HasChanges {
            get { return _hasChanges; }
            set { _hasChanges = value; RaisePropertyChanged(); }
        }



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

        /// <summary>
        /// Scan for alle dicitionaries (default resx files, without culture identification)
        /// </summary>
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
            LocTable = new DataTable();

            // search for default resx file (without culture identification) ...
            var defaultResourceFile = _resourceDirectory.GetFiles(SelectedDictionary + ".resx").First();
            // ... and parse it's values
            ParseLocalizedValues(defaultResourceFile);

            // search for all localized resx files
            var localizedResourceFiles = _resourceDirectory.GetFiles(SelectedDictionary + "*.resx")
                                                           .Where(f => f.Name != defaultResourceFile.Name)
                                                           .OrderBy(f => f.Name.Length)
                                                           .ToList();

            // remember dictionary name and file ending
            string[] parts = defaultResourceFile.Name.Split('.');
            foreach (var localizedResourceFile in localizedResourceFiles) {
                // replace both for every localized resx file to get the culture identification ...
                string cultureString = localizedResourceFile.Name.Replace(parts[0] + ".", String.Empty)
                                                                 .Replace("." + parts[1], String.Empty);

                // ... and pass it to the parser
                ParseLocalizedValues(localizedResourceFile, new CultureInfo(cultureString));
            }
        }

        private void ParseLocalizedValues(FileInfo resxFile, CultureInfo ci = null) {
            // Prepare LocTable
            if (ci == null) {
                LocTable.PrimaryKey = new DataColumn[] { LocTable.Columns.Add("Key") };
                LocTable.Columns.Add("Default");
            } else
                LocTable.Columns.Add(ci.Name);


            MatchCollection matches = null;
            using (StreamReader sr = resxFile.OpenText())
                matches = resxRegex.Matches(sr.ReadToEnd());


            foreach (Match match in matches) {
                if (!match.Success)
                    continue;

                string key = match.Groups["key"].Value;
                string value = match.Groups["value"].Value;

                if (ci == null) {
                    DataRow row = LocTable.NewRow();
                    row[0] = key;
                    row[1] = value;
                    LocTable.Rows.Add(row);
                } else {
                    DataRow row = LocTable.Rows.Find(key);
                    row[ci.Name] = value;
                }
            }
        }

        internal void Callback_OnRowEditEnding(object sender, DataGridRowEditEndingEventArgs e) {
            HasChanges = true;
        }

        internal void SaveChanges(DataTable dataTable) {
            string targetFileName = SelectedDictionary + ".resx";
            DirectoryInfo resourceDirectory = ProjectDirectory.GetDirectories("Resources").First();

            FileInfo targetFile = resourceDirectory.GetFiles(targetFileName).First();
            WriteColumnToResx(targetFile);

            FileInfo[] targetFiles = resourceDirectory.GetFiles(SelectedDictionary + ".*.resx");
            foreach (var localizedTargetFile in targetFiles) {
                string columnName = localizedTargetFile.Name.Replace(".resx", String.Empty)
                                                            .Replace(SelectedDictionary + ".", String.Empty);

                WriteColumnToResx(localizedTargetFile, columnName);
            }
        }

        private void WriteColumnToResx(FileInfo targetFile, string columnName = null) {
            string fileContent = null;
            using (StreamReader sr = targetFile.OpenText())
                fileContent = sr.ReadToEnd();

            XDocument xmlDoc = XDocument.Parse(fileContent);

            IEnumerable<XElement> dataNodes = xmlDoc.Root.Elements().Where(e => e.Name == "data");
            dataNodes.Remove();

            XNamespace ns = xmlDoc.Root.GetNamespaceOfPrefix("xml");
            foreach (DataRow row in LocTable.Rows) {
                string key = row[0].ToString();

                string value = null;
                if (!String.IsNullOrEmpty(columnName))
                    value = row[columnName].ToString();
                else
                    value = row[1].ToString();

                xmlDoc.Root.Add(
                    new XElement("data",
                        new XAttribute("name", key),
                        new XAttribute(ns + "space", "preserve"),
                        new XElement("value", value)));
            }


            RemoveReadOnlyAttribute(targetFile);
            xmlDoc.Save(targetFile.FullName);
            HasChanges = false;
        }

        private void RemoveReadOnlyAttribute(FileInfo targetFile) {
            FileAttributes attributes = File.GetAttributes(targetFile.FullName);

            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly) {
                // Show the file.
                attributes = RemoveAttribute(attributes, FileAttributes.ReadOnly);
                File.SetAttributes(targetFile.FullName, attributes);

                Console.WriteLine("The {0} file is no longer read-only.", targetFile.Name);
            }
        }

        private static FileAttributes RemoveAttribute(FileAttributes attributes, FileAttributes attributesToRemove) {
            return attributes & ~attributesToRemove;
        }
    }
}
