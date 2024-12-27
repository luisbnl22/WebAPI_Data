using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public partial class Program
{
    // Define the response structure for the count endpoint
    [JsonSerializable(typeof(CountResponse))]
    //[JsonSerializable(typeof(YearlySalesResponse))]
    [JsonSerializable(typeof(WarningResponse))]
    [JsonSerializable(typeof(OrderDetailResponse))]
    public partial class MyJsonContext : JsonSerializerContext { }

    public class CountResponse
    {
        public int Count { get; set; }
    }

    // Define a response class for yearly sales data
    //public class YearlySalesResponse
    //{
    //    public string Year { get; set; }
    //    public decimal TotalSales { get; set; }
    //}

    // Define a response class for yearly sales data
    public class OrderDetailResponse
    {
        public decimal QuantityOrdered { get; set; }
        public decimal PriceEach { get; set; }
        public decimal Sales { get; set; }
        public string OrderDate { get; set; }
        public string Status { get; set; }
        public string ProductCode { get; set; }
    }

    // Define a response class for yearly sales data
    public class WarningResponse
    {
        public string Error { get; set; }
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        string dbPath = "NewDB.db"; // Path to the SQLite database (same directory)

        // Endpoint to get the total count of sales data
        app.MapGet("/count", async (HttpContext context) =>
        {
            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    await connection.OpenAsync();

                    using (var command = new SqliteCommand("SELECT COUNT(*) FROM sales_data_sample", connection))
                    {
                        object result = await command.ExecuteScalarAsync();

                        if (result != DBNull.Value && result != null)
                        {
                            int count = Convert.ToInt32(result);
                            var response = new CountResponse { Count = count };
                            await context.Response.WriteAsJsonAsync(response, MyJsonContext.Default.CountResponse);
                        }
                        else
                        {
                            // Handle case where the COUNT returns NULL (empty table)
                            await context.Response.WriteAsJsonAsync(new CountResponse { Count = 0 }, MyJsonContext.Default.CountResponse);
                        }
                    }
                }
            }
            catch (SqliteException ex)
            {
                // Handle SQLite specific exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "Database error: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "An error occurred: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
        });

        // New endpoint to get yearly sales sum for a specific country and year
        app.MapGet("/orders/{id}", async (HttpContext context, string id) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { Error = "Order id required." }, MyJsonContext.Default.WarningResponse);
                return;
            }

            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    await connection.OpenAsync();

                    // SQL query to retrieve the sum of sales by country and year
                    string query = @"
                        SELECT QUANTITYORDERED, PRICEEACH, SALES, ORDERDATE, STATUS, PRODUCTCODE
                        FROM sales_data_sample 
                        WHERE ORDERNUMBER = @ordernumber;";

                    using (var command = new SqliteCommand(query, connection))
                    {

                        command.Parameters.AddWithValue("@ordernumber", int.Parse(id));

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            var rows = new List<Dictionary<string, object>>();

                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();

                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }

                                rows.Add(row);
                            }

                            List<OrderDetailResponse> orders_final = new List<OrderDetailResponse>();

                            // Iterate over the rows and map them to OrderDetailResponse objects
                            for (int i = 0; i < rows.Count; i++) // Use < instead of <= to avoid out-of-bounds errors
                            {
                                var row = rows[i]; // Current row (List<object>)





                                // Create a new OrderDetailResponse object for each row
                                var orderToAdd = new OrderDetailResponse
                                {
                                    QuantityOrdered = Convert.ToDecimal(row[reader.GetName(0)]), // Convert to appropriate type
                                    PriceEach = Convert.ToDecimal(row[reader.GetName(1)]),
                                    Sales = Convert.ToDecimal(row[reader.GetName(2)]),
                                    OrderDate = Convert.ToString(row[reader.GetName(3)]),
                                    Status = Convert.ToString(row[reader.GetName(4)]), // Safely handle potential null values
                                    ProductCode = Convert.ToString(row[reader.GetName(5)])
                                };

                                orders_final.Add(orderToAdd);
                            }

                            // Return the final serialized list as a JSON response
                            if (orders_final.Count > 0)
                            {
                                // Serialize the list of OrderDetailResponse objects
                                await context.Response.WriteAsJsonAsync(orders_final, new JsonSerializerOptions
                                {
                                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                                    WriteIndented = true // For better readability (optional)
                                });
                            }
                            else
                            {
                                // Return a 404 response if no orders are found
                                context.Response.StatusCode = 404;
                                await context.Response.WriteAsJsonAsync(new { Error = "No orders found" });
                            }
                        }
                    }
                }
            }
            catch (SqliteException ex)
            {
                // Handle SQLite specific exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "Database error: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "An error occurred: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
        });
            
        //3. PUT METHOD TO UPDATE PRICE OF PRODUCT INSIDE ORDER

        app.MapPut("/orders/{ordernumber}/{productcode}/{newprice}", async (HttpContext context, int ordernumber, string productcode, float newprice) =>
        {
            if (string.IsNullOrWhiteSpace(Convert.ToString(ordernumber)) || string.IsNullOrWhiteSpace(productcode) || string.IsNullOrWhiteSpace(Convert.ToString(newprice)))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new { Error = "Order number, productcode and new price required." }, MyJsonContext.Default.WarningResponse);
                return;
            }

            try
            {
                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    await connection.OpenAsync();

                    // SQL query to retrieve the sum of sales by country and year
                    string query = @"
                        update sales_data_sample 
                        set PRICEEACH = @newprice,
                        SALES = @newprice * QUANTITYORDERED
                        WHERE ORDERNUMBER = @ordernumber and PRODUCTCODE = @productcode;";

                    using (var command = new SqliteCommand(query, connection))
                    {

                        command.Parameters.AddWithValue("@newprice", newprice);
                        command.Parameters.AddWithValue("@ordernumber", ordernumber);
                        command.Parameters.AddWithValue("@productcode", productcode);

                        command.ExecuteNonQueryAsync();

                        //if (result != DBNull.Value && result != null)
                        //{
                        //    decimal totalSales = Convert.ToDecimal(result);
                        //    var response = new YearlySalesResponse
                        //    {
                        //        Year = "tewef",
                        //        TotalSales = totalSales
                        //    };

                        //    await context.Response.WriteAsJsonAsync(response, MyJsonContext.Default.YearlySalesResponse);
                        //}
                        //else
                        //{
                        //    // Handle case where no data is found
                        //    await context.Response.WriteAsJsonAsync(new { Error = "No sales data found for the given country and year." }, MyJsonContext.Default.YearlySalesResponse);
                        //}
                    }
                }
            }
            catch (SqliteException ex)
            {
                // Handle SQLite specific exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "Database error: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new { Error = "An error occurred: " + ex.Message }, MyJsonContext.Default.WarningResponse);
            }
        });


        // Run the application
        app.Run();
    }
}
