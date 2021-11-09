using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace ViewSource.SQLServerRepository
{
    public interface ISqlRepository
    {
        string ConnectionString { get; set; }
        int CommandTimeout { get; set; }
        Task BeginTransaction();
        Task CommitTransaction();
        Task RollbackTransaction();
        IDataProvider? DataProvider { get; set; }
        Task<IEnumerable<T>> Execute<T>(IBuilder<T> builder);
        Task<IEnumerable<T>> Execute<T>(Func<IDataProvider, IEnumerable<T>> builder);
        Task<IEnumerable<T>> Execute<T>();
        Task<int> Execute();
        Task<T> ExecuteScalar<T>();
        ISqlRepository WithStoredProcedure(string storedProcName);
        ISqlRepository WithSqlStatement(string sqlStatement);
        ISqlRepository AddParameter(string name, object value);
        ISqlRepository AddParameter(SqlParameter sqlParameter);
        ISqlRepository AddParameters(dynamic value);
    }
}