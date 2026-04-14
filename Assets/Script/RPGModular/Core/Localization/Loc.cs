namespace RPGModular
{
    /// <summary>
    /// Static shortcut for localization. Loc.Get("key") = LocalizationService.Instance.Get("key").
    /// </summary>
    public static class Loc
    {
        public static string Get(string key) => LocalizationService.Instance.Get(key);

        public static string Get(string key, params (string name, string value)[] args)
            => LocalizationService.Instance.Get(key, args);
    }
}
