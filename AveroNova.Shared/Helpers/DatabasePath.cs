using System;
using System.Collections.Generic;
using System.Text;
//using Microsoft.Maui.Storage;

namespace AveroNova.Shared.Helpers
{
    public static class DatabasePath
    {
        //public static string GetDatabasePath()
        //{
        //    var folder = Path.Combine(AppContext.BaseDirectory, "Data");

        //    Directory.CreateDirectory(folder);

        //    return Path.Combine(folder, "AveroNova.db");
        //}

        public static string GetDatabasePath(string contentRootPath)
        {
            var folder = Path.Combine(contentRootPath, "Data");

            Directory.CreateDirectory(folder);

            return Path.Combine(folder, "AveroNova.db");
        }

    }
}
