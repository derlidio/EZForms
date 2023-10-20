/*__ ____  _             
| __|_  / /_\  _ __ _ __ 
| _| / / / _ \| '_ \ '_ \
|___/___/_/ \_\ .__/ .__/
|  \/  |__ _| |_|__|_| _ 
| |\/| / _` | / / -_) '_|
|_|  |_\__,_|_\_\___|_|
 
(C)2022-2023 Derlidio Siqueira - Expoente Zero */

using Microsoft.Data.Sqlite;

namespace EZForms
{
	public static class EZSQLite
	{
        static EZSQLite()
		{
			SQLitePCL.Batteries.Init();
		}

		public static SqliteConnection Connect(string db)
        {
			SqliteConnection connection = null;

			if (!string.IsNullOrWhiteSpace(db))
            {
				string database = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), db.Trim());

				try
                {
					connection = new SqliteConnection($"Data Source={database}");
				}
                catch { /* Dismiss */ }
			}

			return connection;
		}
	}
}

