/*
 * Copyright (C) 2021 ryancheung <https://github.com/ryancheung/AsyncSql>
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

namespace AsyncSql
{
    public class DatabaseLoader
    {
        public DatabaseLoader(bool autoSetup = true)
        {
            _autoSetup = autoSetup;
        }

        public void AddDatabase<T>(MySqlBase<T> database, MySqlConnectionInfo connectionInfo, int asyncThreads = 1)
        {
            _open.Add(() =>
            {
                var error = database.Initialize(connectionInfo, asyncThreads);
                if (error != MySqlErrorCode.None)
                {
                    // Database does not exist
                    if (error == MySqlErrorCode.UnknownDatabase && _autoSetup)
                    {
                        Loggers.Server?.Info($"Database \"{connectionInfo.Database}\" does not exist, do you want to create it? [yes (default) / no]: ");

                        string answer = Console.ReadLine();
                        if (string.IsNullOrEmpty(answer) || answer[0] != 'y')
                            return false;

                        Loggers.Server?.Info($"Creating database \"{connectionInfo.Database}\"...");
                        string sqlString = $"CREATE DATABASE `{connectionInfo.Database}` DEFAULT CHARACTER SET utf8 COLLATE utf8_general_ci";
                        // Try to create the database and connect again if auto setup is enabled
                        if (database.Apply(sqlString) && database.Initialize(connectionInfo, asyncThreads) == MySqlErrorCode.None)
                            error = MySqlErrorCode.None;
                    }

                    // If the error wasn't handled quit
                    if (error != MySqlErrorCode.None)
                    {
                        Loggers.Server?.Error($"\nDatabase {connectionInfo.Database} NOT opened. There were errors opening the MySQL connections. Check your SQLErrors for specific errors.");
                        return false;
                    }

                    Loggers.Server?.Info("Done.");
                }
                return true;
            });

            _prepare.Add(() =>
            {
                database.LoadPreparedStatements();
                return true;
            });
        }

        public bool Load()
        {
            if (!OpenDatabases())
                return false;

            if (!PrepareStatements())
                return false;

            return true;
        }

        bool OpenDatabases()
        {
            return Process(_open);
        }

        // Processes the elements of the given stack until a predicate returned false.
        bool Process(List<Func<bool>> list)
        {
            while (list.Count > 0)
            {
                if (!list[0].Invoke())
                    return false;

                list.RemoveAt(0);
            }
            return true;
        }

        bool PrepareStatements()
        {
            return Process(_prepare);
        }

        bool _autoSetup;
        List<Func<bool>> _open = new List<Func<bool>>();
        List<Func<bool>> _prepare = new List<Func<bool>>();
    }
}
