using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace VS.SQLServerRepository
{
    public class DataProvider : IDataProvider
    {
        public IEnumerable<T> Read<T>()
        {
            return Results.Read<T>().ToList();
        }

        public IEnumerable<dynamic> Read()
        {
            return Read<dynamic>().ToList();
        }

        public SqlMapper.GridReader? Results { get; set; }
    }
}