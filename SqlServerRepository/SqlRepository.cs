using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ViewSource.SQLServerRepository
{
    public class SqlRepository : ISqlRepository
    {
        private enum QueryType
        {
            ExecuteNonQueryWithParam,
            ExecuteNonQuery,
            ExecuteScalar
		}

		public string ConnectionString { get; set; }
		public int CommandTimeout { get; set; }
        public IDataProvider? DataProvider { get; set; }
        public readonly List<SqlParameter> Parameters;

		private string? _cmdText;
		private bool _isStoredProc;
		private bool _isDynamic;
		private SqlConnection? _sqlConnection;
		private SqlTransaction? _transaction;

		public SqlRepository(string connectionString)
		{
			ConnectionString = connectionString;
            Parameters = new List<SqlParameter>();
            CommandTimeout = 30;
		}
		
		public async Task BeginTransaction()
		{
			if (_sqlConnection != null)
				throw new ApplicationException("Transaction already active");

			_sqlConnection = CreateSqlConnection();
			await _sqlConnection.OpenAsync();
			_transaction = _sqlConnection.BeginTransaction("SqlServerRepositoryTransaction");
		}

		public async Task CommitTransaction()
		{
			if (_transaction == null)
				throw new ApplicationException("No transaction active");

			await _transaction.CommitAsync();
		}

		public async Task RollbackTransaction()
		{
			if (_transaction == null)
				throw new ApplicationException("No transaction active");

			await _transaction.RollbackAsync();
		}

		public async Task<int> Execute()
		{
			if (_isDynamic)
				return (int)(await Execute(QueryType.ExecuteNonQueryWithParam, Parameters[0].Value));

			return (int)(await Execute(QueryType.ExecuteNonQuery));
		}

		public async Task<IEnumerable<T>> Execute<T>()
		{
			return await ExecuteToType<T>();
		}
		
		public async Task<IEnumerable<T>> Execute<T>(IBuilder<T> builder)
        {
            return await ExecuteToType(builder);
		}

        public async Task<IEnumerable<T>> Execute<T>(Func<IDataProvider, IEnumerable<T>> builder)
        {
            return await ExecuteToType(funcBuilder: builder);
		}
		
		public async Task<T> ExecuteScalar<T>()
		{
			return (T)(await Execute(QueryType.ExecuteScalar));
		}

		public ISqlRepository WithStoredProcedure(string storedProcName)
		{
            return SetSqlOrStoredProc(storedProcName, true);
        }
		
		public ISqlRepository WithSqlStatement(string sqlStatement)
        {
            return SetSqlOrStoredProc(sqlStatement, false);
        }
		
        public ISqlRepository AddParameter(string name, object value)
		{
			AddParameter(new SqlParameter(name, value));
			return this;
		}

		public ISqlRepository AddParameter(SqlParameter sqlParameter)
		{
			Parameters.Add(sqlParameter);
			return this;
		}

		public ISqlRepository AddParameters(dynamic value)
		{
			_isDynamic = true;
			AddParameter(new SqlParameter(string.Empty, value));
			return this;
		}

        private ISqlRepository SetSqlOrStoredProc(string statement, bool isStoredProc)
        {
            _isStoredProc = isStoredProc;
            _isDynamic = false;
            Parameters.Clear();
            _cmdText = statement;
            return this;
        }

		private async Task<object> Execute(QueryType type, object? param = null)
		{
			var connection = _sqlConnection ?? CreateSqlConnection();
			var createdConnection = _sqlConnection == null;

			try
            {
                await using var sqlCommand = CreateSqlCommand(connection);
                if (Parameters.Count != 0)
                {
                    sqlCommand.Parameters.AddRange(Parameters.ToArray());
                }
				
                if (_transaction != null)
                {
                    sqlCommand.Transaction = _transaction;
                }

                return type switch
                {
                    QueryType.ExecuteNonQuery => await sqlCommand.ExecuteNonQueryAsync(),
                    QueryType.ExecuteScalar => await sqlCommand.ExecuteScalarAsync(),
                    QueryType.ExecuteNonQueryWithParam => await connection.ExecuteAsync(_cmdText, param, commandType: _isStoredProc ? CommandType.StoredProcedure : CommandType.Text),
                    _ => throw new InvalidOperationException()
                };
            }
			finally
			{
				if (createdConnection)
				{
					if (connection.State == ConnectionState.Open)
						await connection.CloseAsync();

					await connection.DisposeAsync();
				}
			}
		}

		private SqlConnection CreateSqlConnection()
		{
			return new SqlConnection(ConnectionString);
		}

		private SqlCommand CreateSqlCommand(SqlConnection sqlConnection)
		{
			var sqlCommand = new SqlCommand(_cmdText, sqlConnection)
			{
				CommandTimeout = CommandTimeout,
				CommandType = _isStoredProc ? CommandType.StoredProcedure : CommandType.Text
			};

			return sqlCommand;
		}

		private async Task<IEnumerable<T>> ExecuteToType<T>(
            IBuilder<T>? builder = null, 
            Func<IDataProvider, IEnumerable<T>>? funcBuilder = null
        )
		{
			var connection = _sqlConnection ?? CreateSqlConnection();
			var createdConnection = _sqlConnection == null;

			try
			{
				if (connection.State == ConnectionState.Closed)
				{
					await connection.OpenAsync();
				}

				var p = new DynamicParameters();

				if (Parameters.Count != 0 && !_isDynamic)
				{
					foreach (var sqlParameter in Parameters.ToArray())
					{
						p.Add(sqlParameter.ParameterName, sqlParameter.Value, sqlParameter.DbType, sqlParameter.Direction);
					}
				}
				
				if (builder != null || funcBuilder != null)
				{
                    DataProvider ??= new DataProvider();

					DataProvider.Results = _isDynamic
						? await connection.QueryMultipleAsync(_cmdText, Parameters[0].Value, _transaction, commandType: _isStoredProc ? CommandType.StoredProcedure : CommandType.Text)
						: await connection.QueryMultipleAsync(_cmdText, p, _transaction, commandType: _isStoredProc ? CommandType.StoredProcedure : CommandType.Text);

					return funcBuilder != null
									? funcBuilder(DataProvider)
									: builder.Build(DataProvider);
                }
				else
				{
					return _isDynamic
						? await connection.QueryAsync<T>(_cmdText, Parameters.Any() ? Parameters[0].Value : null, _transaction)
						: (await connection.QueryAsync<T>(_cmdText, p, _transaction, commandType: _isStoredProc ? CommandType.StoredProcedure : CommandType.Text)).ToList();
				}
            }
			finally
			{
				if (createdConnection)
				{
					if (connection.State == ConnectionState.Open)
						await connection.CloseAsync();

					await connection.DisposeAsync();
				}
			}
		}
    }
}