namespace OrderSystem
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var seeder = new DataSeeder();
            await seeder.ResetAsync();
        }
    }
}
