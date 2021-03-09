namespace AsyncSql.Sample
{
    public class TestDatabase : MySqlBase<LoginStatements>
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
}
