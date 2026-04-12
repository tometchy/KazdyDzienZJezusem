using StackExchange.Redis;

if (args.Length == 0)
{
    Console.WriteLine("Usage: <book:chapter:verse> e.g. Matt:1:1");
    return;
}

var input = args[0];
var key = "gnt:" + input;

try
{
    var redis = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
    var db = redis.GetDatabase();

    var value = await db.StringGetAsync(key);

    if (value.IsNullOrEmpty)
    {
        Console.WriteLine("NOT FOUND");
    }
    else
    {
        Console.WriteLine(value);
    }
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex.Message);
}
