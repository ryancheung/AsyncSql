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

using System;
using System.Threading;

namespace AsyncSql
{
    public interface ISqlOperation
    {
        bool Execute<T>(MySqlBase<T> mySqlBase);
    }

    class DatabaseWorker<T> : IDisposable
    {
        Thread _workerThread;
        volatile bool _cancelationToken;
        ProducerConsumerQueue<ISqlOperation> _queue;
        MySqlBase<T> _mySqlBase;

        private bool _disposed;

        public DatabaseWorker(ProducerConsumerQueue<ISqlOperation> newQueue, MySqlBase<T> mySqlBase)
        {
            _queue = newQueue;
            _mySqlBase = mySqlBase;
            _cancelationToken = false;
            _workerThread = new Thread(WorkerThread) { Name = $"DB {mySqlBase.ConnectionInfo.Database} Worker Thread" };
            _workerThread.Start();
        }

        void WorkerThread()
        {
            if (_queue == null)
                return;

            for (; ; )
            {
                ISqlOperation operation;

                _queue.WaitAndPop(out operation);

                if (_cancelationToken || operation == null)
                    return;

                operation.Execute(_mySqlBase);
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _cancelationToken = true;

                _workerThread.Join();
            }

            _disposed = true;
        }
    }
}
