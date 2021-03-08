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

using System.Threading;
using System.Collections.Concurrent;

namespace AsyncSql
{
    public interface ISqlOperation
    {
        bool Execute<T>(MySqlBase<T> mySqlBase);
    }

    class DatabaseWorker<T>
    {
        Thread _workerThread;
        volatile bool _cancelationToken;
        ConcurrentQueue<ISqlOperation> _queue;
        MySqlBase<T> _mySqlBase;

        public DatabaseWorker(ConcurrentQueue<ISqlOperation> newQueue, MySqlBase<T> mySqlBase)
        {
            _queue = newQueue;
            _mySqlBase = mySqlBase;
            _cancelationToken = false;
            _workerThread = new Thread(WorkerThread);
            _workerThread.Start();
        }

        void WorkerThread()
        {
            if (_queue == null)
                return;

            for (; ; )
            {
                ISqlOperation operation;

                while (!_queue.TryDequeue(out operation) && !_cancelationToken) {}

                if (operation == null)
                    return;

                operation.Execute(_mySqlBase);
            }
        }
    }
}
