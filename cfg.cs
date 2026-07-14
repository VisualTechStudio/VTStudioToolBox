// cfg.cs
using System;

namespace VTStudioToolBox
{
    public static class Cfg
    {
        private const string AppVersionPrefix = "[Release] 1.1 (Build.";

        public static string AppVersion
        {
            get
            {
                var now = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.UtcNow,
                    TimeZoneInfo.FindSystemTimeZoneById("China Standard Time"));
                return $"{AppVersionPrefix}{now:yyMMddHHmm})";
            }
        }

        public const string Website = "https://visualtechstudio.github.io/";
        public const string GithubRepo = "https://github.com/VisualTechStudio/VTStudioToolBox";
        public const string GPLV3 = "https://choosealicense.com/licenses/gpl-3.0/";
    }
}