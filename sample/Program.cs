using System;

namespace AsyncSql.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var TestDB = new TestDatabase();

            DatabaseLoader loader = new DatabaseLoader(false);
            loader.AddDatabase(TestDB, new MySqlConnectionInfo()
            {
                Host = "127.0.0.1",
                Port = "3306",
                Username = "asyncsql",
                Password = "asyncsql",
                Database = "asyncsql_test"
            });

            var dbLoaded = loader.Load();

            if (!dbLoaded)
            {
                Console.WriteLine("test db load failed!");
                return;
            }

            var success = TestDB.DirectExecute("SELECT 1 FROM account");
            if (success)
                Console.WriteLine("Direct execute sql successfully.");

            PreparedStatement stmt = TestDB.GetPreparedStatement(LoginStatements.INS_ACCOUNT);
            stmt.AddValue(0, "asyncsql1");
            TestDB.DirectExecute(stmt);

            stmt = TestDB.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_NAME);
            stmt.AddValue(0, "asyncsql1");

            var result = TestDB.Query(stmt);
            if (result.IsEmpty())
            {
                Console.WriteLine("Query failed.");
            }
            else
            {
                uint accountId = result.Read<uint>(0);
                string accountName = result.Read<string>(1);

                Console.WriteLine("Query success. accountId: {0}, accountName: {1}", accountId, accountName);
            }

            TestDB.Dispose();
        }
    }
}
