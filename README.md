# AsyncSQL

AsyncSQL is the missing library in dotnet world for executing raw SQL queries asynchronously for game server development. It provides easy APIs to execute sql queries with async callbacks.

# Supported Database

MySQL 5.6+

# Usage

First, create an DB class:

```c#
public class LoginDatabase : MySqlBase<LoginStatements>
{
    public override void PreparedStatements()
    {
        PrepareStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_NAME, "SELECT id, username FROM account WHERE username = ?");
        PrepareStatement(LoginStatements.INS_ACCOUNT, "INSERT INTO account (username) VALUES(?)");
    }
}

public enum LoginStatements
{
    SEL_ACCOUNT_LIST_BY_NAME,
    INS_ACCOUNT,

    MAX_LOGINDATABASE_STATEMENTS
}
```

Second, loaded the database:

```c#
var LoginDB = new LoginDatabase();

DatabaseLoader loader = new DatabaseLoader(false);
loader.AddDatabase(LoginDB, new MySqlConnectionInfo()
{
    Host = "127.0.0.1",
    Port = "3306",
    Username = "asyncsql",
    Password = "asyncsql",
    Database = "login_test"
});

var dbLoaded = loader.Load();
```

Then, execute sql queries synchronously:

```c#
PreparedStatement stmt = LoginDB.GetPreparedStatement(LoginStatements.INS_ACCOUNT);
stmt.AddValue(0, "asyncsql1");
LoginDB.DirectExecute(stmt);

// Or
stmt = LoginDB.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_NAME);
stmt.AddValue(0, "asyncsql1");

var result = LoginDB.Query(stmt);
if (!result.IsEmpty())
{
    uint accountId = result.Read<uint>(0);
    string accountName = result.Read<string>(1);

    Console.WriteLine("Query success. Result: accountId {0}, accountName {1}", accountId, accountName);
}
```

Or execute sql queries asynchronously:

```c#
var queryProcessor = new AsyncCallbackProcessor<QueryCallback>();

var stmt = LoginDB.GetPreparedStatement(LoginStatements.SEL_ACCOUNT_LIST_BY_NAME);
stmt.AddValue(0, "asyncsql1");
queryProcessor.AddCallback(LoginDB.AsyncQuery(stmt).WithCallback(async result =>
{
    if (!result.IsEmpty())
    {
        do
        {
            var accountId = result.Read<uint>(0);
            string accountName = result.Read<string>(1);
            Console.WriteLine("Async execute query {0} - Result: accountId {1}, accountName {2}", stmt.CommandText, accountId, accountName);

        } while (result.NextRow());
    }
}));

// Process async queries callback in your game loop.
new Thread(() => {

    // Assume this is the game loop
    while(true)
    {
        queryProcessor.ProcessReadyCallbacks();
    }

}).Start();
```