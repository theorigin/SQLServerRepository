using System.Collections.Generic;
using Dapper;

namespace ViewSource.SQLServerRepository
{
    public interface IDataProvider
    {
        IEnumerable<T> Read<T>();
        IEnumerable<dynamic> Read();
        SqlMapper.GridReader? Results { get; set; }
    }
}