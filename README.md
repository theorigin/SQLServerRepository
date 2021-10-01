[![andyrobinson MyGet Build Status](https://www.myget.org/BuildSource/Badge/andyrobinson?identifier=9522c31d-4062-4dc6-b36a-593de3a39d87)](https://www.myget.org/)

# SQL Server Repository

A fluent SQL Server repository built over Dapper.  



## Usage

Create an instance of `ISqlServerRepository`

```c#
ISqlServerRepository sqlRepo = new SqlServerRepository("Server=.;Initial Catalog=YOUR_DB;User Id=USER;Password=PASSWORD;");
```

### Create

```C#
var records = await sqlRepo
    	.WithSqlStatement("INSERT INTO Product (Name, Barcode, Cost) VALUES (@name, @barcode, @cost)")
	.AddParameters(new { name = "Banana", barcode = "08765412", cost = .45 })
	.Execute();
```

### Update

```C#
var records = await sqlRepo
    	.WithSqlStatement("UPDATE Product SET Cost = Cost * 0.5")
	.Execute();

var records = await sqlRepo
    	.WithSqlStatement("UPDATE Product SET Cost = Cost * 0.5 WHERE Id = @productId")
    	.AddParameters(new { productId = 123 })
	.Execute();
```

### Delete

```c#
await sqlRepo
	.WithSqlStatement("DELETE FROM Product WHERE Id = @productId")
    	.AddParameters(new { productId = 123 })
	.Execute();
```

### Select

```C#
public class Product {
	public int Id {get; set;}
	public string Name {get; set;}
	public string Barcode {get; set;}    
	public decimal Price {get; set;}        
}

IEnumerable<Product> products = await sqlRepo
		.WithSqlStatement("SELECT Id, Name, Barcode, Cost Price FROM Product")
		.Execute<Product>();

IEnumerable<Product> products = await sqlRepo
		.WithSqlStatement("SELECT Id, Name, Barcode, Cost Price FROM Product WHERE Id = @productId")
	    	.AddParameters(new { productId = 123 })
		.Execute<Product>();
```



### Stored procedures

You can called stored procedures using the `.WithStoredProcedure` method. 

```C#
IEnumerable<Product> products = await sqlRepo
		.WithStoredProcedure("Products_Get_ById")
	    	.AddParameters(new { productId = 123 })
		.Execute<Product>();
```

This is the definition of the `Products_Get_ById` stored procedure.

```sql
CREATE PROCEDURE [dbo].[Products_Get_ById]
    @ProductId	INT
AS
BEGIN
	SELECT Id, Name, Barcode, Cost Price FROM Product WHERE Id = @ProductId 
END
```



### Parameters

If you need more control over parameters you can create them create them and add via `.AddParameter`. Here's an example of using an `InputOutput` parameter.

```sql
var param = new SqlParameter("@barcode", SqlDbType.VarChar) { 
	Direction = ParameterDirection.InputOutput, Value = "01234567890" 
};
await sqlRepo
	.WithStoredProcedure("FindProductByBarcode")		
	.AddParameter(param)
	.Execute();	
```

The `param` variable will contain the result from the stored procedure. This is the definition of the `Product_Get_ByBarcode` stored procedure.

```sql
ALTER PROCEDURE Product_Get_ByBarcode (
    @barcode VARCHAR(50) OUT
) AS
BEGIN
	SELECT @barcode
	SELECT @barcode = Name FROM Product WHERE Barcode = @barcode    
END;
```



### Transactions

You can start a transaction using the following:-

```C#
await sqlRepo.BeginTransaction();
```

You can then either commit it 

```c#
await sqlRepo.CommitTransaction();
```

or roll it back

```c#
await sqlRepo.RollbackTransaction();
```



### Multi-mapping

If you need data from multiple related tables then you can issue multiple queries and use a builder or a `Func` to create your objects. You can use SQL statements or stored procedures to return the data.

##### Using a builder

A builder implements the `IBuilder<T>` interface and returns `IEnumerable<T>`. The concept of the a builder is that it knows how to build object relationships from multiple sets of data and it can be reused via dependency injection. It can also be tested in isolation of the rest of your code.

```c#
public class ProductBuilder : IBuilder<Product>
{
	public List<Product> Build(IDataProvider dataProvider)
	{
		if (dataProvider == null)
			throw new ArgumentNullException("dataProvider");
			
		var products = dataProvider.Read<Product>().ToList();
		var productPriceHistory = dataProvider.Read<ProductPriceHistory>().ToList();
		
		foreach (var product in products)
		{
			product.PriceHistory = productPriceHistory.Where(x => x.ProductId == product.Id).ToList();			
		}
		
		return products;
	}
}
```

and to use the builder you pass it in as parameter to the `Execute` method. Here's an example of using the builder above

```c#
var builder = new ProductBuilder();
var propertyGroups = await sqlRepo
		.WithSqlStatement("SELECT Id, Name, Cost Price FROM Product; SELECT Id, Cost Price, [From], [To], Product_Id ProductId FROM ProductPriceHistory")		
		.Execute<Product>(builder);
```

##### Using a `FUNC`

While a builder is a nice way to write reusable/testable code sometimes you won't want to create a separate class and implement the `IBuilder<T>` interface. So you can use a  `Func<IDataProvider, IEnumerable<T>>` to generate your related objects. 

```c#
await sqlRepo
	.WithStoredProcedure("Products_GetAll")
	.Execute<Product>(dataProvider => CreateProducts(dataProvider));
```

and `CreateProducts` is

```c#
public IEnumerable<Product> CreateProducts(IDataProvider dataProvider)
{
	var products = dataProvider.Read<Product>().ToList();
	var productPriceHistory = dataProvider.Read<ProductPriceHistory>().ToList();

	foreach (var product in products)
	{
		product.PriceHistory = productPriceHistory.Where(x => x.ProductId == product.Id).ToList();
	}
	return products;
}
```



### Dependency injection

Configure your container to provide an implementation of `ISqlServerRepository`.



