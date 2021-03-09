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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsyncSql
{
    public class QueryCallback : ISqlCallback
    {
        public QueryCallback(Task<SQLResult> result)
        {
            _result = result;
        }

        public QueryCallback WithCallback(Action<SQLResult> callback)
        {
            return WithChainingCallback((queryCallback, result) => callback(result));
        }

        public QueryCallback WithCallback<T>(Action<T, SQLResult> callback, T obj)
        {
            return WithChainingCallback((queryCallback, result) => callback(obj, result));
        }

        public QueryCallback WithChainingCallback(Action<QueryCallback, SQLResult> callback)
        {
            _callbacks.Enqueue(new QueryCallbackData(callback));
            return this;
        }

        public void SetNextQuery(QueryCallback next)
        {
            _result = next._result;
        }

        public bool InvokeIfReady()
        {
            QueryCallbackData callback = _callbacks.Peek();

            while (true)
            {
                if (_result != null && _result.Wait(0))
                {
                    Task<SQLResult> f = _result;
                    Action<QueryCallback, SQLResult> cb = callback._result;
                    _result = null;

                    cb(this, f.Result);

                    _callbacks.Dequeue();
                    bool hasNext = _result != null;
                    if (_callbacks.Count == 0)
                    {
                        if (hasNext)
                        {
                            Loggers.Server?.Fatal("AsyncSql: Assert !hasNext failed when _callbacks.Count == 0");

                            throw new Exception();
                        }

                        return true;
                    }

                    // abort chain
                    if (!hasNext)
                        return true;

                    callback = _callbacks.Peek();
                }
                else
                    return false;
            }
        }

        Task<SQLResult> _result;
        Queue<QueryCallbackData> _callbacks = new Queue<QueryCallbackData>();
    }

    struct QueryCallbackData
    {
        public QueryCallbackData(Action<QueryCallback, SQLResult> callback)
        {
            _result = callback;
        }

        public void Clear()
        {
            _result = null;
        }

        public Action<QueryCallback, SQLResult> _result;
    }
}
