/*__ ____  _             
| __|_  / /_\  _ __ _ __ 
| _| / / / _ \| '_ \ '_ \
|___/___/_/ \_\ .__/ .__/
|  \/  |__ _| |_|__|_| _ 
| |\/| / _` | / / -_) '_|
|_|  |_\__,_|_\_\___|_|
 
(C)2022-2023 Derlidio Siqueira - Expoente Zero */

using Microsoft.Data.Sqlite;

using EZAppMaker.Interfaces;

namespace EZForms
{
    public class EZDatasetRow
    {
        public int Row { get; set; }
        public List<object> Columns { get; set; }
    }

    public class EZListEntry
    {
        public object Key { get; set; }
        public object Item { get; set; }
        public object Detail { get; set; }
        public object Group { get; set; }
    }

    public class EZColumnPragma
    {
        public long Index { get; set; }
        public string ColumnType { get; set; }
        public long Null { get; set; }
        public object Default { get; set; }
        public long PrimaryKey { get; set; }
    }

    public class EZDataset : IDisposable
    {
        private readonly Dictionary<string, object> parameters = new Dictionary<string, object>();
        private readonly Dictionary<string, EZColumnPragma> pragma = new Dictionary<string, EZColumnPragma>();

        private readonly List<string> columns = new List<string>();
        private readonly List<EZDatasetRow> rows = new List<EZDatasetRow>();

        private readonly string database;
        private readonly string source;
        private readonly string order;

        private int size = 1;
        private int page = 0;
        private int pages = 0;
        private int count = 0;

        private SqliteConnection connection;
        private SqliteCommand command;

        public int Count => count;
        public int Page => page;
        public int Pages => pages;
        public int PageSize => size;

        public List<string> Columns => columns;
        public List<EZDatasetRow> Rows => rows;

        public EZDataset(string database, string source, Dictionary<string, object> parameters = null, string order = null, int size = 1)
        {
            if (string.IsNullOrWhiteSpace(database))
            {
                System.Diagnostics.Debug.WriteLine("EZDataset: null database name!");
                return;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                System.Diagnostics.Debug.WriteLine("EZDataset: null data source!");
                return;
            }

            this.database = database.Trim();
            this.source = source.Trim();
            this.order = order?.Trim();
            this.size = size;

            if (parameters != null)
            {
                this.parameters = parameters;
            }

            if (this.size < 0)
            {
                System.Diagnostics.Debug.WriteLine($"EZDataset: [{database}].[{source}] - Page size should not be less than zero! Coerced to zero (all records will be fetch).");
                this.size = 0;
            }

            Build();
        }

        ~EZDataset()
        {
            Disconnect();
            System.Diagnostics.Debug.WriteLine($"EZDataset: [{database}].[{source}] Finalized!");
        }

        public void Dispose()
        {
            Disconnect();
            System.Diagnostics.Debug.WriteLine($"EZDataset: [{database}].[{source}] Disposed!");
        }

        //  ___      _    _ _      __  __           _                
        // | _ \_  _| |__| (_)__  |  \/  |___ _ __ | |__  ___ _ _ ___
        // |  _/ || | '_ \ | / _| | |\/| / -_) '  \| '_ \/ -_) '_(_-<
        // |_|  \_,_|_.__/_|_\__| |_|  |_\___|_|_|_|_.__/\___|_| /__/

        public void Filter(string column, object value)
        {
            if (string.IsNullOrWhiteSpace(column)) return;

            string name = column.Trim();

            if (parameters.ContainsKey(name))
            {
                parameters[name] = value;
                return;
            }

            parameters.Add(name, value);

            if (connection != null)
            {
                Disconnect();
                Build();
            }
        }

        public bool ColumnExists(string column)
        {
            return ColumnIndex(column) != -1;
        }

        public int ColumnIndex(string column)
        {
            int index = -1;

            if ((columns != null) && !string.IsNullOrWhiteSpace(column))
            {
                if (columns.Contains(column))
                {
                    index = columns.IndexOf(column);
                }
            }

            return index;
        }

        public object Value(string column, int row = 0)
        {
            object value = null;

            if (rows.Count > 0)
            {
                int index = ColumnIndex(column);

                if (index != -1)
                {
                    try
                    {
                        value = rows[row].Columns[index];

                        if (DBNull.Value.Equals(value))
                        {
                            value = null;
                        }
                    }
                    catch { /* Dismiss */ }
                }
            }

            return value;
        }

        public object Value(int column, int row = 0)
        {
            object value = null;

            if (rows.Count > 0)
            {
                try
                {
                    value = rows[row].Columns[column];

                    if (DBNull.Value.Equals(value))
                    {
                        value = null;
                    }
                }
                catch { /* Dismiss */ }
            }

            return value;
        }

        public object DefaultValue(string column)
        {
            object result = null;

            if (pragma.ContainsKey(column))
            {
                switch (pragma[column].ColumnType)
                {
                    case "INT": result = new long(); break;
                    case "INTEGER": result = new long(); break;
                    case "TEXT": result = string.Empty; break;
                    case "REAL": result = new double(); break;
                }
            }

            return result;
        }

        public Type DefaultType(string column)
        {
            Type type = typeof(object);

            if (pragma.ContainsKey(column))
            {
                switch (pragma[column].ColumnType)
                {
                    case "INT": type = typeof(long); break;
                    case "INTEGER": type = typeof(long); break;
                    case "TEXT": type = typeof(string); break;
                    case "REAL": type = typeof(double); break;
                }
            }

            return type;
        }

        public bool FetchNextPage()
        {
            if (page < (pages - 1))
            {
                page++;
                return FetchPage();
            }

            return false;            
        }

        public bool FetchPrevPage()
        {
            if (page > 0)
            {
                page--;
                return FetchPage();
            }

            return false;
        }

        public bool FetchFirstPage()
        {
            if (page > 0)
            {
                page = 0;
                return FetchPage();
            }

            return false;
        }

        public bool FetchLastPage()
        {
            if (page < pages - 1)
            {
                page = pages - 1;
                return FetchPage();
            }

            return false;
        }

        public bool FetchLastInsert()
        {
            bool success = false;

            long rowid;

            SqliteCommand cmd = connection.CreateCommand();

            cmd.CommandText = "SELECT last_insert_rowid()";

            try
            {
                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    rowid = (long)reader.GetValue(0);
                }

                success = FetchRowId(rowid);

                CountRecords();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            cmd.Dispose();

            return success;
        }

        public bool FetchRowId(long rowid)
        {
            bool success = false;

            SqliteCommand cmd = connection.CreateCommand();

            try
            {
                cmd.CommandText = RepositionQuery(rowid);

                using (SqliteDataReader reader = cmd.ExecuteReader())
                {
                    reader.Read();
                    page = (int)(((long)reader.GetValue(0)) - 1);
                }

                success = FetchPage();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            cmd.Dispose();

            return success;
        }

        public bool FetchPage(int pg)
        {
            if ((pg >= 0) && (pg <= (pages - 1)))
            {
                page = pg;                
                return FetchPage();
            }

            return false;
        }

        public bool FetchPage()
        {
            bool success = false;

            if ((connection != null) && (command != null))
            {
                command.CommandText = FetchPageQuery();

                try
                {
                    rows.Clear();

                    EZDatasetRow row;

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        int r = 0;

                        while (reader.Read())
                        {
                            row = new EZDatasetRow() { Row = r, Columns = new List<object>() };

                            for (int i = 0; i < reader.VisibleFieldCount; i++)
                            {
                                row.Columns.Add(reader.GetValue(i));
                            }

                            rows.Add(row);

                            r++;
                        }
                    }
                    success = true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Failed to fetch page!");
                    System.Diagnostics.Debug.WriteLine(e.Message);
                }
            }

            return success;
        }

        public List<EZListEntry> ToListItemsSource(string item, string key = null, string detail = null, string group = null)
        {
            if (string.IsNullOrWhiteSpace(item) || (ColumnIndex(item) == -1)) return null;

            List<EZListEntry> list = new List<EZListEntry>();

            int col_key = ColumnIndex(key);
            int col_item = ColumnIndex(item);
            int col_detail = ColumnIndex(detail);
            int col_group = ColumnIndex(group);

            bool has_key = !string.IsNullOrWhiteSpace(key) && (col_key != -1);
            bool has_detail = !string.IsNullOrWhiteSpace(detail) && (col_detail != -1);
            bool has_group = !string.IsNullOrWhiteSpace(group) && (col_group != -1);

            EZListEntry entry;

            foreach (EZDatasetRow record in rows)
            {
                entry = new EZListEntry() { Item = record.Columns[col_item] };

                if (has_key) entry.Key = record.Columns[col_key];
                if (has_detail) entry.Detail = record.Columns[col_detail];
                if (has_group) entry.Group = record.Columns[col_group];

                list.Add(entry);
            }

            return list;
        }

        public List<EZListEntry> ToSearchItemsSource(string column)
        {
            List<EZListEntry> list = new List<EZListEntry>();

            command.CommandText = FetchSearchableQuery(column);

            object key;
            object item;

            using(SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    key = reader.GetValue(reader.GetOrdinal("rowid"));
                    item = reader.GetValue(reader.GetOrdinal(column));

                    if (key != null)
                    {
                        list.Add(new EZListEntry() { Key = key, Item = item });
                    }
                }
            }

            return list;
        }

        //  ___       _        _                     ___                     _   _             
        // |   \ __ _| |_ __ _| |__  __ _ ___ ___   / _ \ _ __  ___ _ _ __ _| |_(_)___ _ _  ___
        // | |) / _` |  _/ _` | '_ \/ _` (_-</ -_) | (_) | '_ \/ -_) '_/ _` |  _| / _ \ ' \(_-<
        // |___/\__,_|\__\__,_|_.__/\__,_/__/\___|  \___/| .__/\___|_| \__,_|\__|_\___/_||_/__/
        //                                               |_|

        public int Insert(Dictionary<string, IEZComponent> record)
        {
            int result = 0;

            string fields = "";
            string values = "";
            object value;

            SqliteCommand insert = connection.CreateCommand();
            
            foreach(string column in columns)
            {
                if ((record.ContainsKey(column)) && !record[column].Detached)
                {
                    value = record[column].ToDatabaseValue(DefaultValue(column));

                    _ = insert.Parameters.AddWithValue($"@{column}", value ?? DBNull.Value);

                    if (fields == "")
                    {
                        fields = column;
                        values = $"@{column}";
                        continue;
                    }

                    fields += $", {column}";
                    values += $", @{column}";
                }
            }

            insert.CommandText = $"INSERT INTO {source} ({fields}) VALUES({values})";

            try
            {
                result = insert.ExecuteNonQuery();

                if (result > 0)
                {
                    _ = FetchLastInsert();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            insert.Dispose();

            return result;
        }

        public int Update(Dictionary<string, IEZComponent> record, int row = 0)
        {
            int result = 0;
            long rowid = 0;

            string fields = "";
            string where = "";
            string seek = "";

            object value;
            object condition;

            SqliteCommand update = connection.CreateCommand();

            foreach (string column in columns)
            {
                if ((record.ContainsKey(column)) && !record[column].Detached)
                {
                    value = record[column].ToDatabaseValue(DefaultValue(column));

                    condition = Value(column, row);
                    
                    _ = update.Parameters.AddWithValue($"@new_{column}", value ?? DBNull.Value);
                    _ = update.Parameters.AddWithValue($"@old_{column}", condition ?? DBNull.Value);

                    if (fields == "")
                    {
                        fields = $"{column}=@new_{column}";
                        //where = DBNull.Value.Equals(condition) ? $"({column} IS NULL)" : $"({column}=@old_{column})";
                        where = (condition == null) ? $"({column} IS NULL)" : $"({column}=@old_{column})";
                        seek = (value == null) ? $"({column} IS NULL)" : $"({column}=@new_{column})";
                        continue;
                    }

                    fields += $", {column}=@new_{column}";
                    //where += DBNull.Value.Equals(condition) ? $" AND ({column} IS NULL)" : $" AND ({column}=@old_{column})";
                    where += (condition == null) ? $" AND ({column} IS NULL)" : $" AND ({column}=@old_{column})";
                    seek += (value == null) ? $" AND ({column} IS NULL)" : $" AND ({column}=@new_{column})";
                }
            }

            update.CommandText = $"UPDATE {source} SET {fields} WHERE {where}";

            try
            {
                result = update.ExecuteNonQuery();

                if (result > 0)
                {
                    update.CommandText = $"SELECT rowid FROM {source} WHERE {seek}";

                    using (SqliteDataReader reader = update.ExecuteReader())
                    {
                        reader.Read();
                        rowid = (long)reader.GetValue(0);
                    }

                    _ = FetchRowId(rowid);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            update.Dispose();

            return result;
        }

        public int Delete(Dictionary<string, IEZComponent> record)
        {
            int result = 0;

            string where = "";
            object condition;

            SqliteCommand delete = connection.CreateCommand();

            foreach (string column in columns)
            {
                if (record.ContainsKey(column)) 
                {
                    condition = record[column].ToDatabaseValue(DefaultValue(column));

                    _ = delete.Parameters.AddWithValue($"@{column}", condition ?? DBNull.Value);

                    if (where == "")
                    {
                        where = (condition == null) ? $"({column} IS NULL)" : $"({column}=@{column})";
                        continue;
                    }

                    where += (condition == null) ? $" AND ({column} IS NULL)" : $" AND ({column}=@{column})";
                }
            }

            delete.CommandText = $"DELETE FROM {source} WHERE {where}";

            try
            {
                result = delete.ExecuteNonQuery();

                Console.WriteLine(delete.CommandText);
                Console.WriteLine($"Result: {result}");

                if (result > 0)
                {
                    CountRecords();
                    if (page > (pages - 1)) page = pages - 1;
                    _ = FetchPage();
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            delete.Dispose();

            return result;
        }

        //  ___     _          _         __  __           _                
        // | _ \_ _(_)_ ____ _| |_ ___  |  \/  |___ _ __ | |__  ___ _ _ ___
        // |  _/ '_| \ V / _` |  _/ -_) | |\/| / -_) '  \| '_ \/ -_) '_(_-<
        // |_| |_| |_|\_/\__,_|\__\___| |_|  |_\___|_|_|_|_.__/\___|_| /__/

        private void Build()
        {
            if (string.IsNullOrEmpty(database) || string.IsNullOrWhiteSpace(source)) return;

            connection = EZSQLite.Connect(database);

            if (connection == null)
            {
                System.Diagnostics.Debug.WriteLine("EZDataset: Could not establish a connection to [{database}]");
                return;
            }

            try
            {
                connection.Open();

                command = connection.CreateCommand();

                command.CommandText = ColumnsQuery();

                foreach (KeyValuePair<string, object> pair in parameters)
                {
                    command.Parameters.AddWithValue($"@ez_prm_{pair.Key}", pair.Value);
                }

                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    reader.Read();

                    for (int i = 0; i < reader.VisibleFieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }
                }

                GetPragma();
                CountRecords();
                _ = FetchPage();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"EZDataset: Could not build [{database}].[{source}]");
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
        }

        private void Disconnect()
        {
            try
            {
                count = 0;
                pages = 0;
                page = 0;

                rows.Clear();

                command?.Cancel();
                command?.Dispose();

                connection?.Close();
                connection?.Dispose();
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"EZDataset: Failed to dispose [{database}].[{source}]");
            }
        }

        private void GetPragma()
        {
            command.CommandText = $"PRAGMA table_info({source})";

            EZColumnPragma column_pragma;

            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    //column_pragma = new EZColumnPragma()
                    //{
                    //    Index = (long)reader.GetValue(0),
                    //    ColumnType = (string)reader.GetValue(2),
                    //    Null = (long)reader.GetValue(3),
                    //    Default = reader.GetValue(4),
                    //    PrimaryKey = (long)reader.GetValue(5)
                    //};

                    column_pragma = new EZColumnPragma()
                    {
                        Index = (long)reader.GetValue(reader.GetOrdinal("cid")),
                        ColumnType = (string)reader.GetValue(reader.GetOrdinal("type")),
                        Null = (long)reader.GetValue(reader.GetOrdinal("notnull")),
                        Default = reader.GetValue(reader.GetOrdinal("dflt_value")),
                        PrimaryKey = (long)reader.GetValue(reader.GetOrdinal("pk"))
                    };

                    pragma.Add((string)reader.GetValue(1), column_pragma);
                }
            }
        }

        private void CountRecords()
        {
            command.CommandText = CountQuery();

            try
            {
                using (SqliteDataReader reader = command.ExecuteReader())
                {
                    reader.Read();
                    object value = reader.GetValue(0);
                    count = Convert.ToInt32(reader.GetValue(0));
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);

                count = 0;
            }

            CountPages();
        }

        private void CountPages()
        {
            if (size == 0)
            {
                pages = (count > 0) ? 1 : 0;
            }
            else
            {
                pages = count / size + ((count % size) > 0 ? 1 : 0);
            }
        }

        private string FetchPageQuery()
        {
            string limit = "";

            if (size != 0)
            {
                limit = $"LIMIT {size} OFFSET {page * size}";
            }

            string query = $"SELECT ROW_NUMBER() OVER({OrderBy()}) ez_row_nr, * FROM ({source}) {Parameters()} {limit}";

            return query.Trim();
        }

        private string FetchSearchableQuery(string column)
        {
            string id = ColumnExists("rowid") ? "" : " rowid 'rowid',";

            string query = $"SELECT ROW_NUMBER() OVER ({OrderBy()}) ez_row_nr,{id} * FROM ({source}) {Parameters()}";

            return query;
        }

        private string ColumnsQuery()
        {
            string query = $"SELECT 0 AS ez_row_nr, * FROM ({source}) {Parameters()} WHERE true = false";

            return query;
        }

        private string CountQuery()
        {
            string query = $"SELECT COUNT(*) AS records FROM ({source}) {Parameters()}";

            return query.Trim();
        }

        private string RepositionQuery(long rowid)
        {
            string parameters = Parameters();

            if (parameters == "")
            {
                parameters = $"WHERE rowid = {rowid}";
            }
            else
            {
                parameters += $" AND (rowid = {rowid})";
            }

            string id = ColumnExists("rowid") ? "" : " rowid,";

            string query = $"SELECT ez_row_nr FROM (SELECT ROW_NUMBER() OVER ({OrderBy()}) ez_row_nr,{id} * FROM ({source})) {parameters}";

            return query;
        }

        private string Parameters()
        {
            if (parameters.Count == 0) return "";

            string list = "";

            foreach (KeyValuePair<string, object> pair in parameters)
            {
                list += (list == "") ? $"({pair.Key}=@ez_prm_{pair.Key})" : $" AND ({pair.Key}=@ez_prm_{pair.Key})";
            }

            list = $"WHERE ({list})";

            return list;
        }

        private string OrderBy()
        {
            string sort = "";

            if (!string.IsNullOrWhiteSpace(order))
            {
                string[] tokens = order.Split(',');
                string column;

                foreach (string token in tokens)
                {
                    column = token.Trim();

                    if (ColumnExists(column))
                    {
                        sort += (sort == "") ? column : $",{column}";
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"No such column: [{column}] for ORDER BY on [{database}].[{source}");
                }

                if (sort != "")
                {
                    sort = $"ORDER BY {sort}";
                }
            }

            return sort;
        }
    }
}