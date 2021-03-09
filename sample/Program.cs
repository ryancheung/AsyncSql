using System;
using System.Threading;
using System.Threading.Tasks;

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

            // Next teset async query

            AsyncCallbackProcessor<QueryCallback> queryProcessor = new AsyncCallbackProcessor<QueryCallback>();

            stmt = TestDB.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST);
            queryProcessor.AddCallback(TestDB.AsyncQuery(stmt).WithCallback(async result =>
            {
                if (!result.IsEmpty())
                {
                    do
                    {
                        var accountId = result.Read<uint>(0);
                        string accountName = result.Read<string>(1);
                        Console.WriteLine("Async execute query {0} - accountId: {1}, accountName: {2}", stmt.CommandText, accountId, accountName);

                    } while (result.NextRow());
                }

                await AsyncFoo();
                Console.WriteLine("Async query callback DONE.");
            }));

            // Process async queries in another thread.
            var updateThread = new Thread(() => {

                while(true)
                {
                    queryProcessor.ProcessReadyCallbacks();
                    Thread.Sleep(500);
                }

            }) { IsBackground = true, Name = "AsyncQueryThread" };
            updateThread.Start();

            Thread.Sleep(2000);

            TestDB.Dispose();
        }

        static async Task AsyncFoo()
        {
            await Task.Run(() => {
                Console.WriteLine("Mimic a task delay.");
                Task.Delay(1000).Wait();
            });
        }
    }
}
