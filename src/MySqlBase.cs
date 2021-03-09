/*
 * Copyright (C) 2021-2021 CypherCore <https://github.com/ryancheung/AsyncSql>
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace AsyncSql
{
    public class MySqlConnectionInfo
    {
        public MySqlConnection GetConnection()
        {
            return new MySqlConnection($"Server={Host};Port={Port};User Id={Username};Password={Password};Database={Database};Allow User Variables=True;Pooling=true;");
        }

        public MySqlConnection GetConnectionNoDatabase()
        {
            return new MySqlConnection($"Server={Host};Port={Port};User Id={Username};Password={Password};Allow User Variables=True;Pooling=true;");
        }

        public string Host;
        public string Port;
        public string Username;
        public string Password;
        public string Database;
        public int Poolsize;
    }

    public abstract class MySqlBase<T>
    {
        Dictionary<T, string> _preparedQueries = new Dictionary<T, string>();
        ConcurrentQueue<ISqlOperation> _queue = new ConcurrentQueue<ISqlOperation>();

        MySqlConnectionInfo _connectionInfo;
        public MySqlConnectionInfo ConnectionInfo => _connectionInfo;

        DatabaseWorker<T> _worker;

        public MySqlErrorCode Initialize(MySqlConnectionInfo connectionInfo)
        {
            _connectionInfo = connectionInfo;
            _worker = new DatabaseWorker<T>(_queue, this);

            try
            {
                using (var connection = _connectionInfo.GetConnection())
                {
                    connection.Open();
                    Loggers.Server?.Info($"Connected to MySQL(ver: {connection.ServerVersion}) Database: {_connectionInfo.Database}");
                    return MySqlErrorCode.None;
                }
            }
            catch (MySqlException ex)
            {
                return HandleMySQLException(ex);
            }
        }

        public bool DirectExecute(string sql, params object[] args)
        {
            return DirectExecute(new PreparedStatement(string.Format(sql, args)));
        }

        public bool DirectExecute(PreparedStatement stmt)
        {
            try
            {
                using (var Connection = _connectionInfo.GetConnection())
                {
                    Connection.Open();
                    using (MySqlCommand cmd = Connection.CreateCommand())
                    {
                        cmd.CommandText = stmt.CommandText;
                        foreach (var parameter in stmt.Parameters)
                            cmd.Parameters.AddWithValue("@" + parameter.Key, parameter.Value);

                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (MySqlException ex)
            {
                HandleMySQLException(ex, stmt.CommandText, stmt.Parameters);
                return false;
            }
        }

        public void Execute(string sql, params object[] args)
        {
            Execute(new PreparedStatement(string.Format(sql, args)));
        }

        public void Execute(PreparedStatement stmt)
        {
            PreparedStatementTask task = new PreparedStatementTask(stmt);
            _queue.Enqueue(task);
        }

        public void ExecuteOrAppend(SQLTransaction trans, PreparedStatement stmt)
        {
            if (trans == null)
                Execute(stmt);
            else
                trans.Append(stmt);
        }

        public SQLResult Query(string sql, params object[] args)
        {
            return Query(new PreparedStatement(string.Format(sql, args)));
        }

        public SQLResult Query(PreparedStatement stmt)
        {
            try
            {
                MySqlConnection Connection = _connectionInfo.GetConnection();
                Connection.Open();

                MySqlCommand cmd = Connection.CreateCommand();
                cmd.CommandText = stmt.CommandText;
                foreach (var parameter in stmt.Parameters)
                    cmd.Parameters.AddWithValue("@" + parameter.Key, parameter.Value);

                return new SQLResult(cmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection));
            }
            catch (MySqlException ex)
            {
                HandleMySQLException(ex, stmt.CommandText, stmt.Parameters);
                return new SQLResult();
            }
        }

        public QueryCallback AsyncQuery(PreparedStatement stmt)
        {
            PreparedStatementTask task = new PreparedStatementTask(stmt, true);
            // Store future result before enqueueing - task might get already processed and deleted before returning from this method
            Task<SQLResult> result = task.GetFuture();
            _queue.Enqueue(task);
            return new QueryCallback(result);
        }

        public Task<SQLQueryHolder<R>> DelayQueryHolder<R>(SQLQueryHolder<R> holder)
        {
            SQLQueryHolderTask<R> task = new SQLQueryHolderTask<R>(holder);
            // Store future result before enqueueing - task might get already processed and deleted before returning from this method
            Task<SQLQueryHolder<R>> result = task.GetFuture();
            _queue.Enqueue(task);
            return result;
        }

        public void LoadPreparedStatements()
        {
            PreparedStatements();
        }

        public void PrepareStatement(T statement, string sql)
        {
            StringBuilder sb = new StringBuilder();
            int index = 0;
            for (var i = 0; i < sql.Length; i++)
            {
                if (sql[i].Equals('?'))
                    sb.Append("@" + index++);
                else
                    sb.Append(sql[i]);
            }

            _preparedQueries[statement] = sb.ToString();
        }

        public PreparedStatement GetPreparedStatement(T statement)
        {
            return new PreparedStatement(_preparedQueries[statement]);
        }

        public bool Apply(string sql)
        {
            try
            {
                using (var Connection = _connectionInfo.GetConnectionNoDatabase())
                {
                    Connection.Open();
                    using (MySqlCommand cmd = Connection.CreateCommand())
                    {
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (MySqlException ex)
            {
                HandleMySQLException(ex, sql);
                return false;
            }
        }

        public bool ApplyFile(string path)
        {
            try
            {
                string query = File.ReadAllText(path);
                if (string.IsNullOrEmpty(query))
                    return false;

                if (Encoding.UTF8.GetByteCount(query) > 1048576) //Default size limit of querys
                    Apply("SET GLOBAL max_allowed_packet=1073741824;");

                using (var connection = _connectionInfo.GetConnection())
                {
                    connection.Open();
                    using (MySqlCommand cmd = connection.CreateCommand())
                    {
                        cmd.CommandTimeout = 120;
                        cmd.CommandText = query;
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                }
            }
            catch (MySqlException ex)
            {
                HandleMySQLException(ex, path);
                return false;
            }
        }

        public void EscapeString(ref string str)
        {
            str = MySqlHelper.EscapeString(str);
        }

        public void CommitTransaction(SQLTransaction transaction)
        {
            _queue.Enqueue(new TransactionTask(transaction));
        }

        public TransactionCallback AsyncCommitTransaction(SQLTransaction transaction)
        {
            TransactionWithResultTask task = new TransactionWithResultTask(transaction);
            Task<bool> result = task.GetFuture();
            _queue.Enqueue(task);
            return new TransactionCallback(result);
        }

        public MySqlErrorCode DirectCommitTransaction(SQLTransaction transaction)
        {
            using (var Connection = _connectionInfo.GetConnection())
            {
                string query = "";

                Connection.Open();
                using (MySqlTransaction trans = Connection.BeginTransaction())
                {
                    try
                    {
                        using (var scope = new TransactionScope())
                        {
                            foreach (var cmd in transaction.commands)
                            {
                                cmd.Transaction = trans;
                                cmd.Connection = Connection;
                                cmd.ExecuteNonQuery();
                                query = cmd.CommandText;
                            }

                            trans.Commit();
                            scope.Complete();
                        }
                        return  MySqlErrorCode.None;
                    }
                    catch (MySqlException ex) //error occurred
                    {
                        trans.Rollback();
                        return HandleMySQLException(ex, query);
                    }
                }
            }
        }

        MySqlErrorCode HandleMySQLException(MySqlException ex, string query = "", Dictionary<int, object> parameters = null)
        {
            MySqlErrorCode code = (MySqlErrorCode)ex.Number;
            if (ex.InnerException is MySqlException)
                code = (MySqlErrorCode)((MySqlException)ex.InnerException).Number;

            StringBuilder stringBuilder = new StringBuilder($"SqlException: MySqlErrorCode: {code} Message: {ex.Message} SqlQuery: {query} ");
            if (parameters != null)
            {
                stringBuilder.Append("Parameters: ");
                foreach (var pair in parameters)
                    stringBuilder.Append($"{pair.Key} : {pair.Value}");
            }

            Loggers.Sql?.Error(stringBuilder.ToString());

            switch (code)
            {
                case MySqlErrorCode.BadFieldError:
                case MySqlErrorCode.NoSuchTable:
                    Loggers.Sql?.Error("Your database structure is not up to date. Please check your sql queries.");
                    break;
                case MySqlErrorCode.ParseError:
                    Loggers.Sql?.Error("Error while parsing SQL. Please check your sql queries.");
                    break;
            }

            return code;
        }

        public string GetDatabaseName()
        {
            return _connectionInfo.Database;
        }

        public abstract void PreparedStatements();
    }
}
