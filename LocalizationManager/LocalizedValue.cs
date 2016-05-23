namespace LocalizationManager {
    public class LocalizedValue {
        public string Key { get; set; }

        public string Default { get; set; }
        public string de_DE { get; set; }


        public override string ToString() {
            return $"{Key} -> {Default} | {de_DE}";
        }
    }
}
